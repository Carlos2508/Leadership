using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Tests;

public sealed class MfaSqlTests : SqlTestBase
{
    protected override Dictionary<string, string?> ExtraConfiguration =>
        new() { ["AccountAccess:RequireMfaForPermissions:0"] = Permissions.ManageAccounts };

    // RFC 6238 TOTP (SHA-1, 30 s, 6 digits) computed independently from Identity.
    internal static string Totp(string formattedKey, long offset = 0)
    {
        var key = Base32(formattedKey.Replace(" ", "").ToUpperInvariant());
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + offset;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        var hash = HMACSHA1.HashData(key, counter);
        var o = hash[^1] & 0x0f;
        var value = ((hash[o] & 0x7f) << 24) | (hash[o + 1] << 16) | (hash[o + 2] << 8) | hash[o + 3];
        return (value % 1_000_000).ToString("D6");
    }

    private static byte[] Base32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var buffer = 0;
        var output = new List<byte>();
        foreach (var c in input.TrimEnd('='))
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits < 8) continue;
            output.Add((byte)(buffer >> (bits - 8)));
            bits -= 8;
        }
        return output.ToArray();
    }

    private async Task<(HttpClient Browser, HttpResponseMessage Response)> PasswordStep(string username)
    {
        var browser = App.Browser();
        var html = await browser.GetStringAsync("/Identity/Account/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var response = await browser.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Input.Username"] = username, ["Input.Password"] = Password, ["__RequestVerificationToken"] = token }));
        return (browser, response);
    }

    private static async Task<HttpResponseMessage> SecondStep(HttpClient browser, string code, bool recovery = false)
    {
        var path = "/Identity/Account/LoginWith2fa" + (recovery ? "?UseRecovery=true" : "");
        var html = await browser.GetStringAsync(path);
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        return await browser.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Input.Code"] = code, ["UseRecovery"] = recovery ? "true" : "false", ["__RequestVerificationToken"] = token }));
    }

    private static async Task<(string Key, string[] Codes)> Enroll(HttpClient browser)
    {
        var setup = await (await Post(browser, "/api/v1/me/mfa/setup", new { })).Content.ReadFromJsonAsync<JsonElement>();
        var key = setup.GetProperty("sharedKey").GetString()!;
        Assert.Contains("<svg", setup.GetProperty("qrSvg").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(browser, "/api/v1/me/mfa/enable", new MfaCodeRequest("000000"))).StatusCode);
        var enabled = await Post(browser, "/api/v1/me/mfa/enable", new MfaCodeRequest(Totp(key)));
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        var codes = (await enabled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codes").EnumerateArray().Select(x => x.GetString()!).ToArray();
        return (key, codes);
    }

    [Fact]
    public async Task Sensitive_permissions_wait_for_mfa_and_login_then_requires_the_second_factor()
    {
        using var admin = await SignIn(OwnerName);
        var session = await admin.GetFromJsonAsync<JsonElement>("/api/v1/session");
        Assert.False(session.GetProperty("twoFactorEnabled").GetBoolean());
        Assert.Equal(Permissions.ManageAccounts, session.GetProperty("mfaPending")[0].GetString());
        Assert.DoesNotContain(Permissions.ManageAccounts, session.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/access/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/people")).StatusCode);

        var (key, codes) = await Enroll(admin);
        Assert.Equal(10, codes.Length);
        // The enabling request refreshed the caller's own cookie: the session keeps working with the new stamp.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/access/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(admin, "/api/v1/me/mfa/disable", new MfaCodeRequest(Totp(key)))).StatusCode);

        var (browser, password) = await PasswordStep(OwnerName);
        Assert.Contains("LoginWith2fa", password.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SecondStep(browser, "123456")).StatusCode);
        // The enrollment already used the current code; the next time step is also accepted by the verification window.
        Assert.Equal(HttpStatusCode.Redirect, (await SecondStep(browser, Totp(key, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/access/accounts")).StatusCode);

        var (recoveryBrowser, _) = await PasswordStep(OwnerName);
        Assert.Equal(HttpStatusCode.Redirect, (await SecondStep(recoveryBrowser, codes[0], recovery: true)).StatusCode);
        var (reuse, _) = await PasswordStep(OwnerName);
        Assert.Equal(HttpStatusCode.OK, (await SecondStep(reuse, codes[0], recovery: true)).StatusCode);
        var actions = await Use<IdentityDbContext, string[]>(db => db.AuditEvents.Select(x => x.Action).ToArrayAsync());
        Assert.Contains("MfaEnabled", actions);
        Assert.Contains("LoginRecoveryCodeUsed", actions);
        Assert.Contains("LoginSecondFactorFailed", actions);
    }

    [Fact]
    public async Task App_login_requires_the_second_factor_for_enrolled_accounts()
    {
        using var admin = await SignIn(OwnerName);
        var (key, _) = await Enroll(admin);
        using var app = App.Browser();
        var token = (await app.GetFromJsonAsync<JsonElement>("/api/v1/auth/antiforgery")).GetProperty("token").GetString()!;
        async Task<HttpResponseMessage> AppPost(string path, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.Add("X-XSRF-TOKEN", token);
            return await app.SendAsync(request);
        }
        var password = await AppPost("/api/v1/auth/login", new { username = OwnerName, password = Password });
        Assert.Equal("second-factor", (await password.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        // The pending second step is not a session.
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/api/v1/session")).StatusCode);
        var wrong = await AppPost("/api/v1/auth/login/second-factor", new { code = "000000" });
        Assert.Equal("second-factor-failed", (await wrong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var ok = await AppPost("/api/v1/auth/login/second-factor", new { code = Totp(key, 1) });
        Assert.Equal("signed-in", (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/api/v1/access/accounts")).StatusCode);
    }

    [Fact]
    public async Task Users_disable_with_a_code_and_managers_reset_a_lost_authenticator()
    {
        var userId = await Account("synthetic-member");
        using var member = await SignIn("synthetic-member");
        var (key, _) = await Enroll(member);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(member, "/api/v1/me/mfa/disable", new MfaCodeRequest("999999"))).StatusCode);

        // A manager (who must have MFA because of the policy) resets the member's authenticator after verification.
        using var admin = await SignIn(OwnerName);
        await Enroll(admin);
        var accounts = await admin.GetFromJsonAsync<JsonElement>("/api/v1/access/accounts?search=synthetic-member");
        var row = accounts.GetProperty("items")[0];
        Assert.True(row.GetProperty("twoFactorEnabled").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await Post(admin, $"/api/v1/access/accounts/{userId}/reset-mfa",
            new AccountChangeRequest(row.GetProperty("version").GetString()!, Reason))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/v1/session")).StatusCode);
        var (browser, response) = await PasswordStep("synthetic-member");
        Assert.Equal("/workspace", response.Headers.Location!.OriginalString);
        Assert.False((await browser.GetFromJsonAsync<JsonElement>("/api/v1/me/mfa")).GetProperty("enabled").GetBoolean());
        _ = key;
    }
}

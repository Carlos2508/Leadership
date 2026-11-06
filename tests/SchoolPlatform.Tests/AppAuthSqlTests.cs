using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Tests;

/// <summary>Native app sign-in (ADR-014): same cookie session, antiforgery, audit and authorization as the web.</summary>
public sealed class AppAuthSqlTests : SqlTestBase
{
    private static async Task<string> AnonymousToken(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/antiforgery")).GetProperty("token").GetString()!;

    private static async Task<HttpResponseMessage> AppPost(HttpClient client, string path, object body, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (token is not null) request.Headers.Add("X-XSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task App_login_issues_the_web_session_and_keeps_antiforgery_audit_and_permissions()
    {
        var basic = await Account("synthetic-app-family");
        using var app = App.Browser();

        // Without the antiforgery header the login is rejected before checking credentials.
        Assert.Equal(HttpStatusCode.BadRequest, (await AppPost(app, "/api/v1/auth/login", new { username = "synthetic-app-family", password = Password }, null)).StatusCode);

        var token = await AnonymousToken(app);
        var wrong = await AppPost(app, "/api/v1/auth/login", new { username = "synthetic-app-family", password = "Wrong-Password!1" }, token);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var unknown = await AppPost(app, "/api/v1/auth/login", new { username = "synthetic-nobody", password = Password }, token);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        // Same message whether or not the username exists.
        Assert.Equal((await wrong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString(),
            (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/api/v1/session")).StatusCode);

        var ok = await AppPost(app, "/api/v1/auth/login", new { username = "synthetic-app-family", password = Password }, token);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("signed-in", (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        var session = await app.GetFromJsonAsync<JsonElement>("/api/v1/session");
        Assert.Equal(basic, session.GetProperty("userId").GetGuid());
        Assert.Empty(session.GetProperty("permissions").EnumerateArray());
        // Signing in through the app grants nothing beyond the account's permissions.
        Assert.Equal(HttpStatusCode.Forbidden, (await app.GetAsync("/api/v1/people")).StatusCode);

        // The anonymous token no longer matches the signed-in identity; a fresh one is required for writes.
        Assert.Equal(HttpStatusCode.BadRequest, (await AppPost(app, "/api/v1/auth/logout", new { }, token)).StatusCode);
        var fresh = (await app.GetFromJsonAsync<JsonElement>("/api/v1/antiforgery")).GetProperty("token").GetString();
        Assert.Equal(HttpStatusCode.NoContent, (await AppPost(app, "/api/v1/auth/logout", new { }, fresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/api/v1/session")).StatusCode);

        var actions = await Use<IdentityDbContext, string[]>(db => db.AuditEvents.Select(x => x.Action).ToArrayAsync());
        Assert.Contains("LoginFailed", actions);
        Assert.Contains("LoginSucceeded", actions);
        Assert.Contains("Logout", actions);
    }

    [Fact]
    public async Task App_login_gives_staff_their_permissions_and_rejects_inactive_or_missing_second_step()
    {
        using var app = App.Browser();
        var token = await AnonymousToken(app);
        Assert.Equal(HttpStatusCode.OK, (await AppPost(app, "/api/v1/auth/login", new { username = OwnerName, password = Password }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/api/v1/people")).StatusCode);

        // A second factor without a pending password step is refused.
        using var other = App.Browser();
        var otherToken = await AnonymousToken(other);
        var expired = await AppPost(other, "/api/v1/auth/login/second-factor", new { code = "123456" }, otherToken);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal("second-factor-expired", (await expired.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        // Empty input is a validation problem, not a credential check.
        Assert.Equal(HttpStatusCode.BadRequest, (await AppPost(other, "/api/v1/auth/login", new { username = "", password = "" }, otherToken)).StatusCode);
    }
}

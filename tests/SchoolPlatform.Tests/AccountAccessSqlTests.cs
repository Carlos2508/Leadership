using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Tests;

[Trait("Category", "SqlServer")]
public sealed class AccountAccessSqlTests : IAsyncLifetime
{
    private readonly string database = "SchoolPlatform_Test_" + Guid.NewGuid().ToString("N");
    private readonly TestClock clock = new();
    private TestApplication app = null!;
    private Guid owner;
    private const string OwnerName = "synthetic-owner";
    private const string Password = "Synthetic-Only!12345";
    private const string NewPassword = "Changed-Synthetic!67890";
    private const string Reason = "Autorización institucional de prueba verificada.";

    public async Task InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SCHOOLPLATFORM_TEST_SQL") ??
            "Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true") { InitialCatalog = database };
        app = new TestApplication(configuration: new() { ["ConnectionStrings:SchoolDatabase"] = connection.ConnectionString },
            configureServices: services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        await app.MigrateOwnedDatabaseAsync(database);
        using var scope = app.Services.CreateScope();
        owner = await Use(x => x.ProvisionOwnerAsync(OwnerName, Password));
    }

    public async Task DisposeAsync()
    {
        if (app is null) return;
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (!database.StartsWith("SchoolPlatform_Test_", StringComparison.Ordinal) || db.Database.GetDbConnection().Database != database)
            throw new InvalidOperationException("Refusing to delete a database not owned by this test.");
        await db.Database.EnsureDeletedAsync();
        await app.DisposeAsync();
    }

    private async Task<T> Use<T>(Func<AccountAccessService, Task<T>> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AccountAccessService>());
    }
    private async Task Use(Func<AccountAccessService, Task> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AccountAccessService>());
    }
    private async Task<T> Read<T>(Func<IdentityDbContext, Task<T>> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IdentityDbContext>());
    }
    private Task<string> Version(Guid userId) => Read(db => db.Users.Where(x => x.Id == userId).Select(x => x.ConcurrencyStamp!).SingleAsync());
    private Task<IssueCodeResult> Invite(string name = "synthetic-student", string profile = "basic") =>
        Use(x => x.InviteAsync(owner, new(name, profile, Reason)));
    private async Task<IssueCodeResult> Recovery(Guid userId) => await Use(async x => await x.IssueCodeAsync(owner, userId,
        new(await Version(userId), AccessPurpose.Recovery, Reason, true)));
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static async Task<HttpResponseMessage> Login(HttpClient browser, string username, string password)
    {
        var html = await browser.GetStringAsync("/Identity/Account/Login");
        return await browser.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username, ["Input.Password"] = password, ["__RequestVerificationToken"] = Token(html)
        }));
    }
    private static async Task<HttpResponseMessage> Post(HttpClient browser, string path, object body)
    {
        var token = (await browser.GetFromJsonAsync<JsonElement>("/api/v1/antiforgery")).GetProperty("token").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request);
    }

    [Fact]
    public async Task Invitation_activates_without_email_and_only_hash_is_stored()
    {
        var ticket = await Invite();
        Assert.Equal(HttpStatusCode.OK, (await Login(app.Browser(), "synthetic-student", Password)).StatusCode);
        var stored = await Read(db => db.AccessGrants.SingleAsync(x => x.UserId == ticket.UserId));
        Assert.Equal(32, stored.SecretHash.Length);
        var audit = await Read(db => db.AuditEvents.ToArrayAsync());
        Assert.DoesNotContain(ticket.Code, JsonSerializer.Serialize(audit));
        Assert.DoesNotContain(Password, JsonSerializer.Serialize(audit));
        await Use(x => x.RedeemAsync(ticket.Code, Password));
        using var browser = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await Login(browser, "synthetic-student", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/session")).StatusCode);
        Assert.Null(await Read(db => db.Users.Where(x => x.Id == ticket.UserId).Select(x => x.Email).SingleAsync()));
        var error = await Assert.ThrowsAsync<AccessOperationException>(() => Use(x => x.RedeemAsync(ticket.Code, NewPassword)));
        Assert.Equal("invalid-code", error.Code);
        Assert.Equal(1, await Read(db => db.AuditEvents.CountAsync(x => x.TargetId == ticket.UserId && x.Action == "AccountActivated")));
    }

    [Fact]
    public async Task Parallel_redemption_has_exactly_one_success()
    {
        var ticket = await Invite();
        async Task<bool> TryRedeem()
        {
            try { await Use(x => x.RedeemAsync(ticket.Code, Password)); return true; }
            catch (AccessOperationException error) when (error.Code == "invalid-code") { return false; }
        }
        Assert.Single(await Task.WhenAll(TryRedeem(), TryRedeem()), x => x);
        Assert.Equal(1, await Read(db => db.AuditEvents.CountAsync(x => x.Action == "AccountActivated")));
    }

    [Fact]
    public async Task Expired_and_superseded_codes_are_rejected_without_consuming_the_new_code()
    {
        var first = await Invite();
        clock.UtcNow = first.ExpiresAt;
        Assert.Equal("invalid-code", (await Assert.ThrowsAsync<AccessOperationException>(() => Use(x => x.RedeemAsync(first.Code, Password)))).Code);
        var next = await Use(async x => await x.IssueCodeAsync(owner, first.UserId, new(await Version(first.UserId), AccessPurpose.Activation, Reason, true)));
        Assert.Equal("invalid-code", (await Assert.ThrowsAsync<AccessOperationException>(() => Use(x => x.RedeemAsync(first.Code, Password)))).Code);
        await Use(x => x.RedeemAsync(next.Code, Password));
        Assert.True(await Read(db => db.Users.Where(x => x.Id == first.UserId).Select(x => x.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task Recovery_requires_verification_and_revokes_old_sessions_after_password_change()
    {
        var ticket = await Invite();
        await Use(x => x.RedeemAsync(ticket.Code, Password));
        using var oldBrowser = app.Browser();
        await Login(oldBrowser, "synthetic-student", Password);
        Assert.Equal(HttpStatusCode.OK, (await oldBrowser.GetAsync("/api/v1/session")).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessOperationException>(() => Use(async x => await x.IssueCodeAsync(owner,
            ticket.UserId, new(await Version(ticket.UserId), AccessPurpose.Recovery, Reason, false))))).Status);
        var recovery = await Recovery(ticket.UserId);
        await Use(x => x.RedeemAsync(recovery.Code, NewPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldBrowser.GetAsync("/api/v1/session")).StatusCode);
        using var browser = app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await Login(browser, "synthetic-student", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Login(browser, "synthetic-student", NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/session")).StatusCode);
    }

    [Fact]
    public async Task Weak_password_does_not_consume_code_and_invalid_response_does_not_echo_secrets()
    {
        var ticket = await Invite();
        using var browser = app.Browser();
        var token = Token(await browser.GetStringAsync("/Identity/Account/Activate"));
        const string weak = "weak";
        var response = await browser.PostAsync("/Identity/Account/Activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Code"] = ticket.Code, ["Input.Password"] = weak, ["Input.Confirmation"] = weak, ["__RequestVerificationToken"] = token
        }));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Usa entre 12 y 1024 caracteres", html);
        Assert.DoesNotContain(ticket.Code, html);
        Assert.DoesNotContain("value=\"weak\"", html);
        Assert.Null(await Read(db => db.AccessGrants.Where(x => x.UserId == ticket.UserId).Select(x => x.ConsumedAt).SingleAsync()));
        await Use(x => x.RedeemAsync(ticket.Code, Password));
    }

    [Fact]
    public async Task Last_active_manager_cannot_be_disabled_or_demoted_even_with_a_pending_manager()
    {
        await Invite("future-manager", "access-manager");
        var version = await Version(owner);
        Assert.Equal("last-manager", (await Assert.ThrowsAsync<AccessOperationException>(() => Use(async x => await x.SetActiveAsync(owner, owner, new(version, Reason, IsActive: false))))).Code);
        Assert.Equal("last-manager", (await Assert.ThrowsAsync<AccessOperationException>(() => Use(x => x.ChangeProfileAsync(owner, owner, new(version, Reason, "basic"))))).Code);
        Assert.True(await Read(db => db.Users.Where(x => x.Id == owner).Select(x => x.IsActive).SingleAsync()));
        Assert.False(await Read(db => db.AuditEvents.AnyAsync(x => x.Action == "AccountSuspended" || x.Action == "AccessProfileChanged")));
    }

    [Fact]
    public async Task Permissions_are_enforced_server_side_and_rechecked_from_sql()
    {
        var ticket = await Invite("synthetic-auditor", "auditor");
        await Use(x => x.RedeemAsync(ticket.Code, Password));
        using var browser = app.Browser();
        await Login(browser, "synthetic-auditor", Password);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/api/v1/access/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.PostAsJsonAsync("/api/v1/access/invitations", new InviteRequest("forbidden", "access-owner", Reason))).StatusCode);
        await Read(db => db.AccountPermissions.Where(x => x.UserId == ticket.UserId).ExecuteDeleteAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/api/v1/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Browser().GetAsync("/api/v1/access/accounts")).StatusCode);
    }

    [Fact]
    public async Task Admin_api_requires_csrf_and_conflicts_do_not_apply_stale_changes()
    {
        using var browser = app.Browser();
        await Login(browser, OwnerName, Password);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/api/v1/access/invitations", new InviteRequest("csrf-test", "basic", Reason))).StatusCode);
        var invited = await Post(browser, "/api/v1/access/invitations", new InviteRequest("csrf-test", "basic", Reason));
        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        var result = (await invited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid();
        var oldVersion = await Version(result);
        await Use(async x => await x.RevokeSessionsAsync(owner, result, new(oldVersion, Reason)));
        var conflict = await Post(browser, $"/api/v1/access/accounts/{result}/profile", new AccountChangeRequest(oldVersion, Reason, "access-owner"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.False(await Read(db => db.AccountPermissions.AnyAsync(x => x.UserId == result)));
        var list = await browser.GetStringAsync("/api/v1/access/accounts");
        Assert.DoesNotContain("secretHash", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"code\":", list, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_cancelled_code_cannot_be_redeemed()
    {
        var ticket = await Invite();
        await Use(async x => await x.CancelCodesAsync(owner, ticket.UserId, new(await Version(ticket.UserId), Reason)));
        Assert.Equal("invalid-code", (await Assert.ThrowsAsync<AccessOperationException>(() => Use(x => x.RedeemAsync(ticket.Code, Password)))).Code);
        Assert.Null(await Read(db => db.Users.Where(x => x.Id == ticket.UserId).Select(x => x.PasswordHash).SingleAsync()));
    }

    [Fact]
    public async Task Revoking_sessions_rejects_existing_cookie_and_preserves_password()
    {
        var ticket = await Invite();
        await Use(x => x.RedeemAsync(ticket.Code, Password));
        using var browser = app.Browser();
        await Login(browser, "synthetic-student", Password);
        await Use(async x => await x.RevokeSessionsAsync(owner, ticket.UserId, new(await Version(ticket.UserId), Reason)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Login(browser, "synthetic-student", Password)).StatusCode);
    }

    [Fact]
    public async Task Audit_failure_rolls_back_account_and_code_creation()
    {
        await Read(db => db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER [audit].[RejectTestInvites] ON [audit].[Events] AFTER INSERT AS
            BEGIN
                IF EXISTS (SELECT 1 FROM inserted WHERE Action = 'AccountInvited')
                    THROW 51002, 'Synthetic audit storage failure', 1;
            END;
            """));
        await Assert.ThrowsAsync<DbUpdateException>(() => Invite("must-rollback"));
        Assert.False(await Read(db => db.Users.AnyAsync(x => x.UserName == "must-rollback")));
        Assert.Equal(0, await Read(db => db.AccessGrants.CountAsync()));
        Assert.Equal(2, await Read(db => db.AccountPermissions.CountAsync()));
    }

    [Fact]
    public async Task Audit_cannot_be_updated_or_deleted_even_using_direct_sql()
    {
        var update = await Assert.ThrowsAsync<SqlException>(() => Read(db => db.Database.ExecuteSqlRawAsync("UPDATE [audit].[Events] SET Detail='tampered'")));
        Assert.Equal(51001, update.Number);
        var delete = await Assert.ThrowsAsync<SqlException>(() => Read(db => db.Database.ExecuteSqlRawAsync("DELETE FROM [audit].[Events]")));
        Assert.Equal(51001, delete.Number);
        Assert.Equal(1, await Read(db => db.AuditEvents.CountAsync()));
    }

    [Fact]
    public async Task Initial_owner_cannot_be_created_twice_and_pending_account_cannot_bypass_activation()
    {
        Assert.Equal("owner-exists", (await Assert.ThrowsAsync<AccessOperationException>(() => Use(x => x.ProvisionOwnerAsync("second-owner", Password)))).Code);
        var ticket = await Invite();
        Assert.Equal("activation-required", (await Assert.ThrowsAsync<AccessOperationException>(() => Use(async x => await x.SetActiveAsync(owner, ticket.UserId,
            new(await Version(ticket.UserId), Reason, IsActive: true))))).Code);
    }

    [Fact]
    public async Task Flagged_mfa_accounts_are_not_signed_in_without_a_challenge()
    {
        var ticket = await Invite();
        await Use(x => x.RedeemAsync(ticket.Code, Password));
        await Read(db => db.Users.Where(x => x.Id == ticket.UserId).ExecuteUpdateAsync(x => x.SetProperty(u => u.TwoFactorEnabled, true)));
        using var browser = app.Browser();
        // Flagged in SQL without an authenticator: Identity would skip the second step, so the login is refused.
        Assert.Equal(HttpStatusCode.OK, (await Login(browser, "synthetic-student", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/session")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_managers_cannot_both_remove_their_management_access()
    {
        var second = await Invite("another-manager", "access-manager");
        await Use(x => x.RedeemAsync(second.Code, Password));
        var firstVersion = await Version(owner);
        var secondVersion = await Version(second.UserId);
        async Task<bool> Demote(Guid id, string version)
        {
            try { await Use(x => x.ChangeProfileAsync(id, id, new(version, Reason, "basic"))); return true; }
            catch (AccessOperationException error) when (error.Code == "last-manager") { return false; }
        }
        Assert.Single(await Task.WhenAll(Demote(owner, firstVersion), Demote(second.UserId, secondVersion)), x => x);
        Assert.Equal(1, await Read(db => db.Users.CountAsync(u => u.IsActive && u.PasswordHash != null &&
            db.AccountPermissions.Any(p => p.UserId == u.Id && p.Permission == Permissions.ManageAccounts))));
    }

    [Fact]
    public async Task A_forged_secret_does_not_consume_the_valid_code()
    {
        var ticket = await Invite();
        var tampered = ticket.Code[..33] + (ticket.Code[33] == 'A' ? 'B' : 'A') + ticket.Code[34..];
        Assert.Equal("invalid-code", (await Assert.ThrowsAsync<AccessOperationException>(() => Use(x => x.RedeemAsync(tampered, Password)))).Code);
        await Use(x => x.RedeemAsync(ticket.Code, Password));
        Assert.Equal(1, await Read(db => db.AuditEvents.CountAsync(x => x.Action == "AccountActivated")));
    }

    [Fact]
    public async Task Suspension_revokes_codes_and_sessions_and_reenabling_does_not_restore_codes()
    {
        var ticket = await Invite();
        await Use(x => x.RedeemAsync(ticket.Code, Password));
        using var browser = app.Browser();
        await Login(browser, "synthetic-student", Password);
        var recovery = await Recovery(ticket.UserId);
        var version = await Version(ticket.UserId);
        await Use(x => x.SetActiveAsync(owner, ticket.UserId, new(version, Reason, IsActive: false)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/session")).StatusCode);
        Assert.Equal("invalid-code", (await Assert.ThrowsAsync<AccessOperationException>(() => Use(x => x.RedeemAsync(recovery.Code, NewPassword)))).Code);
        var suspendedVersion = await Version(ticket.UserId);
        await Use(x => x.SetActiveAsync(owner, ticket.UserId, new(suspendedVersion, Reason, IsActive: true)));
        Assert.Equal("invalid-code", (await Assert.ThrowsAsync<AccessOperationException>(() => Use(x => x.RedeemAsync(recovery.Code, NewPassword)))).Code);
        Assert.Equal(HttpStatusCode.Redirect, (await Login(browser, "synthetic-student", Password)).StatusCode);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 5, 5, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}


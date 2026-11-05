using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Tests;

[Trait("Category", "SqlServer")]
public sealed class IdentitySqlTests : IAsyncLifetime
{
    private readonly string databaseName = "SchoolPlatform_Test_" + Guid.NewGuid().ToString("N");
    private TestApplication app = null!;
    private const string Password = "Synthetic-Only!12345";

    public async Task InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SCHOOLPLATFORM_TEST_SQL") ??
            "Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true") { InitialCatalog = databaseName };
        app = new TestApplication(configuration: new() { ["ConnectionStrings:SchoolDatabase"] = connection.ConnectionString });
        await app.MigrateOwnedDatabaseAsync(databaseName);
    }

    public async Task DisposeAsync()
    {
        if (app is null) return;
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        // The fixture owns only its generated database, never a database supplied by the caller.
        if (db.Database.GetDbConnection().Database != databaseName || !databaseName.StartsWith("SchoolPlatform_Test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Se rechazó eliminar una base no creada por esta prueba.");
        await db.Database.EnsureDeletedAsync();
        await app.DisposeAsync();
    }

    private async Task<string> CreateUser(bool isActive = true)
    {
        using var scope = app.Services.CreateScope();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "test-" + Guid.NewGuid().ToString("N"), IsActive = isActive };
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
        return user.UserName;
    }

    private static string ExtractToken(string html) => WebUtility.HtmlDecode(
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    private static async Task<HttpResponseMessage> Login(HttpClient browser, string username, string password, string suffix = "")
    {
        var html = await browser.GetStringAsync("/Identity/Account/Login" + suffix);
        var token = ExtractToken(html);
        Assert.NotEmpty(token);
        return await browser.PostAsync("/Identity/Account/Login" + suffix, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username, ["Input.Password"] = password, ["__RequestVerificationToken"] = token
        }));
    }

    [Fact]
    public async Task Real_identity_cookie_access_and_logout_require_valid_session_and_csrf()
    {
        var username = await CreateUser();
        using var browser = app.Browser();
        var login = await Login(browser, username, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/workspace", login.Headers.Location!.ToString());
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), x => x.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/session")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/Identity/Account/Logout", new FormUrlEncodedContent([]))).StatusCode);
        var html = await browser.GetStringAsync("/Identity/Account/Logout");
        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync("/Identity/Account/Logout", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = ExtractToken(html) }))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/session")).StatusCode);
    }

    [Fact]
    public async Task Disabled_accounts_cannot_sign_in()
    {
        var username = await CreateUser(isActive: false);
        using var browser = app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await Login(browser, username, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/session")).StatusCode);
    }

    [Fact]
    public async Task Disabling_an_account_takes_effect_with_the_same_cookie()
    {
        var username = await CreateUser();
        using var browser = app.Browser();
        await Login(browser, username, Password);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/v1/session")).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Users.ExecuteUpdateAsync(x => x.SetProperty(m => m.IsActive, false));
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/api/v1/session")).StatusCode);
    }

    [Fact]
    public async Task Lockout_blocks_even_the_correct_password_after_five_failures()
    {
        var username = await CreateUser();
        using var browser = app.Browser();
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.OK, (await Login(browser, username, "Wrong!Password123")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(browser, username, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/session")).StatusCode);
    }

    [Fact]
    public async Task External_return_url_does_not_create_an_open_redirect()
    {
        var username = await CreateUser();
        using var browser = app.Browser();
        var response = await Login(browser, username, Password, "?ReturnUrl=https%3A%2F%2Fexample.com");
        Assert.Equal("/workspace", response.Headers.Location!.ToString());
    }
}

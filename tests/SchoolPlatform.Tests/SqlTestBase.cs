using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Tests;

/// <summary>Random SQL Server catalog per test class instance, migrated and owned by the fixture, with an administrator.</summary>
[Trait("Category", "SqlServer")]
public abstract class SqlTestBase : IAsyncLifetime
{
    private readonly string database = "SchoolPlatform_Test_" + Guid.NewGuid().ToString("N");
    protected TestApplication App { get; private set; } = null!;
    protected Guid Owner { get; private set; }
    /// <summary>Server-side errors logged during the test (unhandled exceptions included), to explain an unexpected 500.</summary>
    protected System.Collections.Concurrent.ConcurrentQueue<string> Errors { get; } = new();
    protected const string OwnerName = "synthetic-owner";
    protected const string Password = "Synthetic-Only!12345";
    protected const string Reason = "Verificación institucional sintética de prueba.";

    public virtual async Task InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SCHOOLPLATFORM_TEST_SQL") ??
            "Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true") { InitialCatalog = database };
        App = new TestApplication(configuration: new() { ["ConnectionStrings:SchoolDatabase"] = connection.ConnectionString },
            configureServices: services => services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(new ErrorCapture(Errors)));
        await App.MigrateOwnedDatabaseAsync(database);
        Owner = await Use<AccountAccessService, Guid>(x => x.ProvisionOwnerAsync(OwnerName, Password));
        var version = await Use<IdentityDbContext, string>(db => db.Users.Where(x => x.Id == Owner).Select(x => x.ConcurrencyStamp!).SingleAsync());
        await Use<AccountAccessService>(x => x.ChangeProfileAsync(Owner, Owner, new(version, Reason, "administrator")));
    }

    public async Task DisposeAsync()
    {
        if (App is null) return;
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            if (!database.StartsWith("SchoolPlatform_Test_", StringComparison.Ordinal) || db.Database.GetDbConnection().Database != database)
                throw new InvalidOperationException("Refusing to delete a database not owned by this test.");
            await db.Database.EnsureDeletedAsync();
        }
        await App.DisposeAsync();
        // Any unhandled server exception is a defect even if the test assertions passed (e.g. an untranslatable query).
        if (!Errors.IsEmpty) throw new InvalidOperationException("Unhandled server errors during the test: " + string.Join(" || ", Errors));
    }

    protected async Task<T> Use<TService, T>(Func<TService, Task<T>> action) where TService : notnull
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    protected async Task Use<TService>(Func<TService, Task> action) where TService : notnull
    {
        await using var scope = App.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    protected async Task<Guid> Account(string username, string profile = "basic")
    {
        await using var scope = App.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountAccessService>();
        var ticket = await service.InviteAsync(Owner, new(username, profile, Reason));
        await service.RedeemAsync(ticket.Code, Password);
        return ticket.UserId;
    }

    protected async Task<HttpClient> SignIn(string username)
    {
        var browser = App.Browser();
        var html = await browser.GetStringAsync("/Identity/Account/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var response = await browser.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Input.Username"] = username, ["Input.Password"] = Password, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return browser;
    }

    protected static async Task<HttpResponseMessage> Post(HttpClient browser, string path, object body)
    {
        var token = (await browser.GetFromJsonAsync<JsonElement>("/api/v1/antiforgery")).GetProperty("token").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request);
    }
}

internal sealed class ErrorCapture(System.Collections.Concurrent.ConcurrentQueue<string> errors) : Microsoft.Extensions.Logging.ILoggerProvider
{
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new Logger(errors);
    public void Dispose() { }

    private sealed class Logger(System.Collections.Concurrent.ConcurrentQueue<string> errors) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => logLevel >= Microsoft.Extensions.Logging.LogLevel.Error;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) errors.Enqueue(formatter(state, exception) + " " + exception);
        }
    }
}

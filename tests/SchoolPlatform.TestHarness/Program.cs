using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchoolPlatform.Host.IdentityAccess;

// Browser tests own this isolated database. This harness is never shipped with the web host.
var builder = Host.CreateApplicationBuilder();
var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SCHOOLPLATFORM_E2E_SQL") ??
    "Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true");
var database = Environment.GetEnvironmentVariable("SCHOOLPLATFORM_E2E_DATABASE") ?? "";
if (!Regex.IsMatch(database, "^SchoolPlatform_E2E_[a-f0-9]{32}$"))
    throw new InvalidOperationException("The browser harness requires its generated E2E database name.");
connection.InitialCatalog = database;
builder.Services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connection.ConnectionString));
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options => options.Password.RequiredLength = 12)
    .AddEntityFrameworkStores<IdentityDbContext>().AddDefaultTokenProviders();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SecurityAudit>();
builder.Services.AddScoped<AccountAccessService>();
builder.Services.AddOptions<AccountAccessOptions>();
using var host = builder.Build();
using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
if (args is ["prepare"])
{
    if (await db.Database.CanConnectAsync()) throw new InvalidOperationException("The generated E2E database already exists; refusing to modify it.");
    await db.Database.MigrateAsync();
    var username = Environment.GetEnvironmentVariable("SCHOOLPLATFORM_E2E_USERNAME") ?? throw new InvalidOperationException("Missing synthetic username.");
    var password = Environment.GetEnvironmentVariable("SCHOOLPLATFORM_E2E_PASSWORD") ?? throw new InvalidOperationException("Missing synthetic password.");
    await scope.ServiceProvider.GetRequiredService<AccountAccessService>().ProvisionOwnerAsync(username, password);
}
else if (args is ["cleanup"])
{
    if (db.Database.GetDbConnection().Database != database) throw new InvalidOperationException("Database ownership mismatch.");
    await db.Database.EnsureDeletedAsync();
}
else throw new InvalidOperationException("Use prepare or cleanup.");

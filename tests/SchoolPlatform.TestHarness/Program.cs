using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.SchoolData;

// Browser tests own this isolated database. This harness is never shipped with the web host.
var builder = Host.CreateApplicationBuilder();
var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SCHOOLPLATFORM_E2E_SQL") ??
    "Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true");
var database = Environment.GetEnvironmentVariable("SCHOOLPLATFORM_E2E_DATABASE") ?? "";
if (!Regex.IsMatch(database, "^SchoolPlatform_E2E_[a-f0-9]{32}$"))
    throw new InvalidOperationException("The browser harness requires its generated E2E database name.");
connection.InitialCatalog = database;
builder.Services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connection.ConnectionString));
builder.Services.AddDbContext<SchoolDbContext>(options => options.UseSqlServer(connection.ConnectionString,
    sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "school")));
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
    await scope.ServiceProvider.GetRequiredService<SchoolDbContext>().Database.MigrateAsync();
    var username = Environment.GetEnvironmentVariable("SCHOOLPLATFORM_E2E_USERNAME") ?? throw new InvalidOperationException("Missing synthetic username.");
    var password = Environment.GetEnvironmentVariable("SCHOOLPLATFORM_E2E_PASSWORD") ?? throw new InvalidOperationException("Missing synthetic password.");
    await scope.ServiceProvider.GetRequiredService<AccountAccessService>().ProvisionOwnerAsync(username, password);
}
else if (args is ["cleanup"])
{
    if (db.Database.GetDbConnection().Database != database) throw new InvalidOperationException("Database ownership mismatch.");
    if (Environment.GetEnvironmentVariable("SCHOOLPLATFORM_E2E_MIGRATION") == "1")
    {
        // Only backup files generated for this exact, randomly named E2E catalog.
        await using var sql = new SqlConnection(connection.ConnectionString);
        await sql.OpenAsync();
        await using var list = new SqlCommand("SELECT mf.physical_device_name FROM msdb.dbo.backupset bs JOIN msdb.dbo.backupmediafamily mf ON mf.media_set_id=bs.media_set_id WHERE bs.database_name=@database", sql);
        list.Parameters.AddWithValue("@database", database);
        var files = new List<string>();
        await using (var reader = await list.ExecuteReaderAsync())
            while (await reader.ReadAsync()) files.Add(reader.GetString(0));
        foreach (var file in files.Distinct())
        {
            var name = file.Replace('\\', '/').Split('/').Last();
            if (!Regex.IsMatch(name, "^" + database + "_migration_[a-f0-9]{32}\\.bak$")) continue;
            await using var remove = new SqlCommand("EXEC master.dbo.xp_delete_file 0,@path", sql);
            remove.Parameters.AddWithValue("@path", file);
            await remove.ExecuteNonQueryAsync();
        }
    }
    await db.Database.EnsureDeletedAsync();
}
else throw new InvalidOperationException("Use prepare or cleanup.");

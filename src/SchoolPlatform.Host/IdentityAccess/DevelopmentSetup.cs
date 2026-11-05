using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SchoolPlatform.Host.IdentityAccess;

internal static class DevelopmentSetup
{
    public static async Task RunAsync(WebApplication app, bool createOperator, bool provisionOnly = false)
    {
        if (!provisionOnly && !app.Environment.IsDevelopment()) throw new InvalidOperationException("La preparación automática solo está habilitada en Development.");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var school = scope.ServiceProvider.GetRequiredService<SchoolPlatform.Host.SchoolData.SchoolDbContext>();
        if (provisionOnly)
        {
            if ((await db.Database.GetPendingMigrationsAsync()).Any() || (await school.Database.GetPendingMigrationsAsync()).Any())
                throw new InvalidOperationException("Aplica primero las migraciones revisadas de esta instalación. Este comando no crea ni migra bases.");
        }
        else
        {
            // Identity owns iam/audit; school records reference those tables, so identity always migrates first.
            await db.Database.MigrateAsync();
            await school.Database.MigrateAsync();
        }
        if (!createOperator) return;

        var username = app.Configuration["Bootstrap:Username"];
        var password = app.Configuration["Bootstrap:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Defina Bootstrap__Username y Bootstrap__Password para crear el operador local.");
        await scope.ServiceProvider.GetRequiredService<AccountAccessService>().ProvisionOwnerAsync(username, password);
        app.Logger.LogInformation("Gestor inicial creado para esta instalación. No se conceden permisos académicos o financieros.");
    }
}

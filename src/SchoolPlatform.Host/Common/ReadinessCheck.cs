using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Common;

/// <summary>
/// Readiness: the database answers and both migration sets are fully applied. The web process never migrates by itself,
/// so a deployment that forgot a migration reports Unhealthy instead of failing later in a request. No details are exposed.
/// </summary>
public sealed class ReadinessCheck(IdentityDbContext identity, SchoolDbContext school) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            if (!await identity.Database.CanConnectAsync(ct)) return HealthCheckResult.Unhealthy("database");
            if ((await identity.Database.GetPendingMigrationsAsync(ct)).Any() || (await school.Database.GetPendingMigrationsAsync(ct)).Any())
                return HealthCheckResult.Unhealthy("migrations");
            return HealthCheckResult.Healthy();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("database");
        }
    }
}

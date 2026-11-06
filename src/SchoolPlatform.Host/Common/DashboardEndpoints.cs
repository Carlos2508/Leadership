using System.Security.Claims;

namespace SchoolPlatform.Host.Common;

public static class DashboardEndpoints
{
    public static void MapDashboard(this WebApplication app) =>
        app.MapGroup("/api/v1").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>()
            .MapGet("/dashboard", (Guid? year, ClaimsPrincipal user, DashboardService service, CancellationToken ct) =>
                service.ReadAsync(user, year, ct));
}

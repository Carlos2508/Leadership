using System.Security.Claims;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Attendance;

public static class AttendanceEndpoints
{
    public static void MapAttendance(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        api.MapGet("/courses/{id:guid}/attendance", async (Guid id, DateOnly? date, ClaimsPrincipal user, AttendanceService service, CancellationToken ct) =>
            await service.SheetAsync(Actor(user), id, date, ct) is { } sheet ? Results.Ok(sheet) : Results.NotFound());
        api.MapPost("/courses/{id:guid}/attendance", async (Guid id, SaveAttendanceRequest request, ClaimsPrincipal user, AttendanceService service, CancellationToken ct) =>
            Results.Ok(new { changed = await service.SaveAsync(Actor(user), id, request, ct) }));
        api.MapGet("/me/students/{id:guid}/attendance", async (Guid id, ClaimsPrincipal user, AttendanceService service, CancellationToken ct) =>
            await service.SummaryAsync(Actor(user), id, ct) is { } summary ? Results.Ok(summary) : Results.NotFound());
        api.MapGet("/me/students/{id:guid}/attendance/calendar", async (Guid id, DateOnly from, DateOnly to, ClaimsPrincipal user, AttendanceService service,
            CancellationToken ct) => await service.CalendarAsync(Actor(user), id, from, to, ct) is { } calendar ? Results.Ok(calendar) : Results.NotFound());
    }

    private static Guid Actor(ClaimsPrincipal user) => PeopleEndpoints.Actor(user);
}

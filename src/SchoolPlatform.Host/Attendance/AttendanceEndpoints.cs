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
        // Daily attendance per grade/section (one or several groups at once, e.g. a multigrade room).
        api.MapGet("/attendance/groups", (ClaimsPrincipal user, GroupAttendanceService service, CancellationToken ct) =>
            service.GroupsAsync(Actor(user), ct));
        api.MapGet("/attendance/group", async (Guid yearId, string groups, DateOnly? date, ClaimsPrincipal user, GroupAttendanceService service,
                CancellationToken ct) =>
            await service.SheetAsync(Actor(user), yearId, ParseGroups(groups), date, ct) is { } sheet ? Results.Ok(sheet) : Results.NotFound());
        api.MapPost("/attendance/group", async (SaveGroupAttendanceRequest request, ClaimsPrincipal user, GroupAttendanceService service,
                CancellationToken ct) => Results.Ok(await service.SaveAsync(Actor(user), request, ct)));
        api.MapGet("/me/students/{id:guid}/attendance", async (Guid id, ClaimsPrincipal user, AttendanceService service, CancellationToken ct) =>
            await service.SummaryAsync(Actor(user), id, ct) is { } summary ? Results.Ok(summary) : Results.NotFound());
    }

    /// <summary>"gradeId:sectionId,gradeId:" (empty section = whole grade).</summary>
    private static GroupKey[] ParseGroups(string? value)
    {
        var result = new List<GroupKey>();
        foreach (var part in (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(13))
        {
            var pieces = part.Split(':');
            if (!Guid.TryParse(pieces[0], out var grade)) throw OperationException.Invalid("Grupo inválido.");
            Guid? section = null;
            if (pieces.Length > 1 && pieces[1].Length > 0)
            {
                if (!Guid.TryParse(pieces[1], out var parsed)) throw OperationException.Invalid("Grupo inválido.");
                section = parsed;
            }
            result.Add(new GroupKey(grade, section));
        }
        return result.ToArray();
    }

    private static Guid Actor(ClaimsPrincipal user) => PeopleEndpoints.Actor(user);
}

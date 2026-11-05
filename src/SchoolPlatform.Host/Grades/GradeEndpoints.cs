using System.Security.Claims;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Grades;

public static class GradeEndpoints
{
    public static void MapGrades(this WebApplication app)
    {
        // Course grade sheets: resource authorization (assigned teacher or academic staff) is checked in the service.
        var scoped = app.MapGroup("/api/v1").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        scoped.MapGet("/courses/{id:guid}/grades", async (Guid id, Guid? periodId, ClaimsPrincipal user, GradeService service, CancellationToken ct) =>
            await service.SheetAsync(Actor(user), id, periodId, ct) is { } sheet ? Results.Ok(sheet) : Results.NotFound());
        scoped.MapPost("/courses/{id:guid}/grades", async (Guid id, SaveScoresRequest request, ClaimsPrincipal user, GradeService service, CancellationToken ct) =>
            Results.Ok(await service.SaveAsync(Actor(user), id, request, ct)));
        scoped.MapGet("/me/students/{id:guid}/grades", async (Guid id, ClaimsPrincipal user, GradeService service, CancellationToken ct) =>
            await service.MyGradesAsync(Actor(user), id, ct) is { } grades ? Results.Ok(grades) : Results.NotFound());

        var read = app.MapGroup("/api/v1").RequireAuthorization(Permissions.AcademicRead).AddEndpointFilter<ApiWriteFilter>();
        read.MapGet("/years/{yearId:guid}/publications", (Guid yearId, GradeService service, CancellationToken ct) => service.PublicationsAsync(yearId, ct));
        read.MapGet("/years/{yearId:guid}/clearance-cutoffs", (Guid yearId, GradeService service, CancellationToken ct) => service.CutoffsAsync(yearId, ct));
        app.MapGroup("/api/v1").RequireAuthorization(Permissions.AcademicManage).AddEndpointFilter<ApiWriteFilter>()
            .MapPost("/years/{yearId:guid}/publications", async (Guid yearId, PublishRequest request, ClaimsPrincipal user, GradeService service, CancellationToken ct) =>
            {
                await service.PublishAsync(Actor(user), yearId, request, ct);
                return Results.NoContent();
            });
    }

    private static Guid Actor(ClaimsPrincipal user) => PeopleEndpoints.Actor(user);
}

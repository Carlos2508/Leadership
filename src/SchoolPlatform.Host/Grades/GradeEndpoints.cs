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
        // Activities with points (period result = sum of points); resource checks in the service.
        scoped.MapGet("/courses/{id:guid}/activities", async (Guid id, Guid? periodId, ClaimsPrincipal user, ActivityService service, CancellationToken ct) =>
            await service.BookAsync(Actor(user), id, periodId, ct) is { } book ? Results.Ok(book) : Results.NotFound());
        scoped.MapPost("/courses/{id:guid}/activities", async (Guid id, SaveActivityRequest request, ClaimsPrincipal user, ActivityService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.CreateAsync(Actor(user), id, request, ct) }));
        scoped.MapPost("/courses/{id:guid}/activities/{activityId:guid}", async (Guid id, Guid activityId, SaveActivityRequest request, ClaimsPrincipal user,
            ActivityService service, CancellationToken ct) =>
        {
            await service.UpdateAsync(Actor(user), id, activityId, request, ct);
            return Results.NoContent();
        });
        scoped.MapPost("/courses/{id:guid}/activities/{activityId:guid}/remove", async (Guid id, Guid activityId, RemoveActivityRequest request,
            ClaimsPrincipal user, ActivityService service, CancellationToken ct) =>
        {
            await service.RemoveAsync(Actor(user), id, activityId, request, ct);
            return Results.NoContent();
        });
        scoped.MapPost("/courses/{id:guid}/activities/scores", async (Guid id, SaveActivityScoresRequest request, ClaimsPrincipal user,
            ActivityService service, CancellationToken ct) => Results.Ok(new { changed = await service.SaveScoresAsync(Actor(user), id, request, ct) }));
        scoped.MapPost("/courses/{id:guid}/activities/template", async (Guid id, ApplyTemplateRequest request, ClaimsPrincipal user,
            ActivityService service, CancellationToken ct) => Results.Ok(new { added = await service.ApplyTemplateAsync(Actor(user), id, request, ct) }));
        scoped.MapGet("/me/students/{id:guid}/grades", async (Guid id, ClaimsPrincipal user, GradeService service, CancellationToken ct) =>
            await service.MyGradesAsync(Actor(user), id, ct) is { } grades ? Results.Ok(grades) : Results.NotFound());

        var read = app.MapGroup("/api/v1").RequireAuthorization(Permissions.AcademicRead).AddEndpointFilter<ApiWriteFilter>();
        read.MapGet("/years/{yearId:guid}/publications", (Guid yearId, GradeService service, CancellationToken ct) => service.PublicationsAsync(yearId, ct));
        read.MapGet("/years/{yearId:guid}/periods/{periodId:guid}/course-completion", (Guid yearId, Guid periodId, GradeService service, CancellationToken ct) => service.CourseCompletionAsync(yearId, periodId, ct));
        read.MapGet("/activity-templates", (ActivityService service, CancellationToken ct) => service.TemplatesAsync(false, ct));
        read.MapGet("/years/{yearId:guid}/clearance-cutoffs", (Guid yearId, GradeService service, CancellationToken ct) => service.CutoffsAsync(yearId, ct));
        var manage = app.MapGroup("/api/v1").RequireAuthorization(Permissions.AcademicManage).AddEndpointFilter<ApiWriteFilter>();
        manage.MapPost("/years/{yearId:guid}/publications", async (Guid yearId, PublishRequest request, ClaimsPrincipal user, GradeService service, CancellationToken ct) =>
        {
            await service.PublishAsync(Actor(user), yearId, request, ct);
            return Results.NoContent();
        });
        manage.MapPost("/activity-templates", async (SaveTemplateRequest request, ClaimsPrincipal user, ActivityService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.SaveTemplateAsync(Actor(user), null, request, ct) }));
        manage.MapPost("/activity-templates/{id:guid}", async (Guid id, SaveTemplateRequest request, ClaimsPrincipal user, ActivityService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.SaveTemplateAsync(Actor(user), id, request, ct) }));
    }

    private static Guid Actor(ClaimsPrincipal user) => PeopleEndpoints.Actor(user);
}

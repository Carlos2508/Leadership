using System.Security.Claims;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Learning;

public static class LearningEndpoints
{
    public static void MapLearning(this WebApplication app)
    {
        // Resource checks (assigned teacher, academic staff, the student or a guardian with the faculty) run in the service.
        var api = app.MapGroup("/api/v1").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        api.MapGet("/courses/{courseId:guid}/assignments", async (Guid courseId, ClaimsPrincipal user, LearningService service, CancellationToken ct) =>
            await service.CourseAssignmentsAsync(Actor(user), courseId, ct) is { } items ? Results.Ok(items) : Results.NotFound());
        api.MapPost("/courses/{courseId:guid}/assignments", async (Guid courseId, AssignmentRequest request, ClaimsPrincipal user, LearningService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.SaveAsync(Actor(user), courseId, null, request, ct) }));
        api.MapPost("/courses/{courseId:guid}/assignments/{id:guid}", async (Guid courseId, Guid id, AssignmentRequest request, ClaimsPrincipal user, LearningService service, CancellationToken ct) =>
        {
            await service.SaveAsync(Actor(user), courseId, id, request, ct);
            return Results.NoContent();
        });
        api.MapPost("/courses/{courseId:guid}/assignments/{id:guid}/publish", async (Guid courseId, Guid id, VersionBody request, ClaimsPrincipal user, LearningService service, CancellationToken ct) =>
        {
            await service.SetStatusAsync(Actor(user), courseId, id, AssignmentStatus.Published, request, ct);
            return Results.NoContent();
        });
        api.MapPost("/courses/{courseId:guid}/assignments/{id:guid}/close", async (Guid courseId, Guid id, VersionBody request, ClaimsPrincipal user, LearningService service, CancellationToken ct) =>
        {
            await service.SetStatusAsync(Actor(user), courseId, id, AssignmentStatus.Closed, request, ct);
            return Results.NoContent();
        });
        api.MapGet("/courses/{courseId:guid}/assignments/{id:guid}/submissions", async (Guid courseId, Guid id, ClaimsPrincipal user, LearningService service, CancellationToken ct) =>
            await service.SubmissionsAsync(Actor(user), courseId, id, ct) is { } result ? Results.Ok(result) : Results.NotFound());
        api.MapPost("/courses/{courseId:guid}/submissions/{id:guid}/feedback", async (Guid courseId, Guid id, FeedbackRequest request, ClaimsPrincipal user, LearningService service, CancellationToken ct) =>
        {
            await service.FeedbackAsync(Actor(user), courseId, id, request, ct);
            return Results.NoContent();
        });
        api.MapGet("/me/students/{studentId:guid}/assignments", async (Guid studentId, ClaimsPrincipal user, LearningService service, CancellationToken ct) =>
            await service.StudentAssignmentsAsync(Actor(user), studentId, ct) is { } items ? Results.Ok(items) : Results.NotFound());
        api.MapGet("/me/assignments/{id:guid}/files", async (Guid id, ClaimsPrincipal user, LearningService learning, SubmissionFileService files, CancellationToken ct) =>
            await learning.OwnSubmissionIdAsync(Actor(user), id, ct) is { } submission ? Results.Ok(await files.ListAsync(submission, ct)) : Results.Ok(Array.Empty<FileView>()));
        api.MapPost("/me/assignments/{id:guid}/files", async (Guid id, IFormFile file, ClaimsPrincipal user, SubmissionFileService files, CancellationToken ct) =>
            Results.Ok(await files.UploadAsync(Actor(user), id, file, ct)))
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(12 * 1024 * 1024));
        api.MapPost("/me/assignments/{id:guid}/files/{fileId:guid}/remove", async (Guid id, Guid fileId, ClaimsPrincipal user, SubmissionFileService files, CancellationToken ct) =>
        {
            await files.DeleteAsync(Actor(user), id, fileId, ct);
            return Results.NoContent();
        });
        api.MapGet("/files/{id:guid}", async (Guid id, ClaimsPrincipal user, SubmissionFileService files, CancellationToken ct) =>
            await files.DownloadAsync(Actor(user), id, ct) is { } download
                ? Results.File(download.Path, download.ContentType, download.FileName)
                : Results.NotFound());
        api.MapPost("/me/assignments/{id:guid}/submission", async (Guid id, SubmitRequest request, ClaimsPrincipal user, LearningService service, CancellationToken ct) =>
        {
            await service.SubmitAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
    }

    private static Guid Actor(ClaimsPrincipal user) => PeopleEndpoints.Actor(user);
}

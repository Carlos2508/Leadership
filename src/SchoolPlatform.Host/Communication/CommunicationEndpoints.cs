using System.Security.Claims;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Communication;

public sealed record StatusBody(string Version);

public static class CommunicationEndpoints
{
    public static void MapCommunication(this WebApplication app)
    {
        // Authoring rights depend on the audience (permission or teaching assignment) and are checked in the service.
        var api = app.MapGroup("/api/v1").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        api.MapGet("/announcements/options", (ClaimsPrincipal user, CommunicationService service, CancellationToken ct) => service.OptionsAsync(Actor(user), ct));
        api.MapGet("/announcements", (ClaimsPrincipal user, CommunicationService service, CancellationToken ct) => service.AuthoredAsync(Actor(user), ct));
        api.MapPost("/announcements", async (AnnouncementRequest request, ClaimsPrincipal user, CommunicationService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.SaveAsync(Actor(user), null, request, ct) }));
        api.MapPost("/announcements/{id:guid}", async (Guid id, AnnouncementRequest request, ClaimsPrincipal user, CommunicationService service, CancellationToken ct) =>
        {
            await service.SaveAsync(Actor(user), id, request, ct);
            return Results.NoContent();
        });
        api.MapPost("/announcements/{id:guid}/publish", async (Guid id, StatusBody request, ClaimsPrincipal user, CommunicationService service, CancellationToken ct) =>
        {
            await service.SetStatusAsync(Actor(user), id, AnnouncementStatus.Published, request.Version, ct);
            return Results.NoContent();
        });
        api.MapPost("/announcements/{id:guid}/archive", async (Guid id, StatusBody request, ClaimsPrincipal user, CommunicationService service, CancellationToken ct) =>
        {
            await service.SetStatusAsync(Actor(user), id, AnnouncementStatus.Archived, request.Version, ct);
            return Results.NoContent();
        });
        api.MapGet("/me/announcements", (ClaimsPrincipal user, CommunicationService service, CancellationToken ct) => service.FeedAsync(Actor(user), ct));
        api.MapPost("/me/announcements/{id:guid}/read", async (Guid id, ClaimsPrincipal user, CommunicationService service, CancellationToken ct) =>
        {
            await service.MarkReadAsync(Actor(user), id, ct);
            return Results.NoContent();
        });
    }

    private static Guid Actor(ClaimsPrincipal user) => PeopleEndpoints.Actor(user);
}

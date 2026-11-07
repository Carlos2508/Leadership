using System.Security.Claims;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Chat;

public static class ChatEndpoints
{
    public static void MapChat(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        api.MapGet("/chat/summary", (ClaimsPrincipal user, ChatService service, CancellationToken ct) => service.SummaryAsync(PeopleEndpoints.Actor(user), ct));
        api.MapGet("/chat/years", (ClaimsPrincipal user, ChatService service, CancellationToken ct) => service.YearsAsync(PeopleEndpoints.Actor(user), ct));
        api.MapPost("/chat/activity", async (ChatActivity request, ClaimsPrincipal user, ChatService service, CancellationToken ct) =>
        {
            await service.ActivityAsync(PeopleEndpoints.Actor(user), user.FindFirstValue("AspNet.Identity.SecurityStamp") ?? "", request, ct);
            return Results.NoContent();
        }).RequireRateLimiting("chat-activity");
        var year = api.MapGroup("/years/{yearId:guid}/chat");
        year.MapGet("/contacts", (Guid yearId, string? search, int? page, ClaimsPrincipal user, ChatService service, CancellationToken ct) =>
            service.ContactsAsync(PeopleEndpoints.Actor(user), yearId, search, page ?? 1, ct));
        year.MapGet("/conversations", (Guid yearId, string? search, int? page, ClaimsPrincipal user, ChatService service, CancellationToken ct) => service.BrowseAsync(PeopleEndpoints.Actor(user), yearId, search, page ?? 1, ct));
        year.MapPost("/conversations", async (Guid yearId, StartChat request, ClaimsPrincipal user, ChatService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.StartAsync(PeopleEndpoints.Actor(user), yearId, request, ct) }));
        var conversation = year.MapGroup("/conversations/{id:guid}");
        conversation.MapGet("/messages", (Guid yearId, Guid id, long? before, long? after, long? knownRead, long? knownDelivered, long? receiptFrom, ClaimsPrincipal user, ChatService service, CancellationToken ct) =>
            service.MessagesAsync(PeopleEndpoints.Actor(user), yearId, id, before, after, ct, knownRead ?? 0, knownDelivered ?? 0, receiptFrom ?? 1));
        conversation.MapPost("/messages", (Guid yearId, Guid id, SendChat request, ClaimsPrincipal user, ChatService service, CancellationToken ct) =>
            service.SendAsync(PeopleEndpoints.Actor(user), yearId, id, request, ct)).RequireRateLimiting("chat-send");
        conversation.MapPost("/read", async (Guid yearId, Guid id, ReadChat request, ClaimsPrincipal user, ChatService service, CancellationToken ct) =>
        {
            await service.ReadAsync(PeopleEndpoints.Actor(user), yearId, id, request.ThroughSequence, ct);
            return Results.NoContent();
        });
    }
}

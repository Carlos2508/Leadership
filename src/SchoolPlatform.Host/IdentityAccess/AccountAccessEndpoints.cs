using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace SchoolPlatform.Host.IdentityAccess;

public static class AccountAccessEndpoints
{
    public static void MapAccountAccess(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/access").RequireAuthorization(Permissions.ManageAccounts)
            .AddEndpointFilter<AccessWriteFilter>();
        group.MapGet("/profiles", () => Results.Ok(AccessProfiles.All));
        group.MapGet("/accounts", (string? search, int? page, AccountAccessService service, CancellationToken ct) =>
            service.ListAsync(search, page ?? 1, ct));
        group.MapPost("/invitations", async (InviteRequest request, ClaimsPrincipal actor, AccountAccessService service, CancellationToken ct) =>
            Results.Ok(await service.InviteAsync(Actor(actor), request, ct)));
        group.MapPost("/accounts/{id:guid}/profile", async (Guid id, AccountChangeRequest request, ClaimsPrincipal actor, AccountAccessService service, CancellationToken ct) =>
        {
            await service.ChangeProfileAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });
        group.MapPost("/accounts/{id:guid}/state", async (Guid id, AccountChangeRequest request, ClaimsPrincipal actor, AccountAccessService service, CancellationToken ct) =>
        {
            await service.SetActiveAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });
        group.MapPost("/accounts/{id:guid}/revoke-sessions", async (Guid id, AccountChangeRequest request, ClaimsPrincipal actor, AccountAccessService service, CancellationToken ct) =>
        {
            await service.RevokeSessionsAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });
        group.MapPost("/accounts/{id:guid}/codes", async (Guid id, IssueCodeRequest request, ClaimsPrincipal actor, AccountAccessService service, CancellationToken ct) =>
            Results.Ok(await service.IssueCodeAsync(Actor(actor), id, request, ct)));
        group.MapPost("/accounts/{id:guid}/cancel-codes", async (Guid id, AccountChangeRequest request, ClaimsPrincipal actor, AccountAccessService service, CancellationToken ct) =>
        {
            await service.CancelCodesAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });

        app.MapGet("/api/v1/audit", async (int? page, IdentityDbContext db, CancellationToken ct) =>
        {
            var number = page ?? 1;
            if (number < 1 || number > 10000) return Results.BadRequest();
            const int size = 25;
            var total = await db.AuditEvents.CountAsync(ct);
            var items = await db.AuditEvents.AsNoTracking().OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
                .Skip((number - 1) * size).Take(size).Select(x => new
                {
                    x.Id, x.OccurredAt, x.Action, x.Reason, x.Detail, x.TraceId,
                    actor = db.Users.Where(u => u.Id == x.ActorId).Select(u => u.UserName).FirstOrDefault(),
                    target = db.Users.Where(u => u.Id == x.TargetId).Select(u => u.UserName).FirstOrDefault()
                }).ToArrayAsync(ct);
            return Results.Ok(new { items, total, page = number, pageSize = size });
        }).RequireAuthorization(Permissions.ReadAudit);
    }

    private static Guid Actor(ClaimsPrincipal actor) => Guid.Parse(actor.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed class AccessWriteFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            return await next(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: 400, title: "El formulario venció. Recarga la página e intenta nuevamente.",
                extensions: new Dictionary<string, object?> { ["code"] = "invalid-csrf" });
        }
        catch (AccessOperationException exception)
        {
            return Results.Problem(statusCode: exception.Status, title: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(statusCode: 409, title: "Los datos cambiaron. Recarga la página antes de continuar.");
        }
        catch (Microsoft.Data.SqlClient.SqlException exception) when (exception.Number is 51000 or 1205)
        {
            return Results.Problem(statusCode: 409, title: "Otra operación está en curso. Espera unos segundos e intenta nuevamente.");
        }
    }
}

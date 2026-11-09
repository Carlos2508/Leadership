using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Common;

namespace SchoolPlatform.Host.IdentityAccess;

public static class AccountAccessEndpoints
{
    public static void MapAccountAccess(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/access").RequireAuthorization(Permissions.ManageAccounts)
            .AddEndpointFilter<ApiWriteFilter>();
        group.MapGet("/profiles", (AccessRoleService roles, CancellationToken ct) => roles.ProfilesAsync(ct));
        group.MapGet("/roles", (AccessRoleService roles, CancellationToken ct) => roles.ListAsync(ct));
        group.MapGet("/permissions", () => PermissionCatalog.All);
        group.MapPost("/roles", async (SaveRoleRequest request, ClaimsPrincipal actor, AccessRoleService roles, CancellationToken ct) =>
            Results.Ok(new { key = await roles.SaveAsync(Actor(actor), null, request, ct) }));
        group.MapPost("/roles/{key}", async (string key, SaveRoleRequest request, ClaimsPrincipal actor, AccessRoleService roles, CancellationToken ct) =>
            Results.Ok(new { key = await roles.SaveAsync(Actor(actor), key, request, ct) }));
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
        group.MapPost("/accounts/{id:guid}/reset-mfa", async (Guid id, AccountChangeRequest request, ClaimsPrincipal actor, AccountAccessService service, CancellationToken ct) =>
        {
            await service.ResetMfaAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });
        group.MapPost("/accounts/{id:guid}/cancel-codes", async (Guid id, AccountChangeRequest request, ClaimsPrincipal actor, AccountAccessService service, CancellationToken ct) =>
        {
            await service.CancelCodesAsync(Actor(actor), id, request, ct);
            return Results.NoContent();
        });

        var mfa = app.MapGroup("/api/v1/me/mfa").RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        mfa.MapGet("", (ClaimsPrincipal user, MfaService service, CancellationToken ct) => service.StatusAsync(user, ct));
        mfa.MapPost("/setup", (ClaimsPrincipal user, MfaService service) => service.SetupAsync(user));
        mfa.MapPost("/enable", (MfaCodeRequest request, ClaimsPrincipal user, MfaService service, CancellationToken ct) => service.EnableAsync(user, request, ct));
        mfa.MapPost("/recovery-codes", (MfaCodeRequest request, ClaimsPrincipal user, MfaService service, CancellationToken ct) => service.RegenerateAsync(user, request, ct));
        mfa.MapPost("/disable", async (MfaCodeRequest request, ClaimsPrincipal user, MfaService service, CancellationToken ct) =>
        {
            await service.DisableAsync(user, request, ct);
            return Results.NoContent();
        });

        app.MapGet("/api/v1/audit", async (int? page, string? area, IdentityDbContext db, CancellationToken ct) =>
        {
            var number = page ?? 1;
            if (number < 1 || number > 10000) return Results.BadRequest();
            const int size = 25;
            // Areas group the record types written by each module; account events have no type (historical rows).
            string[]? types = area switch
            {
                null or "" or "all" => null,
                "people" => ["Person", "Relationship", "ImportBatch", "FamilyRegistration"],
                "academic" => ["AcademicYear", "Grade", "Course", "Enrollment", "Subject", "Assignment"],
                "finance" => ["Charge", "Payment", "FeeConcept"],
                "communication" => ["Announcement"],
                "inventory" => ["InventoryProduct"],
                "access" => [],
                _ => null
            };
            if (area is not (null or "" or "all" or "changes" or "people" or "academic" or "finance" or "communication" or "inventory" or "access" or "gate"))
                return Results.BadRequest();
            var query = db.AuditEvents.AsNoTracking();
            // "changes": everything except routine sign-ins/sign-outs; "gate": ID cards, photos, gate marks and scanner devices.
            if (area == "changes") query = query.Where(x => !x.Action.StartsWith("Login") && x.Action != "Logout");
            if (area == "gate")
                query = query.Where(x => x.Action.StartsWith("Gate") || x.Action.StartsWith("StudentCard") || x.Action.StartsWith("StaffCard") ||x.Action.StartsWith("PersonPhoto"));
            if (types is { Length: 0 }) query = query.Where(x => x.TargetType == null);
            else if (types is not null) query = query.Where(x => x.TargetType != null && types.Contains(x.TargetType));
            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
                .Skip((number - 1) * size).Take(size).Select(x => new
                {
                    x.Id, x.OccurredAt, x.Action, x.Reason, x.Detail, x.TraceId, x.TargetId, x.TargetType,
                    actor = db.Users.Where(u => u.Id == x.ActorId).Select(u => u.UserName).FirstOrDefault(),
                    target = db.Users.Where(u => u.Id == x.TargetId).Select(u => u.UserName).FirstOrDefault()
                }).ToArrayAsync(ct);
            return Results.Ok(new { items, total, page = number, pageSize = size });
        }).RequireAuthorization(Permissions.ReadAudit);
    }

    private static Guid Actor(ClaimsPrincipal actor) => Guid.Parse(actor.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

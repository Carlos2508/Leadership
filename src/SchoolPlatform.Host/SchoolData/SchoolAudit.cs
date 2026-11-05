using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Host.SchoolData;

/// <summary>Stages audit events and re-checks the actor's durable permissions inside SchoolDbContext.</summary>
public sealed class SchoolAudit(SchoolDbContext db, TimeProvider time, IHttpContextAccessor http)
{
    // Staged only: the caller commits it in the same SaveChanges as the business change.
    public void Record(string action, Guid actorId, string targetType, Guid targetId, string reason = "", string detail = "") =>
        db.AuditEvents.Add(new SecurityAuditEvent
        {
            Id = Guid.NewGuid(), OccurredAt = time.GetUtcNow(), Action = action, ActorId = actorId,
            TargetId = targetId, TargetType = targetType, Reason = reason,
            Detail = detail.Length > 120 ? detail[..120] : detail,
            TraceId = http.HttpContext?.TraceIdentifier ?? "console"
        });

    public async Task RequireAsync(Guid actorId, CancellationToken ct, params string[] permissions)
    {
        var granted = await db.Permissions.AsNoTracking()
            .Where(x => x.UserId == actorId && permissions.Contains(x.Permission))
            .Where(x => db.Accounts.Any(a => a.Id == actorId && a.IsActive))
            .Select(x => x.Permission).Distinct().CountAsync(ct);
        if (granted != permissions.Distinct().Count())
            throw new OperationException(403, "permission-denied", "No tienes permiso para realizar esta operación.");
    }
}

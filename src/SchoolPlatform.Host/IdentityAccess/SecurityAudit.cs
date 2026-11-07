namespace SchoolPlatform.Host.IdentityAccess;

public sealed class SecurityAuditEvent
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? ActorId { get; set; }
    public Guid? TargetId { get; set; }
    /// <summary>Null for account targets (historical rows); otherwise the kind of school record affected.</summary>
    public string? TargetType { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Detail { get; set; } = "";
    public string TraceId { get; set; } = "";
}

public sealed class SecurityAudit(IdentityDbContext db, TimeProvider time, IHttpContextAccessor http)
{
    // This only stages a record. The caller commits it with the associated business changes.
    public void Record(string action, Guid? actorId, Guid? targetId, string reason = "", string detail = "") =>
        db.AuditEvents.Add(new SecurityAuditEvent
        {
            Id = Guid.NewGuid(), OccurredAt = time.GetUtcNow(), Action = action,
            ActorId = actorId, TargetId = targetId, Reason = reason, Detail = detail.Length > 120 ? detail[..120] : detail,
            TraceId = http.HttpContext?.TraceIdentifier ?? "console"
        });
}

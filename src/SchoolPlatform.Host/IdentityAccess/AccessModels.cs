namespace SchoolPlatform.Host.IdentityAccess;

public static class AccessPurpose
{
    public const string Activation = "Activation";
    public const string Recovery = "Recovery";
}

public sealed class AccountAccessGrant
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid IssuedBy { get; set; }
    public string Purpose { get; set; } = "";
    public byte[] SecretHash { get; set; } = [];
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class AccountAccessOptions
{
    public int ActivationMinutes { get; set; } = 1440;
    public int RecoveryMinutes { get; set; } = 30;
    /// <summary>
    /// Permissions that only work while the account has two-step verification enabled. Empty by default so a new
    /// installation can bootstrap; production should list at least accounts.manage, audit.read and finance.reverse.
    /// </summary>
    public string[] RequireMfaForPermissions { get; set; } = [];
}

public sealed record IssueCodeResult(Guid UserId, string Code, string Purpose, DateTimeOffset ExpiresAt);
public sealed record InviteRequest(string Username, string Profile, string Reason);
public sealed record AccountChangeRequest(string Version, string Reason, string? Profile = null, bool? IsActive = null);
public sealed record IssueCodeRequest(string Version, string Purpose, string VerificationNote, bool IdentityVerified);
public sealed record AccountSummary(Guid Id, string Username, string Status, string Profile, string Version,
    DateTimeOffset? CodeExpiresAt, string? CodePurpose, bool TwoFactorEnabled = false);
public sealed record PageResult<T>(T[] Items, int Total, int Page, int PageSize);

public sealed class AccessOperationException(int status, string code, string message)
    : SchoolPlatform.Host.Common.OperationException(status, code, message);

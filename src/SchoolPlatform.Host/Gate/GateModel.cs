namespace SchoolPlatform.Host.Gate;

/// <summary>
/// Printed student ID card. The token printed as QR and Code 128 is random (not the student code), so a lost card is
/// revoked by reissuing it. At most one active card per student.
/// </summary>
public sealed class StudentCard
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string Token { get; set; } = "";
    public DateTimeOffset IssuedAt { get; set; }
    public Guid IssuedBy { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public string? RevokeReason { get; set; }
}

/// <summary>Photo of a person for the ID card and the gate screen, stored privately outside the web root.</summary>
public sealed class PersonPhoto
{
    public Guid PersonId { get; set; }
    public string StorageKey { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }
    public Guid UploadedBy { get; set; }
}

/// <summary>Entry or exit at the school gate, with server time. Voided marks are kept with their reason.</summary>
public sealed class GateEvent
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string Direction { get; set; } = GateDirection.In;
    public DateTimeOffset OccurredAt { get; set; }
    public DateOnly LocalDate { get; set; }
    public string Source { get; set; } = GateSource.Scanner;
    public Guid? CardId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? RecordedBy { get; set; }
    public string Note { get; set; } = "";
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
}

/// <summary>Phone or tablet authorized to scan cards with its own revocable token (stored only as a hash).</summary>
public sealed class GateDevice
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
}

public static class GateDirection
{
    public const string In = "In";
    public const string Out = "Out";
}

public static class GateSource
{
    public const string Scanner = "Scanner";
    public const string Manual = "Manual";
    public const string Mobile = "Mobile";
}

public static class GateMode
{
    public const string Auto = "Auto";
    public const string In = "In";
    public const string Out = "Out";
    public static readonly string[] All = [Auto, In, Out];
}

/// <summary>Configuration section "Gate".</summary>
public sealed class GateOptions
{
    /// <summary>A second read of the same card in the same direction within this window is reported, not recorded.</summary>
    public int DuplicateSeconds { get; set; } = 120;
    /// <summary>In automatic mode, a read this soon after an entry is treated as a repeated read, not as the exit.</summary>
    public int AutoExitAfterMinutes { get; set; } = 30;
    public long MaxPhotoBytes { get; set; } = 2 * 1024 * 1024;
    /// <summary>Public HTTPS address written into device pairing codes (defaults to the address used by the administrator).</summary>
    public string PublicUrl { get; set; } = "";
}

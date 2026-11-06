namespace SchoolPlatform.Host.LegacyImport;

public sealed class RosterBatch
{
    public Guid Id { get; set; }
    public string FileSha256 { get; set; } = "";
    public string PlanSha256 { get; set; } = "";
    public Guid AcademicYearId { get; set; }
    public string Source { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset AppliedAt { get; set; }
    // Only technical IDs/counts, never source names/contacts or credentials.
    public string ResultJson { get; set; } = "";
}

public sealed class RosterSourceAlias
{
    public string Source { get; set; } = "";
    public string Entity { get; set; } = "";
    public string LegacyId { get; set; } = "";
    public string Discriminator { get; set; } = "";
    public Guid PersonId { get; set; }
    public Guid BatchId { get; set; }
}

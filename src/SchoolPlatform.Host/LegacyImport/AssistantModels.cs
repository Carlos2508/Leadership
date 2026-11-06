namespace SchoolPlatform.Host.LegacyImport;

public sealed class MigrationAssistantOptions
{
    public bool Enabled { get; set; }
    public string PhpPath { get; set; } = "C:/xampp/php/php.exe";
    public string LegacyConfigPath { get; set; } = "C:/xampp/htdocs/sys/sys-escolar/mvc/config/database.php";
    public string ExporterPath { get; set; } = "../../scripts/Export-LegacyRoster.php";
    public string StorageRoot { get; set; } = "../../.local/migration-assistant";
}

public sealed record AssistantSourceConnection(string? Host, string? Database, string? Username, string? Password, int Port = 3306);
public sealed record AssistantInspect(AssistantSourceYear[] Years);
public sealed record AssistantInspectRequest(AssistantSourceConnection? Connection);
public sealed record AssistantSourceYear(string SchoolyearID, string Schoolyear, int Students = 0);
public sealed record AssistantReadRequest(string SourceYearId, Guid TargetYearId, string NameFormat, AssistantSourceConnection? Connection);
public sealed record AssistantYear(Guid Id, string Label, DateOnly StartsOn, DateOnly EndsOn, string Status);
public sealed record AssistantGrade(Guid Id, string Name, int Sequence, string Stage);
public sealed record AssistantSection(Guid Id, Guid AcademicYearId, Guid GradeLevelId, string Name);
public sealed record AssistantCatalog(string Server, string Database, AssistantYear[] Years, AssistantGrade[] Grades,
    AssistantSection[] Sections, bool NeedsPreparation);

// Proposals remain drafts until the operator confirms them in the review screen.
public sealed record AssistantStudent(string LegacyId, string FullName, string GivenNames, string FamilyNames,
    string? BirthDate, string Code, string SourceGradeId, string SourceGradeName, string? FamilyId,
    bool Include, Guid? GradeId, Guid? SectionId, bool DistinctPersonConfirmed, string[] Notices);
public sealed record AssistantAdult(string Key, string FullName, string GivenNames, string FamilyNames, string Kind,
    bool Include, bool DistinctPersonConfirmed, string? SameAsKey = null);
public sealed record AssistantFamily(string LegacyId, AssistantAdult[] Adults, string[] Notices);
public sealed record AssistantReview(Guid RunId, string NameFormat, Guid YearId, string YearLabel, DateOnly EnrolledOn,
    AssistantStudent[] Students, AssistantFamily[] Families, bool Confirmed);
public sealed record AssistantPlanRequest(AssistantReview Review);
public sealed record AssistantApplyRequest(Guid RunId, string PlanSha256);
public sealed record AssistantResult(RosterReport Report, bool BackupVerified);

public sealed record RawRosterStudent(string LegacyStudentId, string FullName, string? BirthDate, string? StudentCodeCandidate,
    string? RegisterNumberCandidate, string? Phone, string? Email, string? FamilyLegacyId, string SourceGradeId, string? SourceSectionId);
public sealed record RawRosterFamily(string LegacyFamilyId, string SourceYearId, string? RecordName, string? FatherName,
    string? MotherName, string? SharedPhone, string? SharedEmail);
public sealed record RawRosterGrade(string SourceGradeId, string Name, int? Sequence);
public sealed record RawRosterExport(int FormatVersion, string ArtifactKind, string Source, string SourceYearId,
    string SourceYearLabel, DateTimeOffset ExportedAt, RawRosterStudent[] Students, RawRosterFamily[] Families, RawRosterGrade[] Grades);

namespace SchoolPlatform.Host.Grades;

public static class ScoreStatus
{
    public const string Scored = "Scored";
    public const string Missing = "Missing";
    public const string Excused = "Excused";
    public static readonly string[] All = [Scored, Missing, Excused];
}

// Current period result of a student in a course. Zero is a valid score; a value is required only when Scored.
public sealed class PeriodScore
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Guid PeriodId { get; set; }
    public Guid StudentId { get; set; }
    public decimal? Score { get; set; }
    public string Status { get; set; } = ScoreStatus.Scored;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public byte[] Version { get; set; } = [];
}

// Every change keeps the previous and new value, actor and reason (corrections after publication require a reason).
public sealed class ScoreRevision
{
    public Guid Id { get; set; }
    public Guid ScoreId { get; set; }
    public decimal? PreviousScore { get; set; }
    public string? PreviousStatus { get; set; }
    public decimal? NewScore { get; set; }
    public string NewStatus { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset ChangedAt { get; set; }
    public Guid ChangedBy { get; set; }
}

// Publication of one academic period for a year, tied to the clearance cutoff chosen by the school for that release.
public sealed class GradePublication
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid PeriodId { get; set; }
    public Guid CutoffId { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public Guid PublishedBy { get; set; }
    public string Note { get; set; } = "";
}

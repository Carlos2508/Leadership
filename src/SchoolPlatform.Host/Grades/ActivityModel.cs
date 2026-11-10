namespace SchoolPlatform.Host.Grades;

/// <summary>
/// Graded activity of a course in one period (homework, quiz, project, exam...). Its points add up to the period result,
/// as in the school's previous system ("actividades a calificar" with their own points). A homework published to students
/// can be linked to exactly one activity. The activities of a course and period add up to at most 100 points.
/// </summary>
public sealed class GradeActivity
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Guid PeriodId { get; set; }
    public string Title { get; set; } = "";
    public decimal MaxPoints { get; set; }
    public int Sequence { get; set; }
    public Guid? AssignmentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    /// <summary>Removed activities keep their points and revisions but no longer count toward the period result.</summary>
    public DateTimeOffset? RemovedAt { get; set; }
    public Guid? RemovedBy { get; set; }
    public string? RemoveReason { get; set; }
    public byte[] Version { get; set; } = [];
}

/// <summary>Points of one student in one activity. Missing counts as zero; Excused is left out of the sum.</summary>
public sealed class ActivityScore
{
    public Guid Id { get; set; }
    public Guid ActivityId { get; set; }
    public Guid StudentId { get; set; }
    public decimal? Points { get; set; }
    public string Status { get; set; } = ScoreStatus.Scored;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public byte[] Version { get; set; } = [];
}

/// <summary>Every change of activity points keeps the previous and new value, actor and reason.</summary>
public sealed class ActivityScoreRevision
{
    public Guid Id { get; set; }
    public Guid ScoreId { get; set; }
    public decimal? PreviousPoints { get; set; }
    public string? PreviousStatus { get; set; }
    public decimal? NewPoints { get; set; }
    public string NewStatus { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset ChangedAt { get; set; }
    public Guid ChangedBy { get; set; }
}

/// <summary>
/// Reusable list of activities with points defined by academic management (for example the eight activities every course
/// grades per period). A teacher applies it to a course and period in one step and can adjust it afterwards.
/// </summary>
public sealed class ActivityTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ItemsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed record TemplateItem(string Title, decimal Points);

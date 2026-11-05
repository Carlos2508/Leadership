namespace SchoolPlatform.Host.Learning;

public static class AssignmentStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Closed = "Closed";
}

// Course activity. Its audience is derived from the course roster between publication and due date (dated placements),
// so a student who joins the course before the due date receives it and one who left before publication does not.
public sealed class Assignment
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public DateTimeOffset DueAt { get; set; }
    public bool AllowLate { get; set; }
    public string Status { get; set; } = AssignmentStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public byte[] Version { get; set; } = [];
}

public static class SubmissionStatus
{
    public const string Submitted = "Submitted";
    public const string Reviewed = "Reviewed";
}

// One current submission per student and assignment, written by the student's own account with server time.
public sealed class Submission
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public string Text { get; set; } = "";
    public DateTimeOffset SubmittedAt { get; set; }
    public Guid SubmittedBy { get; set; }
    public int Attempts { get; set; }
    public string Status { get; set; } = SubmissionStatus.Submitted;
    public string? Feedback { get; set; }
    public DateTimeOffset? FeedbackAt { get; set; }
    public Guid? FeedbackBy { get; set; }
    public byte[] Version { get; set; } = [];
}

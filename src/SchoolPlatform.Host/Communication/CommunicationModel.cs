namespace SchoolPlatform.Host.Communication;

public static class AudienceKinds
{
    public const string School = "School";   // whole installation
    public const string Grade = "Grade";     // one grade of a year
    public const string Course = "Course";   // one course
    public static readonly string[] All = [School, Grade, Course];
}

public static class AnnouncementStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Archived = "Archived";
}

// In-app notice. Recipients are resolved at read time from current enrollments, relationships and assignments;
// nothing is sent outside the platform. Guardians receive it only with the Communications faculty for that student.
public sealed class Announcement
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string AudienceKind { get; set; } = AudienceKinds.School;
    public Guid? AcademicYearId { get; set; }
    public Guid? AudienceId { get; set; }
    public bool ForStudents { get; set; }
    public bool ForGuardians { get; set; }
    public bool ForStaff { get; set; }
    public string Status { get; set; } = AnnouncementStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class AnnouncementRead
{
    public Guid AnnouncementId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ReadAt { get; set; }
}

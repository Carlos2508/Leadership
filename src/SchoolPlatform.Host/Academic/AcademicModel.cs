namespace SchoolPlatform.Host.Academic;

// A school year is a dimension of records, never a global session context. Every command names its year explicitly.
public sealed class AcademicYear
{
    public Guid Id { get; set; }
    public string Label { get; set; } = "";
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string Status { get; set; } = YearStatus.Draft;
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public byte[] Version { get; set; } = [];
    public List<AcademicPeriod> Periods { get; set; } = [];
}

public static class YearStatus
{
    public const string Draft = "Draft";
    public const string EnrollmentOpen = "EnrollmentOpen";
    public const string Active = "Active";
    public const string Closed = "Closed";
    public static readonly string[] Order = [Draft, EnrollmentOpen, Active, Closed];
    public static bool AcceptsEnrollment(string status) => status is EnrollmentOpen or Active;
    public static bool AllowsCalendarChanges(string status) => status is Draft or EnrollmentOpen;
}

public sealed class AcademicPeriod
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public int Sequence { get; set; }
    public string Name { get; set; } = "";
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
}

// Grade catalog of this school (not versioned per year in this increment).
public sealed class GradeLevel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Stage { get; set; } = "";
    public int Sequence { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] Version { get; set; } = [];
}

// Optional visible group inside a grade for one year. Enrollment never requires a section.
public sealed class Section
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid GradeLevelId { get; set; }
    public string Name { get; set; } = "";
    public string? Shift { get; set; }
    public int? Capacity { get; set; }
    public byte[] Version { get; set; } = [];
}

// One canonical enrollment per student and year. Changes are placements with effective dates.
public sealed class Enrollment
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public string Status { get; set; } = EnrollmentStatus.Active;
    public DateOnly EnrolledOn { get; set; }
    public DateOnly? WithdrawnOn { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] Version { get; set; } = [];
    public List<EnrollmentPlacement> Placements { get; set; } = [];
}

public static class EnrollmentStatus
{
    public const string Active = "Active";
    public const string Withdrawn = "Withdrawn";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";
}

// Interval [StartsOn, EndsOn): EndsOn is exclusive; null means current.
public sealed class EnrollmentPlacement
{
    public Guid Id { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid GradeLevelId { get; set; }
    public Guid? SectionId { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}

// Subject catalog of the school (e.g. Matemática). Plans/curriculum versions are a later refinement.
public sealed class Subject
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public byte[] Version { get; set; } = [];
}

// A subject taught in one year to a grade, optionally only to one section. Its roster is derived from dated placements.
public sealed class Course
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid GradeLevelId { get; set; }
    public Guid? SectionId { get; set; }
    public Guid SubjectId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public byte[] Version { get; set; } = [];
}

public static class TeachingRoles
{
    public const string Lead = "Lead";
    public const string Assistant = "Assistant";
}

// Dated assignment of a staff person to a course. Teaching scope (rosters, grades later) comes only from active assignments.
public sealed class TeachingAssignment
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Guid StaffId { get; set; }
    public string Role { get; set; } = TeachingRoles.Lead;
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public string? EndReason { get; set; }
}

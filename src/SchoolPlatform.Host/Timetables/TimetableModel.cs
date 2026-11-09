namespace SchoolPlatform.Host.Timetables;

public sealed class TimetablePlan
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid GradeLevelId { get; set; }
    public Guid? SectionId { get; set; }
}
public sealed class TimetableRevision
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public string State { get; set; } = "Draft";
    public DateOnly StartsOn { get; set; }
    public DateOnly UntilExclusive { get; set; }
    public string StructureJson { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public byte[] Version { get; set; } = [];
}
public sealed class TimetableEntry
{
    public Guid Id { get; set; }
    public Guid RevisionId { get; set; }
    public Guid PeriodId { get; set; }
    public int Day { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
    public string Kind { get; set; } = "Lesson";
    public Guid? CourseId { get; set; }
    public Guid? AssignmentId { get; set; }
    public Guid? StaffId { get; set; }
    public Guid? RoomId { get; set; }
    public string Subject { get; set; } = "";
    public string Teacher { get; set; } = "";
    public string Room { get; set; } = "";
}
public sealed class TimetableRoom
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}
public sealed class TimetableTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string StructureJson { get; set; } = "";
}
public sealed class TimetableOperation
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public byte[] Hash { get; set; } = [];
    public string ResultJson { get; set; } = "";
}
public sealed record TimetablePeriod(Guid Id, string Name, int Start, int End);
public sealed record TimetableStructure(int[] Days, TimetablePeriod[] Periods);
public sealed record TimetableCell(Guid PeriodId, int Day, string Kind, Guid? CourseId, Guid? AssignmentId, Guid? RoomId);
public sealed record SaveTimetable(Guid RequestId, Guid GradeId, Guid? SectionId, Guid? RevisionId, string? Version,
    DateOnly StartsOn, DateOnly Through, TimetableStructure Structure, TimetableCell[] Cells, string Reason);
public sealed record PublishTimetable(Guid RequestId, string Version, string Reason);
public sealed record SavedTimetable(Guid PlanId, Guid RevisionId, string Version);
public sealed record TimetableView(Guid Id, Guid PlanId, Guid YearId, string Year, Guid GradeId, string Grade,
    Guid? SectionId, string? Section, string State, DateOnly StartsOn, DateOnly Through, DateTimeOffset? PublishedAt,
    string Version, TimetableStructure Structure, TimetableCell[] Cells, TimetableIssue[] Issues);
public sealed record TimetableIssue(string Kind, string Message, int Day, Guid PeriodId);
public sealed record TimetableSummary(Guid PlanId, Guid GradeId, string Grade, Guid? SectionId, string? Section,
    Guid? DraftId, Guid? PublishedId, DateOnly? StartsOn, DateOnly? Through);
public sealed record TimetableSlot(int Day, int Start, int End, string Kind, string Subject, string Teacher,
    string Grade, string? Section, string Room, Guid? CourseId, Guid GradeId, Guid? SectionId);
public sealed record TimetableScope(Guid GradeId, Guid? SectionId, TimetableStructure Structure);
public sealed record PublishedTimetable(string Title, string Year, DateOnly On, TimetableSlot[] Slots, TimetableStructure Structure, TimetableScope[] Scopes);

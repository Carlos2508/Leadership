namespace SchoolPlatform.Host.Academic;

public sealed record YearRequest(string Label, DateOnly StartsOn, DateOnly EndsOn, string? Version = null);
public sealed record YearStatusRequest(string Version, string Status, string Reason);
public sealed record VersionRequest(string Version);
public sealed record PeriodRequest(string? Name, DateOnly StartsOn, DateOnly EndsOn);
public sealed record GradeRequest(string Code, string Name, string? Stage, int Sequence, bool IsActive = true, string? Version = null);
public sealed record SectionRequest(Guid GradeLevelId, string Name, string? Shift, int? Capacity, string? Version = null);
public sealed record GeneratePeriodsRequest(string Version, int Count);
public sealed record CopyFromYearRequest(Guid SourceYearId);

public sealed record YearSummary(Guid Id, string Label, DateOnly StartsOn, DateOnly EndsOn, string Status, bool IsDefault,
    string Version, int Enrollments);
public sealed record PeriodView(Guid Id, int Sequence, string Name, DateOnly StartsOn, DateOnly EndsOn);
public sealed record SectionView(Guid Id, Guid GradeLevelId, string Name, string? Shift, int? Capacity, int Current, string Version);
public sealed record YearDetail(YearSummary Year, PeriodView[] Periods, SectionView[] Sections);
public sealed record GradeView(Guid Id, string Code, string Name, string Stage, int Sequence, bool IsActive, string Version);

public sealed record EnrollRequest(Guid StudentId, Guid AcademicYearId, Guid GradeLevelId, Guid? SectionId, DateOnly? EnrolledOn, string? Reason);
public sealed record PlacementRequest(string Version, Guid GradeLevelId, Guid? SectionId, DateOnly EffectiveOn, string Reason);
public sealed record WithdrawRequest(string Version, DateOnly EffectiveOn, string Reason);

public sealed record PlacementView(Guid Id, Guid GradeLevelId, string Grade, Guid? SectionId, string? Section,
    DateOnly StartsOn, DateOnly? EndsOn, string Reason);
public sealed record EnrollmentRow(Guid Id, Guid StudentId, string StudentName, string StudentCode, string Status,
    DateOnly EnrolledOn, DateOnly? WithdrawnOn, Guid? GradeLevelId, string? Grade, Guid? SectionId, string? Section, string Version);
public sealed record EnrollmentHistory(Guid Id, Guid AcademicYearId, string Year, string YearStatus, string Status,
    DateOnly EnrolledOn, DateOnly? WithdrawnOn, string Version, PlacementView[] Placements);
public sealed record CurrentPlacement(string Year, string Grade, string? Section, string Status);

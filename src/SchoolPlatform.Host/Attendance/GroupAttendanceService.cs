using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Attendance;

public sealed record GroupKey(Guid GradeLevelId, Guid? SectionId);
public sealed record AttendanceGroup(Guid AcademicYearId, string Year, Guid GradeLevelId, string Grade, Guid? SectionId, string? Section, int Courses);
public sealed record GroupAttendanceRow(Guid StudentId, string Name, string Code, string Group, string? Status, string Note);
public sealed record GroupAttendanceSheet(Guid AcademicYearId, DateOnly Date, bool CanEdit, int Courses, GroupAttendanceRow[] Rows);
public sealed record GroupAttendanceEntry(Guid StudentId, string Status, string? Note);
public sealed record SaveGroupAttendanceRequest(Guid AcademicYearId, GroupKey[] Groups, DateOnly Date, GroupAttendanceEntry[] Entries);
public sealed record SaveGroupAttendanceResult(int Students, int Courses, int Changed);

/// <summary>
/// Daily attendance per grade/section (or several grades together, for multigrade rooms). The teacher marks each student
/// once and the same status is written to every course of that student's group on that date that the teacher may teach,
/// so the group is not marked once per subject. Per-course sheets keep working and show the same records.
/// </summary>
public sealed class GroupAttendanceService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, CourseService courses)
{
    /// <summary>Groups the account can take attendance for: from its own courses, or every open year for academic management.</summary>
    public async Task<AttendanceGroup[]> GroupsAsync(Guid userId, CancellationToken ct)
    {
        var teachable = await TeachableCoursesAsync(userId, null, ct);
        if (teachable.Length == 0) return [];
        var yearIds = teachable.Select(x => x.AcademicYearId).Distinct().ToArray();
        var sections = await db.Sections.AsNoTracking().Where(x => yearIds.Contains(x.AcademicYearId))
            .Select(x => new { x.Id, x.AcademicYearId, x.GradeLevelId, x.Name }).ToArrayAsync(ct);
        var years = await db.Years.AsNoTracking().Where(x => yearIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Label, ct);
        var grades = await db.Grades.AsNoTracking().ToDictionaryAsync(x => x.Id, x => new { x.Name, x.Sequence }, ct);
        var result = new Dictionary<(Guid, Guid, Guid?), (int Courses, string? Section)>();
        foreach (var course in teachable)
        {
            var gradeSections = sections.Where(s => s.AcademicYearId == course.AcademicYearId && s.GradeLevelId == course.GradeLevelId).ToArray();
            // A whole-grade course covers every section of the grade; a grade without sections is one group.
            (Guid? Id, string? Name)[] targets = course.SectionId is not null
                ? [(course.SectionId, gradeSections.FirstOrDefault(s => s.Id == course.SectionId)?.Name)]
                : gradeSections.Length == 0 ? [(null, null)] : gradeSections.Select(s => ((Guid?)s.Id, (string?)s.Name)).ToArray();
            foreach (var (sectionId, name) in targets)
            {
                var key = (course.AcademicYearId, course.GradeLevelId, sectionId);
                result[key] = (result.TryGetValue(key, out var current) ? current.Courses + 1 : 1, name);
            }
        }
        return result.Select(x => new AttendanceGroup(x.Key.Item1, years[x.Key.Item1], x.Key.Item2, grades[x.Key.Item2].Name, x.Key.Item3,
                x.Value.Section, x.Value.Courses))
            .OrderBy(x => x.Year).ThenBy(x => grades[x.GradeLevelId].Sequence).ThenBy(x => x.Section).ToArray();
    }

    public async Task<GroupAttendanceSheet?> SheetAsync(Guid userId, Guid yearId, GroupKey[] groups, DateOnly? date, CancellationToken ct)
    {
        var (year, day) = await ResolveAsync(yearId, date, allowFuture: true, ct);
        var plan = await PlanAsync(userId, year, groups, day, ct);
        if (plan is null) return null;
        var courseIds = plan.Courses.Select(x => x.Id).ToArray();
        var studentIds = plan.Students.Select(x => x.Id).ToArray();
        var records = await (from r in db.AttendanceRecords.AsNoTracking()
                             join s in db.AttendanceSessions on r.SessionId equals s.Id
                             where s.Date == day && courseIds.Contains(s.CourseId) && studentIds.Contains(r.StudentId)
                             select new { r.StudentId, r.Status, r.Note, r.UpdatedAt }).ToArrayAsync(ct);
        var latest = records.GroupBy(x => x.StudentId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());
        var editable = day <= courses.LocalDate(time.GetUtcNow());
        return new(year.Id, day, editable, plan.Courses.Length, plan.Students.Select(s => latest.TryGetValue(s.Id, out var r)
            ? new GroupAttendanceRow(s.Id, s.Name, s.Code, s.Group, r.Status, r.Note)
            : new GroupAttendanceRow(s.Id, s.Name, s.Code, s.Group, null, "")).ToArray());
    }

    public async Task<SaveGroupAttendanceResult> SaveAsync(Guid userId, SaveGroupAttendanceRequest request, CancellationToken ct)
    {
        var (year, day) = await ResolveAsync(request.AcademicYearId, request.Date, allowFuture: false, ct);
        var entries = request.Entries ?? [];
        if (entries.Length is 0 or > 400 || entries.Select(x => x.StudentId).Distinct().Count() != entries.Length)
            throw OperationException.Invalid("Envía una fila por estudiante.");
        if (entries.Any(x => !AttendanceStatus.All.Contains(x.Status) || (x.Note?.Length ?? 0) > 200))
            throw OperationException.Invalid("Revisa el estado y la observación (máximo 200 caracteres).");
        var plan = await PlanAsync(userId, year, request.Groups, day, ct)
            ?? throw new OperationException(403, "teaching-denied", "No tienes cursos asignados en ese grupo.");
        var students = plan.Students.ToDictionary(x => x.Id);
        if (entries.Any(x => !students.ContainsKey(x.StudentId))) throw OperationException.Invalid("Un estudiante no pertenecía al grupo en esa fecha.");

        var now = time.GetUtcNow();
        var courseIds = plan.Courses.Select(x => x.Id).ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var sessions = await db.AttendanceSessions.Where(x => courseIds.Contains(x.CourseId) && x.Date == day).ToDictionaryAsync(x => x.CourseId, ct);
        var sessionIds = sessions.Values.Select(x => x.Id).ToArray();
        var current = await db.AttendanceRecords.Where(x => sessionIds.Contains(x.SessionId)).ToListAsync(ct);
        var changed = 0;
        var touchedCourses = new HashSet<Guid>();
        foreach (var entry in entries)
        {
            var note = entry.Note?.Trim() ?? "";
            var student = students[entry.StudentId];
            foreach (var course in plan.Courses.Where(c => c.GradeLevelId == student.GradeLevelId && (c.SectionId == null || c.SectionId == student.SectionId)))
            {
                if (!sessions.TryGetValue(course.Id, out var session))
                {
                    session = new AttendanceSession { Id = Guid.NewGuid(), CourseId = course.Id, Date = day, CreatedAt = now, CreatedBy = userId };
                    db.AttendanceSessions.Add(session);
                    sessions[course.Id] = session;
                }
                var record = current.FirstOrDefault(r => r.SessionId == session.Id && r.StudentId == entry.StudentId);
                if (record is null)
                {
                    record = new AttendanceRecord
                    {
                        Id = Guid.NewGuid(), SessionId = session.Id, StudentId = entry.StudentId, Status = entry.Status, Note = note,
                        UpdatedAt = now, UpdatedBy = userId
                    };
                    db.AttendanceRecords.Add(record);
                    current.Add(record);
                }
                else
                {
                    if (record.Status == entry.Status && record.Note == note) continue;
                    record.Status = entry.Status;
                    record.Note = note;
                    record.UpdatedAt = now;
                    record.UpdatedBy = userId;
                }
                touchedCourses.Add(course.Id);
                changed++;
            }
        }
        foreach (var courseId in touchedCourses)
            audit.Record("AttendanceRecorded", userId, "Course", courseId, detail: $"{day:yyyy-MM-dd}:grupo");
        if (changed > 0) await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(entries.Length, touchedCourses.Count, changed);
    }

    // ---- helpers

    private sealed record PlanCourse(Guid Id, Guid AcademicYearId, Guid GradeLevelId, Guid? SectionId);
    private sealed record PlanStudent(Guid Id, string Name, string Code, string Group, Guid GradeLevelId, Guid? SectionId);
    private sealed record Plan(PlanCourse[] Courses, PlanStudent[] Students);

    private async Task<Plan?> PlanAsync(Guid userId, AcademicYear year, GroupKey[]? groups, DateOnly day, CancellationToken ct)
    {
        groups = (groups ?? []).Distinct().ToArray();
        if (groups.Length is 0 or > 12) throw OperationException.Invalid("Selecciona entre 1 y 12 grupos.");
        var teachable = (await TeachableCoursesAsync(userId, year.Id, ct))
            .Where(c => groups.Any(g => g.GradeLevelId == c.GradeLevelId && (g.SectionId == null || c.SectionId == null || c.SectionId == g.SectionId)))
            .ToArray();
        if (teachable.Length == 0) return null;
        var gradeIds = groups.Select(g => g.GradeLevelId).Distinct().ToArray();
        var placements = await (from p in db.Placements.AsNoTracking()
                                join e in db.Enrollments on p.EnrollmentId equals e.Id
                                where e.AcademicYearId == year.Id && e.Status != EnrollmentStatus.Cancelled && gradeIds.Contains(p.GradeLevelId) &&
                                      p.StartsOn <= day && (p.EndsOn == null || p.EndsOn > day)
                                join person in db.Persons on e.StudentId equals person.Id
                                join student in db.Students on e.StudentId equals student.PersonId
                                join grade in db.Grades on p.GradeLevelId equals grade.Id
                                select new
                                {
                                    e.StudentId, person.GivenNames, person.FamilyNames, person.PreferredName, student.StudentCode, p.GradeLevelId,
                                    p.SectionId, Grade = grade.Name, grade.Sequence,
                                    Section = db.Sections.Where(s => s.Id == p.SectionId).Select(s => s.Name).FirstOrDefault()
                                }).ToArrayAsync(ct);
        var students = placements
            .Where(p => groups.Any(g => g.GradeLevelId == p.GradeLevelId && (g.SectionId == null || g.SectionId == p.SectionId)))
            // Only students reached by at least one course this account may teach.
            .Where(p => teachable.Any(c => c.GradeLevelId == p.GradeLevelId && (c.SectionId == null || c.SectionId == p.SectionId)))
            .DistinctBy(p => p.StudentId)
            .OrderBy(p => p.Sequence).ThenBy(p => p.Section).ThenBy(p => p.FamilyNames).ThenBy(p => p.GivenNames)
            .Select(p => new PlanStudent(p.StudentId, PeopleService.Display(p.GivenNames, p.FamilyNames, p.PreferredName), p.StudentCode,
                p.Section is null ? p.Grade : $"{p.Grade} {p.Section}", p.GradeLevelId, p.SectionId))
            .ToArray();
        return new(teachable, students);
    }

    private async Task<PlanCourse[]> TeachableCoursesAsync(Guid userId, Guid? yearId, CancellationToken ct)
    {
        var isManager = await db.Permissions.AnyAsync(x => x.UserId == userId && x.Permission == Permissions.AcademicManage, ct) &&
                        await db.Accounts.AnyAsync(x => x.Id == userId && x.IsActive, ct);
        if (isManager)
            return (await db.Courses.AsNoTracking()
                    .Where(c => (yearId == null || c.AcademicYearId == yearId) && db.Years.Any(y => y.Id == c.AcademicYearId && y.Status != YearStatus.Closed))
                    .Select(c => new { c.Id, c.AcademicYearId, c.GradeLevelId, c.SectionId }).ToArrayAsync(ct))
                .Select(c => new PlanCourse(c.Id, c.AcademicYearId, c.GradeLevelId, c.SectionId)).ToArray();
        var mine = await courses.MyCoursesAsync(userId, ct);
        return mine.Where(c => yearId == null || c.AcademicYearId == yearId)
            .Select(c => new PlanCourse(c.Id, c.AcademicYearId, c.GradeLevelId, c.SectionId)).ToArray();
    }

    private async Task<(AcademicYear Year, DateOnly Day)> ResolveAsync(Guid yearId, DateOnly? date, bool allowFuture, CancellationToken ct)
    {
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == yearId, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");
        var today = courses.LocalDate(time.GetUtcNow());
        var day = date ?? (today < year.StartsOn ? year.StartsOn : today > year.EndsOn ? year.EndsOn : today);
        if (day < year.StartsOn || day > year.EndsOn) throw OperationException.Invalid("La fecha debe estar dentro del ciclo.");
        if (day > today && !allowFuture) throw OperationException.Invalid("No se registra asistencia de fechas futuras.");
        return (year, day);
    }
}

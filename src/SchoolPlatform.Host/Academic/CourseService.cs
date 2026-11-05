using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Academic;

public sealed record SubjectRequest(string Code, string Name, bool IsActive = true, string? Version = null);
public sealed record SubjectView(Guid Id, string Code, string Name, bool IsActive, string Version);
public sealed record CreateCoursesRequest(Guid GradeLevelId, Guid? SectionId, Guid[] SubjectIds);
public sealed record TeacherView(Guid AssignmentId, Guid StaffId, string Name, string Role, DateOnly StartsOn, DateOnly? EndsOn);
public sealed record CourseView(Guid Id, Guid AcademicYearId, string Year, Guid GradeLevelId, string Grade, Guid? SectionId, string? Section,
    Guid SubjectId, string Subject, TeacherView[] Teachers, int Students);
public sealed record AssignTeacherRequest(Guid StaffId, string Role, DateOnly? StartsOn, string Reason);
public sealed record EndAssignmentRequest(DateOnly? EndsOn, string Reason);
public sealed record RosterStudent(Guid StudentId, string Name, string Code, string? Section, DateOnly From, DateOnly? Until);
public sealed record CourseRoster(CourseView Course, DateOnly On, RosterStudent[] Students);
public sealed record StaffOption(Guid Id, string Name, string Position);

/// <summary>
/// Subjects, courses per year/grade/optional section and dated teaching assignments. A course roster is derived from
/// enrollment placements effective on a date, so moving a student between sections changes their courses from that date.
/// </summary>
public sealed partial class CourseService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, IOptions<InstitutionOptions> institution)
{
    public async Task<SubjectView[]> SubjectsAsync(CancellationToken ct) =>
        (await db.Subjects.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(ct))
        .Select(x => new SubjectView(x.Id, x.Code, x.Name, x.IsActive, Versions.Encode(x.Version))).ToArray();

    public async Task<Guid> SaveSubjectAsync(Guid actor, Guid? id, SubjectRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var code = (request.Code ?? "").Trim().ToUpperInvariant();
        var name = WhiteSpace().Replace(request.Name?.Trim() ?? "", " ");
        if (!CodePattern().IsMatch(code)) throw OperationException.Invalid("El código admite de 1 a 20 letras, números o guion.");
        if (name.Length is < 1 or > 80) throw OperationException.Invalid("Escribe el nombre de la materia (máximo 80 caracteres).");
        Subject subject;
        if (id is null) db.Subjects.Add(subject = new Subject { Id = Guid.NewGuid() });
        else
        {
            subject = await db.Subjects.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos esa materia.");
            if (!subject.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
        }
        if (await db.Subjects.AnyAsync(x => x.Id != subject.Id && (x.Code == code || x.Name == name), ct))
            throw new OperationException(409, "subject-exists", "Ya existe una materia con ese código o nombre.");
        subject.Code = code;
        subject.Name = name;
        subject.IsActive = request.IsActive;
        audit.Record(id is null ? "SubjectCreated" : "SubjectUpdated", actor, "Subject", subject.Id, detail: code);
        await db.SaveChangesAsync(ct);
        return subject.Id;
    }

    public async Task<CourseView[]> CoursesAsync(Guid yearId, Guid? gradeId, CancellationToken ct)
    {
        var ids = await db.Courses.AsNoTracking().Where(x => x.AcademicYearId == yearId && (gradeId == null || x.GradeLevelId == gradeId))
            .Select(x => x.Id).ToArrayAsync(ct);
        return await ViewsAsync(ids, ct);
    }

    /// <summary>One course view without loading every course of its year.</summary>
    public async Task<CourseView> CourseAsync(Guid courseId, CancellationToken ct) =>
        (await ViewsAsync([courseId], ct)).SingleOrDefault() ?? throw OperationException.NotFound("No encontramos ese curso.");

    public async Task<int> CreateCoursesAsync(Guid actor, Guid yearId, CreateCoursesRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == yearId, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");
        if (year.Status == YearStatus.Closed) throw new OperationException(409, "year-closed", "El ciclo está cerrado.");
        if (!await db.Grades.AnyAsync(x => x.Id == request.GradeLevelId && x.IsActive, ct)) throw OperationException.Invalid("Selecciona un grado activo.");
        if (request.SectionId is not null && !await db.Sections.AnyAsync(x => x.Id == request.SectionId && x.AcademicYearId == yearId &&
                x.GradeLevelId == request.GradeLevelId, ct))
            throw OperationException.Invalid("La sección no pertenece a ese grado y ciclo.");
        var subjects = (request.SubjectIds ?? []).Distinct().ToArray();
        if (subjects.Length is 0 or > 40) throw OperationException.Invalid("Selecciona entre 1 y 40 materias.");
        if (await db.Subjects.CountAsync(x => subjects.Contains(x.Id) && x.IsActive, ct) != subjects.Length)
            throw OperationException.Invalid("Selecciona materias activas.");
        var existing = await db.Courses.AsNoTracking().Where(x => x.AcademicYearId == yearId && x.GradeLevelId == request.GradeLevelId &&
            subjects.Contains(x.SubjectId)).Select(x => new { x.SubjectId, x.SectionId }).ToArrayAsync(ct);
        // A subject is taught either to the whole grade or per section, never both at once (students would be counted twice).
        if (existing.Any(x => request.SectionId is null ? x.SectionId is not null : x.SectionId is null))
            throw new OperationException(409, "course-scope-conflict",
                "Una materia ya está organizada de otra forma en ese grado (todo el grado o por sección).");
        var created = 0;
        foreach (var subject in subjects.Where(s => !existing.Any(x => x.SubjectId == s && x.SectionId == request.SectionId)))
        {
            db.Courses.Add(new Course
            {
                Id = Guid.NewGuid(), AcademicYearId = yearId, GradeLevelId = request.GradeLevelId, SectionId = request.SectionId,
                SubjectId = subject, CreatedAt = time.GetUtcNow()
            });
            created++;
        }
        if (created == 0) return 0;
        audit.Record("CoursesCreated", actor, "AcademicYear", yearId, detail: $"{created}");
        await db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<StaffOption[]> StaffAsync(string? search, CancellationToken ct)
    {
        var term = WhiteSpace().Replace(search?.Trim() ?? "", " ");
        var query = db.Staff.AsNoTracking().Join(db.Persons.Where(p => p.Status == PersonStatus.Active), s => s.PersonId, p => p.Id,
            (s, p) => new { s.PersonId, s.Position, p.GivenNames, p.FamilyNames, p.PreferredName });
        if (term != "") query = query.Where(x => x.GivenNames.Contains(term) || x.FamilyNames.Contains(term) || x.Position.Contains(term));
        return (await query.OrderBy(x => x.FamilyNames).Take(30).ToArrayAsync(ct))
            .Select(x => new StaffOption(x.PersonId, PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName), x.Position)).ToArray();
    }

    public async Task<Guid> AssignAsync(Guid actor, Guid courseId, AssignTeacherRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var reason = Reasons.Require(request.Reason);
        if (request.Role is not (TeachingRoles.Lead or TeachingRoles.Assistant)) throw OperationException.Invalid("Selecciona la función docente.");
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courseId, ct) ?? throw CourseNotFound();
        var year = await db.Years.AsNoTracking().SingleAsync(x => x.Id == course.AcademicYearId, ct);
        if (year.Status == YearStatus.Closed) throw new OperationException(409, "year-closed", "El ciclo está cerrado.");
        if (!await db.Staff.AnyAsync(s => s.PersonId == request.StaffId && db.Persons.Any(p => p.Id == s.PersonId && p.Status == PersonStatus.Active), ct))
            throw OperationException.Invalid("Selecciona a una persona activa con perfil de personal.");
        var starts = request.StartsOn ?? Clamp(Today(), year);
        if (starts < year.StartsOn || starts > year.EndsOn) throw OperationException.Invalid("La fecha debe estar dentro del ciclo.");
        if (await db.TeachingAssignments.AnyAsync(x => x.CourseId == courseId && x.StaffId == request.StaffId && x.EndsOn == null, ct))
            throw new OperationException(409, "already-assigned", "Esa persona ya está asignada al curso.");
        var assignment = new TeachingAssignment
        {
            Id = Guid.NewGuid(), CourseId = courseId, StaffId = request.StaffId, Role = request.Role, StartsOn = starts,
            CreatedAt = time.GetUtcNow(), CreatedBy = actor
        };
        db.TeachingAssignments.Add(assignment);
        audit.Record("TeacherAssigned", actor, "Course", courseId, reason, request.Role);
        await db.SaveChangesAsync(ct);
        return assignment.Id;
    }

    public async Task EndAssignmentAsync(Guid actor, Guid courseId, Guid assignmentId, EndAssignmentRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var reason = Reasons.Require(request.Reason);
        var assignment = await db.TeachingAssignments.SingleOrDefaultAsync(x => x.Id == assignmentId && x.CourseId == courseId, ct)
            ?? throw OperationException.NotFound("No encontramos esa asignación.");
        if (assignment.EndsOn is not null) throw new OperationException(409, "assignment-ended", "La asignación ya finalizó.");
        var ends = request.EndsOn ?? Today();
        if (ends < assignment.StartsOn) throw OperationException.Invalid("La fecha final no puede ser anterior al inicio.");
        assignment.EndsOn = ends;
        assignment.EndReason = reason;
        audit.Record("TeacherUnassigned", actor, "Course", courseId, reason);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Staff with academic.read see every course; a teacher only courses whose assignment has not ended.</summary>
    public async Task<bool> CanViewAsync(Guid userId, Guid courseId, CancellationToken ct)
    {
        if (await db.Permissions.AnyAsync(x => x.UserId == userId && x.Permission == Permissions.AcademicRead, ct)) return true;
        var today = Today();
        return await TeacherCourses(userId, today).AnyAsync(x => x == courseId, ct);
    }

    /// <summary>Teaching actions (capture, activities): academic management or a non-ended assignment to the course.</summary>
    public async Task<bool> CanTeachAsync(Guid userId, Guid courseId, CancellationToken ct) =>
        (await db.Permissions.AnyAsync(x => x.UserId == userId && x.Permission == Permissions.AcademicManage, ct) &&
         await db.Accounts.AnyAsync(x => x.Id == userId && x.IsActive, ct)) ||
        await TeacherCourses(userId, Today()).AnyAsync(x => x == courseId, ct);

    /// <summary>Students who belonged to the course at any moment of [from, to] (dated placements; empty intervals excluded).</summary>
    public async Task<(Guid Id, string Name, string Code)[]> StudentsBetweenAsync(Guid courseId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courseId, ct) ?? throw CourseNotFound();
        var rows = await (from p in db.Placements.AsNoTracking()
                          join e in db.Enrollments on p.EnrollmentId equals e.Id
                          where e.AcademicYearId == course.AcademicYearId && e.Status != EnrollmentStatus.Cancelled &&
                                p.GradeLevelId == course.GradeLevelId && (course.SectionId == null || p.SectionId == course.SectionId) &&
                                p.StartsOn <= end && (p.EndsOn == null || (p.EndsOn > start && p.EndsOn > p.StartsOn))
                          join person in db.Persons on e.StudentId equals person.Id
                          join student in db.Students on e.StudentId equals student.PersonId
                          select new { e.StudentId, person.GivenNames, person.FamilyNames, person.PreferredName, student.StudentCode })
            .Distinct().ToArrayAsync(ct);
        return rows.OrderBy(x => x.FamilyNames).ThenBy(x => x.GivenNames)
            .Select(x => (x.StudentId, PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName), x.StudentCode)).ToArray();
    }

    internal DateOnly LocalDate(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant,
        TimeZoneInfo.FindSystemTimeZoneById(institution.Value.Locale.TimeZone)).DateTime);

    public async Task<CourseView[]> MyCoursesAsync(Guid userId, CancellationToken ct) =>
        await ViewsAsync(await TeacherCourses(userId, Today()).ToArrayAsync(ct), ct);

    public async Task<CourseRoster> RosterAsync(Guid courseId, DateOnly? on, CancellationToken ct)
    {
        var course = (await ViewsAsync([courseId], ct)).SingleOrDefault() ?? throw CourseNotFound();
        var year = await db.Years.AsNoTracking().SingleAsync(x => x.Id == course.AcademicYearId, ct);
        var date = on ?? Clamp(Today(), year);
        var students = await (from p in db.Placements.AsNoTracking()
                              join e in db.Enrollments on p.EnrollmentId equals e.Id
                              where e.AcademicYearId == course.AcademicYearId && e.Status != EnrollmentStatus.Cancelled &&
                                    p.GradeLevelId == course.GradeLevelId && (course.SectionId == null || p.SectionId == course.SectionId) &&
                                    p.StartsOn <= date && (p.EndsOn == null || p.EndsOn > date)
                              join person in db.Persons on e.StudentId equals person.Id
                              join student in db.Students on e.StudentId equals student.PersonId
                              select new
                              {
                                  e.StudentId, person.GivenNames, person.FamilyNames, person.PreferredName, student.StudentCode,
                                  Section = db.Sections.Where(s => s.Id == p.SectionId).Select(s => s.Name).FirstOrDefault(), p.StartsOn, p.EndsOn
                              }).OrderBy(x => x.FamilyNames).ThenBy(x => x.GivenNames).ToArrayAsync(ct);
        return new(course, date, students.Select(x => new RosterStudent(x.StudentId, PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName),
            x.StudentCode, x.Section, x.StartsOn, x.EndsOn)).ToArray());
    }

    // ---- helpers

    private IQueryable<Guid> TeacherCourses(Guid userId, DateOnly today) =>
        from link in db.AccountLinks
        where link.UserId == userId && link.UnlinkedAt == null
        join assignment in db.TeachingAssignments on link.PersonId equals assignment.StaffId
        // Access lasts while the assignment has not ended, so a teacher can prepare a course before its start date.
        where assignment.EndsOn == null || assignment.EndsOn >= today
        where db.Persons.Any(p => p.Id == link.PersonId && p.Status == PersonStatus.Active)
        select assignment.CourseId;

    private async Task<CourseView[]> ViewsAsync(Guid[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return [];
        var today = Today();
        var courses = await (from c in db.Courses.AsNoTracking()
                             where ids.Contains(c.Id)
                             join y in db.Years on c.AcademicYearId equals y.Id
                             join g in db.Grades on c.GradeLevelId equals g.Id
                             join s in db.Subjects on c.SubjectId equals s.Id
                             select new
                             {
                                 Course = c, Year = y.Label, Grade = g.Name, GradeOrder = g.Sequence, Subject = s.Name,
                                 Section = db.Sections.Where(x => x.Id == c.SectionId).Select(x => x.Name).FirstOrDefault()
                             }).ToArrayAsync(ct);
        var teachers = await (from a in db.TeachingAssignments.AsNoTracking()
                              where ids.Contains(a.CourseId) && (a.EndsOn == null || a.EndsOn >= today)
                              join p in db.Persons on a.StaffId equals p.Id
                              select new { a, p.GivenNames, p.FamilyNames, p.PreferredName }).ToArrayAsync(ct);
        var counts = new Dictionary<Guid, int>();
        foreach (var course in courses)
            counts[course.Course.Id] = await db.Placements.CountAsync(p => p.GradeLevelId == course.Course.GradeLevelId &&
                (course.Course.SectionId == null || p.SectionId == course.Course.SectionId) && p.EndsOn == null &&
                db.Enrollments.Any(e => e.Id == p.EnrollmentId && e.AcademicYearId == course.Course.AcademicYearId && e.Status == EnrollmentStatus.Active), ct);
        return courses.OrderBy(x => x.GradeOrder).ThenBy(x => x.Grade).ThenBy(x => x.Section).ThenBy(x => x.Subject)
            .Select(x => new CourseView(x.Course.Id, x.Course.AcademicYearId, x.Year, x.Course.GradeLevelId, x.Grade, x.Course.SectionId, x.Section,
                x.Course.SubjectId, x.Subject,
                teachers.Where(t => t.a.CourseId == x.Course.Id).OrderBy(t => t.a.Role).Select(t => new TeacherView(t.a.Id, t.a.StaffId,
                    PeopleService.Display(t.GivenNames, t.FamilyNames, t.PreferredName), t.a.Role, t.a.StartsOn, t.a.EndsOn)).ToArray(),
                counts[x.Course.Id])).ToArray();
    }

    private static DateOnly Clamp(DateOnly date, AcademicYear year) =>
        date < year.StartsOn ? year.StartsOn : date > year.EndsOn ? year.EndsOn : date;

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById(institution.Value.Locale.TimeZone)).DateTime);

    private static OperationException CourseNotFound() => OperationException.NotFound("No encontramos ese curso.");

    [GeneratedRegex(@"\s+")] private static partial Regex WhiteSpace();
    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9\-]{0,19}$")] private static partial Regex CodePattern();
}

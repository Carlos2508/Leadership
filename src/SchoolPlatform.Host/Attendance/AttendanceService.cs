using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Attendance;

public static class AttendanceStatus
{
    public const string Present = "Present";
    public const string Absent = "Absent";
    public const string Late = "Late";
    public const string Excused = "Excused";
    public static readonly string[] All = [Present, Absent, Late, Excused];
}

// One attendance session per course and school date; one record per student. No a1..a31 columns.
public sealed class AttendanceSession
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public DateOnly Date { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}

public sealed class AttendanceRecord
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public string Status { get; set; } = AttendanceStatus.Present;
    public string Note { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed record AttendanceEntry(Guid StudentId, string Status, string? Note, string? Version);
public sealed record SaveAttendanceRequest(DateOnly Date, AttendanceEntry[] Entries);
public sealed record AttendanceRow(Guid StudentId, string Name, string Code, string? Status, string Note, string? Version);
/// <summary>ReadOnlyReason explains a sheet that cannot be edited (a future date, or not the assigned teacher).</summary>
public sealed record AttendanceSheet(CourseView Course, DateOnly Date, bool CanEdit, AttendanceRow[] Rows, string? ReadOnlyReason = null);
public sealed record AttendanceDay(DateOnly Date, string Subject, string Status, string Note);
public sealed record AttendanceSummary(int Present, int Absent, int Late, int Excused, AttendanceDay[] Incidents);
public sealed record CalendarEntry(string Subject, string Status, string Note);
public sealed record CalendarDay(DateOnly Date, CalendarEntry[] Entries);
public sealed record AttendanceCalendar(DateOnly From, DateOnly To, int Present, int Absent, int Late, int Excused, CalendarDay[] Days);

/// <summary>
/// Daily attendance per course. Taken by the assigned teacher (or academic management) for students in the course on that
/// date; dates outside the year or in the future are rejected. Families with AcademicRecords see a summary; not tied to clearance.
/// </summary>
public sealed class AttendanceService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, CourseService courses, PeopleService people)
{
    public async Task<AttendanceSheet?> SheetAsync(Guid userId, Guid courseId, DateOnly? date, CancellationToken ct)
    {
        if (!await courses.CanViewAsync(userId, courseId, ct)) return null;
        var (course, day) = await ResolveAsync(courseId, date, ct, allowFuture: true);
        var students = await courses.StudentsBetweenAsync(courseId, day, day, ct);
        var ids = students.Select(x => x.Id).ToArray();
        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(r => ids.Contains(r.StudentId) && db.AttendanceSessions.Any(s => s.Id == r.SessionId && s.CourseId == courseId && s.Date == day))
            .ToDictionaryAsync(x => x.StudentId, ct);
        var today = courses.LocalDate(time.GetUtcNow());
        var teaches = await courses.CanTeachAsync(userId, courseId, ct);
        var reason = !teaches ? "Solo el docente asignado al curso o coordinación académica registran la asistencia."
            : day > today ? $"Es una fecha futura: la asistencia del {day:dd/MM/yyyy} se registra ese día o después (hoy es {today:dd/MM/yyyy})."
            : null;
        return new(course, day, reason is null, students.Select(s => records.TryGetValue(s.Id, out var r)
            ? new AttendanceRow(s.Id, s.Name, s.Code, r.Status, r.Note, Versions.Encode(r.Version))
            : new AttendanceRow(s.Id, s.Name, s.Code, null, "", null)).ToArray(), reason);
    }

    public async Task<int> SaveAsync(Guid userId, Guid courseId, SaveAttendanceRequest request, CancellationToken ct)
    {
        if (!await courses.CanTeachAsync(userId, courseId, ct)) throw new OperationException(403, "teaching-denied", "No tienes asignado este curso.");
        var (_, day) = await ResolveAsync(courseId, request.Date, ct);
        var entries = request.Entries ?? [];
        if (entries.Length is 0 or > 200 || entries.Select(x => x.StudentId).Distinct().Count() != entries.Length)
            throw OperationException.Invalid("Envía una fila por estudiante.");
        if (entries.Any(x => !AttendanceStatus.All.Contains(x.Status) || (x.Note?.Length ?? 0) > 200))
            throw OperationException.Invalid("Revisa el estado y la observación (máximo 200 caracteres).");
        var allowed = (await courses.StudentsBetweenAsync(courseId, day, day, ct)).Select(x => x.Id).ToHashSet();
        if (entries.Any(x => !allowed.Contains(x.StudentId))) throw OperationException.Invalid("Un estudiante no pertenecía al curso en esa fecha.");
        var now = time.GetUtcNow();
        var session = await db.AttendanceSessions.SingleOrDefaultAsync(x => x.CourseId == courseId && x.Date == day, ct);
        if (session is null)
        {
            session = new AttendanceSession { Id = Guid.NewGuid(), CourseId = courseId, Date = day, CreatedAt = now, CreatedBy = userId };
            db.AttendanceSessions.Add(session);
        }
        var current = await db.AttendanceRecords.Where(x => x.SessionId == session.Id).ToDictionaryAsync(x => x.StudentId, ct);
        var changed = 0;
        foreach (var entry in entries)
        {
            var note = entry.Note?.Trim() ?? "";
            if (current.TryGetValue(entry.StudentId, out var record))
            {
                if (!record.Version.AsSpan().SequenceEqual(Versions.Decode(entry.Version))) throw OperationException.Changed();
                if (record.Status == entry.Status && record.Note == note) continue;
                record.Status = entry.Status;
                record.Note = note;
                record.UpdatedAt = now;
                record.UpdatedBy = userId;
            }
            else
            {
                if (!string.IsNullOrEmpty(entry.Version)) throw OperationException.Changed();
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(), SessionId = session.Id, StudentId = entry.StudentId, Status = entry.Status, Note = note,
                    UpdatedAt = now, UpdatedBy = userId
                });
            }
            changed++;
        }
        if (changed == 0) return 0;
        audit.Record("AttendanceRecorded", userId, "Course", courseId, detail: $"{day:yyyy-MM-dd}:{changed}");
        await db.SaveChangesAsync(ct);
        return changed;
    }

    /// <summary>Summary for the student or a guardian with AcademicRecords in the student's current year; null when no access.</summary>
    public async Task<AttendanceSummary?> SummaryAsync(Guid userId, Guid studentId, CancellationToken ct)
    {
        var access = await people.StudentAccessAsync(userId, studentId, ct);
        if (access is null || !access.Has(Faculties.AcademicRecords)) return null;
        var rows = await (from r in db.AttendanceRecords.AsNoTracking()
                          where r.StudentId == studentId
                          join s in db.AttendanceSessions on r.SessionId equals s.Id
                          join c in db.Courses on s.CourseId equals c.Id
                          join y in db.Years on c.AcademicYearId equals y.Id
                          join subject in db.Subjects on c.SubjectId equals subject.Id
                          select new { s.Date, Subject = subject.Name, r.Status, r.Note, y.IsDefault, YearStart = y.StartsOn })
            .ToArrayAsync(ct);
        var year = rows.OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.YearStart).Select(x => (DateOnly?)x.YearStart).FirstOrDefault();
        var current = rows.Where(x => x.YearStart == year).ToArray();
        return new(current.Count(x => x.Status == AttendanceStatus.Present), current.Count(x => x.Status == AttendanceStatus.Absent),
            current.Count(x => x.Status == AttendanceStatus.Late), current.Count(x => x.Status == AttendanceStatus.Excused),
            current.Where(x => x.Status != AttendanceStatus.Present).OrderByDescending(x => x.Date).Take(30)
                .Select(x => new AttendanceDay(x.Date, x.Subject, x.Status, x.Note)).ToArray());
    }

    /// <summary>
    /// Day-by-day attendance of a student between two dates (at most 62 days), per subject, for the student or a guardian
    /// with AcademicRecords; null when there is no access. Days without records are simply absent from the result.
    /// </summary>
    public async Task<AttendanceCalendar?> CalendarAsync(Guid userId, Guid studentId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var access = await people.StudentAccessAsync(userId, studentId, ct);
        if (access is null || !access.Has(Faculties.AcademicRecords)) return null;
        if (end < start || end.DayNumber - start.DayNumber > 62) throw OperationException.Invalid("Consulta hasta dos meses a la vez.");
        var rows = await (from r in db.AttendanceRecords.AsNoTracking()
                          where r.StudentId == studentId
                          join s in db.AttendanceSessions on r.SessionId equals s.Id
                          where s.Date >= start && s.Date <= end
                          join c in db.Courses on s.CourseId equals c.Id
                          join subject in db.Subjects on c.SubjectId equals subject.Id
                          select new { s.Date, Subject = subject.Name, r.Status, r.Note }).ToArrayAsync(ct);
        return new(start, end, rows.Count(x => x.Status == AttendanceStatus.Present), rows.Count(x => x.Status == AttendanceStatus.Absent),
            rows.Count(x => x.Status == AttendanceStatus.Late), rows.Count(x => x.Status == AttendanceStatus.Excused),
            rows.GroupBy(x => x.Date).OrderBy(x => x.Key)
                .Select(d => new CalendarDay(d.Key, d.OrderBy(x => x.Subject).Select(x => new CalendarEntry(x.Subject, x.Status, x.Note)).ToArray())).ToArray());
    }

    private async Task<(CourseView Course, DateOnly Day)> ResolveAsync(Guid courseId, DateOnly? date, CancellationToken ct, bool allowFuture = false)
    {
        var yearId = await db.Courses.AsNoTracking().Where(x => x.Id == courseId).Select(x => (Guid?)x.AcademicYearId).SingleOrDefaultAsync(ct)
            ?? throw OperationException.NotFound("No encontramos ese curso.");
        var course = await courses.CourseAsync(courseId, ct);
        var year = await db.Years.AsNoTracking().SingleAsync(x => x.Id == yearId, ct);
        var today = courses.LocalDate(time.GetUtcNow());
        var day = date ?? (today < year.StartsOn ? year.StartsOn : today > year.EndsOn ? year.EndsOn : today);
        if (day < year.StartsOn || day > year.EndsOn) throw OperationException.Invalid("La fecha debe estar dentro del ciclo del curso.");
        if (day > today && !allowFuture) throw OperationException.Invalid("No se registra asistencia de fechas futuras.");
        return (course, day);
    }
}

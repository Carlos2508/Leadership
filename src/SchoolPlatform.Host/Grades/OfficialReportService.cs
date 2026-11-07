using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Attendance;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;
using Microsoft.Extensions.Options;

namespace SchoolPlatform.Host.Grades;

public sealed record ReportFile(byte[] Content, string FileName);

/// <summary>
/// Spreadsheet exports for official processes (e.g. MINEDUC/SIRE uploads and archives): enrollment list, grade board per
/// period, yearly consolidated board (period results side by side, no averages or rounding: those remain a school policy)
/// and attendance summary. For staff with academic.read; they include every captured result, published or not, and say so.
/// </summary>
public sealed class OfficialReportService(SchoolDbContext db, GradeService grades, IOptions<InstitutionOptions> institution)
{
    private const string Mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public static string ContentType => Mime;

    public async Task<ReportFile> EnrollmentListAsync(Guid yearId, Guid? gradeId, Guid? sectionId, CancellationToken ct)
    {
        var year = await YearAsync(yearId, ct);
        var rows = await (from e in db.Enrollments.AsNoTracking()
                          where e.AcademicYearId == yearId && e.Status != EnrollmentStatus.Cancelled
                          join p in db.Placements on e.Id equals p.EnrollmentId
                          where p.EndsOn == null || p.EndsOn > p.StartsOn
                          join g in db.Grades on p.GradeLevelId equals g.Id
                          where (gradeId == null || p.GradeLevelId == gradeId) && (sectionId == null || p.SectionId == sectionId)
                          join person in db.Persons on e.StudentId equals person.Id
                          join s in db.Students on e.StudentId equals s.PersonId
                          select new
                          {
                              e.StudentId, s.StudentCode, person.GivenNames, person.FamilyNames, person.BirthDate, Grade = g.Name, GradeCode = g.Code,
                              g.Sequence, Section = db.Sections.Where(x => x.Id == p.SectionId).Select(x => x.Name).FirstOrDefault(),
                              e.Status, e.EnrolledOn, e.WithdrawnOn, p.StartsOn
                          }).ToArrayAsync(ct);
        // Latest placement per enrollment.
        var latest = rows.GroupBy(x => x.StudentId).Select(g => g.OrderByDescending(x => x.StartsOn).First())
            .OrderBy(x => x.Sequence).ThenBy(x => x.Section).ThenBy(x => x.FamilyNames).ThenBy(x => x.GivenNames).ToArray();
        var n = 0;
        var content = SpreadsheetWriter.Write("Inscritos", Title($"Nómina de inscritos · Ciclo {year.Label}"),
            ["No.", "Código", "Apellidos", "Nombres", "Fecha de nacimiento", "Código de grado", "Grado", "Sección", "Estado", "Inscrito el", "Retiro"],
            latest.Select(x => new object?[]
            {
                ++n, x.StudentCode, x.FamilyNames, x.GivenNames, x.BirthDate?.ToString("dd/MM/yyyy"), x.GradeCode, x.Grade, x.Section ?? "",
                Status(x.Status), x.EnrolledOn.ToString("dd/MM/yyyy"), x.WithdrawnOn?.ToString("dd/MM/yyyy") ?? ""
            }));
        return new(content, $"inscritos-{Slug(year.Label)}.xlsx");
    }

    public async Task<ReportFile> PeriodBoardAsync(Guid yearId, Guid periodId, Guid gradeId, Guid? sectionId, CancellationToken ct)
    {
        var year = await YearAsync(yearId, ct);
        var period = await db.Periods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == periodId && x.AcademicYearId == yearId, ct)
            ?? throw OperationException.NotFound("No encontramos ese período en el ciclo.");
        var (label, students, subjects) = await GroupAsync(yearId, gradeId, sectionId, period.StartsOn, period.EndsOn, ct);
        var scores = await ScoresAsync(yearId, gradeId, [period.Id], students.Select(x => x.Id).ToArray(), ct);
        var scale = await grades.ScaleAsync(gradeId, ct);
        var published = await db.GradePublications.AnyAsync(x => x.PeriodId == periodId, ct);
        var n = 0;
        var content = SpreadsheetWriter.Write(period.Name, Title($"Cuadro de calificaciones · {label} · {period.Name} · Ciclo {year.Label}",
                published ? "Período publicado." : "Período NO publicado: resultados en captura."),
            ["No.", "Código", "Estudiante", .. subjects.Select(s => s.Name)],
            students.Select(s => new object?[] { ++n, s.Code, s.Name }
                .Concat(subjects.Select(sub => Value(scores, s.Id, sub.Id, period.Id, scale))).ToArray()));
        return new(content, $"cuadro-{Slug(label)}-{Slug(period.Name)}.xlsx");
    }

    public async Task<ReportFile> ConsolidatedAsync(Guid yearId, Guid gradeId, Guid? sectionId, CancellationToken ct)
    {
        var year = await YearAsync(yearId, ct);
        var periods = await db.Periods.AsNoTracking().Where(x => x.AcademicYearId == yearId).OrderBy(x => x.Sequence).ToArrayAsync(ct);
        if (periods.Length == 0) throw new OperationException(409, "periods-required", "El ciclo no tiene períodos definidos.");
        var (label, students, subjects) = await GroupAsync(yearId, gradeId, sectionId, year.StartsOn, year.EndsOn, ct);
        var scores = await ScoresAsync(yearId, gradeId, periods.Select(p => p.Id).ToArray(), students.Select(x => x.Id).ToArray(), ct);
        var scale = await grades.ScaleAsync(gradeId, ct);
        var header = new List<string> { "No.", "Código", "Estudiante" };
        foreach (var subject in subjects) header.AddRange(periods.Select(p => $"{subject.Name} · {p.Name}"));
        var n = 0;
        var content = SpreadsheetWriter.Write("Consolidado", Title($"Consolidado anual · {label} · Ciclo {year.Label}",
                "Resultados por período tal como fueron registrados; sin promedios ni redondeos."),
            header,
            students.Select(s =>
            {
                var row = new List<object?> { ++n, s.Code, s.Name };
                foreach (var subject in subjects) row.AddRange(periods.Select(p => Value(scores, s.Id, subject.Id, p.Id, scale)));
                return row.ToArray();
            }));
        return new(content, $"consolidado-{Slug(label)}-{Slug(year.Label)}.xlsx");
    }

    public async Task<ReportFile> AttendanceAsync(Guid yearId, Guid gradeId, Guid? sectionId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var year = await YearAsync(yearId, ct);
        var start = from ?? year.StartsOn;
        var end = to ?? year.EndsOn;
        if (end < start) throw OperationException.Invalid("La fecha final no puede ser anterior a la inicial.");
        var (label, students, _) = await GroupAsync(yearId, gradeId, sectionId, start, end, ct);
        var ids = students.Select(x => x.Id).ToArray();
        var records = await (from r in db.AttendanceRecords.AsNoTracking()
                             join s in db.AttendanceSessions on r.SessionId equals s.Id
                             join c in db.Courses on s.CourseId equals c.Id
                             where c.AcademicYearId == yearId && ids.Contains(r.StudentId) && s.Date >= start && s.Date <= end
                             select new { r.StudentId, s.Date, r.Status, r.UpdatedAt }).ToArrayAsync(ct);
        // One status per student and school day: the latest one recorded that day (group attendance writes every course).
        var daily = records.GroupBy(x => (x.StudentId, x.Date)).Select(g => g.OrderByDescending(x => x.UpdatedAt).First())
            .ToLookup(x => x.StudentId);
        var n = 0;
        var content = SpreadsheetWriter.Write("Asistencia", Title($"Asistencia · {label} · {start:dd/MM/yyyy} a {end:dd/MM/yyyy}",
                "Un registro por estudiante y día lectivo con asistencia tomada."),
            ["No.", "Código", "Estudiante", "Días registrados", "Presente", "Ausente", "Tarde", "Justificado", "% asistencia"],
            students.Select(s =>
            {
                var days = daily[s.Id].ToArray();
                var present = days.Count(x => x.Status == AttendanceStatus.Present);
                var late = days.Count(x => x.Status == AttendanceStatus.Late);
                var excused = days.Count(x => x.Status == AttendanceStatus.Excused);
                var absent = days.Count(x => x.Status == AttendanceStatus.Absent);
                object? percent = days.Length == 0 ? null : Math.Round((present + late + excused) * 100m / days.Length, 1);
                return new object?[] { ++n, s.Code, s.Name, days.Length, present, absent, late, excused, percent };
            }));
        return new(content, $"asistencia-{Slug(label)}.xlsx");
    }

    // ---- helpers

    private sealed record Student(Guid Id, string Code, string Name);
    private sealed record SubjectColumn(Guid Id, string Name);

    private async Task<AcademicYear> YearAsync(Guid id, CancellationToken ct) =>
        await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");

    private async Task<(string Label, Student[] Students, SubjectColumn[] Subjects)> GroupAsync(Guid yearId, Guid gradeId, Guid? sectionId,
        DateOnly start, DateOnly end, CancellationToken ct)
    {
        var grade = await db.Grades.AsNoTracking().SingleOrDefaultAsync(x => x.Id == gradeId, ct) ?? throw OperationException.NotFound("No encontramos ese grado.");
        var section = sectionId is null ? null : await db.Sections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sectionId && x.AcademicYearId == yearId, ct)
            ?? throw OperationException.NotFound("No encontramos esa sección en el ciclo.");
        var students = (await (from p in db.Placements.AsNoTracking()
                               join e in db.Enrollments on p.EnrollmentId equals e.Id
                               where e.AcademicYearId == yearId && e.Status != EnrollmentStatus.Cancelled && p.GradeLevelId == gradeId &&
                                     (sectionId == null || p.SectionId == sectionId) &&
                                     p.StartsOn <= end && (p.EndsOn == null || (p.EndsOn > start && p.EndsOn > p.StartsOn))
                               join person in db.Persons on e.StudentId equals person.Id
                               join s in db.Students on e.StudentId equals s.PersonId
                               select new { e.StudentId, s.StudentCode, person.GivenNames, person.FamilyNames, person.PreferredName })
                .Distinct().ToArrayAsync(ct))
            .OrderBy(x => x.FamilyNames).ThenBy(x => x.GivenNames)
            .Select(x => new Student(x.StudentId, x.StudentCode, PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName))).ToArray();
        var subjects = await db.Courses.AsNoTracking()
            .Where(c => c.AcademicYearId == yearId && c.GradeLevelId == gradeId && (sectionId == null || c.SectionId == null || c.SectionId == sectionId))
            .Join(db.Subjects, c => c.SubjectId, s => s.Id, (c, s) => new { s.Id, s.Name }).Distinct().OrderBy(x => x.Name).ToArrayAsync(ct);
        return (section is null ? grade.Name : $"{grade.Name} {section.Name}", students, subjects.Select(x => new SubjectColumn(x.Id, x.Name)).ToArray());
    }

    private async Task<Dictionary<(Guid Student, Guid Subject, Guid Period), (decimal? Score, string Status)>> ScoresAsync(Guid yearId, Guid gradeId,
        Guid[] periodIds, Guid[] studentIds, CancellationToken ct) =>
        (await db.PeriodScores.AsNoTracking()
            .Where(x => periodIds.Contains(x.PeriodId) && studentIds.Contains(x.StudentId))
            .Join(db.Courses.Where(c => c.AcademicYearId == yearId && c.GradeLevelId == gradeId), s => s.CourseId, c => c.Id,
                (s, c) => new { s.StudentId, c.SubjectId, s.PeriodId, s.Score, s.Status, s.UpdatedAt })
            .ToArrayAsync(ct))
        .GroupBy(x => (x.StudentId, x.SubjectId, x.PeriodId))
        .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).Select(x => (x.Score, x.Status)).First());

    private static object? Value(Dictionary<(Guid, Guid, Guid), (decimal? Score, string Status)> scores, Guid student, Guid subject, Guid period,
        EvaluationScale? scale) =>
        !scores.TryGetValue((student, subject, period), out var v) ? null
        : v.Status == ScoreStatus.Missing ? "Pendiente"
        : v.Status == ScoreStatus.Excused ? "Exonerado"
        : scale?.LabelFor(v.Score) is { } label ? label
        : (object?)v.Score;

    private string[] Title(params string[] lines) => [institution.Value.DisplayName, .. lines];

    private static string Status(string status) => status switch
    {
        EnrollmentStatus.Active => "Activo",
        EnrollmentStatus.Withdrawn => "Retirado",
        EnrollmentStatus.Completed => "Finalizado",
        _ => status
    };

    private static string Slug(string value) =>
        new string(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
}

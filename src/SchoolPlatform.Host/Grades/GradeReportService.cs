using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Documents;
using SchoolPlatform.Host.Finance;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Grades;

/// <summary>
/// Data for printable grade documents. Every path reuses the same rules as the screens: families go through
/// <see cref="GradeService.MyGradesAsync"/> (AcademicRecords + clearance on each read); staff report cards cover published
/// periods only and withhold the results of students not cleared for the publication's cutoff, so a printed copy never
/// bypasses the clearance rule.
/// </summary>
public sealed class GradeReportService(SchoolDbContext db, GradeService grades, ClearanceService clearance)
{
    public async Task<StudentReport?> FamilyReportAsync(Guid userId, Guid studentId, CancellationToken ct)
    {
        var mine = await grades.MyGradesAsync(userId, studentId, ct);
        if (mine is null) return null;
        var student = await StudentAsync(studentId, ct);
        var placement = await PlacementLabelAsync(studentId, null, ct);
        return new(student.Name, student.Code, placement, mine.Year ?? "", mine.Periods.Select(p => new ReportSection(p.Name,
            p.Grades.Select(g => new ReportLine(g.Subject, g.Score, g.Status)).ToArray(),
            p.Status switch
            {
                "Available" => null,
                "PendingFinancialSetup" => "Resultados aún no disponibles: el colegio está completando la configuración de cobros.",
                _ => "Resultados no disponibles: hay pagos pendientes del corte de solvencia de este período."
            })).ToArray());
    }

    public async Task<StudentReport[]> PeriodReportsAsync(Guid yearId, Guid periodId, Guid gradeId, Guid? sectionId, CancellationToken ct)
    {
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == yearId, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");
        var period = await db.Periods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == periodId && x.AcademicYearId == yearId, ct)
            ?? throw OperationException.NotFound("No encontramos ese período en el ciclo.");
        var publication = await db.GradePublications.AsNoTracking().Where(x => x.PeriodId == periodId)
            .Join(db.ClearanceCutoffs, p => p.CutoffId, c => c.Id, (p, c) => new { p.CutoffId, Cutoff = c.Name }).SingleOrDefaultAsync(ct)
            ?? throw new OperationException(409, "not-published", "El período aún no está publicado; solo se imprimen informes de períodos publicados.");
        var students = await (from p in db.Placements.AsNoTracking()
                              join e in db.Enrollments on p.EnrollmentId equals e.Id
                              where e.AcademicYearId == yearId && e.Status != EnrollmentStatus.Cancelled && p.GradeLevelId == gradeId &&
                                    (sectionId == null || p.SectionId == sectionId) &&
                                    p.StartsOn <= period.EndsOn && (p.EndsOn == null || (p.EndsOn > period.StartsOn && p.EndsOn > p.StartsOn))
                              join person in db.Persons on e.StudentId equals person.Id
                              join s in db.Students on e.StudentId equals s.PersonId
                              select new { e.StudentId, person.GivenNames, person.FamilyNames, person.PreferredName, s.StudentCode })
            .Distinct().ToArrayAsync(ct);
        if (students.Length > 400) throw OperationException.Invalid("Genera los informes por sección (máximo 400 por documento).");
        var ids = students.Select(x => x.StudentId).ToArray();
        var scores = (await db.PeriodScores.AsNoTracking().Where(x => x.PeriodId == periodId && ids.Contains(x.StudentId))
                .Join(db.Courses, s => s.CourseId, c => c.Id, (s, c) => new { s.StudentId, s.Score, s.Status, c.SubjectId })
                .Join(db.Subjects, x => x.SubjectId, subject => subject.Id, (x, subject) => new { x.StudentId, Subject = subject.Name, x.Score, x.Status })
                .ToArrayAsync(ct))
            .ToLookup(x => x.StudentId);
        var reports = new List<StudentReport>();
        foreach (var student in students.OrderBy(x => x.FamilyNames).ThenBy(x => x.GivenNames))
        {
            var check = await clearance.CheckAsync(student.StudentId, publication.CutoffId, ct);
            var withheld = check.Status == ClearanceStatus.Clear ? null
                : check.Status == ClearanceStatus.PendingFinancialSetup
                    ? "Informe retenido: falta completar la configuración de cobros del corte."
                    : $"Informe retenido: pagos pendientes del corte «{publication.Cutoff}». Entregar cuando el estudiante esté solvente.";
            var lines = withheld is null
                ? scores[student.StudentId].OrderBy(x => x.Subject).Select(x => new ReportLine(x.Subject, x.Score, x.Status)).ToArray()
                : [];
            reports.Add(new(PeopleService.Display(student.GivenNames, student.FamilyNames, student.PreferredName), student.StudentCode,
                await PlacementLabelAsync(student.StudentId, yearId, ct, period.EndsOn), year.Label,
                [new ReportSection(period.Name, lines, withheld)]));
        }
        return reports.ToArray();
    }

    public async Task<CourseSheetDocument?> CourseSheetAsync(Guid userId, Guid courseId, Guid? periodId, CancellationToken ct)
    {
        var sheet = await grades.SheetAsync(userId, courseId, periodId, ct);
        if (sheet is null) return null;
        var course = sheet.Course;
        return new(course.Subject, course.Section is null ? course.Grade : $"{course.Grade} · Sección {course.Section}", course.Year,
            sheet.Period.Name, sheet.Period.Published,
            sheet.Rows.Select(x => new SheetLine(x.Name, x.Code, x.Score, x.Status)).ToArray());
    }

    private async Task<(string Name, string Code)> StudentAsync(Guid studentId, CancellationToken ct)
    {
        var s = await db.Persons.AsNoTracking().Where(x => x.Id == studentId)
            .Join(db.Students, p => p.Id, st => st.PersonId, (p, st) => new { p.GivenNames, p.FamilyNames, p.PreferredName, st.StudentCode })
            .SingleAsync(ct);
        return (PeopleService.Display(s.GivenNames, s.FamilyNames, s.PreferredName), s.StudentCode);
    }

    /// <summary>"Grade · Sección X" of the latest placement (of a year, or the default/latest year), as of a date when given.</summary>
    public async Task<string> PlacementLabelAsync(Guid studentId, Guid? yearId, CancellationToken ct, DateOnly? asOf = null)
    {
        var query = from e in db.Enrollments.AsNoTracking()
                    join y in db.Years on e.AcademicYearId equals y.Id
                    join p in db.Placements on e.Id equals p.EnrollmentId
                    join g in db.Grades on p.GradeLevelId equals g.Id
                    join s in db.Sections on p.SectionId equals s.Id into sections
                    from s in sections.DefaultIfEmpty()
                    where e.StudentId == studentId && e.Status != EnrollmentStatus.Cancelled && (yearId == null || e.AcademicYearId == yearId) &&
                          (asOf == null || p.StartsOn <= asOf) && (p.EndsOn == null || p.EndsOn > p.StartsOn)
                    orderby y.IsDefault descending, y.StartsOn descending, p.StartsOn descending
                    select new { Grade = g.Name, Section = s == null ? null : s.Name };
        var placement = await query.FirstOrDefaultAsync(ct);
        return placement is null ? "" : placement.Section is null ? placement.Grade : $"{placement.Grade} · Sección {placement.Section}";
    }
}

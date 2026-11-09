using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.Timetables;
public sealed partial class TimetableService
{
    public async Task<PublishedTimetable> ReadAsync(Guid year, DateOnly asOf, Guid? grade, Guid? section, Guid? staff, CancellationToken ct, bool wholeGradeOnly = false)
    {
        var y = await db.Years.AsNoTracking().SingleOrDefaultAsync(y => y.Id == year, ct) ?? throw OperationException.NotFound("No encontramos el ciclo.");
        if (asOf < y.StartsOn || asOf > y.EndsOn) throw OperationException.Invalid("Elige una fecha dentro del ciclo.");
        if (section != null && (grade == null || !await db.Sections.AnyAsync(s => s.Id == section && s.GradeLevelId == grade && s.AcademicYearId == year, ct)))
            throw OperationException.Invalid("La sección no pertenece al grado y ciclo seleccionados.");
        var rows = await (from e in db.TimetableEntries.AsNoTracking() join r in db.TimetableRevisions on e.RevisionId equals r.Id
                          join p in db.TimetablePlans on r.PlanId equals p.Id join g in db.Grades on p.GradeLevelId equals g.Id
                          where p.AcademicYearId == year && r.State == "Published" && r.StartsOn <= asOf && r.UntilExclusive > asOf &&
                              (grade == null || p.GradeLevelId == grade) && (section == null || p.SectionId == null || p.SectionId == section) &&
                              (staff == null || e.StaffId == staff) && (!wholeGradeOnly || p.SectionId == null)
                          select new { e, r.StructureJson, p.GradeLevelId, p.SectionId, Grade = g.Name, Section = db.Sections.Where(s => s.Id == p.SectionId).Select(s => s.Name).FirstOrDefault() }).ToArrayAsync(ct);
        var structures = rows.Select(r => r.StructureJson).Distinct().Select(s => System.Text.Json.JsonSerializer.Deserialize<TimetableStructure>(s)!).ToArray();
        var structure = new TimetableStructure(structures.SelectMany(s => s.Days).Distinct().Order().ToArray(),
            structures.SelectMany(s => s.Periods).DistinctBy(p => (p.Start,p.End)).OrderBy(p => p.Start).ThenBy(p => p.End).ToArray());
        var scopes = rows.GroupBy(r => new { r.GradeLevelId, r.SectionId }).Select(g => new TimetableScope(g.Key.GradeLevelId,g.Key.SectionId,
            Combine(g.Select(r => System.Text.Json.JsonSerializer.Deserialize<TimetableStructure>(r.StructureJson)!)))).ToArray();
        var title = staff != null ? "Horario docente" : rows.Length > 0 && section != null ? rows[0].Grade + " · " + await db.Sections.Where(s => s.Id == section).Select(s => s.Name).SingleAsync(ct) : "Horario semanal";
        return new(title, y.Label, asOf, rows.OrderBy(r => r.e.Day).ThenBy(r => r.e.Start).ThenBy(r => r.Grade)
            .Select(r => new TimetableSlot(r.e.Day, r.e.Start, r.e.End, r.e.Kind, r.e.Subject, r.e.Teacher, r.Grade, r.Section, r.e.Room, r.e.CourseId, r.GradeLevelId, r.SectionId)).ToArray(), structure, scopes);
    }
    public async Task<PublishedTimetable> StudentAsync(Guid actor, Guid student, Guid year, DateOnly asOf, CancellationToken ct)
    {
        var access = await people.StudentAccessAsync(actor, student, ct);
        if (access == null || (!access.IsSelf && !access.Faculties.Contains(Faculties.AcademicRecords))) throw OperationException.NotFound("No encontramos ese horario.");
        var placement = await (from p in db.Placements.AsNoTracking() join e in db.Enrollments on p.EnrollmentId equals e.Id
                               where e.StudentId == student && e.AcademicYearId == year && e.Status != EnrollmentStatus.Cancelled &&
                                   p.StartsOn <= asOf && (p.EndsOn == null || p.EndsOn > asOf)
                               select new { p.GradeLevelId, p.SectionId }).SingleOrDefaultAsync(ct);
        if (placement == null) throw OperationException.NotFound("No hay una ubicación del estudiante en ese ciclo y fecha.");
        return await ReadAsync(year, asOf, placement.GradeLevelId, placement.SectionId, null, ct, placement.SectionId == null);
    }
    public async Task<PublishedTimetable> TeacherAsync(Guid actor, Guid year, DateOnly asOf, CancellationToken ct)
    {
        var staff = await (from l in db.AccountLinks.AsNoTracking() join p in db.Persons on l.PersonId equals p.Id
                           join s in db.Staff on p.Id equals s.PersonId
                           where l.UserId == actor && l.UnlinkedAt == null && p.Status == PersonStatus.Active select (Guid?)s.PersonId).SingleOrDefaultAsync(ct);
        if (staff == null) throw OperationException.NotFound("No encontramos un perfil docente vinculado.");
        return await ReadAsync(year, asOf, null, null, staff, ct);
    }
    public async Task<object[]> SelfYearsAsync(Guid actor, CancellationToken ct)
    {
        // Metadata only. Schedule contents still require resource authorization on every read.
        var person = await db.AccountLinks.Where(l => l.UserId == actor && l.UnlinkedAt == null).Select(l => (Guid?)l.PersonId).SingleOrDefaultAsync(ct);
        if (person == null) return [];
        var children = await db.Relationships.Where(r => r.GuardianId == person && r.EndedAt == null && r.Faculties.Any(f => f.Faculty == Faculties.AcademicRecords && f.RevokedAt == null)).Select(r => r.StudentId).ToArrayAsync(ct);
        return await db.Years.AsNoTracking().Where(y => db.Enrollments.Any(e => e.AcademicYearId == y.Id && (e.StudentId == person || children.Contains(e.StudentId))) ||
            db.Courses.Any(c => c.AcademicYearId == y.Id && db.TeachingAssignments.Any(a => a.CourseId == c.Id && a.StaffId == person)))
            .OrderByDescending(y => y.StartsOn).Select(y => (object)new { y.Id, y.Label, y.StartsOn, y.EndsOn, y.IsDefault }).ToArrayAsync(ct);
    }
    public static TimetableStructure Combine(IEnumerable<TimetableStructure> structures) => new(structures.SelectMany(s => s.Days).Distinct().Order().ToArray(),
        structures.SelectMany(s => s.Periods).DistinctBy(p => (p.Start,p.End)).OrderBy(p => p.Start).ThenBy(p => p.End).ToArray());
}

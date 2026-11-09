using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Timetables;

public sealed partial class TimetableService(SchoolDbContext db, SchoolAudit audit, PeopleService people, TimeProvider time)
{
    internal static Task LockAsync(SchoolDbContext db, CancellationToken ct) => db.Database.ExecuteSqlRawAsync("""
        DECLARE @r int;
        EXEC @r = sp_getapplock @Resource=N'SchoolPlatform.Timetables', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=30000;
        IF @r < 0 THROW 51000, 'Timetable operation busy', 1;
        """, ct);

    public async Task<TimetableSummary[]> ListAsync(Guid year, CancellationToken ct)
    {
        var plans = await (from p in db.TimetablePlans.AsNoTracking() where p.AcademicYearId == year
                           join g in db.Grades on p.GradeLevelId equals g.Id
                           select new { p, Grade = g.Name, Section = db.Sections.Where(s => s.Id == p.SectionId).Select(s => s.Name).FirstOrDefault() }).ToArrayAsync(ct);
        var ids = plans.Select(p => p.p.Id).ToArray();
        var revisions = await db.TimetableRevisions.AsNoTracking().Where(r => ids.Contains(r.PlanId)).ToArrayAsync(ct);
        return plans.OrderBy(p => p.Grade).ThenBy(p => p.Section).Select(p => {
            var published = revisions.Where(r => r.PlanId == p.p.Id && r.State == "Published").OrderByDescending(r => r.PublishedAt).FirstOrDefault();
            return new TimetableSummary(p.p.Id, p.p.GradeLevelId, p.Grade, p.p.SectionId, p.Section,
                revisions.SingleOrDefault(r => r.PlanId == p.p.Id && r.State == "Draft")?.Id, published?.Id, published?.StartsOn, published?.UntilExclusive.AddDays(-1));
        }).ToArray();
    }

    public async Task<TimetableView> GetAsync(Guid year, Guid id, CancellationToken ct)
    {
        var r = await db.TimetableRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && db.TimetablePlans.Any(p => p.Id == x.PlanId && p.AcademicYearId == year), ct)
            ?? throw OperationException.NotFound("No encontramos ese horario en el ciclo indicado.");
        var p = await db.TimetablePlans.AsNoTracking().SingleAsync(x => x.Id == r.PlanId, ct);
        var entries = await db.TimetableEntries.AsNoTracking().Where(e => e.RevisionId == id).ToArrayAsync(ct);
        return new(r.Id, p.Id, year, await db.Years.Where(y => y.Id == year).Select(y => y.Label).SingleAsync(ct),
            p.GradeLevelId, await db.Grades.Where(g => g.Id == p.GradeLevelId).Select(g => g.Name).SingleAsync(ct), p.SectionId,
            await db.Sections.Where(s => s.Id == p.SectionId).Select(s => s.Name).FirstOrDefaultAsync(ct), r.State, r.StartsOn, r.UntilExclusive.AddDays(-1),
            r.PublishedAt, Versions.Encode(r.Version), JsonSerializer.Deserialize<TimetableStructure>(r.StructureJson)!,
            entries.Select(e => new TimetableCell(e.PeriodId, e.Day, e.Kind, e.CourseId, e.AssignmentId, e.RoomId)).ToArray(),
            r.State == "Draft" ? await IssuesAsync(p, r, entries, ct) : []);
    }

    public async Task<SavedTimetable> SaveAsync(Guid actor, Guid yearId, SaveTimetable request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAsync(db, ct); await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var command = new { yearId, request };
        if (await Previous<SavedTimetable>(actor, request.RequestId, command, ct) is { } previous) return previous;
        var reason = Reasons.Require(request.Reason);
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(y => y.Id == yearId, ct) ?? throw OperationException.NotFound("No encontramos el ciclo.");
        if (year.Status == YearStatus.Closed) throw new OperationException(409, "year-closed", "El ciclo está cerrado.");
        if (request.StartsOn < year.StartsOn || request.Through > year.EndsOn || request.Through < request.StartsOn)
            throw OperationException.Invalid("La vigencia debe estar dentro del ciclo y terminar después de su inicio.");
        if (!await db.Grades.AnyAsync(g => g.Id == request.GradeId && g.IsActive, ct) || (request.SectionId != null && !await db.Sections.AnyAsync(s =>
            s.Id == request.SectionId && s.AcademicYearId == yearId && s.GradeLevelId == request.GradeId, ct)))
            throw OperationException.Invalid("Selecciona un grado y sección del ciclo indicado.");
        ValidateStructure(request.Structure);
        var plan = await db.TimetablePlans.SingleOrDefaultAsync(p => p.AcademicYearId == yearId && p.GradeLevelId == request.GradeId && p.SectionId == request.SectionId, ct);
        if (plan == null) db.TimetablePlans.Add(plan = new() { Id = Guid.NewGuid(), AcademicYearId = yearId, GradeLevelId = request.GradeId, SectionId = request.SectionId });
        var draft = await db.TimetableRevisions.SingleOrDefaultAsync(r => r.PlanId == plan.Id && r.State == "Draft", ct);
        if (draft != null)
        {
            if (draft.Id != request.RevisionId || !draft.Version.SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
            db.TimetableEntries.RemoveRange(await db.TimetableEntries.Where(e => e.RevisionId == draft.Id).ToArrayAsync(ct));
            // Delete the old cells first: replacement keys share a unique (revision, period, day) index.
            await db.SaveChangesAsync(ct);
        }
        else
        {
            if (request.RevisionId != null || !string.IsNullOrEmpty(request.Version)) throw OperationException.Changed();
            db.TimetableRevisions.Add(draft = new() { Id = Guid.NewGuid(), PlanId = plan.Id });
        }
        draft.StartsOn = request.StartsOn; draft.UntilExclusive = request.Through.AddDays(1);
        draft.StructureJson = JsonSerializer.Serialize(request.Structure); draft.UpdatedBy = actor;
        db.Entry(draft).Property(r => r.UpdatedBy).IsModified = db.Entry(draft).State != EntityState.Added;
        var entries = await BuildEntries(plan, draft, request.Structure, request.Cells, ct);
        db.TimetableEntries.AddRange(entries);
        audit.Record("TimetableDraftSaved", actor, "Timetable", plan.Id, reason, $"cells:{entries.Length}");
        await db.SaveChangesAsync(ct);
        var result = new SavedTimetable(plan.Id, draft.Id, Versions.Encode(draft.Version));
        Remember(actor, request.RequestId, command, result); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return result;
    }

    public async Task<SavedTimetable> PublishAsync(Guid actor, Guid year, Guid id, PublishTimetable request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAsync(db, ct); await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        // Include the target resource in the receipt hash, not just the body.
        var command = new { year, id, request };
        if (await Previous<SavedTimetable>(actor, request.RequestId, command, ct) is { } previous) return previous;
        var reason = Reasons.Require(request.Reason);
        var revision = await db.TimetableRevisions.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw OperationException.NotFound("No encontramos el borrador.");
        var plan = await db.TimetablePlans.SingleAsync(p => p.Id == revision.PlanId, ct);
        if (plan.AcademicYearId != year) throw OperationException.NotFound("No encontramos ese horario en el ciclo indicado.");
        if (await db.Years.AnyAsync(y => y.Id == year && y.Status == YearStatus.Closed, ct)) throw new OperationException(409, "year-closed", "El ciclo está cerrado.");
        if (revision.State != "Draft" || !revision.Version.SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
        var last = await db.TimetableRevisions.Where(r => r.PlanId == plan.Id && r.State == "Published").OrderByDescending(r => r.PublishedAt).FirstOrDefaultAsync(ct);
        if (last != null && revision.StartsOn < last.StartsOn) throw OperationException.Invalid("La nueva versión no puede comenzar antes de la última publicada.");
        var structure = JsonSerializer.Deserialize<TimetableStructure>(revision.StructureJson)!;
        var stored = await db.TimetableEntries.Where(e => e.RevisionId == id).ToArrayAsync(ct);
        var refreshed = await BuildEntries(plan, revision, structure, stored.Select(e => new TimetableCell(e.PeriodId, e.Day, e.Kind, e.CourseId, e.AssignmentId, e.RoomId)).ToArray(), ct);
        if (!refreshed.Any(e => e.Kind == "Lesson")) throw OperationException.Invalid("Agrega al menos una clase antes de publicar.");
        var issues = await IssuesAsync(plan, revision, refreshed, ct);
        if (issues.Length != 0) throw new OperationException(409, "timetable-conflicts", issues[0].Message);
        foreach (var entry in stored)
        {
            var source = refreshed.Single(e => e.PeriodId == entry.PeriodId && e.Day == entry.Day);
            entry.Teacher = source.Teacher; entry.StaffId = source.StaffId; entry.Subject = source.Subject; entry.Room = source.Room;
        }
        foreach (var old in await db.TimetableRevisions.Where(r => r.PlanId == plan.Id && r.State == "Published" && r.UntilExclusive > revision.StartsOn).ToArrayAsync(ct))
            old.UntilExclusive = revision.StartsOn;
        revision.State = "Published"; revision.PublishedAt = time.GetUtcNow(); revision.UpdatedBy = actor;
        audit.Record("TimetablePublished", actor, "Timetable", plan.Id, reason, $"revision:{revision.Id}");
        await db.SaveChangesAsync(ct);
        var result = new SavedTimetable(plan.Id, revision.Id, Versions.Encode(revision.Version));
        Remember(actor, request.RequestId, command, result); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return result;
    }

    public static void ValidateStructure(TimetableStructure? structure)
    {
        if (structure?.Days is not { Length: > 0 and <= 7 } days || structure.Periods is not { Length: > 0 and <= 32 } periods ||
            days.Distinct().Count() != days.Length || days.Any(d => d is < 1 or > 7) || periods.Any(p => p == null || p.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 50 || p.Start < 0 || p.End > 1440 || p.Start >= p.End) || periods.Select(p => p.Id).Distinct().Count() != periods.Length)
            throw OperationException.Invalid("Revisa días, nombres y horas de los períodos (hasta 32 por día).");
        var sorted = periods.OrderBy(p => p.Start).ToArray();
        if (sorted.Zip(sorted.Skip(1)).Any(p => p.First.End > p.Second.Start)) throw OperationException.Invalid("Los períodos de la estructura se superponen.");
    }

    private async Task<TimetableEntry[]> BuildEntries(TimetablePlan plan, TimetableRevision revision, TimetableStructure structure, TimetableCell[]? cells, CancellationToken ct)
    {
        if (cells == null || cells.Length > 224 || cells.Any(c => c == null || !structure.Days.Contains(c.Day) || !structure.Periods.Any(p => p.Id == c.PeriodId) ||
            c.Kind is not ("Lesson" or "Break") || (c.Kind == "Lesson" ? c.CourseId == null : c.CourseId != null || c.AssignmentId != null || c.RoomId != null)) ||
            cells.Select(c => (c.PeriodId, c.Day)).Distinct().Count() != cells.Length)
            throw OperationException.Invalid("Revisa las clases y recreos de cada día y período.");
        var courses = await (from c in db.Courses.AsNoTracking() where c.AcademicYearId == plan.AcademicYearId && c.GradeLevelId == plan.GradeLevelId &&
                              (c.SectionId == plan.SectionId || c.SectionId == null)
                             join s in db.Subjects on c.SubjectId equals s.Id select new { c.Id, s.Name }).ToDictionaryAsync(x => x.Id, ct);
        var ids = cells.Where(c => c.AssignmentId != null).Select(c => c.AssignmentId!.Value).Distinct().ToArray();
        var assignments = await (from a in db.TeachingAssignments.AsNoTracking() where ids.Contains(a.Id)
                                 join p in db.Persons on a.StaffId equals p.Id
                                 select new { a, p.GivenNames, p.FamilyNames, p.PreferredName, p.Status }).ToDictionaryAsync(x => x.a.Id, ct);
        var rooms = await db.TimetableRooms.AsNoTracking().ToDictionaryAsync(r => r.Id, ct);
        return cells.Select(c => {
            var period = structure.Periods.Single(p => p.Id == c.PeriodId);
            if (c.Kind == "Lesson" && !courses.ContainsKey(c.CourseId!.Value)) throw OperationException.Invalid("La materia no pertenece al grupo y ciclo seleccionados.");
            if (c.AssignmentId is { } aid && (!assignments.TryGetValue(aid, out var a) || a.a.CourseId != c.CourseId || a.Status != PersonStatus.Active))
                throw OperationException.Invalid("El docente no está asignado a ese curso o su ficha está inactiva.");
            if (c.RoomId is { } room && !rooms.ContainsKey(room)) throw OperationException.Invalid("No encontramos el aula seleccionada.");
            var assignment = c.AssignmentId is { } aId ? assignments[aId] : null;
            return new TimetableEntry { Id = Guid.NewGuid(), RevisionId = revision.Id, PeriodId = c.PeriodId, Day = c.Day, Start = period.Start, End = period.End, Kind = c.Kind,
                CourseId = c.CourseId, AssignmentId = c.AssignmentId, StaffId = assignment?.a.StaffId, RoomId = c.RoomId,
                Subject = c.Kind == "Break" ? period.Name : courses[c.CourseId!.Value].Name,
                Teacher = assignment == null ? "" : PeopleService.Display(assignment.GivenNames, assignment.FamilyNames, assignment.PreferredName),
                Room = c.RoomId is { } rid ? rooms[rid].Name : "" };
        }).ToArray();
    }

    private async Task<TimetableIssue[]> IssuesAsync(TimetablePlan plan, TimetableRevision revision, TimetableEntry[] entries, CancellationToken ct)
    {
        var issues = new List<TimetableIssue>();
        var assignmentIds = entries.Where(e => e.AssignmentId != null).Select(e => e.AssignmentId!.Value).ToArray();
        var assignments = await db.TeachingAssignments.AsNoTracking().Where(a => assignmentIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var staffIds = entries.Where(e => e.StaffId != null).Select(e => e.StaffId!.Value).Distinct().ToArray();
        var activeStaff = await db.Persons.AsNoTracking().Where(p => staffIds.Contains(p.Id) && p.Status == PersonStatus.Active).Select(p => p.Id).ToArrayAsync(ct);
        var others = await (from e in db.TimetableEntries.AsNoTracking() join r in db.TimetableRevisions on e.RevisionId equals r.Id
                            join p in db.TimetablePlans on r.PlanId equals p.Id join g in db.Grades on p.GradeLevelId equals g.Id
                            where p.Id != plan.Id && r.State == "Published" && r.StartsOn < revision.UntilExclusive && r.UntilExclusive > revision.StartsOn && r.StartsOn < r.UntilExclusive
                            select new { e, r.StartsOn, r.UntilExclusive, p.AcademicYearId, p.GradeLevelId, p.SectionId, Grade = g.Name }).ToArrayAsync(ct);
        foreach (var e in entries)
        {
            void Add(string kind, string message) => issues.Add(new(kind, message, e.Day, e.PeriodId));
            if (e.Kind == "Lesson" && (e.AssignmentId is not { } aId || !assignments.TryGetValue(aId, out var a) || !activeStaff.Contains(a.StaffId) ||
                a.StartsOn > revision.StartsOn || (a.EndsOn != null && a.EndsOn < revision.UntilExclusive.AddDays(-1))))
                Add("teacher-required", $"{e.Subject}: selecciona un docente cuya asignación cubra toda la vigencia del horario.");
            foreach (var other in others.Where(x => x.e.Day == e.Day && x.e.Start < e.End && e.Start < x.e.End))
            {
                var from = other.StartsOn > revision.StartsOn ? other.StartsOn : revision.StartsOn;
                var until = other.UntilExclusive < revision.UntilExclusive ? other.UntilExclusive : revision.UntilExclusive;
                if (!Occurs(e.Day, from, until)) continue;
                var group = other.AcademicYearId == plan.AcademicYearId && other.GradeLevelId == plan.GradeLevelId &&
                    (other.SectionId == null || plan.SectionId == null || other.SectionId == plan.SectionId);
                var teacher = e.StaffId != null && e.StaffId == other.e.StaffId;
                var room = e.RoomId != null && e.RoomId == other.e.RoomId;
                if (group || teacher || room)
                    Add(group ? "group-overlap" : teacher ? "teacher-overlap" : "room-overlap",
                        $"Cruce de {(group ? "grupo" : teacher ? "docente" : "aula")}: {e.Subject} coincide con {other.e.Subject} de {other.Grade}, {Clock(other.e.Start)}–{Clock(other.e.End)}.");
            }
        }
        return issues.Distinct().ToArray();
    }

    internal static bool Occurs(int day, DateOnly from, DateOnly until) => from.AddDays((day - ((int)from.DayOfWeek == 0 ? 7 : (int)from.DayOfWeek) + 7) % 7) < until;
    public static string Clock(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    public async Task<object> CatalogAsync(Guid year, CancellationToken ct) => new {
        rooms = await db.TimetableRooms.AsNoTracking().OrderBy(r => r.Name).ToArrayAsync(ct),
        templates = (await db.TimetableTemplates.AsNoTracking().OrderBy(t => t.Name).ToArrayAsync(ct)).Select(t => new { t.Id, t.Name, Structure = JsonSerializer.Deserialize<TimetableStructure>(t.StructureJson) }),
        assignments = await (from a in db.TeachingAssignments.AsNoTracking() join c in db.Courses on a.CourseId equals c.Id join p in db.Persons on a.StaffId equals p.Id
                             where c.AcademicYearId == year && p.Status == PersonStatus.Active
                             select new { a.Id, a.CourseId, a.StaffId, Name = p.GivenNames + " " + p.FamilyNames, a.StartsOn, a.EndsOn }).ToArrayAsync(ct)
    };

    public async Task<Guid> RoomAsync(Guid actor, string name, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await LockAsync(db, ct);
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage); name = CleanName(name);
        var room = await db.TimetableRooms.SingleOrDefaultAsync(r => r.Name == name, ct);
        if (room != null) return room.Id;
        room = new() { Id = Guid.NewGuid(), Name = name }; db.TimetableRooms.Add(room);
        audit.Record("TimetableRoomCreated", actor, "Timetable", room.Id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return room.Id;
    }
    public async Task<Guid> TemplateAsync(Guid actor, string name, TimetableStructure structure, CancellationToken ct)
    {
        ValidateStructure(structure); name = CleanName(name);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); await LockAsync(db, ct);
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        if (await db.TimetableTemplates.AnyAsync(t => t.Name == name, ct)) throw new OperationException(409, "template-exists", "Ya existe una estructura con ese nombre.");
        var template = new TimetableTemplate { Id = Guid.NewGuid(), Name = name, StructureJson = JsonSerializer.Serialize(structure) }; db.TimetableTemplates.Add(template);
        audit.Record("TimetableTemplateCreated", actor, "Timetable", template.Id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return template.Id;
    }
    private static string CleanName(string? name)
    {
        var result = Regex.Replace(name?.Trim() ?? "", @"\s+", " ");
        return result.Length is > 0 and <= 80 ? result : throw OperationException.Invalid("Escribe un nombre de hasta 80 caracteres.");
    }
    private async Task<T?> Previous<T>(Guid actor, Guid id, object request, CancellationToken ct) where T : class
    {
        if (id == Guid.Empty) throw OperationException.Invalid("Falta el identificador del intento.");
        var old = await db.TimetableOperations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, ct);
        if (old == null) return null;
        if (old.ActorId != actor || !old.Hash.SequenceEqual(Hash(request))) throw new OperationException(409, "attempt-reused", "Ese intento corresponde a otra operación.");
        return JsonSerializer.Deserialize<T>(old.ResultJson)!;
    }
    private void Remember(Guid actor, Guid id, object request, object result) => db.TimetableOperations.Add(new() { Id = id, ActorId = actor, Hash = Hash(request), ResultJson = JsonSerializer.Serialize(result) });
    private static byte[] Hash(object request) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request));
}

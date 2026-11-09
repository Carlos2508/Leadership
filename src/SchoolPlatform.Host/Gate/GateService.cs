using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Gate;

public sealed record GateStudent(Guid Id, string Name, string Code, string Placement, string Year, bool HasPhoto);
public sealed record GateEventView(Guid Id, Guid StudentId, string StudentName, string StudentCode, string Direction, DateTimeOffset OccurredAt,
    string Source, string RecordedBy, string Note, bool Voided, string? VoidReason);
/// <summary>Outcome: Recorded, Duplicate, Unknown (no such card), Revoked (card replaced), NotEnrolled.</summary>
public sealed record ScanResult(string Outcome, string? Direction, DateTimeOffset? At, string Message, GateStudent? Student, Guid? EventId,
    DateTimeOffset? Previous);
public sealed record ScanRequest(string Code, string? Mode);
public sealed record ManualMarkRequest(Guid StudentId, string Direction, string Reason);
public sealed record VoidMarkRequest(string Reason);
public sealed record DailyRow(Guid StudentId, string Name, string Code, string Placement, DateTimeOffset? FirstIn, DateTimeOffset? LastOut,
    string State, int Marks);
public sealed record DailyReport(DateOnly Date, int Expected, int Arrived, int Inside, int Left, int Absent, DailyRow[] Rows);
public sealed record MyGateDay(DateOnly Date, DateTimeOffset? FirstIn, DateTimeOffset? LastOut, int Marks);

/// <summary>Who records a gate mark: a signed-in account (kiosk/manual) or a registered scanner device.</summary>
public sealed record GateActor(Guid? UserId, Guid? DeviceId, string Source);

/// <summary>
/// School gate attendance with ID cards. Each read is decided under a per-student lock with server time:
/// repeated reads (scanner bounce, a second device, someone scanning twice) are reported and never stored twice;
/// entry/exit follows the kiosk mode (automatic alternation with a minimum stay, or fixed entry/exit).
/// Scans are their own record (who/which device); manual marks and voids also go to the audit log.
/// </summary>
public sealed class GateService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, CourseService courses, IOptions<GateOptions> options)
{
    private GateOptions Options => options.Value;

    public static string NormalizeCode(string? code)
    {
        // Crockford base32: scanners and people may send lowercase, dashes or confusable letters.
        var value = (code ?? "").Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
        return value.Replace('O', '0').Replace('I', '1').Replace('L', '1');
    }

    public async Task<ScanResult> ScanAsync(GateActor actor, ScanRequest request, CancellationToken ct)
    {
        if (actor.UserId is { } user) await audit.RequireAsync(user, ct, Permissions.AttendanceGate);
        var mode = request.Mode ?? GateMode.Auto;
        if (!GateMode.All.Contains(mode)) throw OperationException.Invalid("Modo de lectura inválido.");
        var code = NormalizeCode(request.Code);
        if (code.Length is < 8 or > 16) return new("Unknown", null, null, "Código no reconocido. Usa la búsqueda manual.", null, null, null);
        var card = await db.StudentCards.AsNoTracking().SingleOrDefaultAsync(x => x.Token == code, ct);
        if (card is null)
            // Staff cards identify personnel; the gate marks student attendance only (no new outcome for existing clients).
            return new("Unknown", null, null, await db.StaffCards.AnyAsync(x => x.Token == code, ct)
                ? "Carnet de personal. La portería registra solo la asistencia de estudiantes."
                : "Carnet no reconocido. Usa la búsqueda manual.", null, null, null);
        var student = (await StudentsAsync([card.StudentId], ct)).GetValueOrDefault(card.StudentId);
        if (card.RevokedAt is not null)
            return new("Revoked", null, null, "Este carnet fue reemplazado por uno nuevo. Retenlo y usa la búsqueda manual.", student, null, null);
        if (student is null || student.Year.Length == 0)
            return new("NotEnrolled", null, null, "Sin inscripción vigente hoy. No se registró la marca.", student, null, null);
        return await MarkAsync(actor, student, mode, card.Id, "", ct);
    }

    public async Task<ScanResult> ManualAsync(Guid actor, ManualMarkRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AttendanceGate);
        if (request.Direction is not (GateDirection.In or GateDirection.Out)) throw OperationException.Invalid("Indica entrada o salida.");
        var reason = Reasons.Require(request.Reason);
        var student = (await StudentsAsync([request.StudentId], ct)).GetValueOrDefault(request.StudentId)
            ?? throw OperationException.NotFound("No encontramos ese estudiante.");
        if (student.Year.Length == 0) throw new OperationException(409, "not-enrolled", "El estudiante no tiene inscripción vigente hoy.");
        var result = await MarkAsync(new(actor, null, GateSource.Manual), student, request.Direction, null, reason, ct);
        return result;
    }

    private async Task<ScanResult> MarkAsync(GateActor actor, GateStudent student, string mode, Guid? cardId, string note, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize marks of one student (kiosk + phone + manual at once) so duplicate detection sees the latest mark.
        await LockStudentAsync(db, student.Id, ct);
        if (actor.UserId is { } user) await audit.RequireAsync(user, ct, Permissions.AttendanceGate);
        // A card may have been replaced while this read waited for the student lock.
        if (cardId is { } id && !await db.StudentCards.AnyAsync(x => x.Id == id && x.RevokedAt == null, ct))
            return new("Revoked", null, null, "Este carnet fue reemplazado por uno nuevo. Retenlo y usa la búsqueda manual.", student, null, null);
        var now = time.GetUtcNow();
        var today = courses.LocalDate(now);
        var last = await db.GateEvents.AsNoTracking().Where(x => x.StudentId == student.Id && x.LocalDate == today && x.VoidedAt == null)
            .OrderByDescending(x => x.OccurredAt).FirstOrDefaultAsync(ct);
        var since = last is null ? TimeSpan.MaxValue : now - last.OccurredAt;
        string direction;
        if (mode == GateMode.Auto)
        {
            if (last is null || last.Direction == GateDirection.Out)
            {
                if (last is not null && since < TimeSpan.FromSeconds(Options.DuplicateSeconds))
                    return Duplicate(student, last, "Salida ya registrada");
                direction = GateDirection.In;
            }
            else
            {
                if (since < TimeSpan.FromMinutes(Options.AutoExitAfterMinutes))
                    return Duplicate(student, last, "Entrada ya registrada");
                direction = GateDirection.Out;
            }
        }
        else
        {
            direction = mode;
            if (last is not null && last.Direction == direction)
            {
                // A fixed-direction kiosk never stores the same direction twice in a row (a re-entry needs an exit first).
                if (actor.Source != GateSource.Manual || since < TimeSpan.FromSeconds(Options.DuplicateSeconds))
                    return Duplicate(student, last, direction == GateDirection.In ? "Entrada ya registrada" : "Salida ya registrada");
                throw new OperationException(409, "same-direction",
                    direction == GateDirection.In ? "El estudiante ya tiene una entrada sin salida hoy." : "La última marca de hoy ya es una salida.");
            }
        }
        var mark = new GateEvent
        {
            Id = Guid.NewGuid(), StudentId = student.Id, Direction = direction, OccurredAt = now, LocalDate = today, Source = actor.Source,
            CardId = cardId, DeviceId = actor.DeviceId, RecordedBy = actor.UserId, Note = note
        };
        db.GateEvents.Add(mark);
        if (actor.Source == GateSource.Manual)
            audit.Record("GateManualMark", actor.UserId!.Value, "Person", student.Id, note, direction);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var label = direction == GateDirection.In ? "Entrada registrada" : "Salida registrada";
        var warning = direction == GateDirection.Out && last is null ? " (sin entrada registrada hoy)" : "";
        return new("Recorded", direction, now, label + warning, student, mark.Id, last?.OccurredAt);
    }

    private static ScanResult Duplicate(GateStudent student, GateEvent last, string label) =>
        new("Duplicate", last.Direction, last.OccurredAt, $"{label}. No se volvió a marcar.", student, last.Id, last.OccurredAt);

    public async Task VoidAsync(Guid actor, Guid eventId, VoidMarkRequest request, CancellationToken ct)
    {
        var reason = Reasons.Require(request.Reason);
        var studentId = await db.GateEvents.AsNoTracking().Where(x => x.Id == eventId).Select(x => (Guid?)x.StudentId).SingleOrDefaultAsync(ct)
            ?? throw OperationException.NotFound("No encontramos esa marca.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockStudentAsync(db, studentId, ct);
        await audit.RequireAsync(actor, ct, Permissions.AttendanceManage);
        var mark = await db.GateEvents.SingleOrDefaultAsync(x => x.Id == eventId, ct) ?? throw OperationException.NotFound("No encontramos esa marca.");
        if (mark.VoidedAt is not null) throw new OperationException(409, "already-voided", "La marca ya estaba anulada.");
        mark.VoidedAt = time.GetUtcNow();
        mark.VoidedBy = actor;
        mark.VoidReason = reason;
        audit.Record("GateMarkVoided", actor, "Person", mark.StudentId, reason, $"{mark.Direction}:{mark.OccurredAt:O}");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    internal static Task LockStudentAsync(SchoolDbContext context, Guid studentId, CancellationToken ct) =>
        context.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource={0}, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=5000;
            IF @result < 0 THROW 51000, 'Gate busy', 1;
            """, ["SchoolPlatform.Gate." + studentId], ct);

    /// <summary>Students with an enrollment valid today, by name or code (manual marking at the gate).</summary>
    public async Task<GateStudent[]> SearchAsync(string? query, CancellationToken ct)
    {
        var text = (query ?? "").Trim();
        if (text.Length < 2) return [];
        var today = courses.LocalDate(time.GetUtcNow());
        var ids = await (from p in db.Persons.AsNoTracking()
                         join s in db.Students on p.Id equals s.PersonId
                         where s.StudentCode == text || p.GivenNames.Contains(text) || p.FamilyNames.Contains(text) ||
                               (p.PreferredName != null && p.PreferredName.Contains(text)) ||
                               (p.GivenNames + " " + p.FamilyNames).Contains(text)
                         where db.Enrollments.Any(e => e.StudentId == p.Id && e.Status == EnrollmentStatus.Active &&
                               db.Years.Any(y => y.Id == e.AcademicYearId && y.StartsOn <= today && y.EndsOn >= today))
                         orderby p.FamilyNames, p.GivenNames
                         select p.Id).Take(20).ToArrayAsync(ct);
        var students = await StudentsAsync(ids, ct);
        return ids.Where(students.ContainsKey).Select(x => students[x]).ToArray();
    }

    public async Task<GateEventView[]> RecentAsync(int take, CancellationToken ct)
    {
        var today = courses.LocalDate(time.GetUtcNow());
        return await ViewsAsync(db.GateEvents.AsNoTracking().Where(x => x.LocalDate == today).OrderByDescending(x => x.OccurredAt)
            .Take(Math.Clamp(take, 1, 50)), ct);
    }

    public async Task<GateEventView[]> StudentEventsAsync(Guid studentId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        if (end < start || end.DayNumber - start.DayNumber > 370) throw OperationException.Invalid("Elige un rango de hasta un año.");
        return await ViewsAsync(db.GateEvents.AsNoTracking().Where(x => x.StudentId == studentId && x.LocalDate >= start && x.LocalDate <= end)
            .OrderByDescending(x => x.OccurredAt), ct);
    }

    public async Task<DailyReport> DailyAsync(DateOnly date, Guid? gradeId, Guid? sectionId, CancellationToken ct)
    {
        var expected = await (from e in db.Enrollments.AsNoTracking()
                              join y in db.Years on e.AcademicYearId equals y.Id
                              join p in db.Placements on e.Id equals p.EnrollmentId
                              join g in db.Grades on p.GradeLevelId equals g.Id
                              join s in db.Sections on p.SectionId equals s.Id into sections
                              from s in sections.DefaultIfEmpty()
                              join person in db.Persons on e.StudentId equals person.Id
                              join student in db.Students on e.StudentId equals student.PersonId
                              where e.Status != EnrollmentStatus.Cancelled && y.StartsOn <= date && y.EndsOn >= date &&
                                    e.EnrolledOn <= date && (e.WithdrawnOn == null || e.WithdrawnOn > date) &&
                                    p.StartsOn <= date && (p.EndsOn == null || p.EndsOn > date) &&
                                    (gradeId == null || p.GradeLevelId == gradeId) && (sectionId == null || p.SectionId == sectionId)
                              select new
                              {
                                  e.StudentId, person.GivenNames, person.FamilyNames, person.PreferredName, student.StudentCode,
                                  GradeSequence = g.Sequence, Grade = g.Name, Section = s == null ? null : s.Name
                              }).ToArrayAsync(ct);
        var ids = expected.Select(x => x.StudentId).Distinct().ToArray();
        var marks = (await db.GateEvents.AsNoTracking().Where(x => x.LocalDate == date && x.VoidedAt == null && ids.Contains(x.StudentId))
            .Select(x => new { x.StudentId, x.Direction, x.OccurredAt }).ToArrayAsync(ct)).ToLookup(x => x.StudentId);
        var rows = expected.DistinctBy(x => x.StudentId)
            .OrderBy(x => x.GradeSequence).ThenBy(x => x.Section).ThenBy(x => x.FamilyNames).ThenBy(x => x.GivenNames)
            .Select(x =>
            {
                var own = marks[x.StudentId].OrderBy(m => m.OccurredAt).ToArray();
                var state = own.Length == 0 ? "Absent" : own[^1].Direction == GateDirection.In ? "Inside" : "Left";
                return new DailyRow(x.StudentId, PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName), x.StudentCode,
                    x.Section is null ? x.Grade : $"{x.Grade} · Sección {x.Section}",
                    own.FirstOrDefault(m => m.Direction == GateDirection.In)?.OccurredAt,
                    own.LastOrDefault(m => m.Direction == GateDirection.Out)?.OccurredAt, state, own.Length);
            }).ToArray();
        return new(date, rows.Length, rows.Count(x => x.FirstIn is not null), rows.Count(x => x.State == "Inside"), rows.Count(x => x.State == "Left"),
            rows.Count(x => x.State == "Absent"), rows);
    }

    /// <summary>Family/student view: days with marks (no operator names). Requires the AcademicRecords faculty or being the student.</summary>
    public async Task<MyGateDay[]?> MyDaysAsync(PeopleService people, Guid userId, Guid studentId, CancellationToken ct)
    {
        var access = await people.StudentAccessAsync(userId, studentId, ct);
        if (access is null || !access.Has(Faculties.AcademicRecords)) return null;
        var since = courses.LocalDate(time.GetUtcNow()).AddDays(-45);
        var marks = await db.GateEvents.AsNoTracking().Where(x => x.StudentId == studentId && x.VoidedAt == null && x.LocalDate >= since)
            .Select(x => new { x.LocalDate, x.Direction, x.OccurredAt }).ToArrayAsync(ct);
        return marks.GroupBy(x => x.LocalDate).OrderByDescending(x => x.Key).Select(day => new MyGateDay(day.Key,
            day.Where(x => x.Direction == GateDirection.In).Min(x => (DateTimeOffset?)x.OccurredAt),
            day.Where(x => x.Direction == GateDirection.Out).Max(x => (DateTimeOffset?)x.OccurredAt), day.Count())).ToArray();
    }

    /// <summary>Card/kiosk summary of students: name, code, today's placement and cycle (empty when not enrolled today), photo flag.</summary>
    public async Task<Dictionary<Guid, GateStudent>> StudentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct, DateOnly? asOf = null)
    {
        if (ids.Count == 0) return [];
        var date = asOf ?? courses.LocalDate(time.GetUtcNow());
        var people = await (from p in db.Persons.AsNoTracking()
                            join s in db.Students on p.Id equals s.PersonId
                            where ids.Contains(p.Id)
                            select new { p.Id, p.GivenNames, p.FamilyNames, p.PreferredName, s.StudentCode, Photo = db.PersonPhotos.Any(x => x.PersonId == p.Id) })
            .ToArrayAsync(ct);
        var placements = (await (from e in db.Enrollments.AsNoTracking()
                                 join y in db.Years on e.AcademicYearId equals y.Id
                                 join p in db.Placements on e.Id equals p.EnrollmentId
                                 join g in db.Grades on p.GradeLevelId equals g.Id
                                 join s in db.Sections on p.SectionId equals s.Id into sections
                                 from s in sections.DefaultIfEmpty()
                                 where ids.Contains(e.StudentId) && e.Status == EnrollmentStatus.Active && y.StartsOn <= date && y.EndsOn >= date &&
                                       (y.Status == YearStatus.EnrollmentOpen || y.Status == YearStatus.Active) &&
                                       p.StartsOn <= date && (p.EndsOn == null || p.EndsOn > date)
                                 select new { e.StudentId, y.Label, Grade = g.Name, Section = s == null ? null : s.Name }).ToArrayAsync(ct))
            .DistinctBy(x => x.StudentId).ToDictionary(x => x.StudentId);
        return people.ToDictionary(x => x.Id, x =>
        {
            placements.TryGetValue(x.Id, out var placement);
            return new GateStudent(x.Id, PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName), x.StudentCode,
                placement is null ? "" : placement.Section is null ? placement.Grade : $"{placement.Grade} · Sección {placement.Section}",
                placement?.Label ?? "", x.Photo);
        });
    }

    private async Task<GateEventView[]> ViewsAsync(IQueryable<GateEvent> query, CancellationToken ct)
    {
        var rows = await (from x in query
                          join p in db.Persons on x.StudentId equals p.Id
                          join s in db.Students on x.StudentId equals s.PersonId
                          join a in db.Accounts on x.RecordedBy equals a.Id into accounts
                          from a in accounts.DefaultIfEmpty()
                          join d in db.GateDevices on x.DeviceId equals d.Id into devices
                          from d in devices.DefaultIfEmpty()
                          select new
                          {
                              x.Id, x.StudentId, p.GivenNames, p.FamilyNames, p.PreferredName, s.StudentCode, x.Direction, x.OccurredAt, x.Source,
                              Account = a == null ? null : a.UserName, Device = d == null ? null : d.Name, x.Note, x.VoidedAt, x.VoidReason
                          }).ToArrayAsync(ct);
        return rows.Select(x => new GateEventView(x.Id, x.StudentId, PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName), x.StudentCode,
            x.Direction, x.OccurredAt, x.Source, x.Device is null ? x.Account ?? "" : $"Dispositivo {x.Device}", x.Note, x.VoidedAt is not null,
            x.VoidReason)).ToArray();
    }
}

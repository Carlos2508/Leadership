using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Documents;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.Learning;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Gate;

public sealed record CardView(Guid StudentId, string Token, DateTimeOffset IssuedAt);
public sealed record CardYear(Guid Id, string Label);
/// <summary>Kind is "student" or "staff"; Token is null until the first card is printed.</summary>
public sealed record PersonCardView(string Kind, string? Token, DateTimeOffset? IssuedAt, CardYear[] Years);
public sealed record ReissueRequest(string Reason);
public sealed record CardBatchRequest(Guid YearId, Guid GradeId, Guid? SectionId);
public sealed record PhotoFile(string Path, string ContentType);

/// <summary>
/// ID cards (students and staff; guardians get none) and photos. Issuing (or printing, which issues missing cards)
/// requires people.manage; a reissue revokes the previous card, so a lost card stops working. Printing names its cycle
/// explicitly. Photos: JPEG/PNG verified by signature, stored privately with a random key.
/// </summary>
public sealed class CardService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, GateService gate, CourseService courses,
    IOptions<StorageOptions> storage, IOptions<GateOptions> gateOptions, IWebHostEnvironment environment)
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string NewToken()
    {
        Span<byte> random = stackalloc byte[12];
        RandomNumberGenerator.Fill(random);
        var chars = new char[12];
        for (var i = 0; i < chars.Length; i++) chars[i] = Alphabet[random[i] & 31];
        return new string(chars);
    }

    public async Task<CardView?> CurrentAsync(Guid studentId, CancellationToken ct) =>
        await db.StudentCards.AsNoTracking().Where(x => x.StudentId == studentId && x.RevokedAt == null)
            .Select(x => new CardView(x.StudentId, x.Token, x.IssuedAt)).SingleOrDefaultAsync(ct);

    public async Task<CardView> ReissueAsync(Guid actor, Guid studentId, ReissueRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await GateService.LockStudentAsync(db, studentId, ct);
        await audit.RequireAsync(actor, ct, Permissions.PeopleManage);
        var reason = Reasons.Require(request.Reason);
        if (!await db.Students.AnyAsync(x => x.PersonId == studentId, ct)) throw OperationException.NotFound("No encontramos ese estudiante.");
        var now = time.GetUtcNow();
        var current = await db.StudentCards.SingleOrDefaultAsync(x => x.StudentId == studentId && x.RevokedAt == null, ct);
        if (current is not null)
        {
            current.RevokedAt = now;
            current.RevokedBy = actor;
            current.RevokeReason = reason;
            // The filtered unique index allows one active card: persist the revocation before adding the new one.
            await db.SaveChangesAsync(ct);
        }
        var card = new StudentCard { Id = Guid.NewGuid(), StudentId = studentId, Token = NewToken(), IssuedAt = now, IssuedBy = actor };
        db.StudentCards.Add(card);
        audit.Record("StudentCardReissued", actor, "Person", studentId, reason, current is null ? "first" : "replaced");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(studentId, card.Token, card.IssuedAt);
    }

    /// <summary>Card data for printing; students without an active card get one (audited as a batch).</summary>
    public async Task<CardData[]> PrintAsync(Guid actor, IReadOnlyList<Guid> studentIds, DateOnly? asOf, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.PeopleManage);
        if (studentIds.Count is 0 or > 400) throw OperationException.Invalid("Imprime entre 1 y 400 carnets por documento.");
        var ids = studentIds.Distinct().ToArray();
        var students = await gate.StudentsAsync(ids, ct, asOf);
        if (students.Count != ids.Length) throw OperationException.NotFound("No encontramos uno de los estudiantes.");
        var names = await NamesAsync(ids, ct);
        var cards = await db.StudentCards.Where(x => ids.Contains(x.StudentId) && x.RevokedAt == null).ToDictionaryAsync(x => x.StudentId, ct);
        var issued = 0;
        foreach (var id in ids.Where(x => !cards.ContainsKey(x)))
        {
            var card = new StudentCard { Id = Guid.NewGuid(), StudentId = id, Token = NewToken(), IssuedAt = time.GetUtcNow(), IssuedBy = actor };
            db.StudentCards.Add(card);
            cards[id] = card;
            issued++;
        }
        if (issued > 0)
        {
            audit.Record("StudentCardsIssued", actor, "Person", ids[0], "", $"{issued}");
            await db.SaveChangesAsync(ct);
        }
        var photos = await PhotosAsync(ids, ct);
        return ids.Select(id =>
        {
            var s = students[id];
            return new CardData(CardKind.Student, names[id].Given, names[id].Family, s.Code, s.Placement, s.Year, cards[id].Token, photos.GetValueOrDefault(id));
        }).ToArray();
    }

    /// <summary>Students placed in a grade (or section) of a year, as of today within the year (or its last day).</summary>
    public async Task<(Guid[] Ids, DateOnly AsOf)> BatchAsync(CardBatchRequest request, CancellationToken ct)
    {
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.YearId, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");
        var date = AsOf(year);
        // Ordered after Distinct (SQL DISTINCT does not keep ORDER BY): by section, then family and given names, so the
        // printed sheets can be handed out classroom by classroom.
        var rows = await (from e in db.Enrollments.AsNoTracking()
                          join p in db.Placements on e.Id equals p.EnrollmentId
                          join person in db.Persons on e.StudentId equals person.Id
                          from section in db.Sections.Where(x => x.Id == p.SectionId).DefaultIfEmpty()
                          where e.AcademicYearId == year.Id && e.Status == EnrollmentStatus.Active && p.GradeLevelId == request.GradeId &&
                                (request.SectionId == null || p.SectionId == request.SectionId) &&
                                p.StartsOn <= date && (p.EndsOn == null || p.EndsOn > date)
                          select new { e.StudentId, Section = section == null ? "" : section.Name, person.FamilyNames, person.GivenNames })
            .Distinct().ToArrayAsync(ct);
        var ids = rows.OrderBy(x => x.Section, StringComparer.Ordinal).ThenBy(x => x.FamilyNames, StringComparer.CurrentCulture)
            .ThenBy(x => x.GivenNames, StringComparer.CurrentCulture).Select(x => x.StudentId).Distinct().ToArray();
        return (ids, date);
    }

    // ---- any person with a card: students and staff (guardians have none)

    /// <summary>Which card a person gets: the student card when they study here, otherwise the staff card; null for anyone else.</summary>
    public async Task<CardKind?> KindAsync(Guid personId, CancellationToken ct)
    {
        if (await db.Students.AnyAsync(x => x.PersonId == personId, ct)) return CardKind.Student;
        if (await db.Staff.AnyAsync(x => x.PersonId == personId, ct)) return CardKind.Staff;
        return null;
    }

    /// <summary>Current card of a student or staff member, plus the cycles a card can be printed for. Null when the person gets no card.</summary>
    public async Task<PersonCardView?> PersonCardAsync(Guid personId, CancellationToken ct)
    {
        if (await KindAsync(personId, ct) is not { } kind) return null;
        var current = kind == CardKind.Student
            ? await db.StudentCards.AsNoTracking().Where(x => x.StudentId == personId && x.RevokedAt == null)
                .Select(x => new { x.Token, x.IssuedAt }).SingleOrDefaultAsync(ct)
            : await db.StaffCards.AsNoTracking().Where(x => x.StaffId == personId && x.RevokedAt == null)
                .Select(x => new { x.Token, x.IssuedAt }).SingleOrDefaultAsync(ct);
        return new(kind == CardKind.Student ? "student" : "staff", current?.Token, current?.IssuedAt, await YearsAsync(ct));
    }

    /// <summary>Cycles a card can be printed for (enrollment open or in course), newest first.</summary>
    public Task<CardYear[]> YearsAsync(CancellationToken ct) =>
        db.Years.AsNoTracking().Where(x => x.Status == YearStatus.EnrollmentOpen || x.Status == YearStatus.Active)
            .OrderByDescending(x => x.StartsOn).Select(x => new CardYear(x.Id, x.Label)).ToArrayAsync(ct);

    public async Task<PersonCardView> ReissuePersonAsync(Guid actor, Guid personId, ReissueRequest request, CancellationToken ct)
    {
        switch (await KindAsync(personId, ct))
        {
            case CardKind.Student:
                await ReissueAsync(actor, personId, request, ct);
                break;
            case CardKind.Staff:
                await ReissueStaffAsync(actor, personId, request, ct);
                break;
            default:
                throw NoCard();
        }
        return (await PersonCardAsync(personId, ct))!;
    }

    /// <summary>Prints the card of one person for an explicit cycle (students: their placement in that cycle).</summary>
    public async Task<CardData> PrintPersonAsync(Guid actor, Guid personId, Guid yearId, CancellationToken ct)
    {
        var kind = await KindAsync(personId, ct) ?? throw NoCard();
        var year = await CardYearAsync(yearId, ct);
        if (kind == CardKind.Staff) return (await PrintStaffAsync(actor, [personId], yearId, ct))[0];
        var asOf = AsOf(year);
        // A student card states grade and validity: only for a placement in the chosen cycle.
        if ((await gate.StudentsAsync([personId], ct, asOf)).GetValueOrDefault(personId) is not { Year.Length: > 0 })
            throw new OperationException(409, "not-enrolled", "El estudiante no tiene inscripción activa en ese ciclo.");
        return (await PrintAsync(actor, [personId], asOf, ct))[0];
    }

    public async Task<CardView> ReissueStaffAsync(Guid actor, Guid staffId, ReissueRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockStaffAsync(staffId, ct);
        await audit.RequireAsync(actor, ct, Permissions.PeopleManage);
        var reason = Reasons.Require(request.Reason);
        if (!await db.Staff.AnyAsync(x => x.PersonId == staffId, ct)) throw OperationException.NotFound("No encontramos a esa persona del personal.");
        var now = time.GetUtcNow();
        var current = await db.StaffCards.SingleOrDefaultAsync(x => x.StaffId == staffId && x.RevokedAt == null, ct);
        if (current is not null)
        {
            current.RevokedAt = now;
            current.RevokedBy = actor;
            current.RevokeReason = reason;
            // The filtered unique index allows one active card: persist the revocation before adding the new one.
            await db.SaveChangesAsync(ct);
        }
        var card = new StaffCard { Id = Guid.NewGuid(), StaffId = staffId, Token = NewToken(), IssuedAt = now, IssuedBy = actor };
        db.StaffCards.Add(card);
        audit.Record("StaffCardReissued", actor, "Person", staffId, reason, current is null ? "first" : "replaced");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(staffId, card.Token, card.IssuedAt);
    }

    /// <summary>Staff card data for printing (active people only); members without an active card get one (audited as a batch).</summary>
    public async Task<CardData[]> PrintStaffAsync(Guid actor, IReadOnlyList<Guid> staffIds, Guid yearId, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.PeopleManage);
        if (staffIds.Count is 0 or > 400) throw OperationException.Invalid("Imprime entre 1 y 400 carnets por documento.");
        var year = await CardYearAsync(yearId, ct);
        var ids = staffIds.Distinct().ToArray();
        var staff = await (from p in db.Persons.AsNoTracking()
                           join s in db.Staff on p.Id equals s.PersonId
                           where ids.Contains(p.Id) && p.Status == PersonStatus.Active
                           select new { p.Id, s.Position }).ToDictionaryAsync(x => x.Id, x => x.Position, ct);
        if (staff.Count != ids.Length) throw OperationException.NotFound("No encontramos a una de las personas activas del personal.");
        var names = await NamesAsync(ids, ct);
        var cards = await db.StaffCards.Where(x => ids.Contains(x.StaffId) && x.RevokedAt == null).ToDictionaryAsync(x => x.StaffId, ct);
        var issued = 0;
        foreach (var id in ids.Where(x => !cards.ContainsKey(x)))
        {
            var card = new StaffCard { Id = Guid.NewGuid(), StaffId = id, Token = NewToken(), IssuedAt = time.GetUtcNow(), IssuedBy = actor };
            db.StaffCards.Add(card);
            cards[id] = card;
            issued++;
        }
        if (issued > 0)
        {
            audit.Record("StaffCardsIssued", actor, "Person", ids[0], "", $"{issued}");
            await db.SaveChangesAsync(ct);
        }
        var photos = await PhotosAsync(ids, ct);
        return ids.Select(id => new CardData(CardKind.Staff, names[id].Given, names[id].Family, "",
            staff[id].Length > 0 ? staff[id] : "Personal", year.Label, cards[id].Token, photos.GetValueOrDefault(id))).ToArray();
    }

    /// <summary>Active staff members ordered by family and given names (sheet printing for the whole staff).</summary>
    public async Task<Guid[]> StaffBatchAsync(CancellationToken ct)
    {
        var rows = await (from p in db.Persons.AsNoTracking()
                          join s in db.Staff on p.Id equals s.PersonId
                          where p.Status == PersonStatus.Active
                          select new { p.Id, p.FamilyNames, p.GivenNames }).ToArrayAsync(ct);
        return rows.OrderBy(x => x.FamilyNames, StringComparer.CurrentCulture).ThenBy(x => x.GivenNames, StringComparer.CurrentCulture)
            .Select(x => x.Id).ToArray();
    }

    private static OperationException NoCard() =>
        OperationException.NotFound("Solo estudiantes y personal del colegio tienen carnet.");

    private async Task<AcademicYear> CardYearAsync(Guid yearId, CancellationToken ct)
    {
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == yearId, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");
        if (year.Status is not (YearStatus.EnrollmentOpen or YearStatus.Active))
            throw OperationException.Invalid("Elige un ciclo con inscripción abierta o en curso.");
        return year;
    }

    /// <summary>Today when it falls within the year; otherwise its first or last day.</summary>
    private DateOnly AsOf(AcademicYear year)
    {
        var today = courses.LocalDate(time.GetUtcNow());
        return today < year.StartsOn ? year.StartsOn : today > year.EndsOn ? year.EndsOn : today;
    }

    /// <summary>Printed name in two lines: preferred or given names, then family names.</summary>
    private async Task<Dictionary<Guid, (string Given, string Family)>> NamesAsync(Guid[] ids, CancellationToken ct) =>
        (await db.Persons.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.PreferredName, x.GivenNames, x.FamilyNames }).ToArrayAsync(ct))
            .ToDictionary(x => x.Id, x => ((x.PreferredName ?? x.GivenNames).Trim(), x.FamilyNames.Trim()));

    private async Task<Dictionary<Guid, byte[]>> PhotosAsync(Guid[] ids, CancellationToken ct)
    {
        var files = await db.PersonPhotos.AsNoTracking().Where(x => ids.Contains(x.PersonId)).ToArrayAsync(ct);
        var result = new Dictionary<Guid, byte[]>();
        foreach (var file in files)
            if (File.Exists(PathFor(file.StorageKey))) result[file.PersonId] = await File.ReadAllBytesAsync(PathFor(file.StorageKey), ct);
        return result;
    }

    private Task LockStaffAsync(Guid staffId, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource={0}, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=5000;
            IF @result < 0 THROW 51000, 'Card busy', 1;
            """, ["SchoolPlatform.StaffCard." + staffId], ct);

    // ---- photos

    public async Task UploadPhotoAsync(Guid actor, Guid personId, IFormFile file, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.PeopleManage);
        if (!await db.Persons.AnyAsync(x => x.Id == personId, ct)) throw OperationException.NotFound("No encontramos esa persona.");
        if (file.Length is 0 || file.Length > gateOptions.Value.MaxPhotoBytes)
            throw OperationException.Invalid($"La foto debe pesar como máximo {gateOptions.Value.MaxPhotoBytes / 1024 / 1024} MB.");
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var type = bytes.AsSpan().StartsWith((byte[])[0xFF, 0xD8, 0xFF]) ? "image/jpeg"
            : bytes.AsSpan().StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) ? "image/png"
            : throw OperationException.Invalid("Usa una foto JPG o PNG.");
        var key = $"photos/{Guid.NewGuid():N}";
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, ct);
        var previous = await db.PersonPhotos.SingleOrDefaultAsync(x => x.PersonId == personId, ct);
        var oldKey = previous?.StorageKey;
        if (previous is null)
        {
            previous = new PersonPhoto { PersonId = personId };
            db.PersonPhotos.Add(previous);
        }
        previous.StorageKey = key;
        previous.ContentType = type;
        previous.Size = bytes.Length;
        previous.Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        previous.UploadedAt = time.GetUtcNow();
        previous.UploadedBy = actor;
        audit.Record("PersonPhotoUpdated", actor, "Person", personId, "", oldKey is null ? "added" : "replaced");
        try { await db.SaveChangesAsync(ct); }
        catch { File.Delete(path); throw; }
        if (oldKey is not null) File.Delete(PathFor(oldKey));
    }

    public async Task RemovePhotoAsync(Guid actor, Guid personId, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.PeopleManage);
        var photo = await db.PersonPhotos.SingleOrDefaultAsync(x => x.PersonId == personId, ct) ?? throw OperationException.NotFound("Esa persona no tiene foto.");
        db.PersonPhotos.Remove(photo);
        audit.Record("PersonPhotoRemoved", actor, "Person", personId);
        await db.SaveChangesAsync(ct);
        File.Delete(PathFor(photo.StorageKey));
    }

    public async Task<PhotoFile?> PhotoAsync(Guid personId, CancellationToken ct)
    {
        var photo = await db.PersonPhotos.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == personId, ct);
        if (photo is null) return null;
        var path = PathFor(photo.StorageKey);
        return File.Exists(path) ? new(path, photo.ContentType) : null;
    }

    private string PathFor(string key)
    {
        var root = Path.GetFullPath(Path.IsPathRooted(storage.Value.Root) ? storage.Value.Root : Path.Combine(environment.ContentRootPath, storage.Value.Root));
        var full = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("Ruta de almacenamiento inválida.");
        return full;
    }
}

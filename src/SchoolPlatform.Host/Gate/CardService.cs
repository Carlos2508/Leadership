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
public sealed record ReissueRequest(string Reason);
public sealed record CardBatchRequest(Guid YearId, Guid GradeId, Guid? SectionId);
public sealed record PhotoFile(string Path, string ContentType);

/// <summary>
/// Student ID cards and photos. Issuing (or printing, which issues missing cards) requires people.manage; a reissue
/// revokes the previous card, so a lost card stops working at the gate. Photos: JPEG/PNG verified by signature,
/// stored privately with a random key.
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
        var students = await gate.StudentsAsync(studentIds, ct, asOf);
        if (students.Count != studentIds.Distinct().Count()) throw OperationException.NotFound("No encontramos uno de los estudiantes.");
        var cards = await db.StudentCards.Where(x => studentIds.Contains(x.StudentId) && x.RevokedAt == null).ToDictionaryAsync(x => x.StudentId, ct);
        var issued = 0;
        foreach (var id in studentIds.Where(x => !cards.ContainsKey(x)).Distinct())
        {
            var card = new StudentCard { Id = Guid.NewGuid(), StudentId = id, Token = NewToken(), IssuedAt = time.GetUtcNow(), IssuedBy = actor };
            db.StudentCards.Add(card);
            cards[id] = card;
            issued++;
        }
        if (issued > 0)
        {
            audit.Record("StudentCardsIssued", actor, "Person", studentIds[0], "", $"{issued}");
            await db.SaveChangesAsync(ct);
        }
        var photos = await db.PersonPhotos.AsNoTracking().Where(x => studentIds.Contains(x.PersonId)).ToDictionaryAsync(x => x.PersonId, ct);
        var result = new List<CardData>();
        foreach (var id in studentIds.Distinct())
        {
            var s = students[id];
            byte[]? photo = null;
            if (photos.TryGetValue(id, out var file) && File.Exists(PathFor(file.StorageKey))) photo = await File.ReadAllBytesAsync(PathFor(file.StorageKey), ct);
            result.Add(new(s.Name, s.Code, s.Placement, s.Year, cards[id].Token, photo));
        }
        return result.ToArray();
    }

    /// <summary>Students placed in a grade (or section) of a year, as of today within the year (or its last day).</summary>
    public async Task<(Guid[] Ids, DateOnly AsOf)> BatchAsync(CardBatchRequest request, CancellationToken ct)
    {
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.YearId, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");
        var today = courses.LocalDate(time.GetUtcNow());
        var date = today < year.StartsOn ? year.StartsOn : today > year.EndsOn ? year.EndsOn : today;
        var ids = await (from e in db.Enrollments.AsNoTracking()
                         join p in db.Placements on e.Id equals p.EnrollmentId
                         join person in db.Persons on e.StudentId equals person.Id
                         where e.AcademicYearId == year.Id && e.Status == EnrollmentStatus.Active && p.GradeLevelId == request.GradeId &&
                               (request.SectionId == null || p.SectionId == request.SectionId) &&
                               p.StartsOn <= date && (p.EndsOn == null || p.EndsOn > date)
                         orderby person.FamilyNames, person.GivenNames
                         select e.StudentId).Distinct().ToArrayAsync(ct);
        return (ids, date);
    }

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

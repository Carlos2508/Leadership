using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.LegacyImport;

// Reviewed, normalized staging produced outside this repository from an authorized read-only export.
public sealed record StagingManifest(string Source, Guid SchoolId, string Scope, DateTimeOffset ExportedAt);
public sealed record StagingStudent(string LegacyId, string? GivenNames, string? FamilyNames, string? BirthDate, string? StudentCode,
    string? ParentLegacyId, string? Phone, string? Email);
public sealed record StagingGuardian(string LegacyId, string? GivenNames, string? FamilyNames, string? Phone, string? Email,
    string? Relationship);
public sealed record StagingFile(StagingManifest Manifest, StagingStudent[] Students, StagingGuardian[] Guardians);

public sealed record ImportIncident(string Entity, string LegacyId, string Code, string Message);
public sealed record ImportPlan(string Sha256, int StudentsToCreate, int GuardiansToCreate, int RelationshipsToCreate,
    int AlreadyImported, ImportIncident[] Incidents, bool Applied);

// Idempotency map between legacy rows and new persons. Several legacy IDs may point to the same person.
public sealed class LegacyIdentity
{
    public string Source { get; set; } = "";
    public string Entity { get; set; } = "";
    public string LegacyId { get; set; } = "";
    public Guid PersonId { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public string FileSha256 { get; set; } = "";
}

/// <summary>
/// MIG-001a: limited import of students, related guardians and verifiable relationships from a reviewed staging file.
/// plan = dry-run report with incidents; apply requires the exact reviewed checksum, this installation's SchoolId and an
/// active actor with people.manage. Never imports passwords, grades, payments or tasks; relationships carry no faculty,
/// so nobody gains portal access until the school verifies each one. Re-running apply creates no duplicates.
/// </summary>
public sealed partial class LegacyPeopleImporter(SchoolDbContext db, SchoolAudit audit, TimeProvider time, IOptions<InstitutionOptions> institution)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ImportPlan> RunAsync(byte[] content, bool apply, string? expectedSha256, string? actorUsername, CancellationToken ct)
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(content));
        StagingFile file;
        try { file = JsonSerializer.Deserialize<StagingFile>(content, Json) ?? throw new JsonException(); }
        catch (JsonException) { throw new InvalidOperationException("El archivo de staging no tiene el formato esperado."); }
        if (file.Manifest is null || file.Students is null || file.Guardians is null) throw new InvalidOperationException("Falta el manifiesto o las listas del staging.");
        if (file.Manifest.SchoolId != institution.Value.SchoolId)
            throw new InvalidOperationException("El manifiesto pertenece a otra instalación (SchoolId distinto). No se importa.");
        var source = Clean(file.Manifest.Source);
        if (!SourcePattern().IsMatch(source)) throw new InvalidOperationException("Fuente del manifiesto inválida.");

        var incidents = new List<ImportIncident>();
        var map = await db.LegacyIdentities.AsNoTracking().Where(x => x.Source == source)
            .ToDictionaryAsync(x => (x.Entity, x.LegacyId), x => x.PersonId, ct);
        var existingCodes = (await db.Students.AsNoTracking().Select(x => x.StudentCode).ToArrayAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var guardians = new Dictionary<string, (StagingGuardian Row, string Given, string Family)>();
        foreach (var g in file.Guardians)
        {
            if (string.IsNullOrWhiteSpace(g.LegacyId) || guardians.ContainsKey(g.LegacyId))
            { incidents.Add(new("guardian", g.LegacyId ?? "", "duplicate-or-empty-id", "Identificador vacío o repetido en el staging.")); continue; }
            if (!Names(g.GivenNames, g.FamilyNames, out var given, out var family))
            { incidents.Add(new("guardian", g.LegacyId, "names-review", "Nombres y apellidos separados requeridos (no se dividen automáticamente).")); continue; }
            guardians[g.LegacyId] = (g, given, family);
        }
        var referenced = file.Students.Select(s => s.ParentLegacyId).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet();
        foreach (var unused in guardians.Keys.Where(k => !referenced.Contains(k)).ToArray())
        {
            incidents.Add(new("guardian", unused, "no-student", "Encargado sin estudiante seleccionado; se excluye."));
            guardians.Remove(unused);
        }

        var students = new List<(StagingStudent Row, string Given, string Family, DateOnly? Birth, string Code)>();
        var seenStudents = new HashSet<string>();
        var codesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in file.Students)
        {
            if (string.IsNullOrWhiteSpace(s.LegacyId) || !seenStudents.Add(s.LegacyId))
            { incidents.Add(new("student", s.LegacyId ?? "", "duplicate-or-empty-id", "Identificador vacío o repetido en el staging.")); continue; }
            if (!Names(s.GivenNames, s.FamilyNames, out var given, out var family))
            { incidents.Add(new("student", s.LegacyId, "names-review", "Nombres y apellidos separados requeridos (no se dividen automáticamente).")); continue; }
            DateOnly? birth = null;
            if (!string.IsNullOrWhiteSpace(s.BirthDate))
            {
                if (!DateOnly.TryParseExact(s.BirthDate, "yyyy-MM-dd", out var parsed) || parsed.Year < 1990 || parsed > DateOnly.FromDateTime(DateTime.UtcNow))
                { incidents.Add(new("student", s.LegacyId, "birth-review", "Fecha de nacimiento inválida; no se inventa, revisar.")); continue; }
                birth = parsed;
            }
            var code = (s.StudentCode ?? "").Trim().ToUpperInvariant();
            if (map.ContainsKey(("student", s.LegacyId))) { students.Add((s, given, family, birth, code)); continue; }
            if (!CodePattern().IsMatch(code))
            { incidents.Add(new("student", s.LegacyId, "code-review", "Código de estudiante ausente o inválido; definirlo en el padrón.")); continue; }
            if (existingCodes.Contains(code) || !codesInFile.Add(code))
            { incidents.Add(new("student", s.LegacyId, "code-collision", "Código de estudiante repetido; no se unen personas por código sin revisión.")); continue; }
            if (!string.IsNullOrWhiteSpace(s.ParentLegacyId) && !guardians.ContainsKey(s.ParentLegacyId) && !map.ContainsKey(("guardian", s.ParentLegacyId)))
                incidents.Add(new("student", s.LegacyId, "orphan-parent", "Referencia a encargado inexistente o excluido; el estudiante se importa sin vínculo."));
            students.Add((s, given, family, birth, code));
        }

        var studentsToCreate = students.Where(x => !map.ContainsKey(("student", x.Row.LegacyId))).ToArray();
        var guardiansToCreate = guardians.Values.Where(x => !map.ContainsKey(("guardian", x.Row.LegacyId))).ToArray();
        var pairs = students.Where(x => !string.IsNullOrWhiteSpace(x.Row.ParentLegacyId) &&
                (guardians.ContainsKey(x.Row.ParentLegacyId!) || map.ContainsKey(("guardian", x.Row.ParentLegacyId!))))
            .Select(x => (Student: x.Row.LegacyId, Guardian: x.Row.ParentLegacyId!)).ToArray();
        var alreadyImported = students.Count(x => map.ContainsKey(("student", x.Row.LegacyId))) +
            guardians.Values.Count(x => map.ContainsKey(("guardian", x.Row.LegacyId)));

        if (!apply)
            return new(sha, studentsToCreate.Length, guardiansToCreate.Length, pairs.Length, alreadyImported, incidents.ToArray(), false);

        // ---- apply: only the exact reviewed file, by an accountable actor
        if (!string.Equals(expectedSha256, sha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El checksum no coincide con el plan revisado. Ejecuta plan y revisa de nuevo antes de aplicar.");
        var actor = await db.Accounts.AsNoTracking().Where(a => a.UserName == actorUsername && a.IsActive).Select(a => (Guid?)a.Id).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Indica una cuenta activa responsable de la importación.");
        await audit.RequireAsync(actor, ct, Permissions.PeopleManage);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = time.GetUtcNow();
        var created = new Dictionary<(string, string), Guid>(map);
        foreach (var g in guardiansToCreate)
        {
            var person = NewPerson(g.Given, g.Family, null, g.Row.Phone, g.Row.Email, now);
            person.Guardian = new GuardianProfile { PersonId = person.Id, CreatedAt = now };
            db.Persons.Add(person);
            db.LegacyIdentities.Add(new LegacyIdentity { Source = source, Entity = "guardian", LegacyId = g.Row.LegacyId, PersonId = person.Id, ImportedAt = now, FileSha256 = sha });
            created[("guardian", g.Row.LegacyId)] = person.Id;
        }
        foreach (var s in studentsToCreate)
        {
            var person = NewPerson(s.Given, s.Family, s.Birth, s.Row.Phone, s.Row.Email, now);
            person.Student = new StudentProfile { PersonId = person.Id, StudentCode = s.Code, CreatedAt = now };
            db.Persons.Add(person);
            db.LegacyIdentities.Add(new LegacyIdentity { Source = source, Entity = "student", LegacyId = s.Row.LegacyId, PersonId = person.Id, ImportedAt = now, FileSha256 = sha });
            created[("student", s.Row.LegacyId)] = person.Id;
        }
        await db.SaveChangesAsync(ct);
        var relationships = 0;
        foreach (var (student, guardian) in pairs)
        {
            var studentId = created[("student", student)];
            var guardianId = created[("guardian", guardian)];
            if (await db.Relationships.AnyAsync(r => r.StudentId == studentId && r.GuardianId == guardianId && r.EndedAt == null, ct)) continue;
            var kind = guardians.TryGetValue(guardian, out var g) && RelationshipKinds.All.Contains(g.Row.Relationship ?? "") ? g.Row.Relationship! : "Other";
            // No faculty: the legacy reference is evidence of association, not of legal or portal rights.
            db.Relationships.Add(new GuardianRelationship
            {
                Id = Guid.NewGuid(), StudentId = studentId, GuardianId = guardianId, Kind = kind,
                StartsOn = DateOnly.FromDateTime(now.UtcDateTime), CreatedAt = now, CreatedBy = actor
            });
            relationships++;
        }
        audit.Record("LegacyPeopleImported", actor, "ImportBatch", Guid.NewGuid(),
            "Importación limitada desde staging revisado.", $"{studentsToCreate.Length}:{guardiansToCreate.Length}:{relationships}:{sha[..12]}");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(sha, studentsToCreate.Length, guardiansToCreate.Length, relationships, alreadyImported, incidents.ToArray(), true);
    }

    private static Person NewPerson(string given, string family, DateOnly? birth, string? phone, string? email, DateTimeOffset now)
    {
        var person = new Person
        {
            Id = Guid.NewGuid(), GivenNames = given, FamilyNames = family, BirthDate = birth, Status = PersonStatus.Active, CreatedAt = now, UpdatedAt = now
        };
        if (!string.IsNullOrWhiteSpace(phone) && PhonePattern().IsMatch(phone.Trim()))
            person.Contacts.Add(new PersonContact { Id = Guid.NewGuid(), PersonId = person.Id, Kind = ContactKinds.Phone, Value = phone.Trim(), Label = "Legado", IsPrimary = true });
        if (!string.IsNullOrWhiteSpace(email) && System.Net.Mail.MailAddress.TryCreate(email.Trim(), out var mail) && mail.Address == email.Trim())
            person.Contacts.Add(new PersonContact { Id = Guid.NewGuid(), PersonId = person.Id, Kind = ContactKinds.Email, Value = email.Trim(), Label = "Legado", IsPrimary = true });
        return person;
    }

    private static bool Names(string? givenNames, string? familyNames, out string given, out string family)
    {
        given = Clean(givenNames);
        family = Clean(familyNames);
        return given.Length is >= 1 and <= 100 && family.Length is >= 1 and <= 100;
    }

    private static string Clean(string? value) => WhiteSpace().Replace((value ?? "").Normalize().Trim(), " ");

    [GeneratedRegex(@"\s+")] private static partial Regex WhiteSpace();
    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9.\-]{0,29}$")] private static partial Regex CodePattern();
    [GeneratedRegex(@"^[a-z0-9\-]{1,40}$")] private static partial Regex SourcePattern();
    [GeneratedRegex(@"^\+?[0-9][0-9 \-]{5,24}$")] private static partial Regex PhonePattern();
}

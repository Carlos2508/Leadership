using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.People;

public sealed partial class PeopleService(SchoolDbContext db, SchoolAudit audit, TimeProvider time,
    IOptions<InstitutionOptions> institution)
{
    private const int PageSize = 20;

    public async Task<PagedResult<PersonSummary>> ListAsync(string? search, string? role, string? status, int page, CancellationToken ct)
    {
        if (page is < 1 or > 10000 || search?.Length > 100) throw OperationException.Invalid("Consulta inválida.");
        var query = db.Persons.AsNoTracking();
        query = role switch
        {
            null or "" or "all" => query,
            "student" => query.Where(x => x.Student != null),
            "guardian" => query.Where(x => x.Guardian != null),
            "staff" => query.Where(x => x.Staff != null),
            "none" => query.Where(x => x.Student == null && x.Guardian == null && x.Staff == null),
            _ => throw OperationException.Invalid("Filtro de rol inválido.")
        };
        query = status switch
        {
            null or "" or PersonStatus.Active => query.Where(x => x.Status == PersonStatus.Active),
            PersonStatus.Archived => query.Where(x => x.Status == PersonStatus.Archived),
            "all" => query,
            _ => throw OperationException.Invalid("Filtro de estado inválido.")
        };
        foreach (var term in Normalize(search).Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5))
            query = query.Where(x => x.GivenNames.Contains(term) || x.FamilyNames.Contains(term) ||
                (x.PreferredName != null && x.PreferredName.Contains(term)) ||
                (x.Student != null && x.Student.StudentCode.Contains(term)));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.FamilyNames).ThenBy(x => x.GivenNames).ThenBy(x => x.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(x => new
            {
                x.Id, x.GivenNames, x.FamilyNames, x.PreferredName, StudentCode = x.Student != null ? x.Student.StudentCode : null,
                IsGuardian = x.Guardian != null, IsStaff = x.Staff != null, x.Status,
                Account = db.AccountLinks.Where(l => l.PersonId == x.Id && l.UnlinkedAt == null)
                    .Join(db.Accounts, l => l.UserId, a => a.Id, (l, a) => a.UserName).FirstOrDefault()
            }).ToArrayAsync(ct);
        return new(items.Select(x => new PersonSummary(x.Id, Display(x.GivenNames, x.FamilyNames, x.PreferredName),
            x.GivenNames, x.FamilyNames, x.StudentCode, x.IsGuardian, x.Status, x.Account, x.IsStaff)).ToArray(), total, page, PageSize);
    }

    public async Task<PersonDetail> GetAsync(Guid id, CancellationToken ct)
    {
        var person = await db.Persons.AsNoTracking().Include(x => x.Contacts).Include(x => x.Student).Include(x => x.Guardian).Include(x => x.Staff)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw PersonNotFound();
        var link = await db.AccountLinks.AsNoTracking().Where(x => x.PersonId == id && x.UnlinkedAt == null)
            .Join(db.Accounts, l => l.UserId, a => a.Id, (l, a) => new { l.UserId, a.UserName, a.IsActive, a.PasswordHash, l.LinkedAt })
            .SingleOrDefaultAsync(ct);
        var asStudent = await Relationships(db.Relationships.Where(x => x.StudentId == id), otherIsGuardian: true, ct);
        var asGuardian = await Relationships(db.Relationships.Where(x => x.GuardianId == id), otherIsGuardian: false, ct);
        return new PersonDetail(person.Id, person.DisplayName, person.GivenNames, person.FamilyNames, person.PreferredName,
            person.BirthDate, person.Status, Versions.Encode(person.Version),
            person.Contacts.OrderBy(x => x.Kind).ThenByDescending(x => x.IsPrimary).ThenBy(x => x.Value)
                .Select(x => new ContactView(x.Id, x.Kind, x.Value, x.Label, x.IsPrimary)).ToArray(),
            person.Student?.StudentCode, person.Guardian is not null,
            link is null ? null : new LinkedAccountView(link.UserId, link.UserName ?? "",
                link.PasswordHash is null ? "Pending" : link.IsActive ? "Active" : "Suspended", link.LinkedAt),
            asStudent, asGuardian, person.CreatedAt, person.Staff?.Position);
    }

    private async Task<RelationshipView[]> Relationships(IQueryable<GuardianRelationship> source, bool otherIsGuardian, CancellationToken ct)
    {
        var rows = await source.AsNoTracking().Select(r => new
        {
            r.Id, OtherId = otherIsGuardian ? r.GuardianId : r.StudentId, r.Kind, r.StartsOn, r.EndedAt, r.EndReason, r.Version,
            Faculties = r.Faculties.Where(f => f.RevokedAt == null).Select(f => f.Faculty).ToArray()
        }).ToArrayAsync(ct);
        var ids = rows.Select(x => x.OtherId).Distinct().ToArray();
        var people = await db.Persons.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.GivenNames, x.FamilyNames, x.PreferredName, Code = x.Student != null ? x.Student.StudentCode : null })
            .ToDictionaryAsync(x => x.Id, ct);
        return rows.OrderBy(x => x.EndedAt != null).ThenBy(x => x.StartsOn).Select(x =>
        {
            var other = people[x.OtherId];
            return new RelationshipView(x.Id, x.OtherId, Display(other.GivenNames, other.FamilyNames, other.PreferredName), other.Code,
                x.Kind, x.StartsOn, x.EndedAt, x.EndReason, OrderFaculties(x.Faculties), Versions.Encode(x.Version));
        }).ToArray();
    }

    public async Task<CreatedPerson> CreateAsync(Guid actorId, CreatePersonRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actorId, ct, Permissions.PeopleManage);
        var given = Name(request.GivenNames, "nombres");
        var family = Name(request.FamilyNames, "apellidos");
        var preferred = OptionalName(request.PreferredName);
        var birth = BirthDate(request.BirthDate);
        var contacts = Contacts(request.Contacts);
        var code = request.StudentCode is null || request.StudentCode.Trim() == "" ? null : StudentCode(request.StudentCode);
        if (!request.ConfirmDuplicate && await db.Persons.AnyAsync(x => x.GivenNames == given && x.FamilyNames == family &&
                (birth == null || x.BirthDate == null || x.BirthDate == birth), ct))
            throw new OperationException(409, "possible-duplicate",
                "Ya existe una persona con el mismo nombre. Revisa el directorio antes de registrar otra; si son personas distintas, confirma el registro.");
        if (code is not null && await db.Students.AnyAsync(x => x.StudentCode == code, ct)) throw CodeTaken();
        var position = request.StaffPosition is null ? null : Normalize(request.StaffPosition);
        if (position is not null && position.Length is < 1 or > 80)
            throw OperationException.Invalid("Escribe el cargo o puesto (máximo 80 caracteres).");
        var now = time.GetUtcNow();
        var person = new Person
        {
            Id = Guid.NewGuid(), GivenNames = given, FamilyNames = family, PreferredName = preferred, BirthDate = birth,
            Status = PersonStatus.Active, CreatedAt = now, UpdatedAt = now
        };
        person.Contacts.AddRange(contacts.Select(x => new PersonContact
            { Id = Guid.NewGuid(), PersonId = person.Id, Kind = x.Kind, Value = x.Value, Label = x.Label ?? "", IsPrimary = x.IsPrimary }));
        if (code is not null) person.Student = new StudentProfile { PersonId = person.Id, StudentCode = code, CreatedAt = now };
        if (request.IsGuardian) person.Guardian = new GuardianProfile { PersonId = person.Id, CreatedAt = now };
        if (position is not null) person.Staff = new StaffProfile { PersonId = person.Id, Position = position, CreatedAt = now };
        db.Persons.Add(person);
        audit.Record("PersonCreated", actorId, "Person", person.Id, detail: Roles(code is not null, request.IsGuardian));
        if (position is not null) audit.Record("StaffProfileAdded", actorId, "Person", person.Id);
        await db.SaveChangesAsync(ct);
        return new(person.Id);
    }

    public async Task UpdateAsync(Guid actorId, Guid id, UpdatePersonRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actorId, ct, Permissions.PeopleManage);
        var person = await Load(id, request.Version, ct, includeContacts: true);
        RequireActive(person);
        var changed = new List<string>();
        void Set<T>(string field, T current, T value, Action<T> apply)
        {
            if (EqualityComparer<T>.Default.Equals(current, value)) return;
            apply(value);
            changed.Add(field);
        }
        Set("GivenNames", person.GivenNames, Name(request.GivenNames, "nombres"), x => person.GivenNames = x);
        Set("FamilyNames", person.FamilyNames, Name(request.FamilyNames, "apellidos"), x => person.FamilyNames = x);
        Set("PreferredName", person.PreferredName, OptionalName(request.PreferredName), x => person.PreferredName = x);
        Set("BirthDate", person.BirthDate, BirthDate(request.BirthDate), x => person.BirthDate = x);
        var contacts = Contacts(request.Contacts);
        var currentContacts = person.Contacts.Select(x => (x.Kind, x.Value, x.Label, x.IsPrimary)).Order().ToArray();
        var desiredContacts = contacts.Select(x => (x.Kind, x.Value, Label: x.Label ?? "", x.IsPrimary)).Order().ToArray();
        if (!currentContacts.SequenceEqual(desiredContacts))
        {
            db.PersonContacts.RemoveRange(person.Contacts);
            foreach (var contact in contacts)
                db.PersonContacts.Add(new PersonContact
                    { Id = Guid.NewGuid(), PersonId = person.Id, Kind = contact.Kind, Value = contact.Value, Label = contact.Label ?? "", IsPrimary = contact.IsPrimary });
            changed.Add("Contacts");
        }
        if (changed.Count == 0) return;
        person.UpdatedAt = time.GetUtcNow();
        db.Entry(person).Property(x => x.UpdatedAt).IsModified = true;
        audit.Record("PersonUpdated", actorId, "Person", person.Id, detail: string.Join(',', changed));
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRoleAsync(Guid actorId, Guid id, AddRoleRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actorId, ct, Permissions.PeopleManage);
        var person = await Load(id, request.Version, ct);
        RequireActive(person);
        var now = time.GetUtcNow();
        if (request.Role == "student")
        {
            if (await db.Students.AnyAsync(x => x.PersonId == id, ct))
                throw new OperationException(409, "role-exists", "Esta persona ya tiene perfil de estudiante.");
            var code = StudentCode(request.StudentCode);
            if (await db.Students.AnyAsync(x => x.StudentCode == code, ct)) throw CodeTaken();
            db.Students.Add(new StudentProfile { PersonId = id, StudentCode = code, CreatedAt = now });
        }
        else if (request.Role == "staff")
        {
            if (await db.Staff.AnyAsync(x => x.PersonId == id, ct))
                throw new OperationException(409, "role-exists", "Esta persona ya tiene perfil de personal.");
            var position = Normalize(request.Position);
            if (position.Length is < 1 or > 80) throw OperationException.Invalid("Escribe el cargo o puesto (máximo 80 caracteres).");
            db.Staff.Add(new StaffProfile { PersonId = id, Position = position, CreatedAt = now });
        }
        else if (request.Role == "guardian")
        {
            if (await db.Guardians.AnyAsync(x => x.PersonId == id, ct))
                throw new OperationException(409, "role-exists", "Esta persona ya tiene perfil de encargado.");
            db.Guardians.Add(new GuardianProfile { PersonId = id, CreatedAt = now });
        }
        else throw OperationException.Invalid("Selecciona un perfil válido.");
        Touch(person);
        audit.Record(request.Role switch { "student" => "StudentProfileAdded", "staff" => "StaffProfileAdded", _ => "GuardianProfileAdded" },
            actorId, "Person", id);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(Guid actorId, Guid id, PersonStatusRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actorId, ct, Permissions.PeopleManage);
        var reason = Reasons.Require(request.Reason);
        if (request.Status is not (PersonStatus.Active or PersonStatus.Archived)) throw OperationException.Invalid("Estado inválido.");
        var person = await Load(id, request.Version, ct);
        if (person.Status == request.Status) return;
        if (request.Status == PersonStatus.Archived)
        {
            if (await db.AccountLinks.AnyAsync(x => x.PersonId == id && x.UnlinkedAt == null, ct))
                throw new OperationException(409, "linked-account", "Desvincula primero la cuenta de acceso de esta persona.");
            if (await db.Relationships.AnyAsync(x => (x.StudentId == id || x.GuardianId == id) && x.EndedAt == null, ct))
                throw new OperationException(409, "active-relationships", "Finaliza primero los vínculos familiares vigentes de esta persona.");
        }
        person.Status = request.Status;
        Touch(person);
        audit.Record(request.Status == PersonStatus.Archived ? "PersonArchived" : "PersonRestored", actorId, "Person", id, reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> CreateRelationshipAsync(Guid actorId, CreateRelationshipRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actorId, ct, Permissions.PeopleManage);
        var reason = Reasons.Require(request.Reason);
        var kind = Kind(request.Kind);
        var faculties = FacultySet(request.Faculties);
        if (request.StudentId == request.GuardianId) throw OperationException.Invalid("Una persona no puede ser su propio encargado.");
        if (!await db.Students.AnyAsync(x => x.PersonId == request.StudentId && db.Persons.Any(p => p.Id == x.PersonId && p.Status == PersonStatus.Active), ct))
            throw OperationException.Invalid("Selecciona un estudiante activo.");
        if (!await db.Guardians.AnyAsync(x => x.PersonId == request.GuardianId && db.Persons.Any(p => p.Id == x.PersonId && p.Status == PersonStatus.Active), ct))
            throw OperationException.Invalid("Selecciona un encargado activo.");
        if (await db.Relationships.AnyAsync(x => x.StudentId == request.StudentId && x.GuardianId == request.GuardianId && x.EndedAt == null, ct))
            throw new OperationException(409, "relationship-exists", "Ya existe un vínculo vigente entre estas personas.");
        var now = time.GetUtcNow();
        var relationship = new GuardianRelationship
        {
            Id = Guid.NewGuid(), StudentId = request.StudentId, GuardianId = request.GuardianId, Kind = kind,
            StartsOn = request.StartsOn ?? Today(), CreatedAt = now, CreatedBy = actorId
        };
        if (relationship.StartsOn > Today().AddYears(1) || relationship.StartsOn < new DateOnly(1950, 1, 1))
            throw OperationException.Invalid("La fecha de inicio del vínculo no es válida.");
        relationship.Faculties.AddRange(faculties.Select(f => new RelationshipFaculty
            { Id = Guid.NewGuid(), RelationshipId = relationship.Id, Faculty = f, GrantedAt = now, GrantedBy = actorId }));
        db.Relationships.Add(relationship);
        audit.Record("GuardianLinked", actorId, "Relationship", relationship.Id, reason, $"{kind}:{string.Join(',', faculties)}");
        await db.SaveChangesAsync(ct);
        return relationship.Id;
    }

    public async Task UpdateRelationshipAsync(Guid actorId, Guid id, UpdateRelationshipRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actorId, ct, Permissions.PeopleManage);
        var reason = Reasons.Require(request.Reason);
        var kind = Kind(request.Kind);
        var desired = FacultySet(request.Faculties);
        var relationship = await db.Relationships.Include(x => x.Faculties.Where(f => f.RevokedAt == null))
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw RelationshipNotFound();
        CheckVersion(relationship.Version, request.Version);
        if (relationship.EndedAt is not null)
            throw new OperationException(409, "relationship-ended", "Este vínculo ya finalizó; registra uno nuevo si corresponde.");
        var now = time.GetUtcNow();
        var current = relationship.Faculties.Select(x => x.Faculty).ToHashSet();
        foreach (var faculty in relationship.Faculties.Where(x => !desired.Contains(x.Faculty)))
        {
            faculty.RevokedAt = now;
            faculty.RevokedBy = actorId;
        }
        var added = desired.Where(x => !current.Contains(x)).ToArray();
        foreach (var faculty in added)
            db.RelationshipFaculties.Add(new RelationshipFaculty
                { Id = Guid.NewGuid(), RelationshipId = id, Faculty = faculty, GrantedAt = now, GrantedBy = actorId });
        var kindChanged = relationship.Kind != kind;
        relationship.Kind = kind;
        if (!kindChanged && added.Length == 0 && current.SetEquals(desired)) return;
        db.Entry(relationship).Property(x => x.Kind).IsModified = true;
        audit.Record("GuardianFacultiesChanged", actorId, "Relationship", id, reason, $"{kind}:{string.Join(',', desired)}");
        await db.SaveChangesAsync(ct);
    }

    public async Task EndRelationshipAsync(Guid actorId, Guid id, EndRelationshipRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actorId, ct, Permissions.PeopleManage);
        var reason = Reasons.Require(request.Reason);
        var relationship = await db.Relationships.Include(x => x.Faculties.Where(f => f.RevokedAt == null))
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw RelationshipNotFound();
        CheckVersion(relationship.Version, request.Version);
        if (relationship.EndedAt is not null) throw new OperationException(409, "relationship-ended", "Este vínculo ya había finalizado.");
        var now = time.GetUtcNow();
        relationship.EndedAt = now;
        relationship.EndedBy = actorId;
        relationship.EndReason = reason;
        foreach (var faculty in relationship.Faculties)
        {
            faculty.RevokedAt = now;
            faculty.RevokedBy = actorId;
        }
        audit.Record("GuardianUnlinked", actorId, "Relationship", id, reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task<LinkableAccount[]> LinkableAccountsAsync(string? search, CancellationToken ct)
    {
        if (search?.Length > 100) throw OperationException.Invalid("Consulta inválida.");
        var query = db.Accounts.AsNoTracking().Where(a => !db.AccountLinks.Any(l => l.UserId == a.Id && l.UnlinkedAt == null));
        var term = Normalize(search);
        if (term != "") query = query.Where(a => a.UserName!.Contains(term));
        return (await query.OrderBy(x => x.UserName).Take(20).Select(x => new { x.Id, x.UserName, x.IsActive, x.PasswordHash }).ToArrayAsync(ct))
            .Select(x => new LinkableAccount(x.Id, x.UserName ?? "", x.PasswordHash == null ? "Pending" : x.IsActive ? "Active" : "Suspended"))
            .ToArray();
    }

    public async Task LinkAccountAsync(Guid actorId, Guid personId, LinkAccountRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actorId, ct, Permissions.PeopleManage, Permissions.ManageAccounts);
        var reason = Reasons.Require(request.Reason);
        var person = await Load(personId, request.Version, ct);
        RequireActive(person);
        if (!await db.Accounts.AnyAsync(x => x.Id == request.UserId, ct))
            throw OperationException.NotFound("No encontramos esa cuenta.");
        if (await db.AccountLinks.AnyAsync(x => x.PersonId == personId && x.UnlinkedAt == null, ct))
            throw new OperationException(409, "person-linked", "Esta persona ya tiene una cuenta vinculada.");
        if (await db.AccountLinks.AnyAsync(x => x.UserId == request.UserId && x.UnlinkedAt == null, ct))
            throw new OperationException(409, "account-linked", "Esa cuenta ya representa a otra persona.");
        db.AccountLinks.Add(new AccountLink
            { Id = Guid.NewGuid(), UserId = request.UserId, PersonId = personId, LinkedAt = time.GetUtcNow(), LinkedBy = actorId });
        Touch(person);
        audit.Record("AccountLinkedToPerson", actorId, "Person", personId, reason, request.UserId.ToString("N"));
        await db.SaveChangesAsync(ct);
    }

    public async Task UnlinkAccountAsync(Guid actorId, Guid personId, UnlinkAccountRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actorId, ct, Permissions.PeopleManage, Permissions.ManageAccounts);
        var reason = Reasons.Require(request.Reason);
        var person = await Load(personId, request.Version, ct);
        var link = await db.AccountLinks.SingleOrDefaultAsync(x => x.PersonId == personId && x.UnlinkedAt == null, ct)
            ?? throw new OperationException(409, "not-linked", "Esta persona no tiene una cuenta vinculada.");
        link.UnlinkedAt = time.GetUtcNow();
        link.UnlinkedBy = actorId;
        Touch(person);
        audit.Record("AccountUnlinkedFromPerson", actorId, "Person", personId, reason, link.UserId.ToString("N"));
        await db.SaveChangesAsync(ct);
    }

    // ---- Self-service projections: resource authorization comes from the durable account link and faculties.

    public async Task<MyProfile> MeAsync(Guid userId, CancellationToken ct)
    {
        var person = await db.AccountLinks.AsNoTracking().Where(l => l.UserId == userId && l.UnlinkedAt == null)
            .Join(db.Persons.Where(p => p.Status == PersonStatus.Active), l => l.PersonId, p => p.Id, (l, p) => new
            {
                p.Id, p.GivenNames, p.FamilyNames, p.PreferredName,
                Code = p.Student != null ? p.Student.StudentCode : null, IsGuardian = p.Guardian != null, IsStaff = p.Staff != null
            }).SingleOrDefaultAsync(ct);
        if (person is null) return new(null, []);
        var students = person.IsGuardian
            ? await db.Relationships.AsNoTracking().Where(r => r.GuardianId == person.Id && r.EndedAt == null)
                .Join(db.Persons.Where(p => p.Status == PersonStatus.Active), r => r.StudentId, p => p.Id, (r, p) => new
                {
                    p.Id, p.GivenNames, p.FamilyNames, p.PreferredName, Code = p.Student!.StudentCode, r.Kind,
                    Faculties = r.Faculties.Where(f => f.RevokedAt == null).Select(f => f.Faculty).ToArray()
                }).OrderBy(x => x.GivenNames).ToArrayAsync(ct)
            : [];
        return new(new MyPerson(person.Id, Display(person.GivenNames, person.FamilyNames, person.PreferredName), person.Code, person.IsGuardian,
                person.IsStaff),
            students.Select(x => new MyStudent(x.Id, Display(x.GivenNames, x.FamilyNames, x.PreferredName), x.Code, x.Kind,
                OrderFaculties(x.Faculties))).ToArray());
    }

    /// <summary>
    /// Durable resource check for a student record: the account represents the student, or represents a guardian
    /// with an active relationship. Returns null when there is no access (callers respond 404 to avoid enumeration).
    /// </summary>
    public async Task<StudentAccess?> StudentAccessAsync(Guid userId, Guid studentId, CancellationToken ct)
    {
        var personId = await db.AccountLinks.AsNoTracking().Where(l => l.UserId == userId && l.UnlinkedAt == null)
            .Where(l => db.Persons.Any(p => p.Id == l.PersonId && p.Status == PersonStatus.Active))
            .Select(l => (Guid?)l.PersonId).SingleOrDefaultAsync(ct);
        if (personId is null || !await db.Students.AnyAsync(s => s.PersonId == studentId &&
                db.Persons.Any(p => p.Id == s.PersonId && p.Status == PersonStatus.Active), ct)) return null;
        if (personId == studentId) return new(true, []);
        var faculties = await db.Relationships.AsNoTracking()
            .Where(r => r.StudentId == studentId && r.GuardianId == personId && r.EndedAt == null)
            .Select(r => r.Faculties.Where(f => f.RevokedAt == null).Select(f => f.Faculty).ToArray())
            .SingleOrDefaultAsync(ct);
        return faculties is null ? null : new(false, OrderFaculties(faculties));
    }

    // ---- Helpers

    private async Task<Person> Load(Guid id, string version, CancellationToken ct, bool includeContacts = false)
    {
        IQueryable<Person> query = includeContacts ? db.Persons.Include(x => x.Contacts) : db.Persons;
        var person = await query.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw PersonNotFound();
        CheckVersion(person.Version, version);
        return person;
    }

    private static void CheckVersion(byte[] current, string supplied)
    {
        if (!current.AsSpan().SequenceEqual(Versions.Decode(supplied))) throw OperationException.Changed();
    }

    private void Touch(Person person)
    {
        // Updating the row bumps its rowversion, so concurrent edits of the same person are detected.
        person.UpdatedAt = time.GetUtcNow();
        db.Entry(person).Property(x => x.UpdatedAt).IsModified = true;
    }

    private static void RequireActive(Person person)
    {
        if (person.Status != PersonStatus.Active)
            throw new OperationException(409, "person-archived", "Esta persona está archivada. Restáurala antes de modificarla.");
    }

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById(institution.Value.Locale.TimeZone)).DateTime);

    private DateOnly? BirthDate(DateOnly? value)
    {
        if (value is null) return null;
        if (value > Today() || value < new DateOnly(1900, 1, 1)) throw OperationException.Invalid("La fecha de nacimiento no es válida.");
        return value;
    }

    internal static string Display(string given, string family, string? preferred) => $"{preferred ?? given} {family}".Trim();

    private static string Normalize(string? value) => WhiteSpace().Replace(value?.Trim() ?? "", " ");

    private static string Name(string? value, string field)
    {
        var name = Normalize(value);
        if (name.Length is < 1 or > 100 || name.Any(char.IsControl))
            throw OperationException.Invalid($"Escribe los {field} (máximo 100 caracteres).");
        return name;
    }

    private static string? OptionalName(string? value)
    {
        var name = Normalize(value);
        if (name == "") return null;
        if (name.Length > 60 || name.Any(char.IsControl)) throw OperationException.Invalid("El nombre preferido admite como máximo 60 caracteres.");
        return name;
    }

    private static string StudentCode(string? value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? "";
        if (!StudentCodePattern().IsMatch(code))
            throw OperationException.Invalid("El código de estudiante admite de 1 a 30 letras, números, punto o guion.");
        return code;
    }

    private static string Kind(string? value) => value is not null && RelationshipKinds.All.Contains(value)
        ? value : throw OperationException.Invalid("Selecciona el parentesco o tipo de vínculo.");

    private static string[] FacultySet(string[]? values)
    {
        var set = (values ?? []).Distinct().ToArray();
        if (set.Length > Faculties.All.Length || set.Any(x => !Faculties.IsKnown(x))) throw OperationException.Invalid("Facultad inválida.");
        return OrderFaculties(set);
    }

    private static string[] OrderFaculties(IEnumerable<string> values)
    {
        var set = values.ToHashSet();
        return Faculties.All.Select(x => x.Key).Where(set.Contains).ToArray();
    }

    private static ContactInput[] Contacts(ContactInput[]? values)
    {
        var contacts = (values ?? []).Select(x => x with
        {
            Kind = x.Kind?.Trim() ?? "", Value = Normalize(x.Value), Label = Normalize(x.Label)
        }).Where(x => x.Value != "").ToArray();
        if (contacts.Length > 10) throw OperationException.Invalid("Registra como máximo 10 datos de contacto.");
        foreach (var contact in contacts)
        {
            if (!ContactKinds.All.Contains(contact.Kind)) throw OperationException.Invalid("Tipo de contacto inválido.");
            if (contact.Value.Length > 200 || contact.Label!.Length > 60 || contact.Value.Any(char.IsControl))
                throw OperationException.Invalid("Un dato de contacto es demasiado largo.");
            if (contact.Kind == ContactKinds.Phone && !PhonePattern().IsMatch(contact.Value))
                throw OperationException.Invalid("Escribe el teléfono solo con números, espacios, guiones o el prefijo +.");
            if (contact.Kind == ContactKinds.Email && (!MailAddress.TryCreate(contact.Value, out var mail) || mail.Address != contact.Value))
                throw OperationException.Invalid("Escribe un correo electrónico válido.");
        }
        if (contacts.GroupBy(x => x.Kind).Any(g => g.Count(x => x.IsPrimary) > 1))
            throw OperationException.Invalid("Marca como principal solo un contacto de cada tipo.");
        if (contacts.GroupBy(x => (x.Kind, x.Value.ToLowerInvariant())).Any(g => g.Count() > 1))
            throw OperationException.Invalid("Hay datos de contacto repetidos.");
        return contacts;
    }

    private static string Roles(bool student, bool guardian) =>
        string.Join(',', new[] { student ? "Student" : null, guardian ? "Guardian" : null }.Where(x => x is not null));

    private static OperationException PersonNotFound() => OperationException.NotFound("No encontramos a esa persona.");
    private static OperationException RelationshipNotFound() => OperationException.NotFound("No encontramos ese vínculo.");
    private static OperationException CodeTaken() =>
        new(409, "student-code-taken", "Ese código de estudiante ya está asignado. Verifica el expediente existente.");

    [GeneratedRegex(@"\s+")] private static partial Regex WhiteSpace();
    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9.\-]{0,29}$")] private static partial Regex StudentCodePattern();
    [GeneratedRegex(@"^\+?[0-9][0-9 \-]{5,24}$")] private static partial Regex PhonePattern();
}

using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.People;

// An operation receipt, not a household identity: people and family relationships stay independent.
public sealed class FamilyRegistration
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public Guid AcademicYearId { get; set; }
    public byte[] RequestHash { get; set; } = [];
    public string ResultJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed record FamilyPersonInput(Guid? ExistingId, string? Version, string? GivenNames, string? FamilyNames,
    DateOnly? BirthDate, string? Phone, string? Email, string? StudentCode, bool ConfirmDuplicate = false);
public sealed record FamilyGuardianInput(FamilyPersonInput Person);
public sealed record FamilyLinkInput(int GuardianIndex, string Kind, string[]? Faculties);
public sealed record FamilyStudentInput(FamilyPersonInput Person, Guid GradeId, Guid? SectionId, FamilyLinkInput[] Guardians);
public sealed record RegisterFamilyRequest(Guid RequestId, Guid AcademicYearId, DateOnly EnrolledOn,
    FamilyGuardianInput[] Guardians, FamilyStudentInput[] Students, string Reason);
public sealed record RegisteredFamilyStudent(Guid PersonId, Guid EnrollmentId, Guid[] RelationshipIds);
public sealed record RegisteredFamily(Guid RequestId, Guid AcademicYearId, Guid[] GuardianIds, RegisteredFamilyStudent[] Students);

public sealed class FamilyRegistrationService(SchoolDbContext db, PeopleService people, EnrollmentService enrollment,
    SchoolAudit audit, TimeProvider time)
{
    public async Task<RegisteredFamily> RegisterAsync(Guid actor, RegisterFamilyRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.AcademicYearId == Guid.Empty || request.Guardians is null || request.Students is null ||
            request.Guardians.Length > 4 || request.Students.Length is < 1 or > 8 ||
            request.Guardians.Any(g => g?.Person is null) || request.Students.Any(s => s?.Person is null || s.Guardians is null || s.Guardians.Any(g => g is null)))
            throw OperationException.Invalid("Revisa el registro: admite hasta cuatro encargados y entre uno y ocho estudiantes.");
        var reason = Reasons.Require(request.Reason);
        foreach (var child in request.Students)
            if (child.GradeId == Guid.Empty || child.Guardians.Any(g => g.GuardianIndex < 0 || g.GuardianIndex >= request.Guardians.Length) ||
                child.Guardians.Select(g => g.GuardianIndex).Distinct().Count() != child.Guardians.Length)
                throw OperationException.Invalid("Revisa el grado y los encargados seleccionados para cada estudiante.");
        var inputs = request.Guardians.Select(g => g.Person).Concat(request.Students.Select(s => s.Person)).ToArray();
        foreach (var input in inputs)
            if (input.ExistingId is not null && (input.ExistingId == Guid.Empty || input.BirthDate is not null ||
                new[] { input.GivenNames, input.FamilyNames, input.Phone, input.Email, input.StudentCode }.Any(v => !string.IsNullOrEmpty(v))))
                throw OperationException.Invalid("Una ficha existente se reutiliza sin modificar sus datos desde este formulario.");
        if (request.Guardians.Select(g => g.Person.ExistingId).Where(id => id != null).GroupBy(id => id).Any(g => g.Count() > 1) ||
            request.Students.Select(s => s.Person.ExistingId).Where(id => id != null).GroupBy(id => id).Any(g => g.Count() > 1))
            throw OperationException.Invalid("No selecciones dos veces a la misma persona.");
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await EnrollmentWriteScope.LockAsync(db, ct);
        await audit.RequireAsync(actor, ct, Permissions.PeopleRead, Permissions.PeopleManage, Permissions.AcademicRead, Permissions.EnrollmentManage);
        var previous = await db.FamilyRegistrations.AsNoTracking().SingleOrDefaultAsync(b => b.Id == request.RequestId, ct);
        if (previous is not null)
        {
            if (previous.ActorId != actor || !previous.RequestHash.SequenceEqual(hash))
                throw new OperationException(409, "family-attempt-reused", "Ese intento ya corresponde a otro registro familiar.");
            await tx.CommitAsync(ct);
            return JsonSerializer.Deserialize<RegisteredFamily>(previous.ResultJson)!;
        }
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(y => y.Id == request.AcademicYearId, ct)
            ?? throw OperationException.NotFound("No encontramos el ciclo seleccionado.");
        if (!YearStatus.AcceptsEnrollment(year.Status) || request.EnrolledOn < year.StartsOn || request.EnrolledOn > year.EndsOn)
            throw OperationException.Invalid("El ciclo debe aceptar inscripciones y la fecha debe estar dentro de sus fechas.");
        var ids = inputs.Where(p => p.ExistingId != null).Select(p => p.ExistingId!.Value).Distinct().ToArray();
        var existing = await db.Persons.AsNoTracking().Include(p => p.Guardian).Include(p => p.Student)
            .Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        async Task<Guid> Resolve(FamilyPersonInput input, bool guardian, string label)
        {
            if (input.ExistingId is { } id)
            {
                if (!existing.TryGetValue(id, out var person) || person.Status != PersonStatus.Active ||
                    (guardian ? person.Guardian is null : person.Student is null))
                    throw OperationException.Invalid(label + ": selecciona una ficha activa del perfil correspondiente.");
                if (!person.Version.SequenceEqual(Versions.Decode(input.Version))) throw OperationException.Changed();
                return id;
            }
            if (!guardian && string.IsNullOrWhiteSpace(input.StudentCode)) throw OperationException.Invalid(label + ": escribe el código del estudiante.");
            if (guardian && !string.IsNullOrEmpty(input.StudentCode)) throw OperationException.Invalid("El código estudiantil se indica en el estudiante.");
            var contacts = new List<ContactInput>();
            if (!string.IsNullOrWhiteSpace(input.Phone)) contacts.Add(new(ContactKinds.Phone, input.Phone, null, true));
            if (!string.IsNullOrWhiteSpace(input.Email)) contacts.Add(new(ContactKinds.Email, input.Email, null, true));
            try
            {
                return (await people.CreateAsync(actor, new(input.GivenNames ?? "", input.FamilyNames ?? "", null, input.BirthDate,
                    contacts.ToArray(), guardian ? null : input.StudentCode, guardian, input.ConfirmDuplicate), ct)).Id;
            }
            catch (OperationException e) { throw new OperationException(e.Status, e.Code, label + ": " + e.Message); }
        }
        var guardians = new List<Guid>();
        for (var i = 0; i < request.Guardians.Length; i++) guardians.Add(await Resolve(request.Guardians[i].Person, true, $"Encargado {i + 1}"));
        var students = new List<RegisteredFamilyStudent>();
        for (var i = 0; i < request.Students.Length; i++)
        {
            var child = request.Students[i];
            var student = await Resolve(child.Person, false, $"Estudiante {i + 1}");
            var enrollmentId = await enrollment.EnrollAsync(actor, new(student, year.Id, child.GradeId, child.SectionId, request.EnrolledOn, reason), ct);
            var relationships = new List<Guid>();
            foreach (var link in child.Guardians)
            {
                var guardian = guardians[link.GuardianIndex];
                var current = await db.Relationships.AsNoTracking().Include(r => r.Faculties.Where(f => f.RevokedAt == null))
                    .SingleOrDefaultAsync(r => r.StudentId == student && r.GuardianId == guardian && r.EndedAt == null, ct);
                if (current is not null)
                {
                    if (current.Kind != link.Kind || !current.Faculties.Select(f => f.Faculty).ToHashSet().SetEquals(link.Faculties ?? []))
                        throw new OperationException(409, "family-link-exists", "Ya existe un vínculo con otros permisos o parentesco. Revísalo desde la ficha de la persona.");
                    relationships.Add(current.Id);
                }
                else relationships.Add(await people.CreateRelationshipAsync(actor, new(student, guardian, link.Kind, null, link.Faculties, reason), ct));
            }
            students.Add(new(student, enrollmentId, relationships.ToArray()));
        }
        var result = new RegisteredFamily(request.RequestId, year.Id, guardians.ToArray(), students.ToArray());
        db.FamilyRegistrations.Add(new() { Id = request.RequestId, ActorId = actor, AcademicYearId = year.Id, RequestHash = hash,
            ResultJson = JsonSerializer.Serialize(result), CreatedAt = time.GetUtcNow() });
        audit.Record("FamilyRegistered", actor, "FamilyRegistration", request.RequestId, reason, $"guardians:{guardians.Count};students:{students.Count}");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return result;
    }
}

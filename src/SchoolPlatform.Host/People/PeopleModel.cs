namespace SchoolPlatform.Host.People;

// A person is the school's stable record of a human being. It is independent from login accounts,
// family relationships and yearly enrollment. Roles (student/guardian) are separate profiles.
public sealed class Person
{
    public Guid Id { get; set; }
    public string GivenNames { get; set; } = "";
    public string FamilyNames { get; set; } = "";
    public string? PreferredName { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string Status { get; set; } = PersonStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] Version { get; set; } = [];
    public List<PersonContact> Contacts { get; set; } = [];
    public StudentProfile? Student { get; set; }
    public GuardianProfile? Guardian { get; set; }
    public StaffProfile? Staff { get; set; }

    public string DisplayName => $"{PreferredName ?? GivenNames} {FamilyNames}".Trim();
}

public static class PersonStatus
{
    public const string Active = "Active";
    public const string Archived = "Archived";
}

public sealed class PersonContact
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string Kind { get; set; } = "";
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public bool IsPrimary { get; set; }
}

public static class ContactKinds
{
    public const string Phone = "Phone";
    public const string Email = "Email";
    public const string Address = "Address";
    public static readonly string[] All = [Phone, Email, Address];
}

public sealed class StudentProfile
{
    public Guid PersonId { get; set; }
    public string StudentCode { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

// School personnel (teachers and other staff). Permissions still come from the account; teaching scope from assignments.
public sealed class StaffProfile
{
    public Guid PersonId { get; set; }
    public string Position { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class GuardianProfile
{
    public Guid PersonId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// Kinship is descriptive. It never grants portal access by itself: access depends on explicit faculties.
public sealed class GuardianRelationship
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid GuardianId { get; set; }
    public string Kind { get; set; } = "";
    public DateOnly StartsOn { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public Guid? EndedBy { get; set; }
    public string? EndReason { get; set; }
    public byte[] Version { get; set; } = [];
    public List<RelationshipFaculty> Faculties { get; set; } = [];
}

public static class RelationshipKinds
{
    public static readonly string[] All = ["Mother", "Father", "Grandparent", "Sibling", "OtherRelative", "LegalGuardian", "Other"];
}

// Each grant keeps its own history (granted/revoked) instead of overwriting a flag set.
public sealed class RelationshipFaculty
{
    public Guid Id { get; set; }
    public Guid RelationshipId { get; set; }
    public string Faculty { get; set; } = "";
    public DateTimeOffset GrantedAt { get; set; }
    public Guid GrantedBy { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
}

public sealed record FacultyDefinition(string Key, string Label, string Description);

public static class Faculties
{
    public const string AcademicRecords = "AcademicRecords";
    public const string Finance = "Finance";
    public const string BillingResponsible = "BillingResponsible";
    public const string EmergencyContact = "EmergencyContact";
    public const string Pickup = "Pickup";
    public const string Communications = "Communications";

    // The school decides which faculty applies to each relationship; none is granted by default or inferred from kinship.
    public static readonly FacultyDefinition[] All =
    [
        new(AcademicRecords, "Consultar información académica", "Ver resultados publicados. Sujeto además a la solvencia que exija el colegio."),
        new(Finance, "Consultar estado de cuenta", "Ver cargos, pagos y comprobantes del estudiante."),
        new(BillingResponsible, "Responsable de pago", "Figura como responsable de las obligaciones del estudiante."),
        new(EmergencyContact, "Contacto de emergencia", "Puede ser localizado por el colegio en una emergencia."),
        new(Pickup, "Autorizado para recoger", "Puede retirar al estudiante del establecimiento."),
        new(Communications, "Recibir comunicaciones", "Recibe avisos y mensajes sobre el estudiante.")
    ];

    public static bool IsKnown(string key) => All.Any(x => x.Key == key);
}

// Verified association between a login account and the person it represents. One active link per account and per person.
public sealed class AccountLink
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PersonId { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
    public Guid LinkedBy { get; set; }
    public DateTimeOffset? UnlinkedAt { get; set; }
    public Guid? UnlinkedBy { get; set; }
}

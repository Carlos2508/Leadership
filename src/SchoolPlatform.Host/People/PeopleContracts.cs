namespace SchoolPlatform.Host.People;

public sealed record ContactInput(string Kind, string Value, string? Label, bool IsPrimary);

public sealed record CreatePersonRequest(string GivenNames, string FamilyNames, string? PreferredName, DateOnly? BirthDate,
    ContactInput[]? Contacts, string? StudentCode, bool IsGuardian, bool ConfirmDuplicate);

public sealed record UpdatePersonRequest(string Version, string GivenNames, string FamilyNames, string? PreferredName,
    DateOnly? BirthDate, ContactInput[]? Contacts);

public sealed record AddRoleRequest(string Version, string Role, string? StudentCode, string? Position = null);
public sealed record PersonStatusRequest(string Version, string Status, string Reason);

public sealed record CreateRelationshipRequest(Guid StudentId, Guid GuardianId, string Kind, DateOnly? StartsOn,
    string[]? Faculties, string Reason);
public sealed record UpdateRelationshipRequest(string Version, string Kind, string[]? Faculties, string Reason);
public sealed record EndRelationshipRequest(string Version, string Reason);

public sealed record LinkAccountRequest(string Version, Guid UserId, string Reason);
public sealed record UnlinkAccountRequest(string Version, string Reason);

public sealed record PersonSummary(Guid Id, string DisplayName, string GivenNames, string FamilyNames, string? StudentCode,
    bool IsGuardian, string Status, string? Account, bool IsStaff = false);

public sealed record ContactView(Guid Id, string Kind, string Value, string Label, bool IsPrimary);
public sealed record LinkedAccountView(Guid UserId, string Username, string Status, DateTimeOffset LinkedAt);
public sealed record RelationshipView(Guid Id, Guid PersonId, string DisplayName, string? StudentCode, string Kind,
    DateOnly StartsOn, DateTimeOffset? EndedAt, string? EndReason, string[] Faculties, string Version);

public sealed record PersonDetail(Guid Id, string DisplayName, string GivenNames, string FamilyNames, string? PreferredName,
    DateOnly? BirthDate, string Status, string Version, ContactView[] Contacts, string? StudentCode, bool IsGuardian,
    LinkedAccountView? Account, RelationshipView[] Guardians, RelationshipView[] Students, DateTimeOffset CreatedAt,
    string? StaffPosition = null);

public sealed record CreatedPerson(Guid Id);
public sealed record LinkableAccount(Guid Id, string Username, string Status);

public sealed record MyStudent(Guid Id, string DisplayName, string StudentCode, string Relationship, string[] Faculties);
public sealed record MyPerson(Guid Id, string DisplayName, string? StudentCode, bool IsGuardian, bool IsStaff = false);
public sealed record MyProfile(MyPerson? Person, MyStudent[] Students);

public sealed record StudentAccess(bool IsSelf, string[] Faculties)
{
    public bool Has(string faculty) => IsSelf || Faculties.Contains(faculty);
}

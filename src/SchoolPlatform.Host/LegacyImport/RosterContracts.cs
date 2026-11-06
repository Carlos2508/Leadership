using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchoolPlatform.Host.LegacyImport;

public sealed record RosterManifest(Guid BatchId, Guid SchoolId, string Source, string SourceYearId,
    string SourceYearLabel, DateTimeOffset ExportedAt, string SourceSha256, string Scope, Guid TargetYearId, string TargetYearLabel);
public sealed record RosterCandidate(string LegacyStudentId, string? FamilyLegacyId, string Decision, string? PersonKey, string Reason);
public sealed record RosterFamilyDecision(string LegacyFamilyId, string Decision, string[] PersonKeys, string Reason);
public sealed record RosterContact(string Kind, string Value);
public sealed record RosterPerson(string Key, string GivenNames, string FamilyNames, string? BirthDate,
    string? StudentCode, string[] Profiles, RosterContact[] Contacts, string PendingUsername, bool DistinctPersonConfirmed);
public sealed record RosterAlias(string Entity, string LegacyId, string Discriminator, string PersonKey);
public sealed record RosterRelationship(string StudentKey, string GuardianKey, string Kind, DateOnly StartsOn, string[] Faculties, bool Reviewed);
public sealed record RosterEnrollment(string StudentKey, Guid AcademicYearId, Guid GradeLevelId, Guid? SectionId, DateOnly EnrolledOn, bool Reviewed);
public sealed record RosterAccountPreparation(string Mode, string RoleKey, bool RequireEmptyEffectiveAdministrativePermissions,
    bool IsActive, string? PasswordHash, bool IssueActivationCodes, bool SendMessages);
public sealed record RosterFile(int FormatVersion, RosterManifest Manifest, RosterCandidate[] Candidates, RosterFamilyDecision[] Families, RosterPerson[] People,
    RosterAlias[] LegacyAliases, RosterRelationship[] Relationships, RosterEnrollment[] Enrollments, RosterAccountPreparation AccountPreparation);
// Credentials never form part of a package, report, audit record or command-line argument.
public sealed record RosterOperator(string Username, string Password, string? SecondFactorCode = null);
public sealed record RosterItemResult(string Key, Guid PersonId, Guid UserId, Guid? EnrollmentId);
public sealed record RosterReport(Guid BatchId, Guid YearId, string YearLabel, string Server, string Database, string FileSha256, string PlanSha256,
    int Students, int Guardians, int Relationships, int Accounts, int Excluded, bool Applied, bool AlreadyApplied,
    RosterItemResult[] Items, string[] Observations);
public sealed class RosterImportException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public static class RosterJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        MaxDepth = 24
    };

    public static RosterFile Read(byte[] content)
    {
        if (content.Length is 0 or > 20_000_000) throw new RosterImportException("invalid-file-size");
        ReadOnlyMemory<byte> json = content;
        // Common Windows editors can add a UTF-8 BOM. Hashing still uses the original bytes.
        if (content.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) json = json[3..];
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 24 });
            RejectDuplicateProperties(document.RootElement);
            return JsonSerializer.Deserialize<RosterFile>(json.Span, Options) ?? throw new RosterImportException("invalid-json");
        }
        catch (JsonException) { throw new RosterImportException("invalid-json"); }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new RosterImportException("duplicate-json-property");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }
}

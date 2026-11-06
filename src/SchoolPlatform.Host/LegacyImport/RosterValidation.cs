using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using System.Text.RegularExpressions;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.LegacyImport;

internal static partial class RosterValidation
{
    internal static void Require([DoesNotReturnIf(false)] bool condition, string code)
    {
        if (!condition) throw new RosterImportException(code);
    }

    internal static string Clean(string value) => WhiteSpace().Replace(value.Normalize().Trim(), " ");
    internal static bool Key(string? value) => value is not null && KeyPattern().IsMatch(value);
    internal static bool Id(string? value) => value is not null && IdPattern().IsMatch(value);
    internal static bool Sha(string? value) => value is not null && ShaPattern().IsMatch(value);

    internal static void Validate(RosterFile file, Guid schoolId, DateOnly today)
    {
        Require(file.FormatVersion == 2 && file.Manifest.Scope == "initial-students-guardians-enrollments-pending-accounts", "unsupported-format-or-scope");
        var m = file.Manifest;
        Require(m.SchoolId == schoolId && schoolId != Guid.Empty, "wrong-installation");
        Require(m.BatchId != Guid.Empty && m.TargetYearId != Guid.Empty && Key(m.Source) && Id(m.SourceYearId) &&
            m.SourceYearLabel.Length is > 0 and <= 40 && m.TargetYearLabel.Length is > 0 and <= 40 &&
            Sha(m.SourceSha256) && m.ExportedAt != default && DateOnly.FromDateTime(m.ExportedAt.UtcDateTime) <= today, "invalid-manifest");
        Require(file.People.Length is > 0 and <= 5000 && file.Candidates.Length is > 0 and <= 10000 &&
            file.Relationships.Length <= 20000 && file.LegacyAliases.Length <= 20000 && file.Families.Length <= 10000 &&
            !file.People.Any(x => x is null) && !file.Candidates.Any(x => x is null) &&
            !file.Relationships.Any(x => x is null) && !file.Enrollments.Any(x => x is null) && !file.LegacyAliases.Any(x => x is null) && !file.Families.Any(x => x is null), "invalid-lists");
        var a = file.AccountPreparation;
        Require(a.Mode == "pending-without-secrets" && a.RequireEmptyEffectiveAdministrativePermissions && !a.IsActive &&
            a.PasswordHash is null && !a.IssueActivationCodes && !a.SendMessages && a.RoleKey.Length is > 0 and <= 80, "unsafe-account-preparation");
        Require(file.People.Select(p => p.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() == file.People.Length &&
            file.People.Select(p => p.PendingUsername).Distinct(StringComparer.OrdinalIgnoreCase).Count() == file.People.Length, "duplicate-person-or-username");
        foreach (var p in file.People)
        {
            Require(Key(p.Key) && UsernamePattern().IsMatch(p.PendingUsername), "invalid-person-key-or-username");
            Require(Clean(p.GivenNames).Length is > 0 and <= 100 && Clean(p.FamilyNames).Length is > 0 and <= 100 &&
                !p.GivenNames.Any(char.IsControl) && !p.FamilyNames.Any(char.IsControl), "names-review");
            Require(p.Profiles.Length is > 0 and <= 2 && p.Profiles.Distinct().Count() == p.Profiles.Length &&
                p.Profiles.All(x => x is "student" or "guardian"), "invalid-profiles");
            Require(p.BirthDate is null || (DateOnly.TryParseExact(p.BirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var birth) && birth <= today), "birth-review");
            Require(p.Profiles.Contains("student") ? p.StudentCode is not null && CodePattern().IsMatch(p.StudentCode) : p.StudentCode is null, "code-review");
            Require(p.Contacts.Length <= 2 && p.Contacts.Select(c => c?.Kind).Distinct().Count() == p.Contacts.Length, "contact-review");
            foreach (var c in p.Contacts)
                Require(c is not null && c.Value.Length <= 200 && !c.Value.Any(char.IsControl) &&
                    (c.Kind == ContactKinds.Phone ? PhonePattern().IsMatch(c.Value) : c.Kind == ContactKinds.Email &&
                        MailAddress.TryCreate(c.Value, out var mail) && mail.Address == c.Value), "contact-review");
        }
        var people = file.People.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var students = file.People.Where(x => x.Profiles.Contains("student")).ToArray();
        Require(students.Length > 0 && students.Select(x => x.StudentCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() == students.Length, "duplicate-student-code");
        Require(file.Candidates.Select(c => c.LegacyStudentId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == file.Candidates.Length, "duplicate-candidate");
        foreach (var c in file.Candidates)
        {
            Require(Id(c.LegacyStudentId) && (c.FamilyLegacyId is null || Id(c.FamilyLegacyId)) && c.Reason.Trim().Length is >= 8 and <= 500 &&
                c.Decision is "include" or "exclude", "unresolved-candidate");
            Require(c.Decision == "exclude" ? c.PersonKey is null : c.PersonKey is not null &&
                people.TryGetValue(c.PersonKey, out var p) && p.Profiles.Contains("student"), "candidate-person-mismatch");
            Require(c.Decision != "include" || file.LegacyAliases.Any(x => x.Entity == "student" && x.LegacyId == c.LegacyStudentId &&
                x.PersonKey == c.PersonKey && x.Discriminator == "person"), "candidate-alias-required");
        }
        Require(students.All(p => file.Candidates.Any(c => c.Decision == "include" && c.PersonKey == p.Key)), "student-without-candidate");
        Require(file.LegacyAliases.Select(x => $"{x.Entity}/{x.LegacyId}/{x.Discriminator}").Distinct(StringComparer.OrdinalIgnoreCase).Count() == file.LegacyAliases.Length,
            "duplicate-alias");
        foreach (var alias in file.LegacyAliases)
            Require(Id(alias.LegacyId) && Key(alias.Discriminator) && people.TryGetValue(alias.PersonKey, out var p) &&
                (alias.Entity == "student" ? p.Profiles.Contains("student") && alias.Discriminator == "person" : alias.Entity == "parents" && p.Profiles.Contains("guardian")) &&
                !file.Candidates.Any(c => c.Decision == "exclude" && alias.Entity == "student" && c.LegacyStudentId == alias.LegacyId), "invalid-alias");
        Require(file.People.All(p => file.LegacyAliases.Any(x => x.PersonKey == p.Key)), "person-without-alias");
        Require(file.Families.Select(f => f.LegacyFamilyId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == file.Families.Length, "duplicate-family-decision");
        foreach (var f in file.Families)
        {
            Require(Id(f.LegacyFamilyId) && f.Reason.Trim().Length is >= 8 and <= 500 && f.Decision is "include" or "exclude" &&
                f.PersonKeys.Distinct().Count() == f.PersonKeys.Length &&
                file.Candidates.Any(c => c.Decision == "include" && c.FamilyLegacyId == f.LegacyFamilyId), "family-review");
            Require(f.Decision == "exclude" ? f.PersonKeys.Length == 0 : f.PersonKeys.Length > 0 && f.PersonKeys.All(k =>
                people.TryGetValue(k, out var p) && p.Profiles.Contains("guardian") && file.LegacyAliases.Any(a => a.Entity == "parents" &&
                    a.LegacyId == f.LegacyFamilyId && a.PersonKey == k)), "family-person-mismatch");
        }
        Require(file.Candidates.Where(c => c.Decision == "include" && c.FamilyLegacyId is not null)
            .All(c => file.Families.Any(f => f.LegacyFamilyId == c.FamilyLegacyId)), "family-decision-required");
        Require(file.LegacyAliases.Where(a => a.Entity == "parents").All(a => file.Families.Any(f => f.Decision == "include" &&
            f.LegacyFamilyId == a.LegacyId && f.PersonKeys.Contains(a.PersonKey))), "family-alias-mismatch");
        Require(file.Relationships.Select(x => $"{x.StudentKey}/{x.GuardianKey}").Distinct(StringComparer.OrdinalIgnoreCase).Count() == file.Relationships.Length, "duplicate-relationship");
        foreach (var r in file.Relationships)
            Require(r.Reviewed && r.Faculties.Length == 0 && r.StartsOn != default && r.StudentKey != r.GuardianKey &&
                people.TryGetValue(r.StudentKey, out var s) && s.Profiles.Contains("student") &&
                people.TryGetValue(r.GuardianKey, out var g) && g.Profiles.Contains("guardian") && RelationshipKinds.All.Contains(r.Kind), "relationship-review");
        Require(file.People.Where(x => x.Profiles.Contains("guardian")).All(p => file.Relationships.Any(r => r.GuardianKey == p.Key)), "guardian-without-student");
        Require(file.Candidates.Where(c => c.Decision == "include" && c.FamilyLegacyId is not null).All(c =>
            file.Families.Any(f => f.LegacyFamilyId == c.FamilyLegacyId && (f.Decision == "exclude" ||
                file.Relationships.Any(r => r.StudentKey == c.PersonKey && f.PersonKeys.Contains(r.GuardianKey))))), "family-relationship-required");
        Require(file.Enrollments.Length == students.Length && file.Enrollments.Select(x => x.StudentKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() == students.Length, "enrollment-count-mismatch");
        foreach (var e in file.Enrollments)
            Require(e.Reviewed && e.AcademicYearId == m.TargetYearId && e.GradeLevelId != Guid.Empty && e.SectionId != Guid.Empty &&
                e.EnrolledOn != default && people.TryGetValue(e.StudentKey, out var p) && p.Profiles.Contains("student"), "enrollment-review");
    }

    [GeneratedRegex(@"\s+")] private static partial Regex WhiteSpace();
    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,39}$")] private static partial Regex KeyPattern();
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9._:-]{0,59}$")] private static partial Regex IdPattern();
    [GeneratedRegex(@"^[a-fA-F0-9]{64}$")] private static partial Regex ShaPattern();
    [GeneratedRegex(@"^[a-z0-9][a-z0-9.-]{0,59}$")] private static partial Regex UsernamePattern();
    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9.\-]{0,29}$")] private static partial Regex CodePattern();
    [GeneratedRegex(@"^\+?[0-9][0-9 \-]{5,24}$")] private static partial Regex PhonePattern();
}

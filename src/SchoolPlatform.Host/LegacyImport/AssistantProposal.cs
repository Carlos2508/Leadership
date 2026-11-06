using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using static SchoolPlatform.Host.LegacyImport.RosterValidation;

namespace SchoolPlatform.Host.LegacyImport;

public static partial class AssistantProposal
{
    public static RawRosterExport ReadRaw(byte[] bytes)
    {
        Require(bytes.Length is > 0 and <= 20_000_000, "invalid-file-size");
        var raw = JsonSerializer.Deserialize<RawRosterExport>(bytes, AssistantJson.Options)!;
        Require(raw is { FormatVersion: 1, ArtifactKind: "raw-roster-export-needs-review" } && raw.Students.Length is > 0 and <= 5000, "invalid-source-file");
        return raw;
    }

    public static AssistantReview Propose(RawRosterExport raw, Guid year, string label, DateOnly startsOn, string format)
    {
        Require(format is "two-surnames" or "one-surname" or "surname-comma-name", "name-format-required");
        var students = raw.Students.Select(s =>
        {
            var split = Split(s.FullName, format);
            var notices = new List<string>(split.Notices);
            var code = (s.StudentCodeCandidate ?? "").Trim().ToUpperInvariant();
            if (!StudentCode().IsMatch(code)) { code = "MIG-" + s.LegacyStudentId; notices.Add("Código nuevo propuesto; revisar"); }
            var birth = s.BirthDate;
            if (birth is not null && (!DateOnly.TryParseExact(birth, "yyyy-MM-dd", out _) || birth == "0000-00-00"))
                notices.Add("Fecha inválida: corrígela o déjala vacía");
            return new AssistantStudent(s.LegacyStudentId, s.FullName, split.Given, split.Family, birth, code, s.SourceGradeId,
                raw.Grades.FirstOrDefault(g => g.SourceGradeId == s.SourceGradeId)?.Name ?? "Grado de origen sin identificar",
                s.FamilyLegacyId, true, null, null, false, notices.ToArray());
        }).ToArray();
        students = students.Select(s => s with { Notices = [.. s.Notices,
            .. (students.Count(x => x.Code == s.Code) > 1 ? new[] { "Código repetido: revisar antes de importar" } : []),
            .. (students.Count(x => x.GivenNames == s.GivenNames && x.FamilyNames == s.FamilyNames) > 1 ? new[] { "Nombre repetido: revisar identidad" } : [])] }).ToArray();
        var families = raw.Students.Where(s => s.FamilyLegacyId is not null).Select(s => s.FamilyLegacyId!).Distinct().Select(id =>
        {
            var row = raw.Families.SingleOrDefault(f => f.LegacyFamilyId == id);
            var adults = new List<AssistantAdult>();
            var notices = new List<string>();
            foreach (var entry in new[] { (Name: row?.FatherName, Kind: "Father", Suffix: "father"), (Name: row?.MotherName, Kind: "Mother", Suffix: "mother") })
            {
                if (string.IsNullOrWhiteSpace(entry.Name) || Placeholder().IsMatch(entry.Name.Trim())) continue;
                var split = Split(entry.Name, format);
                if (split.Notices.Length > 0) notices.Add("Revisar la separación del nombre de un encargado");
                adults.Add(new("family-" + id + "-" + entry.Suffix, entry.Name, split.Given, split.Family, entry.Kind, true, false));
            }
            if (row is null) notices.Add("Referencia antigua inexistente: el estudiante continuará sin encargado");
            else if (adults.Count == 0) notices.Add("Sin adulto identificado: revisar o continuar sin encargado");
            if (adults.Count == 2 && string.Equals(adults[0].FullName.Trim(), adults[1].FullName.Trim(), StringComparison.InvariantCultureIgnoreCase))
                notices.Add("Padre y madre tienen el mismo nombre: corrige o excluye el adulto duplicado");
            if (adults.Any(a => a.GivenNames == "" || a.FamilyNames == "")) notices.Add("Nombre de encargado incompleto");
            return new AssistantFamily(id, adults.ToArray(), notices.Distinct().ToArray());
        }).ToArray();
        families = families.Select(f => f with
        {
            Notices = f.Adults.Any(a => families.Where(other => other.LegacyId != f.LegacyId).SelectMany(other => other.Adults)
                .Any(other => string.Equals(a.GivenNames, other.GivenNames, StringComparison.InvariantCultureIgnoreCase) &&
                    string.Equals(a.FamilyNames, other.FamilyNames, StringComparison.InvariantCultureIgnoreCase)))
                ? [.. f.Notices, "Encargado repetido en otra ficha: confirma si es la misma persona"] : f.Notices
        }).ToArray();
        return new(Guid.NewGuid(), format, year, label, startsOn, students, families, false);
    }

    public static RosterFile Package(RawRosterExport raw, byte[] rawBytes, AssistantReview review, Guid schoolId, string role)
    {
        Require(review.Confirmed && review.RunId != Guid.Empty, "review-required");
        Require(review.Students.Length == raw.Students.Length && review.Students.Select(s => s.LegacyId).Distinct().Count() == raw.Students.Length &&
            review.Students.All(s => raw.Students.Any(r => r.LegacyStudentId == s.LegacyId && r.FamilyLegacyId == s.FamilyId)), "source-review-mismatch");
        var people = new List<RosterPerson>(); var aliases = new List<RosterAlias>(); var candidates = new List<RosterCandidate>();
        var families = new List<RosterFamilyDecision>(); var relationships = new List<RosterRelationship>(); var enrollments = new List<RosterEnrollment>();
        var allAdults = review.Families.SelectMany(f => f.Adults).ToArray();
        Require(allAdults.Select(a => a.Key).Distinct().Count() == allAdults.Length, "duplicate-person-or-username");
        AssistantAdult Canonical(AssistantAdult adult)
        {
            if (string.IsNullOrWhiteSpace(adult.SameAsKey)) return adult;
            var target = allAdults.SingleOrDefault(a => a.Key == adult.SameAsKey && a.Include && string.IsNullOrWhiteSpace(a.SameAsKey));
            Require(target is not null && target.Key != adult.Key, "family-review");
            return target;
        }
        foreach (var s in review.Students)
        {
            var key = "student-" + s.LegacyId;
            candidates.Add(new(s.LegacyId, s.FamilyId, s.Include ? "include" : "exclude", s.Include ? key : null, "Padrón revisado en el asistente por el operador."));
            if (!s.Include) continue;
            Require(s.GradeId is not null && s.GradeId != Guid.Empty, "grade-mapping-required");
            people.Add(new(key, s.GivenNames, s.FamilyNames, string.IsNullOrWhiteSpace(s.BirthDate) ? null : s.BirthDate,
                s.Code, ["student"], [], Username(review.RunId, key), s.DistinctPersonConfirmed));
            aliases.Add(new("student", s.LegacyId, "person", key));
            enrollments.Add(new(key, review.YearId, s.GradeId.Value, s.SectionId, review.EnrolledOn, true));
        }
        foreach (var id in review.Students.Where(s => s.Include && s.FamilyId is not null).Select(s => s.FamilyId!).Distinct())
        {
            var family = review.Families.SingleOrDefault(f => f.LegacyId == id);
            Require(family is not null, "family-decision-required");
            var original = raw.Families.SingleOrDefault(f => f.LegacyFamilyId == id);
            var adults = family.Adults.Where(a => a.Include).ToArray();
            Require(adults.Length <= 2 && (original is not null || adults.Length == 0), "family-review");
            families.Add(new(id, adults.Length == 0 ? "exclude" : "include", adults.Select(a => Canonical(a).Key).Distinct().ToArray(), "Titulares y vínculos revisados en el asistente."));
            foreach (var a in adults)
            {
                Require(a.Kind is "Father" or "Mother", "relationship-review");
                var canonical = Canonical(a);
                if (!people.Any(p => p.Key == canonical.Key))
                    people.Add(new(canonical.Key, canonical.GivenNames, canonical.FamilyNames, null, null, ["guardian"], [], Username(review.RunId, canonical.Key), canonical.DistinctPersonConfirmed));
                aliases.Add(new("parents", id, a.Kind == "Father" ? "father" : "mother", canonical.Key));
                foreach (var s in review.Students.Where(s => s.Include && s.FamilyId == id))
                {
                    Require(!relationships.Any(r => r.StudentKey == "student-" + s.LegacyId && r.GuardianKey == canonical.Key), "relationship-review");
                    relationships.Add(new("student-" + s.LegacyId, canonical.Key, a.Kind, review.EnrolledOn, [], true));
                }
            }
        }
        return new(2, new(review.RunId, schoolId, raw.Source, raw.SourceYearId, raw.SourceYearLabel, raw.ExportedAt,
            Convert.ToHexStringLower(SHA256.HashData(rawBytes)), "initial-students-guardians-enrollments-pending-accounts", review.YearId, review.YearLabel),
            candidates.ToArray(), families.ToArray(), people.ToArray(), aliases.ToArray(), relationships.ToArray(), enrollments.ToArray(),
            new("pending-without-secrets", role, true, false, null, false, false));
    }

    private static string Username(Guid run, string key) => "m-" + Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(run + ":" + key)))[..20];

    public static (string Given, string Family, string[] Notices) Split(string name, string format)
    {
        var text = Clean(name);
        if (format == "surname-comma-name")
        {
            var pair = text.Split(',');
            return pair.Length == 2 && pair.All(p => p.Trim().Length > 0)
                ? (pair[1].Trim(), pair[0].Trim(), []) : (text, "", ["Separar nombres y apellidos"]);
        }
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var surnameCount = format == "two-surnames" ? 2 : 1;
        if (tokens.Length == 2 && surnameCount == 2)
            return (tokens[0], tokens[1], ["Nombre corto: se propone un apellido; revisar"]);
        if (tokens.Length <= surnameCount) return (text, "", ["Nombre corto: revisar apellidos"]);
        var warnings = tokens.Length < 4 || tokens.Any(t => new[] { "de", "del", "la", "las", "los" }.Contains(t.ToLowerInvariant()))
            ? new[] { "Separación propuesta: revisar nombre compuesto" } : [];
        return (string.Join(' ', tokens[..^surnameCount]), string.Join(' ', tokens[^surnameCount..]), warnings);
    }

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9.\-]{0,29}$")] private static partial Regex StudentCode();
    [GeneratedRegex(@"^(?:n/?a|no tiene|sin datos|desconocid[oa]|-+|\.+)$", RegexOptions.IgnoreCase)] private static partial Regex Placeholder();
}

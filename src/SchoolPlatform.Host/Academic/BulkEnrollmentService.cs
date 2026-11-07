using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Academic;

public sealed record BulkEnrollRequest(Guid AcademicYearId, Guid GradeLevelId, Guid? SectionId, Guid[] StudentIds, DateOnly? EnrolledOn, string? Reason);
public sealed record BulkIssue(int Row, string Name, string Message);
public sealed record BulkEnrollResult(int Enrolled, int AlreadyEnrolled, BulkIssue[] Issues);
public sealed record ImportRow(int Row, string GivenNames, string FamilyNames, string? StudentCode, DateOnly? BirthDate, string? Grade,
    string? Section, string Action, string? Message);
public sealed record EnrollmentCandidate(Guid Id, string Name, string Code, string? Previous);
public sealed record ImportResult(bool Applied, int Create, int Existing, int Enroll, int Errors, ImportRow[] Rows);

/// <summary>
/// Mass enrollment: several students into one grade/section at once, and spreadsheet import (.xlsx/.csv) that creates the
/// student records that do not exist yet (matched by student code) and enrolls them in the grade/section of each row.
/// The import is previewed first; applying it runs in one serializable transaction under the enrollment lock, so a
/// failure leaves nothing half done. It never creates accounts, guardians, grades or payments.
/// </summary>
public sealed class BulkEnrollmentService(SchoolDbContext db, SchoolAudit audit, PeopleService people, EnrollmentService enrollments)
{
    public async Task<BulkEnrollResult> EnrollManyAsync(Guid actor, BulkEnrollRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.EnrollmentManage);
        var ids = (request.StudentIds ?? []).Distinct().ToArray();
        if (ids.Length is 0 or > 500) throw OperationException.Invalid("Selecciona entre 1 y 500 estudiantes.");
        await using var write = await EnrollmentWriteScope.BeginAsync(db, ct);
        var already = await db.Enrollments.AsNoTracking().Where(e => e.AcademicYearId == request.AcademicYearId && ids.Contains(e.StudentId))
            .Select(e => e.StudentId).ToArrayAsync(ct);
        var names = await NamesAsync(ids, ct);
        var issues = new List<BulkIssue>();
        var enrolled = 0;
        var row = 0;
        foreach (var id in ids)
        {
            row++;
            if (already.Contains(id)) continue;
            try
            {
                await enrollments.EnrollAsync(actor, new EnrollRequest(id, request.AcademicYearId, request.GradeLevelId, request.SectionId,
                    request.EnrolledOn, string.IsNullOrWhiteSpace(request.Reason) ? "Inscripción masiva" : request.Reason), ct);
                enrolled++;
            }
            catch (OperationException ex) when (ex.Code is "section-full")
            {
                // Capacity reached: stop here so the rest is not silently placed elsewhere; what was saved is kept.
                issues.Add(new(row, names.GetValueOrDefault(id, ""), ex.Message));
                break;
            }
            catch (OperationException ex) when (ex.Status == 400)
            {
                issues.Add(new(row, names.GetValueOrDefault(id, ""), ex.Message));
            }
        }
        await write.CommitAsync(ct);
        return new(enrolled, already.Length, issues.ToArray());
    }

    /// <summary>
    /// Active students without an enrollment in the year. With a source group (e.g. last year's grade and section) only its
    /// students are listed, so a whole group can be promoted at once; otherwise a name/code search (up to 500 results).
    /// </summary>
    public async Task<EnrollmentCandidate[]> CandidatesAsync(Guid yearId, string? search, Guid? fromYearId, Guid? fromGradeId, Guid? fromSectionId,
        CancellationToken ct)
    {
        var term = search?.Trim() ?? "";
        if (term.Length > 100) throw OperationException.Invalid("Búsqueda demasiado larga.");
        var query = from s in db.Students.AsNoTracking()
                    join p in db.Persons on s.PersonId equals p.Id
                    where p.Status == PersonStatus.Active && !db.Enrollments.Any(e => e.StudentId == s.PersonId && e.AcademicYearId == yearId)
                    select new { s.PersonId, s.StudentCode, p.GivenNames, p.FamilyNames, p.PreferredName };
        if (term != "") query = query.Where(x => x.GivenNames.Contains(term) || x.FamilyNames.Contains(term) || x.StudentCode.Contains(term));
        if (fromYearId is not null && fromGradeId is not null)
            query = query.Where(x => db.Enrollments.Any(e => e.StudentId == x.PersonId && e.AcademicYearId == fromYearId &&
                e.Status != EnrollmentStatus.Cancelled && db.Placements.Any(pl => pl.EnrollmentId == e.Id && pl.GradeLevelId == fromGradeId &&
                    (fromSectionId == null || pl.SectionId == fromSectionId) && (pl.EndsOn == null || pl.EndsOn > pl.StartsOn) &&
                    !db.Placements.Any(later => later.EnrollmentId == e.Id && later.StartsOn > pl.StartsOn))));
        var rows = await query.OrderBy(x => x.FamilyNames).ThenBy(x => x.GivenNames).Take(500).ToArrayAsync(ct);
        var ids = rows.Select(x => x.PersonId).ToArray();
        var previous = (await (from e in db.Enrollments.AsNoTracking()
                               where ids.Contains(e.StudentId) && e.AcademicYearId != yearId && e.Status != EnrollmentStatus.Cancelled
                               join y in db.Years on e.AcademicYearId equals y.Id
                               join pl in db.Placements on e.Id equals pl.EnrollmentId
                               join g in db.Grades on pl.GradeLevelId equals g.Id
                               select new { e.StudentId, YearStart = y.StartsOn, Year = y.Label, Grade = g.Name, PlacedOn = pl.StartsOn,
                                   Section = db.Sections.Where(x => x.Id == pl.SectionId).Select(x => x.Name).FirstOrDefault() })
                .ToArrayAsync(ct))
            .GroupBy(x => x.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.YearStart).ThenByDescending(x => x.PlacedOn).First());
        return rows.Select(x => new EnrollmentCandidate(x.PersonId, PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName), x.StudentCode,
            previous.TryGetValue(x.PersonId, out var p) ? $"{p.Grade}{(p.Section is null ? "" : " " + p.Section)} · {p.Year}" : null)).ToArray();
    }

    /// <summary>
    /// Columns (header names, any order, accents optional): Nombres, Apellidos, Código, Fecha de nacimiento, Grado, Sección.
    /// Grado accepts the grade code or name; Sección the section name of that grade in the year. Empty grade = only create.
    /// </summary>
    public async Task<ImportResult> ImportAsync(Guid actor, Guid yearId, Stream file, string fileName, bool apply, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.PeopleManage, Permissions.EnrollmentManage);
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == yearId, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");
        List<string[]> table;
        try { table = SpreadsheetReader.Read(file, fileName); }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
        {
            throw OperationException.Invalid(ex is InvalidDataException ? ex.Message : "No pudimos leer el archivo. Guárdalo como .xlsx o .csv.");
        }
        if (table.Count < 2) throw OperationException.Invalid("El archivo no tiene filas de estudiantes debajo del encabezado.");
        var header = table[0].Select(Key).ToArray();
        int Col(params string[] names) => Array.FindIndex(header, h => names.Contains(h));
        var cGiven = Col("nombres", "nombre");
        var cFamily = Col("apellidos", "apellido");
        var cCode = Col("codigo", "codigoestudiante", "carne", "carnet");
        var cBirth = Col("fechadenacimiento", "nacimiento", "fechanacimiento");
        var cGrade = Col("grado");
        var cSection = Col("seccion");
        if (cGiven < 0 || cFamily < 0) throw OperationException.Invalid("El encabezado debe incluir las columnas Nombres y Apellidos.");

        var grades = await db.Grades.AsNoTracking().Where(g => g.IsActive).Select(g => new { g.Id, g.Code, g.Name }).ToArrayAsync(ct);
        var sections = await db.Sections.AsNoTracking().Where(s => s.AcademicYearId == yearId).Select(s => new { s.Id, s.GradeLevelId, s.Name }).ToArrayAsync(ct);
        var codes = table.Skip(1).Select(r => Cell(r, cCode)).Where(c => c != "").Select(c => c.ToUpperInvariant()).Distinct().ToArray();
        var existing = await db.Students.AsNoTracking().Where(s => codes.Contains(s.StudentCode)).Select(s => new { s.StudentCode, s.PersonId })
            .ToDictionaryAsync(x => x.StudentCode, x => x.PersonId, StringComparer.OrdinalIgnoreCase, ct);
        var enrolledIds = await db.Enrollments.AsNoTracking().Where(e => e.AcademicYearId == yearId).Select(e => e.StudentId).ToArrayAsync(ct);
        var enrolledSet = enrolledIds.ToHashSet();

        var plan = new List<(ImportRow Row, Guid? PersonId, Guid? GradeId, Guid? SectionId)>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < table.Count; i++)
        {
            var r = table[i];
            var given = Cell(r, cGiven);
            var family = Cell(r, cFamily);
            var code = Cell(r, cCode).ToUpperInvariant();
            var gradeText = Cell(r, cGrade);
            var sectionText = Cell(r, cSection);
            if (given == "" && family == "" && code == "") continue;
            string? error = null;
            DateOnly? birth = null;
            try { birth = SpreadsheetReader.ParseDate(Cell(r, cBirth)); }
            catch (FormatException) { error = "Fecha de nacimiento inválida (usa dd/mm/aaaa)."; }
            if (given == "" || family == "") error ??= "Faltan nombres o apellidos.";
            if (code != "" && !seenCodes.Add(code)) error ??= "Código repetido en el archivo.";
            Guid? gradeId = null, sectionId = null;
            if (gradeText != "")
            {
                gradeId = grades.FirstOrDefault(g => Key(g.Code) == Key(gradeText) || Key(g.Name) == Key(gradeText))?.Id;
                if (gradeId is null) error ??= $"No existe el grado «{gradeText}».";
                else if (sectionText != "")
                {
                    sectionId = sections.FirstOrDefault(s => s.GradeLevelId == gradeId && Key(s.Name) == Key(sectionText))?.Id;
                    if (sectionId is null) error ??= $"No existe la sección «{sectionText}» de ese grado en el ciclo {year.Label}.";
                }
            }
            Guid? personId = code != "" && existing.TryGetValue(code, out var found) ? found : null;
            var action = error is not null ? "Error"
                : personId is not null ? (gradeId is not null && !enrolledSet.Contains(personId.Value) ? "EnrollExisting" : "Existing")
                : gradeId is not null ? "CreateAndEnroll" : "Create";
            var message = error ?? (personId is not null && enrolledSet.Contains(personId.Value) ? "Ya inscrito en este ciclo; no se modifica." : null);
            plan.Add((new ImportRow(i + 1, given, family, code == "" ? null : code, birth, gradeText == "" ? null : gradeText,
                sectionText == "" ? null : sectionText, action, message), personId, gradeId, sectionId));
        }
        var rows = plan.Select(p => p.Row).ToArray();
        var errors = rows.Count(r => r.Action == "Error");
        var summary = new ImportResult(false, rows.Count(r => r.Action.StartsWith("Create")), rows.Count(r => r.Action is "Existing" or "EnrollExisting"),
            rows.Count(r => r.Action is "CreateAndEnroll" or "EnrollExisting"), errors, rows);
        if (!apply) return summary;
        if (errors > 0) throw OperationException.Invalid($"Corrige las {errors} filas con error antes de importar.");

        await using var write = await EnrollmentWriteScope.BeginAsync(db, ct);
        foreach (var item in plan)
        {
            var personId = item.PersonId;
            try
            {
                if (personId is null)
                    personId = (await people.CreateAsync(actor, new CreatePersonRequest(item.Row.GivenNames, item.Row.FamilyNames, null, item.Row.BirthDate,
                        null, item.Row.StudentCode ?? await NextCodeAsync(ct), false, ConfirmDuplicate: true), ct)).Id;
                if (item.GradeId is not null && !enrolledSet.Contains(personId.Value))
                {
                    await enrollments.EnrollAsync(actor, new EnrollRequest(personId.Value, yearId, item.GradeId.Value, item.SectionId, null,
                        "Importación desde hoja de cálculo"), ct);
                    enrolledSet.Add(personId.Value);
                }
            }
            catch (OperationException ex)
            {
                // All or nothing: the transaction is disposed without commit.
                throw new OperationException(ex.Status, ex.Code, $"Fila {item.Row.Row} ({item.Row.GivenNames} {item.Row.FamilyNames}): {ex.Message} No se importó nada.");
            }
        }
        audit.Record("StudentsImported", actor, "AcademicYear", yearId, detail: $"{summary.Create}/{summary.Enroll}");
        await db.SaveChangesAsync(ct);
        await write.CommitAsync(ct);
        return summary with { Applied = true };
    }

    private int codeCounter;

    /// <summary>Students need a code; rows without one get the next free numeric code of the year (e.g. 2027-0001).</summary>
    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var prefix = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture) + "-";
        while (true)
        {
            codeCounter++;
            var code = prefix + codeCounter.ToString("0000", CultureInfo.InvariantCulture);
            if (!await db.Students.AnyAsync(s => s.StudentCode == code, ct) && !db.ChangeTracker.Entries<StudentProfile>().Any(e => e.Entity.StudentCode == code))
                return code;
        }
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(Guid[] ids, CancellationToken ct) =>
        (await db.Persons.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.GivenNames, p.FamilyNames, p.PreferredName }).ToArrayAsync(ct))
        .ToDictionary(p => p.Id, p => PeopleService.Display(p.GivenNames, p.FamilyNames, p.PreferredName));

    private static string Cell(string[] row, int index) => index >= 0 && index < row.Length ? row[index].Trim() : "";

    /// <summary>Lowercase without accents or spaces, to match headers and names written in different ways.</summary>
    private static string Key(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && !char.IsWhiteSpace(c) && c is not '.' and not '_' and not '-')
                builder.Append(c);
        return builder.ToString();
    }
}

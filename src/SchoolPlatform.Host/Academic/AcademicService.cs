using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Academic;

/// <summary>Years, periods, grade catalog and sections. Enrollment lives in <see cref="EnrollmentService"/>.</summary>
public sealed partial class AcademicService(SchoolDbContext db, SchoolAudit audit, TimeProvider time,
    IOptions<InstitutionOptions> institution)
{
    public async Task<YearSummary[]> YearsAsync(CancellationToken ct) =>
        (await db.Years.AsNoTracking().OrderByDescending(x => x.StartsOn)
            .Select(x => new { Year = x, Count = db.Enrollments.Count(e => e.AcademicYearId == x.Id && e.Status != EnrollmentStatus.Cancelled) })
            .ToArrayAsync(ct)).Select(x => Summary(x.Year, x.Count)).ToArray();

    public async Task<YearDetail> YearAsync(Guid id, CancellationToken ct)
    {
        var year = await db.Years.AsNoTracking().Include(x => x.Periods).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw YearNotFound();
        var count = await db.Enrollments.CountAsync(e => e.AcademicYearId == id && e.Status != EnrollmentStatus.Cancelled, ct);
        var sections = await db.Sections.AsNoTracking().Where(x => x.AcademicYearId == id)
            .Select(x => new
            {
                Section = x,
                Current = db.Placements.Count(p => p.SectionId == x.Id && p.EndsOn == null &&
                    db.Enrollments.Any(e => e.Id == p.EnrollmentId && e.Status == EnrollmentStatus.Active))
            }).ToArrayAsync(ct);
        return new(Summary(year, count),
            year.Periods.OrderBy(x => x.Sequence).Select(x => new PeriodView(x.Id, x.Sequence, x.Name, x.StartsOn, x.EndsOn)).ToArray(),
            sections.OrderBy(x => x.Section.Name).Select(x => new SectionView(x.Section.Id, x.Section.GradeLevelId, x.Section.Name,
                x.Section.Shift, x.Section.Capacity, x.Current, Versions.Encode(x.Section.Version))).ToArray());
    }

    public async Task<Guid> CreateYearAsync(Guid actor, YearRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var (label, starts, ends) = YearValues(request);
        if (await db.Years.AnyAsync(x => x.Label == label, ct)) throw new OperationException(409, "year-exists", "Ya existe un ciclo con ese nombre.");
        await RejectOverlapAsync(null, starts, ends, ct);
        var year = new AcademicYear
        {
            Id = Guid.NewGuid(), Label = label, StartsOn = starts, EndsOn = ends, Status = YearStatus.Draft,
            IsDefault = !await db.Years.AnyAsync(ct), CreatedAt = time.GetUtcNow()
        };
        db.Years.Add(year);
        audit.Record("AcademicYearCreated", actor, "AcademicYear", year.Id, detail: label);
        await db.SaveChangesAsync(ct);
        return year.Id;
    }

    public async Task UpdateYearAsync(Guid actor, Guid id, YearRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var (label, starts, ends) = YearValues(request);
        var year = await LoadYear(id, request.Version, ct);
        if (!YearStatus.AllowsCalendarChanges(year.Status))
            throw new OperationException(409, "year-locked", "Las fechas de un ciclo activo o cerrado no se modifican desde aquí.");
        if (await db.Years.AnyAsync(x => x.Label == label && x.Id != id, ct)) throw new OperationException(409, "year-exists", "Ya existe un ciclo con ese nombre.");
        await RejectOverlapAsync(id, starts, ends, ct);
        if (await db.Periods.AnyAsync(x => x.AcademicYearId == id && (x.StartsOn < starts || x.EndsOn > ends), ct))
            throw new OperationException(409, "periods-outside", "Hay períodos fuera de las nuevas fechas. Ajusta primero los períodos.");
        if (await db.Enrollments.AnyAsync(x => x.AcademicYearId == id && (x.EnrolledOn < starts || x.EnrolledOn > ends), ct))
            throw new OperationException(409, "enrollments-outside", "Hay inscripciones con fecha fuera del nuevo rango.");
        year.Label = label;
        year.StartsOn = starts;
        year.EndsOn = ends;
        audit.Record("AcademicYearUpdated", actor, "AcademicYear", id, detail: label);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetYearStatusAsync(Guid actor, Guid id, YearStatusRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var reason = Reasons.Require(request.Reason);
        var year = await LoadYear(id, request.Version, ct);
        var from = Array.IndexOf(YearStatus.Order, year.Status);
        var to = Array.IndexOf(YearStatus.Order, request.Status);
        // Forward-only lifecycle. Reopening a closed year needs an explicit rectification flow (not implemented).
        if (to <= from) throw new OperationException(409, "invalid-transition", "El ciclo solo avanza: borrador, inscripciones abiertas, activo y cerrado.");
        if (request.Status is YearStatus.Active or YearStatus.Closed && !await db.Periods.AnyAsync(x => x.AcademicYearId == id, ct))
            throw new OperationException(409, "periods-required", "Define los períodos del ciclo antes de activarlo.");
        year.Status = request.Status;
        audit.Record("AcademicYearStatusChanged", actor, "AcademicYear", id, reason, request.Status);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetDefaultYearAsync(Guid actor, Guid id, VersionRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var year = await LoadYear(id, request.Version, ct);
        if (year.IsDefault) return;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var current in await db.Years.Where(x => x.IsDefault).ToArrayAsync(ct)) current.IsDefault = false;
        await db.SaveChangesAsync(ct);
        year.IsDefault = true;
        audit.Record("AcademicYearDefaultChanged", actor, "AcademicYear", id, detail: year.Label);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<Guid> AddPeriodAsync(Guid actor, Guid yearId, PeriodRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var year = await db.Years.Include(x => x.Periods).SingleOrDefaultAsync(x => x.Id == yearId, ct) ?? throw YearNotFound();
        if (!YearStatus.AllowsCalendarChanges(year.Status))
            throw new OperationException(409, "year-locked", "Los períodos de un ciclo activo o cerrado no se modifican desde aquí.");
        if (year.Periods.Count >= 12) throw OperationException.Invalid("Un ciclo admite como máximo 12 períodos.");
        CheckPeriod(year, null, request.StartsOn, request.EndsOn);
        var sequence = year.Periods.Count == 0 ? 1 : year.Periods.Max(x => x.Sequence) + 1;
        if (year.Periods.Any(x => x.StartsOn > request.StartsOn))
            throw OperationException.Invalid("Agrega los períodos en orden cronológico.");
        var period = new AcademicPeriod
        {
            Id = Guid.NewGuid(), AcademicYearId = yearId, Sequence = sequence,
            Name = PeriodName(request.Name, sequence), StartsOn = request.StartsOn, EndsOn = request.EndsOn
        };
        db.Periods.Add(period);
        TouchYear(year);
        audit.Record("AcademicPeriodAdded", actor, "AcademicYear", yearId, detail: $"{sequence}");
        await db.SaveChangesAsync(ct);
        return period.Id;
    }

    public async Task UpdatePeriodAsync(Guid actor, Guid periodId, PeriodRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var period = await db.Periods.SingleOrDefaultAsync(x => x.Id == periodId, ct) ?? throw OperationException.NotFound("No encontramos ese período.");
        var year = await db.Years.Include(x => x.Periods).SingleAsync(x => x.Id == period.AcademicYearId, ct);
        if (!YearStatus.AllowsCalendarChanges(year.Status))
            throw new OperationException(409, "year-locked", "Los períodos de un ciclo activo o cerrado no se modifican desde aquí.");
        CheckPeriod(year, period.Id, request.StartsOn, request.EndsOn);
        var ordered = year.Periods.Where(x => x.Id != periodId).Append(new AcademicPeriod { Id = periodId, Sequence = period.Sequence, StartsOn = request.StartsOn })
            .OrderBy(x => x.Sequence).Select(x => x.StartsOn).ToArray();
        if (!ordered.SequenceEqual(ordered.Order())) throw OperationException.Invalid("Las fechas deben respetar el orden de los períodos.");
        period.Name = PeriodName(request.Name, period.Sequence);
        period.StartsOn = request.StartsOn;
        period.EndsOn = request.EndsOn;
        TouchYear(year);
        audit.Record("AcademicPeriodUpdated", actor, "AcademicYear", year.Id, detail: $"{period.Sequence}");
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveLastPeriodAsync(Guid actor, Guid yearId, VersionRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var year = await LoadYear(yearId, request.Version, ct);
        if (year.Status != YearStatus.Draft) throw new OperationException(409, "year-locked", "Solo se eliminan períodos de un ciclo en borrador.");
        var last = await db.Periods.Where(x => x.AcademicYearId == yearId).OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct)
            ?? throw OperationException.NotFound("El ciclo no tiene períodos.");
        db.Periods.Remove(last);
        TouchYear(year);
        audit.Record("AcademicPeriodRemoved", actor, "AcademicYear", yearId, detail: $"{last.Sequence}");
        await db.SaveChangesAsync(ct);
    }

    public async Task<GradeView[]> GradesAsync(CancellationToken ct) =>
        (await db.Grades.AsNoTracking().OrderBy(x => x.Sequence).ThenBy(x => x.Name).ToArrayAsync(ct)).Select(Grade).ToArray();

    public async Task<Guid> SaveGradeAsync(Guid actor, Guid? id, GradeRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var code = (request.Code ?? "").Trim().ToUpperInvariant();
        var name = Clean(request.Name);
        var stage = Clean(request.Stage);
        if (!CodePattern().IsMatch(code)) throw OperationException.Invalid("El código del grado admite de 1 a 20 letras, números o guion.");
        if (name.Length is < 1 or > 80 || stage.Length > 60) throw OperationException.Invalid("Escribe el nombre del grado (máximo 80) y el nivel (máximo 60).");
        if (request.Sequence is < 0 or > 999) throw OperationException.Invalid("El orden debe estar entre 0 y 999.");
        GradeLevel grade;
        if (id is null)
        {
            grade = new GradeLevel { Id = Guid.NewGuid() };
            db.Grades.Add(grade);
        }
        else
        {
            grade = await db.Grades.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos ese grado.");
            if (!grade.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
        }
        if (await db.Grades.AnyAsync(x => x.Id != grade.Id && (x.Code == code || x.Name == name), ct))
            throw new OperationException(409, "grade-exists", "Ya existe un grado con ese código o nombre.");
        grade.Code = code;
        grade.Name = name;
        grade.Stage = stage;
        grade.Sequence = request.Sequence;
        grade.IsActive = request.IsActive;
        audit.Record(id is null ? "GradeCreated" : "GradeUpdated", actor, "Grade", grade.Id, detail: code);
        await db.SaveChangesAsync(ct);
        return grade.Id;
    }

    public async Task<Guid> SaveSectionAsync(Guid actor, Guid yearId, Guid? id, SectionRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var name = Clean(request.Name).ToUpperInvariant();
        var shift = Clean(request.Shift);
        if (name.Length is < 1 or > 20) throw OperationException.Invalid("El nombre de la sección admite de 1 a 20 caracteres.");
        if (shift.Length > 40) throw OperationException.Invalid("La jornada admite como máximo 40 caracteres.");
        if (request.Capacity is < 1 or > 500) throw OperationException.Invalid("El cupo debe estar entre 1 y 500.");
        var year = await db.Years.SingleOrDefaultAsync(x => x.Id == yearId, ct) ?? throw YearNotFound();
        if (year.Status == YearStatus.Closed) throw new OperationException(409, "year-closed", "El ciclo está cerrado.");
        if (!await db.Grades.AnyAsync(x => x.Id == request.GradeLevelId && x.IsActive, ct)) throw OperationException.Invalid("Selecciona un grado activo.");
        Section section;
        if (id is null)
        {
            section = new Section { Id = Guid.NewGuid(), AcademicYearId = yearId, GradeLevelId = request.GradeLevelId };
            db.Sections.Add(section);
        }
        else
        {
            section = await db.Sections.SingleOrDefaultAsync(x => x.Id == id && x.AcademicYearId == yearId, ct)
                ?? throw OperationException.NotFound("No encontramos esa sección.");
            if (!section.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
            if (section.GradeLevelId != request.GradeLevelId && await db.Placements.AnyAsync(x => x.SectionId == section.Id, ct))
                throw new OperationException(409, "section-in-use", "La sección ya tiene estudiantes; no puede cambiar de grado.");
            section.GradeLevelId = request.GradeLevelId;
        }
        if (await db.Sections.AnyAsync(x => x.Id != section.Id && x.AcademicYearId == yearId && x.GradeLevelId == request.GradeLevelId && x.Name == name, ct))
            throw new OperationException(409, "section-exists", "Ese grado ya tiene una sección con ese nombre en el ciclo.");
        section.Name = name;
        section.Shift = shift == "" ? null : shift;
        section.Capacity = request.Capacity;
        audit.Record(id is null ? "SectionCreated" : "SectionUpdated", actor, "AcademicYear", yearId, detail: name);
        await db.SaveChangesAsync(ct);
        return section.Id;
    }

    public async Task RemoveSectionAsync(Guid actor, Guid yearId, Guid id, VersionRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AcademicManage);
        var section = await db.Sections.SingleOrDefaultAsync(x => x.Id == id && x.AcademicYearId == yearId, ct)
            ?? throw OperationException.NotFound("No encontramos esa sección.");
        if (!section.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
        if (await db.Placements.AnyAsync(x => x.SectionId == id, ct))
            throw new OperationException(409, "section-in-use", "La sección tiene historial de estudiantes y no puede eliminarse.");
        db.Sections.Remove(section);
        audit.Record("SectionRemoved", actor, "AcademicYear", yearId, detail: section.Name);
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers

    private static YearSummary Summary(AcademicYear x, int enrollments) =>
        new(x.Id, x.Label, x.StartsOn, x.EndsOn, x.Status, x.IsDefault, Versions.Encode(x.Version), enrollments);

    private static GradeView Grade(GradeLevel x) => new(x.Id, x.Code, x.Name, x.Stage, x.Sequence, x.IsActive, Versions.Encode(x.Version));

    private async Task<AcademicYear> LoadYear(Guid id, string? version, CancellationToken ct)
    {
        var year = await db.Years.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw YearNotFound();
        if (!year.Version.AsSpan().SequenceEqual(Versions.Decode(version))) throw OperationException.Changed();
        return year;
    }

    private void TouchYear(AcademicYear year) => db.Entry(year).Property(x => x.Status).IsModified = true;

    private async Task RejectOverlapAsync(Guid? id, DateOnly starts, DateOnly ends, CancellationToken ct)
    {
        if (await db.Years.AnyAsync(x => x.Id != id && x.StartsOn <= ends && starts <= x.EndsOn, ct))
            throw new OperationException(409, "year-overlap", "Las fechas se superponen con otro ciclo.");
    }

    private static (string, DateOnly, DateOnly) YearValues(YearRequest request)
    {
        var label = Clean(request.Label);
        if (label.Length is < 1 or > 40) throw OperationException.Invalid("Escribe el nombre del ciclo (máximo 40 caracteres).");
        if (request.EndsOn <= request.StartsOn) throw OperationException.Invalid("La fecha final debe ser posterior a la inicial.");
        if (request.EndsOn.DayNumber - request.StartsOn.DayNumber > 550) throw OperationException.Invalid("Un ciclo no puede durar más de 18 meses.");
        if (request.StartsOn.Year < 2000 || request.EndsOn.Year > 2100) throw OperationException.Invalid("Fechas fuera de rango.");
        return (label, request.StartsOn, request.EndsOn);
    }

    private static void CheckPeriod(AcademicYear year, Guid? id, DateOnly starts, DateOnly ends)
    {
        if (ends < starts) throw OperationException.Invalid("La fecha final del período debe ser igual o posterior a la inicial.");
        if (starts < year.StartsOn || ends > year.EndsOn) throw OperationException.Invalid("El período debe estar dentro de las fechas del ciclo.");
        if (year.Periods.Any(x => x.Id != id && x.StartsOn <= ends && starts <= x.EndsOn))
            throw new OperationException(409, "period-overlap", "El período se superpone con otro.");
    }

    private string PeriodName(string? name, int sequence)
    {
        var value = Clean(name);
        if (value.Length > 60) throw OperationException.Invalid("El nombre del período admite como máximo 60 caracteres.");
        // Default naming uses the configured institutional term (e.g. "Bimestre"); no fixed count is assumed.
        return value == "" ? $"{institution.Value.Terms.Period} {sequence}".Trim() : value;
    }

    private static string Clean(string? value) => WhiteSpace().Replace(value?.Trim() ?? "", " ");
    private static OperationException YearNotFound() => OperationException.NotFound("No encontramos ese ciclo.");

    [GeneratedRegex(@"\s+")] private static partial Regex WhiteSpace();
    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9\-]{0,19}$")] private static partial Regex CodePattern();
}

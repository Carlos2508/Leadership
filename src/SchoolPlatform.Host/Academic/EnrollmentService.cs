using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Academic;

/// <summary>
/// Canonical yearly enrollment with dated placements. A grade/section change closes the open placement and opens
/// another one; nothing is relabelled retroactively. Every command names its year explicitly.
/// </summary>
public sealed class EnrollmentService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, IOptions<InstitutionOptions> institution)
{
    private const int PageSize = 25;

    public async Task<PagedResult<EnrollmentRow>> ListAsync(Guid yearId, Guid? gradeId, Guid? sectionId, string? status,
        string? search, int page, CancellationToken ct)
    {
        if (page is < 1 or > 10000 || search?.Length > 100) throw OperationException.Invalid("Consulta inválida.");
        if (!await db.Years.AnyAsync(x => x.Id == yearId, ct)) throw OperationException.NotFound("No encontramos ese ciclo.");
        var rows = from e in db.Enrollments.AsNoTracking()
                   where e.AcademicYearId == yearId
                   join person in db.Persons on e.StudentId equals person.Id
                   join student in db.Students on e.StudentId equals student.PersonId
                   select new
                   {
                       Enrollment = e, Person = person, Code = student.StudentCode,
                       Placement = db.Placements.Where(p => p.EnrollmentId == e.Id)
                           .OrderBy(p => p.EndsOn == null ? 0 : 1).ThenByDescending(p => p.StartsOn).ThenByDescending(p => p.CreatedAt)
                           .FirstOrDefault()
                   };
        rows = status switch
        {
            null or "" or "all" => rows,
            EnrollmentStatus.Active or EnrollmentStatus.Withdrawn or EnrollmentStatus.Cancelled or EnrollmentStatus.Completed =>
                rows.Where(x => x.Enrollment.Status == status),
            _ => throw OperationException.Invalid("Filtro de estado inválido.")
        };
        if (gradeId is not null) rows = rows.Where(x => x.Placement != null && x.Placement.GradeLevelId == gradeId);
        if (sectionId is not null) rows = rows.Where(x => x.Placement != null && x.Placement.SectionId == sectionId);
        foreach (var term in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5))
            rows = rows.Where(x => x.Person.GivenNames.Contains(term) || x.Person.FamilyNames.Contains(term) || x.Code.Contains(term));
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Person.FamilyNames).ThenBy(x => x.Person.GivenNames).ThenBy(x => x.Enrollment.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(x => new
            {
                x.Enrollment.Id, x.Enrollment.StudentId, x.Person.GivenNames, x.Person.FamilyNames, x.Person.PreferredName, x.Code,
                x.Enrollment.Status, x.Enrollment.EnrolledOn, x.Enrollment.WithdrawnOn, x.Enrollment.Version,
                GradeId = (Guid?)x.Placement!.GradeLevelId, x.Placement.SectionId,
                Grade = db.Grades.Where(g => g.Id == x.Placement.GradeLevelId).Select(g => g.Name).FirstOrDefault(),
                Section = db.Sections.Where(s => s.Id == x.Placement.SectionId).Select(s => s.Name).FirstOrDefault()
            }).ToArrayAsync(ct);
        return new(items.Select(x => new EnrollmentRow(x.Id, x.StudentId, PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName),
            x.Code, x.Status, x.EnrolledOn, x.WithdrawnOn, x.GradeId, x.Grade, x.SectionId, x.Section, Versions.Encode(x.Version))).ToArray(),
            total, page, PageSize);
    }

    public async Task<EnrollmentHistory[]> StudentHistoryAsync(Guid studentId, CancellationToken ct)
    {
        var enrollments = await db.Enrollments.AsNoTracking().Include(x => x.Placements).Where(x => x.StudentId == studentId)
            .Join(db.Years, e => e.AcademicYearId, y => y.Id, (e, y) => new { Enrollment = e, y.Label, YearStatus = y.Status, y.StartsOn })
            .OrderByDescending(x => x.StartsOn).ToArrayAsync(ct);
        var gradeIds = enrollments.SelectMany(x => x.Enrollment.Placements).Select(x => x.GradeLevelId).Distinct().ToArray();
        var sectionIds = enrollments.SelectMany(x => x.Enrollment.Placements).Where(x => x.SectionId != null).Select(x => x.SectionId!.Value).Distinct().ToArray();
        var grades = await db.Grades.AsNoTracking().Where(x => gradeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var sections = await db.Sections.AsNoTracking().Where(x => sectionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return enrollments.Select(x => new EnrollmentHistory(x.Enrollment.Id, x.Enrollment.AcademicYearId, x.Label, x.YearStatus,
            x.Enrollment.Status, x.Enrollment.EnrolledOn, x.Enrollment.WithdrawnOn, Versions.Encode(x.Enrollment.Version),
            x.Enrollment.Placements.OrderBy(p => p.StartsOn).ThenBy(p => p.CreatedAt).Select(p => new PlacementView(p.Id, p.GradeLevelId,
                grades[p.GradeLevelId], p.SectionId, p.SectionId is null ? null : sections[p.SectionId.Value], p.StartsOn, p.EndsOn, p.Reason))
                .ToArray())).ToArray();
    }

    /// <summary>Current placement in the default year (falls back to the latest year with an enrollment).</summary>
    public async Task<CurrentPlacement?> CurrentAsync(Guid studentId, CancellationToken ct)
    {
        var row = await db.Enrollments.AsNoTracking().Where(x => x.StudentId == studentId && x.Status != EnrollmentStatus.Cancelled)
            .Join(db.Years, e => e.AcademicYearId, y => y.Id, (e, y) => new { e, y })
            .OrderByDescending(x => x.y.IsDefault).ThenByDescending(x => x.y.StartsOn)
            .Select(x => new
            {
                x.y.Label, x.e.Status,
                Placement = db.Placements.Where(p => p.EnrollmentId == x.e.Id).OrderByDescending(p => p.StartsOn).ThenByDescending(p => p.CreatedAt)
                    .Select(p => new
                    {
                        Grade = db.Grades.Where(g => g.Id == p.GradeLevelId).Select(g => g.Name).First(),
                        Section = db.Sections.Where(s => s.Id == p.SectionId).Select(s => s.Name).FirstOrDefault()
                    }).FirstOrDefault()
            }).FirstOrDefaultAsync(ct);
        return row?.Placement is null ? null : new(row.Label, row.Placement.Grade, row.Placement.Section, row.Status);
    }

    public async Task<Guid> EnrollAsync(Guid actor, EnrollRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.EnrollmentManage);
        var reason = Optional(request.Reason);
        var year = await OpenYear(request.AcademicYearId, ct);
        if (!await db.Students.AnyAsync(s => s.PersonId == request.StudentId &&
                db.Persons.Any(p => p.Id == s.PersonId && p.Status == PersonStatus.Active), ct))
            throw OperationException.Invalid("Selecciona un estudiante activo.");
        if (await db.Enrollments.AnyAsync(x => x.StudentId == request.StudentId && x.AcademicYearId == year.Id, ct))
            throw new OperationException(409, "already-enrolled", "El estudiante ya tiene inscripción en este ciclo. Gestiona su ubicación desde ella.");
        var enrolledOn = request.EnrolledOn ?? Clamp(Today(), year);
        InYear(enrolledOn, year);
        await CheckPlacementAsync(year.Id, request.GradeLevelId, request.SectionId, null, ct);
        var now = time.GetUtcNow();
        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(), StudentId = request.StudentId, AcademicYearId = year.Id, Status = EnrollmentStatus.Active,
            EnrolledOn = enrolledOn, CreatedAt = now, CreatedBy = actor, UpdatedAt = now
        };
        enrollment.Placements.Add(new EnrollmentPlacement
        {
            Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, GradeLevelId = request.GradeLevelId, SectionId = request.SectionId,
            StartsOn = enrolledOn, Reason = reason == "" ? "Inscripción" : reason, CreatedAt = now, CreatedBy = actor
        });
        db.Enrollments.Add(enrollment);
        audit.Record("StudentEnrolled", actor, "Enrollment", enrollment.Id, reason, $"{year.Label}");
        await db.SaveChangesAsync(ct);
        return enrollment.Id;
    }

    public async Task ChangePlacementAsync(Guid actor, Guid id, PlacementRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.EnrollmentManage);
        var reason = Reasons.Require(request.Reason);
        var (enrollment, year, current) = await LoadActive(id, request.Version, ct);
        InYear(request.EffectiveOn, year);
        if (request.EffectiveOn < current.StartsOn)
            throw OperationException.Invalid("La fecha efectiva no puede ser anterior al inicio de la ubicación actual.");
        if (current.GradeLevelId == request.GradeLevelId && current.SectionId == request.SectionId)
            throw OperationException.Invalid("La nueva ubicación es igual a la actual.");
        await CheckPlacementAsync(year.Id, request.GradeLevelId, request.SectionId, enrollment.Id, ct);
        await ReplacePlacementAsync(actor, enrollment, current, request.EffectiveOn, request.GradeLevelId, request.SectionId, reason, ct, () =>
            audit.Record("EnrollmentPlacementChanged", actor, "Enrollment", enrollment.Id, reason,
                current.GradeLevelId == request.GradeLevelId ? "Section" : "Grade"));
    }

    public async Task WithdrawAsync(Guid actor, Guid id, WithdrawRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.EnrollmentManage);
        var reason = Reasons.Require(request.Reason);
        var (enrollment, year, current) = await LoadActive(id, request.Version, ct);
        InYear(request.EffectiveOn, year);
        if (request.EffectiveOn < current.StartsOn)
            throw OperationException.Invalid("La fecha de retiro no puede ser anterior al inicio de la ubicación actual.");
        current.EndsOn = request.EffectiveOn;
        enrollment.Status = EnrollmentStatus.Withdrawn;
        enrollment.WithdrawnOn = request.EffectiveOn;
        enrollment.UpdatedAt = time.GetUtcNow();
        audit.Record("StudentWithdrawn", actor, "Enrollment", enrollment.Id, reason, request.EffectiveOn.ToString("yyyy-MM-dd"));
        await db.SaveChangesAsync(ct);
    }

    public async Task ReactivateAsync(Guid actor, Guid id, PlacementRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.EnrollmentManage);
        var reason = Reasons.Require(request.Reason);
        var enrollment = await db.Enrollments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw EnrollmentNotFound();
        CheckVersion(enrollment, request.Version);
        if (enrollment.Status != EnrollmentStatus.Withdrawn)
            throw new OperationException(409, "not-withdrawn", "Solo puede reingresar una inscripción retirada.");
        var year = await OpenYear(enrollment.AcademicYearId, ct);
        InYear(request.EffectiveOn, year);
        if (request.EffectiveOn < enrollment.WithdrawnOn)
            throw OperationException.Invalid("El reingreso no puede ser anterior a la fecha de retiro.");
        await CheckPlacementAsync(year.Id, request.GradeLevelId, request.SectionId, enrollment.Id, ct);
        enrollment.Status = EnrollmentStatus.Active;
        enrollment.WithdrawnOn = null;
        enrollment.UpdatedAt = time.GetUtcNow();
        db.Placements.Add(new EnrollmentPlacement
        {
            Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, GradeLevelId = request.GradeLevelId, SectionId = request.SectionId,
            StartsOn = request.EffectiveOn, Reason = reason, CreatedAt = time.GetUtcNow(), CreatedBy = actor
        });
        audit.Record("StudentReenrolled", actor, "Enrollment", enrollment.Id, reason, request.EffectiveOn.ToString("yyyy-MM-dd"));
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers

    private async Task ReplacePlacementAsync(Guid actor, Enrollment enrollment, EnrollmentPlacement current, DateOnly effectiveOn,
        Guid gradeId, Guid? sectionId, string reason, CancellationToken ct, Action recordAudit)
    {
        // Two statements in one transaction: close the open placement before inserting the next one
        // (the filtered unique index allows a single open placement).
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        current.EndsOn = effectiveOn;
        enrollment.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        db.Placements.Add(new EnrollmentPlacement
        {
            Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, GradeLevelId = gradeId, SectionId = sectionId,
            StartsOn = effectiveOn, Reason = reason, CreatedAt = time.GetUtcNow(), CreatedBy = actor
        });
        recordAudit();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<(Enrollment, AcademicYear, EnrollmentPlacement)> LoadActive(Guid id, string version, CancellationToken ct)
    {
        var enrollment = await db.Enrollments.Include(x => x.Placements.Where(p => p.EndsOn == null))
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw EnrollmentNotFound();
        CheckVersion(enrollment, version);
        if (enrollment.Status != EnrollmentStatus.Active)
            throw new OperationException(409, "enrollment-inactive", "La inscripción no está activa.");
        var year = await OpenYear(enrollment.AcademicYearId, ct);
        var current = enrollment.Placements.SingleOrDefault()
            ?? throw new OperationException(409, "no-placement", "La inscripción no tiene una ubicación vigente.");
        return (enrollment, year, current);
    }

    private static void CheckVersion(Enrollment enrollment, string version)
    {
        if (!enrollment.Version.AsSpan().SequenceEqual(Versions.Decode(version))) throw OperationException.Changed();
    }

    private async Task<AcademicYear> OpenYear(Guid id, CancellationToken ct)
    {
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");
        if (!YearStatus.AcceptsEnrollment(year.Status))
            throw new OperationException(409, "year-not-open", "El ciclo no tiene inscripciones abiertas.");
        return year;
    }

    private async Task CheckPlacementAsync(Guid yearId, Guid gradeId, Guid? sectionId, Guid? enrollmentId, CancellationToken ct)
    {
        if (!await db.Grades.AnyAsync(x => x.Id == gradeId && x.IsActive, ct)) throw OperationException.Invalid("Selecciona un grado activo.");
        if (sectionId is null) return;
        var section = await db.Sections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sectionId, ct);
        if (section is null || section.AcademicYearId != yearId || section.GradeLevelId != gradeId)
            throw OperationException.Invalid("La sección no pertenece a ese grado y ciclo.");
        if (section.Capacity is { } capacity && await db.Placements.CountAsync(p => p.SectionId == sectionId && p.EndsOn == null &&
                p.EnrollmentId != enrollmentId && db.Enrollments.Any(e => e.Id == p.EnrollmentId && e.Status == EnrollmentStatus.Active), ct) >= capacity)
            throw new OperationException(409, "section-full", "La sección alcanzó su cupo. Elige otra o ajusta el cupo.");
    }

    private static void InYear(DateOnly date, AcademicYear year)
    {
        if (date < year.StartsOn || date > year.EndsOn) throw OperationException.Invalid("La fecha debe estar dentro del ciclo seleccionado.");
    }

    private static DateOnly Clamp(DateOnly date, AcademicYear year) =>
        date < year.StartsOn ? year.StartsOn : date > year.EndsOn ? year.EndsOn : date;

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById(institution.Value.Locale.TimeZone)).DateTime);

    private static string Optional(string? value)
    {
        var reason = value?.Trim() ?? "";
        if (reason.Length > 500) throw OperationException.Invalid("El motivo admite como máximo 500 caracteres.");
        return reason;
    }

    private static OperationException EnrollmentNotFound() => OperationException.NotFound("No encontramos esa inscripción.");
}

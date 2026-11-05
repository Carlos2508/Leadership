using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Finance;

/// <summary>Obligations: bulk generation with server preview, single charges, adjustments, voids and statements.</summary>
public sealed class ChargeService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, IOptions<InstitutionOptions> institution)
{
    private string Currency => institution.Value.Locale.Currency;

    internal static async Task<IDbContextTransaction> LockAsync(SchoolDbContext db, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Low-volume money operations of one school: a short exclusive lock keeps balance checks and writes consistent.
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource=N'SchoolPlatform.Finance', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @result < 0 THROW 51000, 'Finance operation busy', 1;
                """, ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    // ---- Bulk generation

    public async Task<GenerationPreview> PreviewAsync(Guid actor, GenerationRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.FinanceCharges);
        var (rows, _) = await PlanAsync(request, ct);
        return Preview(request, rows);
    }

    public async Task<GenerationResult> GenerateAsync(Guid actor, GenerationRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.FinanceCharges);
        await using var transaction = await LockAsync(db, ct);
        var (rows, context) = await PlanAsync(request, ct);
        var preview = Preview(request, rows);
        // The confirmation refers to an exact preview; any change in recipients or existing charges invalidates it.
        if (request.PreviewHash != preview.PreviewHash)
            throw new OperationException(409, "preview-changed", "La lista cambió desde la vista previa. Revisa la vista previa nuevamente antes de confirmar.");
        var batch = Guid.NewGuid();
        var now = time.GetUtcNow();
        foreach (var row in rows.Where(x => x.Outcome == "Create"))
            db.Charges.Add(NewCharge(row.EnrollmentId, row.StudentId, context, request.Occurrence, request.Amount, now, actor, batch));
        audit.Record("ChargesGenerated", actor, "AcademicYear", request.AcademicYearId,
            detail: $"{context.Concept.Code}:{context.Period?.Sequence}:{preview.ToCreate}");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(batch, preview.ToCreate, preview.Existing, preview.Excluded);
    }

    public async Task<Guid> CreateSingleAsync(Guid actor, SingleChargeRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.FinanceCharges);
        var enrollment = await db.Enrollments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.EnrollmentId, ct)
            ?? throw OperationException.NotFound("No encontramos esa inscripción.");
        if (enrollment.Status == EnrollmentStatus.Cancelled) throw OperationException.Invalid("La inscripción está anulada.");
        var context = await ContextAsync(enrollment.AcademicYearId, request.ConceptId, request.BillingPeriodId, request.Amount, request.DueOn,
            request.Description, request.Occurrence, ct);
        await using var transaction = await LockAsync(db, ct);
        var charge = NewCharge(enrollment.Id, enrollment.StudentId, context, request.Occurrence, request.Amount, time.GetUtcNow(), actor, null);
        if (await db.Charges.AnyAsync(x => x.ObligationKey == charge.ObligationKey && x.VoidedAt == null, ct))
            throw new OperationException(409, "charge-exists", "Esa obligación ya fue emitida para el estudiante.");
        db.Charges.Add(charge);
        audit.Record("ChargeCreated", actor, "Charge", charge.Id, detail: context.Concept.Code);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return charge.Id;
    }

    public async Task AdjustAsync(Guid actor, Guid chargeId, AdjustmentRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.FinanceCharges);
        var reason = Reasons.Require(request.Reason);
        if (!AdjustmentKinds.All.Contains(request.Kind)) throw OperationException.Invalid("Tipo de ajuste inválido.");
        Money(request.Amount);
        await using var transaction = await LockAsync(db, ct);
        var charge = await LoadOpen(chargeId, request.Version, ct);
        var balance = (await Balances(db, [chargeId], ct))[chargeId];
        if (request.Amount > balance)
            throw new OperationException(409, "adjustment-exceeds-balance", "El ajuste supera el saldo pendiente del cargo.");
        db.ChargeAdjustments.Add(new ChargeAdjustment
        {
            Id = Guid.NewGuid(), ChargeId = chargeId, Kind = request.Kind, Amount = request.Amount, Reason = reason,
            CreatedAt = time.GetUtcNow(), CreatedBy = actor
        });
        Touch(charge);
        audit.Record("ChargeAdjusted", actor, "Charge", chargeId, reason, request.Kind);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task VoidAsync(Guid actor, Guid chargeId, VoidRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.FinanceCharges);
        var reason = Reasons.Require(request.Reason);
        await using var transaction = await LockAsync(db, ct);
        var charge = await LoadOpen(chargeId, request.Version, ct);
        if (await db.PaymentAllocations.AnyAsync(a => a.ChargeId == chargeId &&
                db.Payments.Any(p => p.Id == a.PaymentId && p.ReversedAt == null), ct))
            throw new OperationException(409, "charge-paid", "El cargo tiene pagos aplicados. Revierte primero esos pagos.");
        charge.VoidedAt = time.GetUtcNow();
        charge.VoidedBy = actor;
        charge.VoidReason = reason;
        audit.Record("ChargeVoided", actor, "Charge", chargeId, reason);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    // ---- Statement

    public async Task<Statement> StatementAsync(Guid studentId, Guid? yearId, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking().Where(x => x.PersonId == studentId)
            .Join(db.Persons, s => s.PersonId, p => p.Id, (s, p) => new { s.StudentCode, p.GivenNames, p.FamilyNames, p.PreferredName })
            .SingleOrDefaultAsync(ct) ?? throw OperationException.NotFound("No encontramos ese estudiante.");
        var charges = await db.Charges.AsNoTracking().Where(x => x.StudentId == studentId && (yearId == null || x.AcademicYearId == yearId))
            .Select(x => new
            {
                Charge = x, Concept = db.FeeConcepts.First(c => c.Id == x.ConceptId),
                Period = db.BillingPeriods.FirstOrDefault(p => p.Id == x.BillingPeriodId),
                Year = db.Years.Where(y => y.Id == x.AcademicYearId).Select(y => y.Label).First()
            }).ToArrayAsync(ct);
        var ids = charges.Select(x => x.Charge.Id).ToArray();
        var adjustments = await db.ChargeAdjustments.AsNoTracking().Where(x => ids.Contains(x.ChargeId))
            .GroupBy(x => x.ChargeId).Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        var paid = await PaidAsync(db, ids, ct);
        var lines = charges.Select(x =>
        {
            var adjusted = adjustments.GetValueOrDefault(x.Charge.Id);
            var paidAmount = paid.GetValueOrDefault(x.Charge.Id);
            var balance = x.Charge.VoidedAt is null ? x.Charge.Amount - adjusted - paidAmount : 0;
            var status = x.Charge.VoidedAt is not null ? "Voided" : balance <= 0 ? (paidAmount == 0 ? "Waived" : "Paid")
                : paidAmount > 0 || adjusted > 0 ? "Partial" : "Pending";
            return new ChargeLine(x.Charge.Id, x.Charge.AcademicYearId, x.Year, x.Concept.Name, x.Concept.CountsForClearance, x.Period?.Sequence,
                x.Period?.Label, x.Charge.Description, x.Charge.Amount, adjusted, paidAmount, balance, x.Charge.DueOn, status,
                Versions.Encode(x.Charge.Version));
        }).OrderBy(x => x.DueOn).ThenBy(x => x.PeriodSequence).ThenBy(x => x.Concept).ToArray();
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.Allocations.Any(a => ids.Contains(a.ChargeId)))
            .OrderByDescending(p => p.ReceivedOn).ThenByDescending(p => p.ReceiptNumber)
            .Select(p => new PaymentSummary(p.Id, p.ReceiptNumber, p.ReceivedOn, p.Amount, p.Method, p.PayerName, p.ReversedAt != null,
                Convert.ToBase64String(p.Version))).ToArrayAsync(ct);
        return new(studentId, PeopleService.Display(student.GivenNames, student.FamilyNames, student.PreferredName), student.StudentCode,
            Currency, lines, payments, lines.Sum(x => x.Balance));
    }

    // ---- Shared balance helpers (confirmed payments only; reversed payments no longer cover anything)

    internal static async Task<Dictionary<Guid, decimal>> PaidAsync(SchoolDbContext db, Guid[] chargeIds, CancellationToken ct) =>
        await db.PaymentAllocations.AsNoTracking()
            .Where(a => chargeIds.Contains(a.ChargeId) && db.Payments.Any(p => p.Id == a.PaymentId && p.ReversedAt == null))
            .GroupBy(a => a.ChargeId).Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

    internal static async Task<Dictionary<Guid, decimal>> Balances(SchoolDbContext db, Guid[] chargeIds, CancellationToken ct)
    {
        var charges = await db.Charges.AsNoTracking().Where(x => chargeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Amount, x.VoidedAt, Adjusted = x.Adjustments.Sum(a => (decimal?)a.Amount) ?? 0 }).ToArrayAsync(ct);
        var paid = await PaidAsync(db, chargeIds, ct);
        return charges.ToDictionary(x => x.Id, x => x.VoidedAt is null ? x.Amount - x.Adjusted - paid.GetValueOrDefault(x.Id) : 0m);
    }

    // ---- helpers

    private sealed record GenerationContext(FeeConcept Concept, BillingPeriod? Period, DateOnly DueOn, string Description, Guid YearId);

    private async Task<GenerationContext> ContextAsync(Guid yearId, Guid conceptId, Guid? periodId, decimal amount, DateOnly? dueOn,
        string? description, int occurrence, CancellationToken ct)
    {
        Money(amount);
        if (occurrence is < 1 or > 12) throw OperationException.Invalid("La ocurrencia debe estar entre 1 y 12.");
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == yearId, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");
        if (year.Status == YearStatus.Closed) throw new OperationException(409, "year-closed", "El ciclo está cerrado; no se emiten cargos nuevos.");
        var concept = await db.FeeConcepts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == conceptId && x.IsActive, ct)
            ?? throw OperationException.Invalid("Selecciona un concepto activo.");
        BillingPeriod? period = null;
        if (concept.IsRecurring)
        {
            period = await db.BillingPeriods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == periodId && x.AcademicYearId == yearId, ct)
                ?? throw OperationException.Invalid("Un concepto recurrente requiere una cuota del calendario de ese ciclo.");
        }
        else if (periodId is not null) throw OperationException.Invalid("Un concepto único no usa cuota del calendario.");
        var due = dueOn ?? period?.DueOn ?? throw OperationException.Invalid("Indica la fecha de vencimiento.");
        var text = (description ?? "").Trim();
        if (text.Length > 160) throw OperationException.Invalid("La descripción admite como máximo 160 caracteres.");
        if (text == "") text = period is null ? concept.Name : $"{concept.Name} · {period.Label}";
        return new(concept, period, due, text, yearId);
    }

    private Charge NewCharge(Guid enrollmentId, Guid studentId, GenerationContext context, int occurrence, decimal amount,
        DateTimeOffset now, Guid actor, Guid? batch) => new()
    {
        Id = Guid.NewGuid(), EnrollmentId = enrollmentId, StudentId = studentId, AcademicYearId = context.YearId,
        ConceptId = context.Concept.Id, BillingPeriodId = context.Period?.Id, Occurrence = occurrence,
        ObligationKey = ObligationKey(enrollmentId, context.Concept.Id, context.Period?.Id, occurrence),
        Description = context.Description, Amount = amount, Currency = Currency, IssuedOn = Today(), DueOn = context.DueOn,
        BatchId = batch, CreatedAt = now, CreatedBy = actor
    };

    internal static string ObligationKey(Guid enrollmentId, Guid conceptId, Guid? periodId, int occurrence) =>
        $"{enrollmentId:N}:{conceptId:N}:{(periodId is null ? "once" : periodId.Value.ToString("N"))}:{occurrence}";

    private async Task<(GenerationRow[] Rows, GenerationContext Context)> PlanAsync(GenerationRequest request, CancellationToken ct)
    {
        var context = await ContextAsync(request.AcademicYearId, request.ConceptId, request.BillingPeriodId, request.Amount, request.DueOn,
            request.Description, request.Occurrence, ct);
        var grades = request.GradeIds ?? [];
        var sections = request.SectionIds ?? [];
        var excluded = (request.ExcludedEnrollmentIds ?? []).ToHashSet();
        if (grades.Length > 100 || sections.Length > 200 || excluded.Count > 2000) throw OperationException.Invalid("Selección demasiado grande.");
        // Recipients: active enrollments of the year by current placement.
        var candidates = await (from e in db.Enrollments.AsNoTracking()
                                where e.AcademicYearId == request.AcademicYearId && e.Status == EnrollmentStatus.Active
                                join p in db.Placements on e.Id equals p.EnrollmentId
                                where p.EndsOn == null
                                join person in db.Persons on e.StudentId equals person.Id
                                join student in db.Students on e.StudentId equals student.PersonId
                                select new
                                {
                                    e.Id, e.StudentId, person.GivenNames, person.FamilyNames, person.PreferredName, student.StudentCode,
                                    p.GradeLevelId, p.SectionId,
                                    Grade = db.Grades.Where(g => g.Id == p.GradeLevelId).Select(g => g.Name).First(),
                                    Section = db.Sections.Where(s => s.Id == p.SectionId).Select(s => s.Name).FirstOrDefault()
                                }).ToArrayAsync(ct);
        var selected = candidates.Where(x => (grades.Length == 0 || grades.Contains(x.GradeLevelId)) &&
            (sections.Length == 0 || (x.SectionId is { } s && sections.Contains(s)))).ToArray();
        var keys = selected.ToDictionary(x => x.Id, x => ObligationKey(x.Id, context.Concept.Id, context.Period?.Id, request.Occurrence));
        var keyValues = keys.Values.ToArray();
        var existing = (await db.Charges.AsNoTracking().Where(x => x.VoidedAt == null && keyValues.Contains(x.ObligationKey))
            .Select(x => x.ObligationKey).ToArrayAsync(ct)).ToHashSet();
        var rows = selected.OrderBy(x => x.FamilyNames).ThenBy(x => x.GivenNames).ThenBy(x => x.Id).Select(x => new GenerationRow(x.Id, x.StudentId,
            PeopleService.Display(x.GivenNames, x.FamilyNames, x.PreferredName), x.StudentCode, x.Grade, x.Section,
            excluded.Contains(x.Id) ? "Excluded" : existing.Contains(keys[x.Id]) ? "Exists" : "Create")).ToArray();
        return (rows, context);
    }

    private GenerationPreview Preview(GenerationRequest request, GenerationRow[] rows)
    {
        var toCreate = rows.Count(x => x.Outcome == "Create");
        var material = string.Join('|', [request.AcademicYearId, request.ConceptId, request.BillingPeriodId, request.Occurrence,
            request.Amount.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture), request.DueOn, request.Description,
            .. rows.Select(x => $"{x.EnrollmentId:N}={x.Outcome}")]);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return new(hash, rows, toCreate, rows.Count(x => x.Outcome == "Exists"), rows.Count(x => x.Outcome == "Excluded"),
            toCreate * request.Amount, Currency);
    }

    private async Task<Charge> LoadOpen(Guid id, string version, CancellationToken ct)
    {
        var charge = await db.Charges.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos ese cargo.");
        if (!charge.Version.AsSpan().SequenceEqual(Versions.Decode(version))) throw OperationException.Changed();
        if (charge.VoidedAt is not null) throw new OperationException(409, "charge-voided", "El cargo está anulado.");
        return charge;
    }

    private void Touch(Charge charge) => db.Entry(charge).Property(x => x.Description).IsModified = true;

    internal static void Money(decimal amount)
    {
        if (amount <= 0 || amount > 1_000_000 || decimal.Round(amount, 2) != amount)
            throw OperationException.Invalid("El importe debe ser mayor que cero, con máximo dos decimales.");
    }

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById(institution.Value.Locale.TimeZone)).DateTime);
}

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Finance;

/// <summary>Fee concepts, the billing calendar of each year and the clearance cutoffs chosen by the school.</summary>
public sealed partial class FinanceSetupService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, IOptions<InstitutionOptions> institution)
{
    public async Task<FinanceSetup> SetupAsync(Guid yearId, CancellationToken ct)
    {
        if (!await db.Years.AnyAsync(x => x.Id == yearId, ct)) throw OperationException.NotFound("No encontramos ese ciclo.");
        var concepts = await db.FeeConcepts.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(ct);
        var periods = await db.BillingPeriods.AsNoTracking().Where(x => x.AcademicYearId == yearId).OrderBy(x => x.Sequence)
            .Select(x => new BillingPeriodView(x.Id, x.Sequence, x.Label, x.DueOn,
                db.Charges.Count(c => c.BillingPeriodId == x.Id && c.VoidedAt == null))).ToArrayAsync(ct);
        var cutoffs = await db.ClearanceCutoffs.AsNoTracking().Where(x => x.AcademicYearId == yearId).OrderBy(x => x.ThroughSequence)
            .ThenBy(x => x.Name).Select(x => new CutoffView(x.Id, x.Name, x.ThroughSequence, x.CreatedAt)).ToArrayAsync(ct);
        return new(concepts.Select(Concept).ToArray(), periods, cutoffs, institution.Value.Locale.Currency);
    }

    public async Task<ConceptView[]> ConceptsAsync(CancellationToken ct) =>
        (await db.FeeConcepts.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(ct)).Select(Concept).ToArray();

    public async Task<Guid> SaveConceptAsync(Guid actor, Guid? id, ConceptRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.FinanceCharges);
        var code = (request.Code ?? "").Trim().ToUpperInvariant();
        var name = Clean(request.Name);
        if (!CodePattern().IsMatch(code)) throw OperationException.Invalid("El código admite de 1 a 20 letras, números o guion.");
        if (name.Length is < 1 or > 80) throw OperationException.Invalid("Escribe el nombre del concepto (máximo 80 caracteres).");
        FeeConcept concept;
        if (id is null)
        {
            concept = new FeeConcept { Id = Guid.NewGuid() };
            db.FeeConcepts.Add(concept);
        }
        else
        {
            concept = await db.FeeConcepts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos ese concepto.");
            if (!concept.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
            if (concept.IsRecurring != request.IsRecurring && await db.Charges.AnyAsync(x => x.ConceptId == concept.Id, ct))
                throw new OperationException(409, "concept-in-use", "El concepto ya tiene cargos; no puede cambiar entre recurrente y único.");
        }
        if (await db.FeeConcepts.AnyAsync(x => x.Id != concept.Id && (x.Code == code || x.Name == name), ct))
            throw new OperationException(409, "concept-exists", "Ya existe un concepto con ese código o nombre.");
        concept.Code = code;
        concept.Name = name;
        concept.IsRecurring = request.IsRecurring;
        concept.CountsForClearance = request.CountsForClearance;
        concept.IsActive = request.IsActive;
        audit.Record(id is null ? "FeeConceptCreated" : "FeeConceptUpdated", actor, "FeeConcept", concept.Id,
            detail: $"{code}:{(request.CountsForClearance ? "clearance" : "no-clearance")}");
        await db.SaveChangesAsync(ct);
        return concept.Id;
    }

    public async Task<Guid> AddPeriodAsync(Guid actor, Guid yearId, BillingPeriodRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.FinanceCharges);
        var label = Label(request.Label);
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(x => x.Id == yearId, ct) ?? throw OperationException.NotFound("No encontramos ese ciclo.");
        if (year.Status == YearStatus.Closed) throw new OperationException(409, "year-closed", "El ciclo está cerrado.");
        var periods = await db.BillingPeriods.Where(x => x.AcademicYearId == yearId).ToArrayAsync(ct);
        if (periods.Length >= 24) throw OperationException.Invalid("Un ciclo admite como máximo 24 períodos de cobro.");
        if (periods.Any(x => x.DueOn >= request.DueOn)) throw OperationException.Invalid("Agrega las cuotas en orden: el vencimiento debe ser posterior al de la última.");
        if (periods.Any(x => x.Label == label)) throw new OperationException(409, "period-exists", "Ya existe una cuota con ese nombre.");
        var period = new BillingPeriod
        {
            Id = Guid.NewGuid(), AcademicYearId = yearId, Sequence = periods.Length == 0 ? 1 : periods.Max(x => x.Sequence) + 1,
            Label = label, DueOn = request.DueOn
        };
        db.BillingPeriods.Add(period);
        audit.Record("BillingPeriodAdded", actor, "AcademicYear", yearId, detail: $"{period.Sequence}");
        await db.SaveChangesAsync(ct);
        return period.Id;
    }

    public async Task UpdatePeriodAsync(Guid actor, Guid id, BillingPeriodRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.FinanceCharges);
        var label = Label(request.Label);
        var period = await db.BillingPeriods.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos esa cuota.");
        var siblings = await db.BillingPeriods.AsNoTracking().Where(x => x.AcademicYearId == period.AcademicYearId && x.Id != id).ToArrayAsync(ct);
        if (siblings.Any(x => x.Label == label)) throw new OperationException(409, "period-exists", "Ya existe una cuota con ese nombre.");
        if (siblings.Any(x => (x.Sequence < period.Sequence && x.DueOn >= request.DueOn) || (x.Sequence > period.Sequence && x.DueOn <= request.DueOn)))
            throw OperationException.Invalid("El vencimiento debe respetar el orden de las cuotas.");
        // Changing the calendar does not rewrite issued charges: their due date stays as issued.
        period.Label = label;
        period.DueOn = request.DueOn;
        audit.Record("BillingPeriodUpdated", actor, "AcademicYear", period.AcademicYearId, detail: $"{period.Sequence}");
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> AddCutoffAsync(Guid actor, Guid yearId, CutoffRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.FinanceCharges);
        var name = Clean(request.Name);
        if (name.Length is < 1 or > 80) throw OperationException.Invalid("Escribe el nombre del corte (máximo 80 caracteres).");
        if (!await db.Years.AnyAsync(x => x.Id == yearId, ct)) throw OperationException.NotFound("No encontramos ese ciclo.");
        if (!await db.BillingPeriods.AnyAsync(x => x.AcademicYearId == yearId && x.Sequence == request.ThroughSequence, ct))
            throw OperationException.Invalid("Selecciona una cuota existente del calendario como límite del corte.");
        if (await db.ClearanceCutoffs.AnyAsync(x => x.AcademicYearId == yearId && x.Name == name, ct))
            throw new OperationException(409, "cutoff-exists", "Ya existe un corte con ese nombre en el ciclo.");
        var cutoff = new ClearanceCutoff
        {
            Id = Guid.NewGuid(), AcademicYearId = yearId, Name = name, ThroughSequence = request.ThroughSequence,
            CreatedAt = time.GetUtcNow(), CreatedBy = actor
        };
        db.ClearanceCutoffs.Add(cutoff);
        audit.Record("ClearanceCutoffCreated", actor, "AcademicYear", yearId, detail: $"{request.ThroughSequence}");
        await db.SaveChangesAsync(ct);
        return cutoff.Id;
    }

    private static ConceptView Concept(FeeConcept x) =>
        new(x.Id, x.Code, x.Name, x.IsRecurring, x.CountsForClearance, x.IsActive, Versions.Encode(x.Version));

    private static string Label(string? value)
    {
        var label = Clean(value);
        if (label.Length is < 1 or > 60) throw OperationException.Invalid("Escribe el nombre de la cuota (máximo 60 caracteres).");
        return label;
    }

    private static string Clean(string? value) => WhiteSpace().Replace(value?.Trim() ?? "", " ");

    [GeneratedRegex(@"\s+")] private static partial Regex WhiteSpace();
    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9\-]{0,19}$")] private static partial Regex CodePattern();
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.LegacyImport;

public sealed class MigrationAssistantService(SchoolDbContext db, IdentityDbContext identity, InitialRosterImporter importer,
    IAssistantSource source, AssistantStorage storage, IAssistantBackup backup, AccessRoleService roles,
    AcademicService academic, IOptions<InstitutionOptions> institution)
{
    // Concurrent tabs cannot overwrite a reviewed package while apply reads it.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    public async Task<AssistantCatalog> CatalogAsync(Guid actor, CancellationToken ct)
    {
        await importer.ActiveOperatorAsync(actor, ct);
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
        if (pending.Any(m => m != "20261106202522_InitialRosterImport"))
            throw new OperationException(409, "installation-update-required", "Actualiza esta instalación antes de abrir el asistente.");
        var emptyRole = await EmptyRoleAsync(ct);
        return new(db.Database.GetDbConnection().DataSource, db.Database.GetDbConnection().Database,
            await db.Years.AsNoTracking().OrderByDescending(y => y.StartsOn).Select(y => new AssistantYear(y.Id, y.Label, y.StartsOn, y.EndsOn, y.Status)).ToArrayAsync(ct),
            await db.Grades.AsNoTracking().Where(g => g.IsActive).OrderBy(g => g.Sequence).Select(g => new AssistantGrade(g.Id, g.Name, g.Sequence, g.Stage)).ToArrayAsync(ct),
            await db.Sections.AsNoTracking().Select(s => new AssistantSection(s.Id, s.AcademicYearId, s.GradeLevelId, s.Name)).ToArrayAsync(ct),
            pending.Length > 0 || emptyRole is null);
    }

    public async Task<AssistantCatalog> PrepareAsync(Guid actor, CancellationToken ct)
    {
        var catalog = await CatalogAsync(actor, ct);
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any()) await db.Database.MigrateAsync(ct);
        if (await EmptyRoleAsync(ct) is null)
            await roles.SaveAsync(actor, null, new("Acceso personal " + Guid.NewGuid().ToString("N")[..6],
                "Acceso propio sin permisos administrativos.", [], true, "Preparación solicitada desde el asistente local de migración."), ct);
        return await CatalogAsync(actor, ct);
    }

    public async Task<AssistantInspect> InspectAsync(Guid actor, AssistantSourceConnection? connection, CancellationToken ct)
    {
        await importer.ActiveOperatorAsync(actor, ct);
        return await source.InspectAsync(connection, ct);
    }

    public async Task<AssistantReview> ReadAsync(Guid actor, AssistantReadRequest request, CancellationToken ct)
    {
        await importer.ActiveOperatorAsync(actor, ct);
        var year = await db.Years.AsNoTracking().SingleOrDefaultAsync(y => y.Id == request.TargetYearId, ct);
        if (year is null || !YearStatus.AcceptsEnrollment(year.Status)) throw OperationException.Invalid("Elige un ciclo con inscripciones abiertas.");
        var run = Guid.NewGuid();
        storage.Create(actor, run);
        await source.ReadAsync(request.SourceYearId, storage.RawPath(actor, run), request.Connection, ct);
        var bytes = await File.ReadAllBytesAsync(storage.RawPath(actor, run), ct);
        var raw = AssistantProposal.ReadRaw(bytes);
        var review = AssistantProposal.Propose(raw, year.Id, year.Label, year.StartsOn, request.NameFormat) with { RunId = run };
        var grades = await db.Grades.AsNoTracking().Where(g => g.IsActive).ToArrayAsync(ct);
        review = review with { Students = review.Students.Select(s =>
        {
            var matches = grades.Where(g => string.Equals(g.Name.Trim(), s.SourceGradeName.Trim(), StringComparison.InvariantCultureIgnoreCase)).ToArray();
            return s with { GradeId = matches.Length == 1 ? matches[0].Id : null };
        }).ToArray() };
        await storage.SaveAsync(new(actor, review, null, null, null), ct);
        return review;
    }

    public Task<AssistantReview[]> RecentAsync(Guid actor, CancellationToken ct) => storage.RecentAsync(actor, ct);

    public async Task<AssistantReview> SaveDraftAsync(Guid actor, AssistantReview review, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(review.RunId, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await importer.ActiveOperatorAsync(actor, ct);
            await using var diskLock = await storage.LockAsync(actor, review.RunId, ct);
            var state = await storage.LoadAsync(actor, review.RunId, ct);
            if (await db.RosterBatches.AnyAsync(b => b.Id == review.RunId, ct)) throw new OperationException(409, "batch-already-applied", "El padrón ya está importado. Verifica su resultado.");
            await storage.SaveAsync(state with { Review = review with { Confirmed = false }, PlanSha256 = null, BackupPath = null }, ct);
            return review with { Confirmed = false };
        }
        finally { gate.Release(); }
    }

    public async Task<Guid> CreateYearAsync(Guid actor, YearRequest request, CancellationToken ct)
    {
        await importer.ActiveOperatorAsync(actor, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var id = await academic.CreateYearAsync(actor, request, ct);
        var detail = await academic.YearAsync(id, ct);
        await academic.SetYearStatusAsync(actor, id, new(detail.Year.Version, YearStatus.EnrollmentOpen, "Preparación del ciclo para el padrón inicial revisado."), ct);
        await transaction.CommitAsync(ct);
        return id;
    }

    public async Task CreateGradesAsync(Guid actor, Guid run, CancellationToken ct)
    {
        await importer.ActiveOperatorAsync(actor, ct);
        _ = await storage.LoadAsync(actor, run, ct);
        var raw = AssistantProposal.ReadRaw(await File.ReadAllBytesAsync(storage.RawPath(actor, run), ct));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var grade in raw.Grades)
            if (!await db.Grades.AnyAsync(g => g.Name == grade.Name, ct))
                await academic.SaveGradeAsync(actor, null, new("MIG-" + grade.SourceGradeId, grade.Name, "", grade.Sequence ?? 0), ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<RosterReport> PlanAsync(Guid actor, AssistantReview review, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(review.RunId, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await importer.ActiveOperatorAsync(actor, ct);
            await using var diskLock = await storage.LockAsync(actor, review.RunId, ct);
            var state = await storage.LoadAsync(actor, review.RunId, ct);
            if (await db.RosterBatches.AnyAsync(b => b.Id == review.RunId, ct))
                throw new OperationException(409, "batch-already-applied", "Este padrón ya fue importado. Usa Verificar resultado; no hace falta importarlo otra vez.");
            var role = await EmptyRoleAsync(ct) ?? throw OperationException.Invalid("Prepara primero la instalación desde el asistente.");
            var bytes = await File.ReadAllBytesAsync(storage.RawPath(actor, review.RunId), ct);
            var package = AssistantProposal.Package(AssistantProposal.ReadRaw(bytes), bytes, review, institution.Value.SchoolId, role);
            var content = JsonSerializer.SerializeToUtf8Bytes(package, RosterJson.Options);
            var report = await importer.RunForAccountAsync(content, "plan", null, actor, ct);
            await File.WriteAllBytesAsync(storage.PackagePath(actor, review.RunId), content, ct);
            await storage.SaveAsync(state with { Review = review, PlanSha256 = report.PlanSha256, RoleKey = role, BackupPath = null }, ct);
            return report;
        }
        finally { gate.Release(); }
    }

    public async Task<AssistantResult> ApplyAsync(Guid actor, AssistantApplyRequest request, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(request.RunId, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await importer.ActiveOperatorAsync(actor, ct);
            await using var diskLock = await storage.LockAsync(actor, request.RunId, ct);
            var state = await storage.LoadAsync(actor, request.RunId, ct);
            if (state.PlanSha256 is null || state.PlanSha256 != request.PlanSha256) throw new RosterImportException("plan-changed");
            if (!await db.RosterBatches.AnyAsync(b => b.Id == request.RunId, ct) && state.BackupPath is null)
            {
                // Back up only after the operator accepts the exact plan, and before any import write.
                var path = await backup.CreateVerifiedAsync(ct);
                state = state with { BackupPath = path };
                await storage.SaveAsync(state, ct);
            }
            var result = await importer.RunForAccountAsync(await File.ReadAllBytesAsync(storage.PackagePath(actor, request.RunId), ct), "apply", request.PlanSha256, actor, ct);
            var checkedResult = await importer.RunForAccountAsync(await File.ReadAllBytesAsync(storage.PackagePath(actor, request.RunId), ct), "reconcile", null, actor, ct);
            return new(checkedResult with { AlreadyApplied = result.AlreadyApplied }, state.BackupPath is not null);
        }
        finally { gate.Release(); }
    }

    public async Task<AssistantResult> ReconcileAsync(Guid actor, Guid run, CancellationToken ct)
    {
        await importer.ActiveOperatorAsync(actor, ct);
        await using var diskLock = await storage.LockAsync(actor, run, ct);
        var state = await storage.LoadAsync(actor, run, ct);
        var report = await importer.RunForAccountAsync(await File.ReadAllBytesAsync(storage.PackagePath(actor, run), ct), "reconcile", null, actor, ct);
        return new(report, state.BackupPath is not null);
    }

    private Task<string?> EmptyRoleAsync(CancellationToken ct) => identity.AccessRoles.AsNoTracking().Where(r => r.IsActive &&
        !identity.AccessRolePermissions.Any(p => p.RoleKey == r.Key)).OrderBy(r => r.Key).Select(r => r.Key).FirstOrDefaultAsync(ct);
}

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Finance;

/// <summary>Confirmed payments with explicit allocations, numbered receipts with an immutable snapshot, and reversals.</summary>
public sealed partial class PaymentService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, IOptions<InstitutionOptions> institution,
    IWebHostEnvironment environment)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ReceiptView> RecordAsync(Guid actor, PaymentRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.FinanceCollect);
        var key = request.IdempotencyKey?.Trim() ?? "";
        if (!KeyPattern().IsMatch(key)) throw OperationException.Invalid("Falta la clave de operación. Recarga la página e intenta nuevamente.");
        // A retried submission with the same key returns the original receipt instead of charging twice.
        var repeated = await db.Payments.AsNoTracking().Where(x => x.IdempotencyKey == key).Select(x => x.Id).FirstOrDefaultAsync(ct);
        if (repeated != Guid.Empty) return await ReceiptAsync(repeated, ct);

        var payer = Clean(request.PayerName, 120, "el nombre de quien paga", required: true);
        var reference = Clean(request.Reference, 80, "la referencia");
        var note = Clean(request.Note, 300, "la observación");
        if (!PaymentMethods.All.Contains(request.Method)) throw OperationException.Invalid("Selecciona el medio de pago.");
        ChargeService.Money(request.Amount);
        if (request.ReceivedOn > Today() || request.ReceivedOn < Today().AddYears(-2)) throw OperationException.Invalid("La fecha de pago no es válida.");
        var allocations = request.Allocations ?? [];
        if (allocations.Length is 0 or > 60 || allocations.Select(x => x.ChargeId).Distinct().Count() != allocations.Length)
            throw OperationException.Invalid("Indica a qué cargos se aplica el pago (sin repetir cargos).");
        foreach (var allocation in allocations) ChargeService.Money(allocation.Amount);
        // Overpayments are not stored as unmanaged credit: the received amount must be fully applied.
        if (allocations.Sum(x => x.Amount) != request.Amount)
            throw OperationException.Invalid("La suma aplicada debe ser igual al monto recibido.");

        await using var transaction = await ChargeService.LockAsync(db, ct);
        if (await db.Payments.AnyAsync(x => x.IdempotencyKey == key, ct))
        {
            await transaction.RollbackAsync(ct);
            return await ReceiptAsync(await db.Payments.Where(x => x.IdempotencyKey == key).Select(x => x.Id).SingleAsync(ct), ct);
        }
        var chargeIds = allocations.Select(x => x.ChargeId).ToArray();
        var charges = await db.Charges.AsNoTracking().Where(x => chargeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (charges.Count != chargeIds.Length || charges.Values.Any(x => x.VoidedAt is not null))
            throw OperationException.Invalid("Uno de los cargos no existe o está anulado. Actualiza el estado de cuenta.");
        var balances = await ChargeService.Balances(db, chargeIds, ct);
        if (allocations.Any(x => x.Amount > balances[x.ChargeId]))
            throw new OperationException(409, "allocation-exceeds-balance", "Un monto aplicado supera el saldo pendiente. Actualiza el estado de cuenta.");

        var number = await NextReceiptNumberAsync(ct);
        var now = time.GetUtcNow();
        var payment = new Payment
        {
            Id = Guid.NewGuid(), ReceiptNumber = number, IdempotencyKey = key, PayerName = payer, Amount = request.Amount,
            Currency = institution.Value.Locale.Currency, Method = request.Method, Reference = reference, ReceivedOn = request.ReceivedOn,
            Note = note, RecordedAt = now, RecordedBy = actor
        };
        payment.Allocations.AddRange(allocations.Select(x => new PaymentAllocation
            { Id = Guid.NewGuid(), PaymentId = payment.Id, ChargeId = x.ChargeId, Amount = x.Amount }));
        payment.ReceiptSnapshot = JsonSerializer.Serialize(await SnapshotAsync(payment, charges, actor, ct), Json);
        db.Payments.Add(payment);
        audit.Record("PaymentRecorded", actor, "Payment", payment.Id, detail: $"{number}:{request.Method}");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await ReceiptAsync(payment.Id, ct);
    }

    public async Task ReverseAsync(Guid actor, Guid id, ReverseRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.FinanceReverse);
        var reason = Reasons.Require(request.Reason);
        await using var transaction = await ChargeService.LockAsync(db, ct);
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos ese pago.");
        if (!payment.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
        if (payment.ReversedAt is not null) throw new OperationException(409, "payment-reversed", "El pago ya fue revertido.");
        // The original payment, allocations and receipt stay; the reversal stops them from covering obligations.
        payment.ReversedAt = time.GetUtcNow();
        payment.ReversedBy = actor;
        payment.ReversalReason = reason;
        audit.Record("PaymentReversed", actor, "Payment", id, reason, $"{payment.ReceiptNumber}");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<ReceiptView> ReceiptAsync(Guid id, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos ese recibo.");
        return new(payment.Id, JsonSerializer.Deserialize<ReceiptSnapshot>(payment.ReceiptSnapshot, Json)!, payment.ReversedAt is not null,
            payment.ReversedAt, payment.ReversalReason);
    }

    public async Task<bool> PaymentTouchesStudentAsync(Guid paymentId, Guid studentId, CancellationToken ct) =>
        await db.PaymentAllocations.AnyAsync(a => a.PaymentId == paymentId && db.Charges.Any(c => c.Id == a.ChargeId && c.StudentId == studentId), ct);

    public async Task<DocumentAsset?> AssetAsync(string sha, CancellationToken ct) =>
        ShaPattern().IsMatch(sha) ? await db.DocumentAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Sha256 == sha, ct) : null;

    public async Task<PaymentSummary[]> RecentAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var start = from ?? Today();
        var end = to ?? start;
        if (end < start || end.DayNumber - start.DayNumber > 92) throw OperationException.Invalid("Selecciona un rango de hasta tres meses.");
        return await db.Payments.AsNoTracking().Where(x => x.ReceivedOn >= start && x.ReceivedOn <= end)
            .OrderByDescending(x => x.ReceiptNumber).Take(500)
            .Select(p => new PaymentSummary(p.Id, p.ReceiptNumber, p.ReceivedOn, p.Amount, p.Method, p.PayerName, p.ReversedAt != null,
                Convert.ToBase64String(p.Version))).ToArrayAsync(ct);
    }

    private async Task<ReceiptSnapshot> SnapshotAsync(Payment payment, Dictionary<Guid, Charge> charges, Guid actor, CancellationToken ct)
    {
        var options = institution.Value;
        var studentIds = charges.Values.Select(x => x.StudentId).Distinct().ToArray();
        var students = await db.Students.AsNoTracking().Where(x => studentIds.Contains(x.PersonId))
            .Join(db.Persons, s => s.PersonId, p => p.Id, (s, p) => new { s.PersonId, s.StudentCode, p.GivenNames, p.FamilyNames, p.PreferredName })
            .ToDictionaryAsync(x => x.PersonId, ct);
        var conceptIds = charges.Values.Select(x => x.ConceptId).Distinct().ToArray();
        var concepts = await db.FeeConcepts.AsNoTracking().Where(x => conceptIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var periodIds = charges.Values.Where(x => x.BillingPeriodId != null).Select(x => x.BillingPeriodId!.Value).Distinct().ToArray();
        var periods = await db.BillingPeriods.AsNoTracking().Where(x => periodIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Label, ct);
        var issuer = await db.Accounts.AsNoTracking().Where(x => x.Id == actor).Select(x => x.UserName).SingleAsync(ct);
        var lines = payment.Allocations.Select(a =>
        {
            var charge = charges[a.ChargeId];
            var student = students[charge.StudentId];
            return new ReceiptLine(PeopleService.Display(student.GivenNames, student.FamilyNames, student.PreferredName), student.StudentCode,
                concepts[charge.ConceptId], charge.BillingPeriodId is { } p ? periods[p] : null, charge.Description, a.Amount);
        }).ToArray();
        return new(payment.ReceiptNumber, options.DisplayName, options.LegalName, options.Documents.TaxId, options.Contact.Address,
            options.Contact.Phone, options.Contact.Email, await StoreLogoAsync(options.Assets.ReceiptLogo, ct), options.Documents.ReceiptFooter,
            payment.PayerName, payment.ReceivedOn, payment.RecordedAt, payment.Method, payment.Reference, payment.Currency, payment.Amount,
            lines, issuer ?? "");
    }

    // Copies the configured receipt logo by content hash, so a later brand change does not alter issued receipts.
    private async Task<string> StoreLogoAsync(string path, CancellationToken ct)
    {
        var file = Path.Combine(environment.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        var bytes = await File.ReadAllBytesAsync(file, ct);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!await db.DocumentAssets.AnyAsync(x => x.Sha256 == sha, ct))
            db.DocumentAssets.Add(new DocumentAsset
            {
                Sha256 = sha, Content = bytes, CreatedAt = time.GetUtcNow(),
                ContentType = Path.GetExtension(file).ToLowerInvariant() switch
                {
                    ".png" => "image/png", ".svg" => "image/svg+xml", ".webp" => "image/webp", _ => "application/octet-stream"
                }
            });
        return sha;
    }

    // NEXT VALUE FOR cannot run inside the subquery EF builds for SqlQueryRaw; use a plain command in the current transaction.
    private async Task<long> NextReceiptNumberAsync(CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction(db.Database.CurrentTransaction!);
        command.CommandText = "SELECT NEXT VALUE FOR finance.ReceiptNumbers";
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    private static string Clean(string? value, int max, string field, bool required = false)
    {
        var text = WhiteSpace().Replace(value?.Trim() ?? "", " ");
        if ((required && text.Length == 0) || text.Length > max || text.Any(char.IsControl))
            throw OperationException.Invalid($"Revisa {field} (máximo {max} caracteres).");
        return text;
    }

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById(institution.Value.Locale.TimeZone)).DateTime);

    [GeneratedRegex(@"\s+")] private static partial Regex WhiteSpace();
    [GeneratedRegex(@"^[A-Za-z0-9\-]{16,64}$")] private static partial Regex KeyPattern();
    [GeneratedRegex(@"^[a-f0-9]{64}$")] private static partial Regex ShaPattern();
}

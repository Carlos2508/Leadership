using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Inventory;

public sealed class InventoryService(SchoolDbContext db, SchoolAudit audit, TimeProvider time)
{
    private const int Limit = 1000000;

    private IQueryable<InventoryProduct> Filter(string? search, string? status, bool low)
    {
        var term = search?.Trim() ?? "";
        if (term.Length > 100) throw OperationException.Invalid("La búsqueda admite hasta 100 caracteres.");
        var query = db.InventoryProducts.AsNoTracking();
        query = status switch
        {
            null or "" or "active" => query.Where(p => p.IsActive),
            "archived" => query.Where(p => !p.IsActive),
            "all" => query,
            _ => throw OperationException.Invalid("El estado del inventario no es válido.")
        };
        if (low) query = query.Where(p => p.IsActive && p.OnHand <= p.Minimum);
        foreach (var word in term.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            query = query.Where(p => p.Code.Contains(word) || p.Name.Contains(word) || p.Category.Contains(word) || p.Location.Contains(word));
        return query;
    }

    public async Task<PagedResult<InventoryProductView>> ProductsAsync(Guid actor, string? search, string? status, bool low, int page, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.InventoryRead);
        if (page is < 1 or > 100000) throw OperationException.Invalid("La página no es válida.");
        var query = Filter(search, status, low);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(p => p.Name).ThenBy(p => p.Code).Skip((page - 1) * 40).Take(40).ToArrayAsync(ct);
        return new(rows.Select(View).ToArray(), total, page, 40);
    }

    public async Task<InventorySummary> SummaryAsync(Guid actor, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.InventoryRead);
        var active = db.InventoryProducts.Where(p => p.IsActive);
        return new(await active.CountAsync(ct), await active.CountAsync(p => p.OnHand <= p.Minimum, ct),
            await active.SumAsync(p => (long?)p.OnHand, ct) ?? 0);
    }

    public async Task<InventoryProductView> ProductAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.InventoryRead);
        return View(await db.InventoryProducts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw Missing());
    }

    public async Task<PagedResult<InventoryMovementView>> MovementsAsync(Guid actor, Guid id, int page, CancellationToken ct)
    {
        await ProductAsync(actor, id, ct);
        if (page is < 1 or > 100000) throw OperationException.Invalid("La página no es válida.");
        var query = db.InventoryMovements.AsNoTracking().Where(m => m.ProductId == id);
        return new(await query.OrderByDescending(m => m.Sequence).Skip((page - 1) * 40).Take(40)
            .Select(m => new InventoryMovementView(m.Id, m.Sequence, m.Kind, m.Delta, m.Before, m.After, m.Reference, m.Reason,
                m.OccurredAt, db.Accounts.Where(a => a.Id == m.ActorId).Select(a => a.UserName).FirstOrDefault() ?? "Cuenta anterior"))
            .ToArrayAsync(ct), await query.CountAsync(ct), page, 40);
    }

    public async Task<InventoryProductView> CreateAsync(Guid actor, CreateInventoryProduct request, CancellationToken ct)
    {
        var code = Code(request.Code); var name = Text(request.Name, 160, true); var category = Text(request.Category, 80); var location = Text(request.Location, 100);
        var reason = Reasons.Require(request.Reason);
        if (request.RequestId == Guid.Empty || request.Minimum is < 0 or > Limit || request.InitialQuantity is < 0 or > Limit)
            throw OperationException.Invalid("Usa cantidades enteras entre 0 y 1,000,000 y un identificador de intento válido.");
        var hash = Hash(new { code, name, category, location, request.Minimum, request.InitialQuantity, reason });
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAsync("attempt:" + request.RequestId.ToString("N"), ct);
        await LockAsync("catalog", ct);
        await audit.RequireAsync(actor, ct, Permissions.InventoryRead, Permissions.InventoryManage);
        if (request.InitialQuantity > 0) await audit.RequireAsync(actor, ct, Permissions.InventoryMove);
        var existing = await db.InventoryProducts.SingleOrDefaultAsync(p => p.Id == request.RequestId, ct);
        if (existing is not null)
        {
            if (existing.CreatedBy != actor || !existing.CreationHash.SequenceEqual(hash)) throw Reused();
            await tx.CommitAsync(ct); return View(existing);
        }
        if (await db.InventoryProducts.AnyAsync(p => p.Code == code, ct)) throw Duplicate();
        if (await db.InventoryMovements.AnyAsync(m => m.Id == request.RequestId, ct)) throw Reused();
        var product = new InventoryProduct { Id = request.RequestId, Code = code, Name = name, Category = category, Location = location,
            Minimum = request.Minimum, OnHand = request.InitialQuantity, CreatedBy = actor, CreatedAt = time.GetUtcNow(), CreationHash = hash };
        db.InventoryProducts.Add(product);
        if (request.InitialQuantity > 0)
        {
            product.LastSequence = 1;
            db.InventoryMovements.Add(new() { Id = request.RequestId, ProductId = product.Id, Sequence = 1, ActorId = actor,
                Kind = "In", Delta = request.InitialQuantity, Before = 0, After = request.InitialQuantity,
                Reason = reason, OccurredAt = time.GetUtcNow(), RequestHash = hash });
            audit.Record("InventoryStockReceived", actor, "InventoryProduct", product.Id, reason, "opening");
        }
        audit.Record("InventoryProductCreated", actor, "InventoryProduct", product.Id, reason);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return View(product);
    }

    public async Task<InventoryProductView> UpdateAsync(Guid actor, Guid id, UpdateInventoryProduct request, CancellationToken ct)
    {
        var code = Code(request.Code); var name = Text(request.Name, 160, true); var category = Text(request.Category, 80); var location = Text(request.Location, 100);
        var reason = Reasons.Require(request.Reason);
        if (request.Minimum is < 0 or > Limit) throw OperationException.Invalid("El mínimo debe estar entre 0 y 1,000,000 unidades.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAsync("catalog", ct); await LockAsync(id.ToString("N"), ct);
        await audit.RequireAsync(actor, ct, Permissions.InventoryRead, Permissions.InventoryManage);
        var product = await db.InventoryProducts.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw Missing();
        CheckVersion(product, request.Version);
        if (!request.IsActive && product.OnHand != 0) throw OperationException.Invalid("Registra la salida de las existencias antes de archivar el artículo.");
        if (await db.InventoryProducts.AnyAsync(p => p.Id != id && p.Code == code, ct)) throw Duplicate();
        product.Code = code; product.Name = name; product.Category = category; product.Location = location;
        product.Minimum = request.Minimum; product.IsActive = request.IsActive;
        audit.Record("InventoryProductUpdated", actor, "InventoryProduct", id, reason);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return View(product);
    }

    public async Task<InventoryProductView> MoveAsync(Guid actor, Guid id, MoveInventoryStock request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.Kind is not ("In" or "Out" or "Count") ||
            request.Quantity < (request.Kind == "Count" ? 0 : 1) || request.Quantity > Limit)
            throw OperationException.Invalid("Elige entrada, salida o conteo y una cantidad entera válida.");
        var reason = Reasons.Require(request.Reason); var reference = Text(request.Reference, 100);
        var hash = Hash(new { productId = id, request.Kind, request.Quantity, reference, reason, request.Version });
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAsync("attempt:" + request.RequestId.ToString("N"), ct); await LockAsync(id.ToString("N"), ct);
        await audit.RequireAsync(actor, ct, Permissions.InventoryRead, request.Kind == "Count" ? Permissions.InventoryManage : Permissions.InventoryMove);
        var existing = await db.InventoryMovements.SingleOrDefaultAsync(m => m.Id == request.RequestId, ct);
        if (existing is not null)
        {
            if (existing.ActorId != actor || existing.ProductId != id || !existing.RequestHash.SequenceEqual(hash)) throw Reused();
            var current = await db.InventoryProducts.SingleAsync(p => p.Id == id, ct);
            await tx.CommitAsync(ct); return View(current);
        }
        var product = await db.InventoryProducts.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw Missing();
        CheckVersion(product, request.Version);
        if (!product.IsActive) throw OperationException.Invalid("Reactiva el artículo antes de registrar movimientos.");
        var after = request.Kind switch { "In" => (long)product.OnHand + request.Quantity, "Out" => (long)product.OnHand - request.Quantity, _ => request.Quantity };
        if (after < 0) throw OperationException.Invalid("No hay suficientes unidades disponibles para esta salida.");
        if (after > Limit) throw OperationException.Invalid("La existencia máxima es 1,000,000 de unidades por artículo.");
        var before = product.OnHand; product.OnHand = (int)after; product.LastSequence++;
        db.InventoryMovements.Add(new() { Id = request.RequestId, ProductId = id, Sequence = product.LastSequence, ActorId = actor,
            Kind = request.Kind, Delta = product.OnHand - before, Before = before, After = product.OnHand, Reference = reference,
            Reason = reason, OccurredAt = time.GetUtcNow(), RequestHash = hash });
        audit.Record("InventoryStock" + request.Kind, actor, "InventoryProduct", id, reason, "sequence:" + product.LastSequence);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return View(product);
    }

    public async Task<byte[]> ExportAsync(Guid actor, string? search, string? status, bool low, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.InventoryRead);
        var rows = await Filter(search, status, low).OrderBy(p => p.Name).ThenBy(p => p.Code).Take(10001).ToArrayAsync(ct);
        if (rows.Length > 10000) throw OperationException.Invalid("Filtra la búsqueda para exportar hasta 10,000 artículos.");
        static string Cell(string value)
        {
            if (value.TrimStart().StartsWithAnyFormula()) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        var csv = new StringBuilder("Código,Artículo,Categoría,Ubicación,Existencias,Mínimo,Estado\r\n");
        foreach (var p in rows) csv.AppendLine(string.Join(',', Cell(p.Code), Cell(p.Name), Cell(p.Category), Cell(p.Location),
            p.OnHand.ToString(System.Globalization.CultureInfo.InvariantCulture), p.Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture), Cell(p.IsActive ? "Activo" : "Archivado")));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    private Task LockAsync(string key, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = {"SchoolPlatform.Inventory:" + key}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @result < 0 THROW 51000, 'No fue posible bloquear el inventario.', 1;", ct);
    private static byte[] Hash(object value) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value));
    private static string Text(string? value, int max, bool required = false)
    {
        var text = (value ?? "").Trim().Normalize();
        if (text.Length > max || (required && text.Length < 2) || text.Any(char.IsControl)) throw OperationException.Invalid("Revisa los nombres, categoría y ubicación del artículo.");
        return text;
    }
    private static string Code(string? value)
    {
        var code = (value ?? "").Trim().ToUpperInvariant();
        if (!Regex.IsMatch(code, "^[A-Z0-9][A-Z0-9._-]{0,39}$", RegexOptions.CultureInvariant))
            throw OperationException.Invalid("El código admite hasta 40 letras, números, puntos, guiones y guion bajo.");
        return code;
    }
    private static void CheckVersion(InventoryProduct p, string version) { if (!p.Version.SequenceEqual(Versions.Decode(version))) throw OperationException.Changed(); }
    private static InventoryProductView View(InventoryProduct p) => new(p.Id, p.Code, p.Name, p.Category, p.Location, p.Minimum, p.OnHand, p.IsActive, p.IsActive && p.OnHand <= p.Minimum, Versions.Encode(p.Version));
    private static OperationException Missing() => OperationException.NotFound("El artículo no existe.");
    private static OperationException Duplicate() => new(409, "inventory-code-exists", "Ese código ya pertenece a otro artículo, incluso si está archivado.");
    private static OperationException Reused() => new(409, "inventory-attempt-reused", "Ese intento ya corresponde a otra operación de inventario.");
}

internal static class InventoryCsvText
{
    public static bool StartsWithAnyFormula(this string value) => value.Length > 0 && "=+-@\t\r\n".Contains(value[0]);
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.Inventory;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Tests;

public abstract class InventoryTestBase : SqlTestBase
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        var role = (await Use<AccessRoleService, RoleView[]>(s => s.ListAsync(default))).Single(r => r.Key == "administrator");
        await Use<AccessRoleService, string>(s => s.SaveAsync(Owner, role.Key, new(role.Label, role.Description,
            role.Permissions.Concat([Permissions.InventoryRead, Permissions.InventoryManage, Permissions.InventoryMove]).Distinct().ToArray(), true, Reason, role.Version), default));
    }
    protected Task<InventoryProductView> Create(int stock = 0, string code = "LAP-01", Guid? requestId = null) =>
        Use<InventoryService, InventoryProductView>(s => s.CreateAsync(Owner, new(requestId ?? Guid.NewGuid(), code, "Lápiz escolar", "Escritura", "Estante A", 5, stock, Reason), default));
    protected Task<InventoryProductView> Move(InventoryProductView p, string kind, int quantity, Guid? attempt = null) =>
        Use<InventoryService, InventoryProductView>(s => s.MoveAsync(Owner, p.Id, new(attempt ?? Guid.NewGuid(), kind, quantity, "Documento sintético", Reason, p.Version), default));
    protected Task<PagedResult<InventoryMovementView>> History(Guid id, int page = 1) => Use<InventoryService, PagedResult<InventoryMovementView>>(s => s.MovementsAsync(Owner, id, page, default));
}

public sealed class InventorySqlTests : InventoryTestBase
{

    [Fact]
    public async Task Opening_stock_and_movements_keep_a_continuous_immutable_ledger_and_atomic_audit()
    {
        var p = await Create(20);
        p = await Move(p, "Out", 6); p = await Move(p, "In", 3); p = await Move(p, "Count", 12);
        var history = await History(p.Id);
        Assert.Equal(12, p.OnHand); Assert.Equal(4, history.Total);
        var ordered = history.Items.OrderBy(m => m.Sequence).ToArray();
        Assert.Equal(new[] { 20, -6, 3, -5 }, ordered.Select(m => m.Delta));
        Assert.Equal(p.OnHand, ordered.Sum(m => m.Delta));
        for (var i = 1; i < ordered.Length; i++) Assert.Equal(ordered[i - 1].After, ordered[i].Before);
        Assert.All(ordered, m => Assert.Equal(OwnerName, m.Actor));
        await Use<SchoolDbContext>(async db =>
        {
            Assert.Equal(5, await db.AuditEvents.CountAsync(a => a.TargetId == p.Id && a.Action.StartsWith("Inventory")));
            var row = await db.InventoryMovements.FirstAsync(m => m.ProductId == p.Id);
            row.Reason = "Intento de modificar historial";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        });
        await Use<SchoolDbContext>(async db =>
        {
            await using var connection = new SqlConnection(db.Database.GetConnectionString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.Parameters.AddWithValue("@id", p.Id);
            command.CommandText = "DELETE FROM [inventory].[Movements] WHERE [ProductId] = @id";
            await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
            command.CommandText = "UPDATE [inventory].[Products] SET [OnHand] = -1 WHERE [Id] = @id";
            await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        });
        Assert.Equal(4, (await History(p.Id)).Total);
    }

    [Fact]
    public async Task Creation_and_stock_retries_never_duplicate_even_after_another_operation()
    {
        var key = Guid.NewGuid(); var p = await Create(10, requestId: key);
        Assert.Equal(p.Id, (await Create(10, requestId: key)).Id); Assert.Single((await History(p.Id)).Items);
        var attempt = Guid.NewGuid(); var command = new MoveInventoryStock(attempt, "In", 8, "Recepción", Reason, p.Version);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Use<InventoryService, InventoryProductView>(s => s.MoveAsync(Owner, p.Id, command, default))));
        Assert.All(results, result => Assert.Equal(18, result.OnHand));
        var current = await Move(results[0], "Out", 2);
        Assert.Equal(16, (await Use<InventoryService, InventoryProductView>(s => s.MoveAsync(Owner, p.Id, command, default))).OnHand);
        Assert.Equal(3, (await History(p.Id)).Total);
        Assert.Equal(409, (await Assert.ThrowsAsync<OperationException>(() => Create(11, requestId: key))).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<OperationException>(() => Use<InventoryService, InventoryProductView>(s => s.MoveAsync(Owner, p.Id, command with { Quantity = 9 }, default)))).Status);
        Assert.Equal(current.OnHand, (await Use<InventoryService, InventoryProductView>(s => s.ProductAsync(Owner, p.Id, default))).OnHand);
    }

    [Fact]
    public async Task Concurrent_exits_reject_stale_versions_and_never_oversell_or_change_history_on_failure()
    {
        var p = await Create(10);
        async Task<bool> TryExit() { try { await Move(p, "Out", 7); return true; } catch (OperationException e) when (e.Status == 409) { return false; } }
        Assert.Single(await Task.WhenAll(TryExit(), TryExit()), ok => ok);
        var current = await Use<InventoryService, InventoryProductView>(s => s.ProductAsync(Owner, p.Id, default));
        Assert.Equal(3, current.OnHand);
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Move(current, "Out", 4))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Move(current, "In", 1000000))).Status);
        Assert.Equal(2, (await History(p.Id)).Total);
    }

    [Fact]
    public async Task Archive_requires_zero_stock_keeps_history_and_prevents_moves_until_reactivation()
    {
        var p = await Create(5);
        Task<InventoryProductView> Active(InventoryProductView row, bool active) => Use<InventoryService, InventoryProductView>(s => s.UpdateAsync(Owner, row.Id,
            new(row.Code, row.Name, row.Category, row.Location, row.Minimum, active, row.Version, Reason), default));
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Active(p, false))).Status);
        p = await Move(p, "Count", 0); p = await Active(p, false);
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Move(p, "In", 1))).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<OperationException>(() => Create(0, code: " lap-01 "))).Status);
        Assert.Empty((await Use<InventoryService, PagedResult<InventoryProductView>>(s => s.ProductsAsync(Owner, null, "active", false, 1, default))).Items);
        Assert.Single((await Use<InventoryService, PagedResult<InventoryProductView>>(s => s.ProductsAsync(Owner, null, "archived", false, 1, default))).Items);
        p = await Active(p, true); p = await Move(p, "In", 2);
        Assert.Equal(2, p.OnHand); Assert.Equal(3, (await History(p.Id)).Total);
    }

    [Fact]
    public async Task Search_low_stock_summary_export_and_pagination_are_real_sql_and_csv_cannot_execute_formulas()
    {
        var p = await Create(4);
        await Use<InventoryService, InventoryProductView>(s => s.UpdateAsync(Owner, p.Id, new(p.Code, "=1+2", "Escritura", "Estante A", 5, true, p.Version, Reason), default));
        await Create(20, "CUAD-01");
        var low = await Use<InventoryService, PagedResult<InventoryProductView>>(s => s.ProductsAsync(Owner, "Estante", "active", true, 1, default));
        Assert.Single(low.Items); Assert.True(low.Items[0].LowStock);
        Assert.Empty((await Use<InventoryService, PagedResult<InventoryProductView>>(s => s.ProductsAsync(Owner, null, "active", false, 2, default))).Items);
        var summary = await Use<InventoryService, InventorySummary>(s => s.SummaryAsync(Owner, default));
        Assert.Equal(new InventorySummary(2, 1, 24), summary);
        var csv = Encoding.UTF8.GetString(await Use<InventoryService, byte[]>(s => s.ExportAsync(Owner, null, "active", false, default)));
        Assert.Contains("'" + "=1+2", csv); Assert.Contains("CUAD-01", csv);
        Assert.Equal(400, (await Assert.ThrowsAsync<OperationException>(() => Use<InventoryService, PagedResult<InventoryProductView>>(s => s.ProductsAsync(Owner, null, "unknown", false, 1, default)))).Status);
    }

    [Fact]
    public async Task Http_requires_separate_sql_permissions_active_account_and_csrf_and_counts_cannot_use_the_exit_route()
    {
        var p = await Create(2);
        using var anonymous = App.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/inventory/products")).StatusCode);
        await Account("inventory-no-access"); using var outsider = await SignIn("inventory-no-access");
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/v1/inventory/products")).StatusCode);
        using var browser = await SignIn(OwnerName);
        var move = new MoveInventoryStock(Guid.NewGuid(), "Out", 1, null, Reason, p.Version);
        var path = $"/api/v1/inventory/products/{p.Id}/movements";
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync(path, move)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(browser, path, move with { Kind = "Count" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(browser, path, move)).StatusCode);
        var role = await Use<AccessRoleService, string>(s => s.SaveAsync(Owner, null, new("Consulta inventario", "Solo consulta", [Permissions.InventoryRead], true, Reason), default));
        await Account("inventory-reader", role); using var reader = await SignIn("inventory-reader");
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/v1/inventory/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(reader, path, move)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(reader, "/api/v1/inventory/products", new CreateInventoryProduct(Guid.NewGuid(), "DENIED", "No permitido", null, null, 0, 0, Reason))).StatusCode);
    }
}

public sealed class InventoryMfaSqlTests : InventoryTestBase
{
    protected override Dictionary<string, string?> ExtraConfiguration => new() { ["AccountAccess:RequireMfaForPermissions:0"] = Permissions.InventoryMove };

    [Fact]
    public async Task Opening_stock_and_counts_require_mfa_of_the_protected_operation()
    {
        using var browser = await SignIn(OwnerName);
        Assert.Equal(HttpStatusCode.OK, (await Post(browser, "/api/v1/inventory/products", new CreateInventoryProduct(Guid.NewGuid(), "MFA-EMPTY", "Artículo sin stock", null, null, 0, 0, Reason))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(browser, "/api/v1/inventory/products", new CreateInventoryProduct(Guid.NewGuid(), "MFA-01", "Artículo", null, null, 0, 1, Reason))).StatusCode);
    }
}

public sealed class InventoryCountMfaSqlTests : InventoryTestBase
{
    protected override Dictionary<string, string?> ExtraConfiguration => new() { ["AccountAccess:RequireMfaForPermissions:0"] = Permissions.InventoryManage };
    [Fact]
    public async Task Count_requires_its_own_mfa_policy_and_cannot_be_smuggled_through_movements()
    {
        var p = await Create(2); using var browser = await SignIn(OwnerName);
        var count = new MoveInventoryStock(Guid.NewGuid(), "Count", 0, null, Reason, p.Version);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(browser, $"/api/v1/inventory/products/{p.Id}/counts", count)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(browser, $"/api/v1/inventory/products/{p.Id}/movements", count)).StatusCode);
        Assert.Equal(2, (await Use<InventoryService, InventoryProductView>(s => s.ProductAsync(Owner, p.Id, default))).OnHand);
    }
}

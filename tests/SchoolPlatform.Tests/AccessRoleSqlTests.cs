using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Tests;

public sealed class AccessRoleSqlTests : SqlTestBase
{
    private Task<string> Save(string name, string[] permissions, string? key = null, string? version = null, bool active = true, Guid? actor = null) =>
        Use<AccessRoleService, string>(x => x.SaveAsync(actor ?? Owner, key, new(name, "Rol sintético", permissions, active, Reason, version), default));
    private async Task<RoleView> Role(string key) => (await Use<AccessRoleService, RoleView[]>(x => x.ListAsync(default))).Single(x => x.Key == key);

    [Fact]
    public async Task Role_names_do_not_limit_permissions_and_edits_propagate_atomically_to_assigned_accounts()
    {
        var all = PermissionCatalog.All.Select(p => p.Key).ToArray();
        var director = await Save("Director", all);
        var secretary = await Save("Secretaría", all);
        Assert.Equal(all.Order(), (await Role(secretary)).Permissions.Order());
        Assert.Equal(all.Order(), (await Role(director)).Permissions.Order());
        var custom = await Save("Consulta de prueba", [Permissions.PeopleRead]);
        var user = await Account("role-reader", custom);
        using var oldCookie = await SignIn("role-reader");
        Assert.Equal(HttpStatusCode.OK, (await oldCookie.GetAsync("/api/v1/people")).StatusCode);
        var role = await Role(custom);
        Assert.Equal(1, role.Accounts);
        await Save("Caja de prueba", [Permissions.FinanceRead], custom, role.Version);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldCookie.GetAsync("/api/v1/session")).StatusCode);
        using var newCookie = await SignIn("role-reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await newCookie.GetAsync("/api/v1/people")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await newCookie.GetAsync("/api/v1/finance/payments")).StatusCode);
        var effective = await Use<IdentityDbContext, string[]>(db => db.AccountPermissions.Where(p => p.UserId == user).Select(p => p.Permission).ToArrayAsync());
        Assert.Equal([Permissions.FinanceRead], effective);
        Assert.True(await Use<IdentityDbContext, bool>(db => db.AuditEvents.AnyAsync(a => a.TargetId == user && a.Action == "AccountRolePermissionsChanged")));
        var updated = await Role(custom);
        Assert.Equal("role-in-use", (await Assert.ThrowsAsync<AccessOperationException>(() => Save("Caja de prueba", [Permissions.FinanceRead], custom, updated.Version, false))).Code);
    }

    [Fact]
    public async Task Last_manager_stale_versions_and_invalid_permissions_are_rejected_without_partial_changes()
    {
        var ownerRole = await Use<IdentityDbContext, string>(db => db.AccountRoleAssignments.Where(a => a.UserId == Owner).Select(a => a.RoleKey).SingleAsync());
        var role = await Role(ownerRole);
        var before = await Use<IdentityDbContext, int>(db => db.AuditEvents.CountAsync());
        Assert.Equal("last-manager", (await Assert.ThrowsAsync<AccessOperationException>(() => Save(role.Label,
            role.Permissions.Where(p => p != Permissions.ManageAccounts).ToArray(), role.Key, role.Version))).Code);
        Assert.Equal(role.Version, (await Role(role.Key)).Version);
        Assert.Equal(before, await Use<IdentityDbContext, int>(db => db.AuditEvents.CountAsync()));
        await Assert.ThrowsAsync<OperationException>(() => Save("Permiso inválido", ["invented.allow"]));
        var key = await Save("Rol sin permisos", []);
        var first = await Role(key);
        await Save("Rol renombrado", [], key, first.Version);
        Assert.Equal(409, (await Assert.ThrowsAsync<OperationException>(() => Save("Cambio obsoleto", [], key, first.Version))).Status);
        await Save("Rol renombrado", [], key, (await Role(key)).Version, false);
        await Assert.ThrowsAsync<AccessOperationException>(() => Account("inactive-role", key));
        var basic = await Account("not-manager");
        Assert.Equal(403, (await Assert.ThrowsAsync<AccessOperationException>(() => Save("No autorizado", [], actor: basic))).Status);
    }

    [Fact]
    public async Task Migration_preserves_nonstandard_existing_permissions_and_never_grants_a_larger_template()
    {
        // Only the fixture's random database is downgraded; its existing permission rows are the migration input.
        await Use<IdentityDbContext>(db => db.GetService<IMigrator>().MigrateAsync("20261105060302_AuditTargetType"));
        var expected = new[] { Permissions.PeopleRead, Permissions.FinanceRead };
        await Use<IdentityDbContext>(async db =>
        {
            var old = await db.AccountPermissions.Where(p => p.UserId == Owner).ToArrayAsync();
            db.AccountPermissions.RemoveRange(old.Where(p => !expected.Contains(p.Permission)));
            foreach (var permission in expected.Except(old.Select(p => p.Permission))) db.AccountPermissions.Add(new() { UserId = Owner, Permission = permission });
            await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync();
        });
        var assignment = await Use<IdentityDbContext, string>(db => db.AccountRoleAssignments.Where(a => a.UserId == Owner).Select(a => a.RoleKey).SingleAsync());
        Assert.StartsWith("legacy-", assignment);
        var actual = await Role(assignment);
        Assert.Equal(expected.Order(), actual.Permissions.Order());
        Assert.Equal(expected.Order(), (await Use<IdentityDbContext, string[]>(db => db.AccountPermissions.Where(p => p.UserId == Owner).Select(p => p.Permission).ToArrayAsync())).Order());
    }
}

public sealed class AccessRoleMfaSqlTests : SqlTestBase
{
    protected override Dictionary<string, string?> ExtraConfiguration => new()
    { ["AccountAccess:RequireMfaForPermissions:0"] = Permissions.ManageAccounts };

    [Fact]
    public async Task Role_management_requires_mfa_when_the_installation_requires_it()
    {
        using var browser = await SignIn(OwnerName);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/api/v1/access/roles")).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<AccessOperationException>(() =>
            Use<AccessRoleService, string>(x => x.SaveAsync(Owner, null, new("No permitido", "", [], true, Reason), default)))).Status);
    }
}

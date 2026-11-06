using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Common;

namespace SchoolPlatform.Host.IdentityAccess;

public sealed class AccessRoleService(IdentityDbContext db, SecurityAudit audit, IOptions<AccountAccessOptions> options)
{
    public async Task<RoleView[]> ListAsync(CancellationToken ct)
    {
        var roles = await db.AccessRoles.AsNoTracking().OrderBy(r => r.Label).ToArrayAsync(ct);
        var permissions = await db.AccessRolePermissions.AsNoTracking().ToArrayAsync(ct);
        var assignments = await db.AccountRoleAssignments.AsNoTracking().GroupBy(a => a.RoleKey)
            .Select(g => new { Key = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return roles.Select(r => new RoleView(r.Key, r.Label, r.Description, r.IsActive, Versions.Encode(r.Version),
            permissions.Where(p => p.RoleKey == r.Key).Select(p => p.Permission).Order().ToArray(), assignments.GetValueOrDefault(r.Key))).ToArray();
    }
    public async Task<AccessProfile[]> ProfilesAsync(CancellationToken ct) =>
        (await ListAsync(ct)).Where(r => r.IsActive).Select(r => new AccessProfile(r.Key, r.Label, r.Permissions)).ToArray();

    public async Task<string> SaveAsync(Guid actor, string? key, SaveRoleRequest request, CancellationToken ct)
    {
        var label = request.Label?.Trim() ?? "";
        var description = request.Description?.Trim() ?? "";
        var reason = request.Reason?.Trim() ?? "";
        if (label.Length is < 2 or > 100 || description.Length > 500 || reason.Length is < 8 or > 500)
            throw OperationException.Invalid("Escribe un nombre de 2 a 100 caracteres y un motivo de 8 a 500 caracteres.");
        var desired = (request.Permissions ?? []).ToHashSet(StringComparer.Ordinal);
        if (desired.Any(p => !PermissionCatalog.All.Any(d => d.Key == p)))
            throw OperationException.Invalid("Selecciona únicamente permisos del catálogo disponible.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Same cross-process lock as account changes, activation and the last-manager guard.
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource=N'SchoolPlatform.AccountAccess',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51000, 'Account access operation busy', 1;
            """, ct);
        if (!await db.Users.AnyAsync(u => u.Id == actor && u.IsActive && u.PasswordHash != null &&
                (!options.Value.RequireMfaForPermissions.Contains(Permissions.ManageAccounts) || u.TwoFactorEnabled) &&
                db.AccountPermissions.Any(p => p.UserId == actor && p.Permission == Permissions.ManageAccounts), ct))
            throw new AccessOperationException(403, "permission-denied", "No tienes permiso para administrar roles.");
        AccessRole role;
        if (key is null)
        {
            role = new AccessRole { Key = Guid.NewGuid().ToString("N") };
            db.AccessRoles.Add(role);
        }
        else
        {
            role = await db.AccessRoles.SingleOrDefaultAsync(r => r.Key == key, ct)
                ?? throw OperationException.NotFound("No encontramos ese rol.");
            if (!role.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
        }
        if (await db.AccessRoles.AnyAsync(r => r.Key != role.Key && r.Label == label, ct))
            throw new AccessOperationException(409, "role-name-taken", "Ya existe un rol con ese nombre.");
        var members = await db.AccountRoleAssignments.Where(a => a.RoleKey == role.Key).Select(a => a.UserId).ToArrayAsync(ct);
        if (!request.IsActive && members.Length > 0)
            throw new AccessOperationException(409, "role-in-use", "Reasigna sus cuentas antes de desactivar este rol.");
        if (!desired.Contains(Permissions.ManageAccounts) && members.Length > 0 &&
            !await db.Users.AnyAsync(u => u.IsActive && u.PasswordHash != null && !members.Contains(u.Id) &&
                db.AccountPermissions.Any(p => p.UserId == u.Id && p.Permission == Permissions.ManageAccounts), ct))
            throw new AccessOperationException(409, "last-manager", "Debe quedar al menos un gestor de accesos activo. Asigna otro gestor antes de continuar.");
        role.Label = label;
        role.Description = description;
        role.IsActive = request.IsActive;
        // Ensure rowversion changes even when only the permissions are edited.
        if (key is not null) db.Entry(role).Property(r => r.Label).IsModified = true;
        var current = await db.AccessRolePermissions.Where(p => p.RoleKey == role.Key).ToArrayAsync(ct);
        db.AccessRolePermissions.RemoveRange(current.Where(p => !desired.Contains(p.Permission)));
        foreach (var p in desired.Except(current.Select(p => p.Permission)))
            db.AccessRolePermissions.Add(new() { RoleKey = role.Key, Permission = p });
        // Durable effective projection: existing authorization queries remain server-side and change atomically.
        var effective = await db.AccountPermissions.Where(p => members.Contains(p.UserId)).ToArrayAsync(ct);
        db.AccountPermissions.RemoveRange(effective.Where(p => !desired.Contains(p.Permission)));
        foreach (var member in members)
            foreach (var permission in desired.Except(effective.Where(p => p.UserId == member).Select(p => p.Permission)))
                db.AccountPermissions.Add(new() { UserId = member, Permission = permission });
        foreach (var user in await db.Users.Where(u => members.Contains(u.Id)).ToArrayAsync(ct))
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            audit.Record("AccountRolePermissionsChanged", actor, user.Id, reason, role.Key);
        }
        audit.Record(key is null ? "AccessRoleCreated" : "AccessRoleUpdated", actor, null, reason, role.Key);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return role.Key;
    }
}

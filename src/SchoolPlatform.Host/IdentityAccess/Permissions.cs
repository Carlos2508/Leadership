using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace SchoolPlatform.Host.IdentityAccess;

public static class Permissions
{
    public const string ManageAccounts = "accounts.manage";
    public const string ReadAudit = "audit.read";
}

public sealed record AccessProfile(string Key, string Label, string[] Permissions);

public static class AccessProfiles
{
    public static readonly AccessProfile[] All =
    [
        new("basic", "Cuenta básica", []),
        new("access-manager", "Gestión de accesos", [Permissions.ManageAccounts]),
        new("auditor", "Consulta de auditoría", [Permissions.ReadAudit]),
        new("access-owner", "Gestión de accesos y auditoría", [Permissions.ManageAccounts, Permissions.ReadAudit])
    ];

    public static AccessProfile Get(string key) => All.FirstOrDefault(x => x.Key == key)
        ?? throw new AccessOperationException(400, "invalid-profile", "Selecciona un perfil de acceso válido.");
    public static string Resolve(IEnumerable<string> permissions)
    {
        var values = permissions.ToHashSet(StringComparer.Ordinal);
        return All.FirstOrDefault(x => values.SetEquals(x.Permissions))?.Key ?? "basic";
    }
}

public sealed class AccountPermission
{
    public Guid UserId { get; set; }
    public string Permission { get; set; } = "";
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionHandler(IdentityDbContext db) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
            await db.AccountPermissions.AsNoTracking().AnyAsync(x => x.UserId == userId && x.Permission == requirement.Permission))
            context.Succeed(requirement);
    }
}

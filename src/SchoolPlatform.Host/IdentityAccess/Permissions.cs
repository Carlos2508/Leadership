using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace SchoolPlatform.Host.IdentityAccess;

public static class Permissions
{
    public const string ManageAccounts = "accounts.manage";
    public const string ReadAudit = "audit.read";
    public const string PeopleRead = "people.read";
    public const string PeopleManage = "people.manage";
    public const string AcademicRead = "academic.read";
    public const string AcademicManage = "academic.manage";
    public const string EnrollmentManage = "enrollment.manage";
    public const string FinanceRead = "finance.read";
    public const string FinanceCharges = "finance.charges";
    public const string FinanceCollect = "finance.collect";
    public const string FinanceReverse = "finance.reverse";
}

public sealed record AccessProfile(string Key, string Label, string[] Permissions);

public static class AccessProfiles
{
    public static readonly AccessProfile[] All =
    [
        new("basic", "Cuenta básica", []),
        new("access-manager", "Gestión de accesos", [Permissions.ManageAccounts]),
        new("auditor", "Consulta de auditoría", [Permissions.ReadAudit]),
        new("access-owner", "Gestión de accesos y auditoría", [Permissions.ManageAccounts, Permissions.ReadAudit]),
        new("people-reader", "Consulta de personas y familias", [Permissions.PeopleRead]),
        new("registrar", "Secretaría: personas, familias e inscripciones",
            [Permissions.PeopleRead, Permissions.PeopleManage, Permissions.AcademicRead, Permissions.EnrollmentManage]),
        new("academic-coordinator", "Coordinación académica: ciclos, grados y secciones",
            [Permissions.PeopleRead, Permissions.AcademicRead, Permissions.AcademicManage]),
        new("cashier", "Caja: cobros y recibos",
            [Permissions.PeopleRead, Permissions.AcademicRead, Permissions.FinanceRead, Permissions.FinanceCollect]),
        new("finance-manager", "Administración financiera",
            [Permissions.PeopleRead, Permissions.AcademicRead, Permissions.FinanceRead, Permissions.FinanceCharges,
             Permissions.FinanceCollect, Permissions.FinanceReverse]),
        new("administrator", "Administración institucional",
            [Permissions.ManageAccounts, Permissions.ReadAudit, Permissions.PeopleRead, Permissions.PeopleManage,
             Permissions.AcademicRead, Permissions.AcademicManage, Permissions.EnrollmentManage,
             Permissions.FinanceRead, Permissions.FinanceCharges, Permissions.FinanceCollect, Permissions.FinanceReverse])
    ];

    public const string Custom = "custom";

    public static AccessProfile Get(string key) => All.FirstOrDefault(x => x.Key == key)
        ?? throw new AccessOperationException(400, "invalid-profile", "Selecciona un perfil de acceso válido.");
    public static string Resolve(IEnumerable<string> permissions)
    {
        var values = permissions.ToHashSet(StringComparer.Ordinal);
        // A permission set that matches no current profile (e.g. after a profile gained a permission) is shown as custom,
        // never silently reported as a basic account.
        return All.FirstOrDefault(x => values.SetEquals(x.Permissions))?.Key ?? Custom;
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

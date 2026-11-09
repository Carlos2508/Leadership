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
    public const string CommunicationPublish = "communication.publish";
    /// <summary>Operate the school gate: scan ID cards and mark entries/exits manually.</summary>
    public const string AttendanceGate = "attendance.gate";
    /// <summary>Read gate attendance reports of any student.</summary>
    public const string AttendanceRead = "attendance.read";
    /// <summary>Register/revoke scanner devices and void gate marks.</summary>
    public const string AttendanceManage = "attendance.manage";
    public const string InventoryRead = "inventory.read";
    public const string InventoryManage = "inventory.manage";
    public const string InventoryMove = "inventory.move";
}

public sealed record AccessProfile(string Key, string Label, string[] Permissions);

public sealed class AccountPermission
{
    public Guid UserId { get; set; }
    public string Permission { get; set; } = "";
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionHandler(IdentityDbContext db, Microsoft.Extensions.Options.IOptions<AccountAccessOptions> options)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !await db.AccountPermissions.AsNoTracking().AnyAsync(x => x.UserId == userId && x.Permission == requirement.Permission))
            return;
        // Sensitive permissions configured by the installation only work with two-step verification enabled.
        if (options.Value.RequireMfaForPermissions.Contains(requirement.Permission) &&
            !await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.TwoFactorEnabled))
            return;
        context.Succeed(requirement);
    }
}

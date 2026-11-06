using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.LegacyImport;

namespace SchoolPlatform.Host.IdentityAccess;

// Only used inside the import's shared SQL transaction and IAM application lock.
public sealed class RosterAccountProvisioner(IdentityDbContext db, UserManager<ApplicationUser> users)
{
    internal async Task<Guid> CreatePendingAsync(string username, string roleKey, CancellationToken ct)
    {
        RosterValidation.Require(db.Database.CurrentTransaction is not null, "shared-transaction-required");
        RosterValidation.Require(await db.AccessRoles.AnyAsync(r => r.Key == roleKey && r.IsActive, ct) &&
            !await db.AccessRolePermissions.AnyAsync(r => r.RoleKey == roleKey, ct), "unsafe-account-role");
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = username, IsActive = false };
        var result = await users.CreateAsync(user); // No password; never calls InviteAsync or issues a grant.
        RosterValidation.Require(result.Succeeded, "account-create-conflict");
        db.AccountRoleAssignments.Add(new() { UserId = user.Id, RoleKey = roleKey });
        await db.SaveChangesAsync(ct);
        return user.Id;
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace SchoolPlatform.Host.IdentityAccess;

public sealed class ActiveAccountRequirement : IAuthorizationRequirement;

public sealed class ActiveAccountHandler(IdentityDbContext db)
    : AuthorizationHandler<ActiveAccountRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveAccountRequirement requirement)
    {
        if (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
            await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive))
            context.Succeed(requirement);
    }
}

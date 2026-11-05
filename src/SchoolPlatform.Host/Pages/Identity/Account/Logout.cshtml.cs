using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SchoolPlatform.Host.IdentityAccess;
using System.Security.Claims;

namespace SchoolPlatform.Host.Pages.Identity.Account;

[Authorize]
public sealed class LogoutModel(SignInManager<ApplicationUser> signInManager, IdentityDbContext db, SecurityAudit audit) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        audit.Record("Logout", userId, userId);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        await signInManager.SignOutAsync();
        return LocalRedirect("/");
    }
}

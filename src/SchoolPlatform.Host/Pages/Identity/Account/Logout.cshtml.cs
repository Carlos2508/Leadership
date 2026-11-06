using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SchoolPlatform.Host.IdentityAccess;
using System.Security.Claims;

namespace SchoolPlatform.Host.Pages.Identity.Account;

[Authorize]
public sealed class LogoutModel(SignInFlow flow) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await flow.SignOutAsync(userId, HttpContext.RequestAborted);
        return LocalRedirect("/");
    }
}

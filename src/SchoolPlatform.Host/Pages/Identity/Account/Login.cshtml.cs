using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Host.Pages.Identity.Account;

[AllowAnonymous]
[RequestSizeLimit(16_384)]
public sealed class LoginModel(SignInManager<ApplicationUser> signInManager, IdentityDbContext db, SecurityAudit audit) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Ingresa tu usuario."), StringLength(256)]
        public string Username { get; set; } = "";
        [Required(ErrorMessage = "Ingresa tu contraseña."), StringLength(1024), DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await signInManager.UserManager.FindByNameAsync(Input.Username);
        // MFA enrollment/challenge is not shipped yet; never bypass it for a flagged account.
        var result = user is { IsActive: true, TwoFactorEnabled: false }
            ? await signInManager.CheckPasswordSignInAsync(user, Input.Password, lockoutOnFailure: true)
            : Microsoft.AspNetCore.Identity.SignInResult.NotAllowed;
        audit.Record(result.Succeeded ? "LoginSucceeded" : result.IsLockedOut ? "LoginLockedOut" : "LoginFailed",
            result.Succeeded ? user?.Id : null, user?.Id);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        await transaction.CommitAsync(HttpContext.RequestAborted);
        if (result.Succeeded)
        {
            await signInManager.SignInAsync(user!, isPersistent: false);
            return LocalRedirect(ReturnUrl is not null && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/workspace");
        }
        ModelState.AddModelError(string.Empty, "No pudimos iniciar sesión. Revisa tus datos o solicita ayuda al colegio.");
        return Page();
    }
}

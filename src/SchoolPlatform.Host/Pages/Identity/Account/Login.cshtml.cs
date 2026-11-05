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
        // Accounts with two-step verification get a pending second-factor cookie, never a full session.
        // Identity silently skips the second step when a flagged account has no usable provider; refuse instead of
        // granting a password-only session (the account must be reset by a manager).
        var flaggedWithoutAuthenticator = user is { TwoFactorEnabled: true } &&
            await signInManager.UserManager.GetAuthenticatorKeyAsync(user) is null;
        var result = user is { IsActive: true } && !flaggedWithoutAuthenticator
            ? await signInManager.PasswordSignInAsync(user, Input.Password, isPersistent: false, lockoutOnFailure: true)
            : Microsoft.AspNetCore.Identity.SignInResult.NotAllowed;
        audit.Record(result.Succeeded ? "LoginSucceeded" : result.RequiresTwoFactor ? "LoginSecondFactorRequired"
                : result.IsLockedOut ? "LoginLockedOut" : "LoginFailed",
            result.Succeeded || result.RequiresTwoFactor ? user?.Id : null, user?.Id);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        await transaction.CommitAsync(HttpContext.RequestAborted);
        var target = ReturnUrl is not null && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/workspace";
        if (result.Succeeded) return LocalRedirect(target);
        if (result.RequiresTwoFactor) return RedirectToPage("/Identity/Account/LoginWith2fa", new { ReturnUrl = target });
        ModelState.AddModelError(string.Empty, "No pudimos iniciar sesión. Revisa tus datos o solicita ayuda al colegio.");
        return Page();
    }
}

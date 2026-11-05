using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Host.Pages.Identity.Account;

/// <summary>Second step of the login for accounts with an authenticator. Lockout applies to failed codes.</summary>
[AllowAnonymous]
[RequestSizeLimit(16_384)]
public sealed class LoginWith2faModel(SignInManager<ApplicationUser> signInManager, IdentityDbContext db, SecurityAudit audit) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty(SupportsGet = true)] public bool UseRecovery { get; set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Escribe el código."), StringLength(32)]
        public string Code { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync() =>
        await signInManager.GetTwoFactorAuthenticationUserAsync() is null ? RedirectToPage("/Identity/Account/Login") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return RedirectToPage("/Identity/Account/Login");
        if (!ModelState.IsValid) return Page();
        // Authenticator codes may be typed with spaces; recovery codes keep their dash (Identity compares them exactly).
        var code = UseRecovery ? Input.Code.Trim() : Input.Code.Replace(" ", "");
        Input = new();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = !user.IsActive ? Microsoft.AspNetCore.Identity.SignInResult.NotAllowed
            : UseRecovery ? await signInManager.TwoFactorRecoveryCodeSignInAsync(code)
            : await signInManager.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false);
        audit.Record(result.Succeeded ? (UseRecovery ? "LoginRecoveryCodeUsed" : "LoginSucceeded")
                : result.IsLockedOut ? "LoginLockedOut" : "LoginSecondFactorFailed",
            result.Succeeded ? user.Id : null, user.Id);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        await transaction.CommitAsync(HttpContext.RequestAborted);
        if (result.Succeeded) return LocalRedirect(ReturnUrl is not null && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/workspace");
        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "La cuenta se bloqueó temporalmente por intentos fallidos. Espera unos minutos o solicita ayuda al colegio."
            : "El código no es válido. Revisa la hora de tu teléfono e intenta con el código actual.");
        return Page();
    }
}

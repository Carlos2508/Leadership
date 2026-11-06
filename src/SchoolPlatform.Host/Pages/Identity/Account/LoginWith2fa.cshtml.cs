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
public sealed class LoginWith2faModel(SignInManager<ApplicationUser> signInManager, SignInFlow flow) : PageModel
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
        if (!ModelState.IsValid)
            return await signInManager.GetTwoFactorAuthenticationUserAsync() is null ? RedirectToPage("/Identity/Account/Login") : Page();
        var code = Input.Code;
        Input = new();
        var outcome = await flow.SecondFactorAsync(code, UseRecovery, HttpContext.RequestAborted);
        if (outcome is null) return RedirectToPage("/Identity/Account/Login");
        if (outcome == SignInOutcome.SignedIn) return LocalRedirect(ReturnUrl is not null && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/workspace");
        ModelState.AddModelError(string.Empty, outcome == SignInOutcome.LockedOut
            ? "La cuenta se bloqueó temporalmente por intentos fallidos. Espera unos minutos o solicita ayuda al colegio."
            : "El código no es válido. Revisa la hora de tu teléfono e intenta con el código actual.");
        return Page();
    }
}

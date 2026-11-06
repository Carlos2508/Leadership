using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Host.Pages.Identity.Account;

[AllowAnonymous]
[RequestSizeLimit(16_384)]
public sealed class LoginModel(SignInFlow flow) : PageModel
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
        var outcome = await flow.PasswordAsync(Input.Username, Input.Password, HttpContext.RequestAborted);
        var target = ReturnUrl is not null && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/workspace";
        if (outcome == SignInOutcome.SignedIn) return LocalRedirect(target);
        if (outcome == SignInOutcome.SecondFactorRequired) return RedirectToPage("/Identity/Account/LoginWith2fa", new { ReturnUrl = target });
        ModelState.AddModelError(string.Empty, "No pudimos iniciar sesión. Revisa tus datos o solicita ayuda al colegio.");
        return Page();
    }
}

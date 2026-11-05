using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SchoolPlatform.Host.IdentityAccess;

namespace SchoolPlatform.Host.Pages.Identity.Account;

[AllowAnonymous]
[RequestSizeLimit(16_384)]
public sealed class ActivateModel(AccountAccessService access) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public bool Completed { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Ingresa el código que te entregó el colegio."), StringLength(100)]
        public string Code { get; set; } = "";
        [Required(ErrorMessage = "Ingresa tu nueva contraseña."), StringLength(1024, MinimumLength = 12,
            ErrorMessage = "Usa entre 12 y 1024 caracteres."), DataType(DataType.Password)]
        public string Password { get; set; } = "";
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden."), DataType(DataType.Password)]
        public string Confirmation { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            try
            {
                await access.RedeemAsync(Input.Code, Input.Password, HttpContext.RequestAborted);
                Completed = true;
            }
            catch (AccessOperationException error) { ModelState.AddModelError(string.Empty, error.Message); }
        }
        // Never echo a one-time code or either password into the returned HTML.
        var errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).ToArray();
        Input = new();
        ModelState.Clear();
        foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
        return Page();
    }
}

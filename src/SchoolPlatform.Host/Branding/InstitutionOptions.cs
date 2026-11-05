using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace SchoolPlatform.Host.Branding;

public sealed class InstitutionOptions
{
    public Guid SchoolId { get; set; }
    public string DisplayName { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string CampusLabel { get; set; } = "";
    public string LegalName { get; set; } = "";
    public string Tagline { get; set; } = "";
    public string WelcomeMessage { get; set; } = "";
    public ContactOptions Contact { get; set; } = new();
    public DocumentOptions Documents { get; set; } = new();
    public AssetOptions Assets { get; set; } = new();
    public ThemeOptions Theme { get; set; } = new();
    public LocaleOptions Locale { get; set; } = new();
    public TermOptions Terms { get; set; } = new();
}

public sealed class ContactOptions
{
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Website { get; set; } = "";
}

public sealed class DocumentOptions
{
    public string TaxId { get; set; } = "";
    public string ReceiptFooter { get; set; } = "";
    public string ReportCardFooter { get; set; } = "";
    /// <summary>Back of the student ID card: short institutional rules (optional, from this installation's config).</summary>
    public string[] CardRules { get; set; } = [];
    /// <summary>Back of the student ID card: e.g. what to do if found (optional).</summary>
    public string CardNotice { get; set; } = "";
}

public sealed class AssetOptions
{
    public string MainLogo { get; set; } = "";
    public string LoginLogo { get; set; } = "";
    public string ReceiptLogo { get; set; } = "";
    public string ReportCardLogo { get; set; } = "";
    public string CardLogo { get; set; } = "";
    public string Favicon { get; set; } = "";
}

public sealed class ThemeOptions
{
    public string Deep { get; set; } = "";
    public string Primary { get; set; } = "";
    public string Accent { get; set; } = "";
    public string Surface { get; set; } = "";
}

public sealed class LocaleOptions
{
    public string Culture { get; set; } = "";
    public string TimeZone { get; set; } = "";
    public string Currency { get; set; } = "";
}

public sealed class TermOptions
{
    public string StudentPlural { get; set; } = "";
    public string GuardianPlural { get; set; } = "";
    public string Tuition { get; set; } = "";
    public string Period { get; set; } = "";
}

public sealed class InstitutionValidator(IWebHostEnvironment environment) : IValidateOptions<InstitutionOptions>
{
    public ValidateOptionsResult Validate(string? name, InstitutionOptions options)
    {
        var errors = new List<string>();
        if (options.SchoolId == Guid.Empty) errors.Add("Institution:SchoolId debe ser un UUID estable.");
        foreach (var value in new[] { options.DisplayName, options.ShortName, options.WelcomeMessage,
                     options.Terms.StudentPlural, options.Terms.GuardianPlural, options.Terms.Tuition, options.Terms.Period })
            if (string.IsNullOrWhiteSpace(value)) errors.Add("Falta un nombre, mensaje o término institucional obligatorio.");

        foreach (var path in new[] { options.Assets.MainLogo, options.Assets.LoginLogo, options.Assets.ReceiptLogo,
                     options.Assets.ReportCardLogo, options.Assets.CardLogo, options.Assets.Favicon })
        {
            if (!Regex.IsMatch(path, @"^/branding/[a-zA-Z0-9_-]+\.(png|svg|ico|webp)$") ||
                !File.Exists(Path.Combine(environment.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))))
                errors.Add($"Recurso institucional inexistente o ruta inválida: {path}");
        }
        foreach (var color in new[] { options.Theme.Deep, options.Theme.Primary, options.Theme.Accent, options.Theme.Surface })
            if (!Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$")) errors.Add("Los colores deben usar formato #RRGGBB.");
        try { _ = CultureInfo.GetCultureInfo(options.Locale.Culture); }
        catch (CultureNotFoundException) { errors.Add("Cultura inválida."); }
        if (string.IsNullOrWhiteSpace(options.Locale.Culture)) errors.Add("Falta la cultura.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(options.Locale.TimeZone); }
        catch (TimeZoneNotFoundException) { errors.Add("Zona horaria inválida."); }
        if (!Regex.IsMatch(options.Locale.Currency, "^[A-Z]{3}$")) errors.Add("Moneda inválida; usar código ISO de tres letras.");
        if (options.Contact.Website.Length > 0 &&
            (!Uri.TryCreate(options.Contact.Website, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
            errors.Add("El sitio institucional debe ser una URL HTTPS.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

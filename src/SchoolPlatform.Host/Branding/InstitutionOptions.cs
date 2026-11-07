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
    /// <summary>Academic policy of this school: evaluation scale per stage and whether grades depend on payments.</summary>
    public AcademicPolicyOptions Academic { get; set; } = new();
    /// <summary>Content of the public home page (optional; empty sections are not shown).</summary>
    public PublicSiteOptions Public { get; set; } = new();
}

public sealed class ContactOptions
{
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Website { get; set; } = "";
    /// <summary>International prefix for click-to-call links (e.g. +502). Numbers are shown as written.</summary>
    public string DialPrefix { get; set; } = "";
    /// <summary>Additional numbers with a label (e.g. Secretaría, Dirección).</summary>
    public LabeledPhone[] Phones { get; set; } = [];
    /// <summary>WhatsApp number in international format, digits only (optional).</summary>
    public string WhatsApp { get; set; } = "";
    public string[] OfficeHours { get; set; } = [];
    /// <summary>Text searched in Google Maps for the location (defaults to Address).</summary>
    public string MapQuery { get; set; } = "";
    public SocialOptions Social { get; set; } = new();
}

public sealed class LabeledPhone
{
    public string Label { get; set; } = "";
    public string Number { get; set; } = "";
}

public sealed class SocialOptions
{
    public string Facebook { get; set; } = "";
    public string Instagram { get; set; } = "";
    public string YouTube { get; set; } = "";
    public string TikTok { get; set; } = "";
}

public sealed class PublicSiteOptions
{
    public string AboutTitle { get; set; } = "";
    public string DigitalTitle { get; set; } = "";
    public string DigitalText { get; set; } = "";
    public string QuestionsTitle { get; set; } = "";
    public string ContactTitle { get; set; } = "";
    public string ContactText { get; set; } = "";
    public string HeroTitle { get; set; } = "";
    public string HeroAccent { get; set; } = "";
    public string HeroDescription { get; set; } = "";
    public string MissionTitle { get; set; } = "";
    public string MissionAccent { get; set; } = "";
    public string MissionText { get; set; } = "";
    public string VisionTitle { get; set; } = "";
    public string VisionText { get; set; } = "";
    public string LifeTitle { get; set; } = "";
    public string LifeText { get; set; } = "";
    public string CommunityTitle { get; set; } = "";
    public string CommunityText { get; set; } = "";
    public string AdmissionsTitle { get; set; } = "";
    public PublicItem[] AdmissionSteps { get; set; } = [];
    public PublicItem[] Questions { get; set; } = [];
    public string About { get; set; } = "";
    public PublicItem[] Highlights { get; set; } = [];
    public PublicItem[] Programs { get; set; } = [];
    public string AdmissionsMessage { get; set; } = "";
}

public sealed class PublicItem
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
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
    /// <summary>
    /// Minimum passing score approved by the school (optional). Only when set do report cards color results as passed or
    /// to reinforce; empty means no pass/fail judgement is printed.
    /// </summary>
    public decimal? PassingScore { get; set; }
}

public sealed class AssetOptions
{
    public string HeroImage { get; set; } = "";
    public string StudentImage { get; set; } = "";
    public string CommunityImage { get; set; } = "";
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

public sealed class AcademicPolicyOptions
{
    /// <summary>
    /// When true, families see published grades only if the student is clear at the publication cutoff (previous rule).
    /// Default false: grades and payments are separate; finance pending items are shown apart and never hide grades.
    /// </summary>
    public bool GradesRequireClearance { get; set; }
    /// <summary>Non-numeric scales for some stages (e.g. Preprimaria). Stages without a scale use numeric 0–100.</summary>
    public EvaluationScale[] Scales { get; set; } = [];

    public EvaluationScale? ScaleFor(string? stage) => string.IsNullOrWhiteSpace(stage) ? null :
        Scales.FirstOrDefault(s => s.Stages.Any(x => string.Equals(x.Trim(), stage.Trim(), StringComparison.OrdinalIgnoreCase)));
}

/// <summary>
/// Qualitative scale: each level is stored as its numeric <see cref="ScaleLevel.Value"/> (so revisions, publication and
/// completeness keep working) and shown with its label. Values must be unique and between 0 and 100.
/// </summary>
public sealed class EvaluationScale
{
    public string Name { get; set; } = "";
    public string[] Stages { get; set; } = [];
    public ScaleLevel[] Levels { get; set; } = [];
    public string? LabelFor(decimal? value) => value is null ? null : Levels.FirstOrDefault(l => l.Value == value)?.Label;
}

public sealed class ScaleLevel
{
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal Value { get; set; }
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
                     options.Assets.ReportCardLogo, options.Assets.CardLogo, options.Assets.Favicon }
                 .Concat(new[] { options.Assets.HeroImage, options.Assets.StudentImage, options.Assets.CommunityImage }.Where(x => x.Length > 0)))
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
        if (options.Documents.PassingScore is < 0 or > 100) errors.Add("Documents:PassingScore debe estar entre 0 y 100.");
        foreach (var scale in options.Academic.Scales)
        {
            if (scale.Stages.Length == 0 || scale.Levels.Length is < 2 or > 10)
                errors.Add($"Academic:Scales «{scale.Name}» necesita etapas y entre 2 y 10 niveles.");
            if (scale.Levels.Any(l => l.Value is < 0 or > 100 || string.IsNullOrWhiteSpace(l.Label)) ||
                scale.Levels.Select(l => l.Value).Distinct().Count() != scale.Levels.Length)
                errors.Add($"Academic:Scales «{scale.Name}» tiene niveles sin nombre, repetidos o fuera de 0–100.");
        }
        foreach (var url in new[] { options.Contact.Social.Facebook, options.Contact.Social.Instagram, options.Contact.Social.YouTube,
                     options.Contact.Social.TikTok })
            if (url.Length > 0 && (!Uri.TryCreate(url, UriKind.Absolute, out var social) || social.Scheme != "https"))
                errors.Add("Las redes sociales deben ser URLs HTTPS.");
        if (options.Contact.WhatsApp.Length > 0 && !Regex.IsMatch(options.Contact.WhatsApp, "^[0-9]{8,15}$"))
            errors.Add("WhatsApp debe tener solo dígitos con código de país (8 a 15).");
        if (options.Contact.DialPrefix.Length > 0 && !Regex.IsMatch(options.Contact.DialPrefix, @"^\+[0-9]{1,4}$"))
            errors.Add("DialPrefix debe tener la forma +502.");
        if (options.Contact.Phones.Any(x => string.IsNullOrWhiteSpace(x.Number) || x.Number.Length > 40 || x.Label.Length > 40))
            errors.Add("Cada teléfono adicional necesita número (y etiqueta corta).");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

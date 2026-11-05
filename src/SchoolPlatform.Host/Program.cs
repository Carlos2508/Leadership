using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.Preview;

var hostArgs = args.Where(x => x is not "--initialize-database" and not "--create-operator" and not "--provision-owner").ToArray();
var builder = WebApplication.CreateBuilder(hostArgs);
var configDirectory = Path.Combine(AppContext.BaseDirectory, "config");
builder.Configuration.AddJsonFile(Path.Combine(configDirectory, "institution.json"), optional: false, reloadOnChange: false)
    .AddJsonFile(Path.Combine(configDirectory, "institution.local.json"), optional: true, reloadOnChange: false)
    .AddEnvironmentVariables().AddCommandLine(hostArgs);
builder.Services.AddSingleton<IValidateOptions<InstitutionOptions>, InstitutionValidator>();
builder.Services.AddOptions<InstitutionOptions>().BindConfiguration("Institution").ValidateOnStart();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SecurityAudit>();
builder.Services.AddScoped<AccountAccessService>();
builder.Services.AddOptions<AccountAccessOptions>().BindConfiguration("AccountAccess")
    .Validate(x => x.ActivationMinutes is >= 1 and <= 4320 && x.RecoveryMinutes is >= 1 and <= 60,
        "Vigencia de códigos fuera del rango permitido.").ValidateOnStart();
builder.Services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("SchoolDatabase") ?? throw new InvalidOperationException("Falta ConnectionStrings:SchoolDatabase.")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = false;
}).AddEntityFrameworkStores<IdentityDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "SchoolPlatform.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Identity/Account/Login";
    // Always compare the durable stamp, including two requests in the same clock tick.
    // The default timed validator can skip when elapsed time is exactly zero.
    options.Events.OnValidatePrincipal = async context =>
    {
        var manager = context.HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
        if (await manager.ValidateSecurityStampAsync(context.Principal) is null)
        {
            context.RejectPrincipal();
            await manager.SignOutAsync();
        }
    };
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 401;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy("ActiveAccount", policy => policy.RequireAuthenticatedUser().AddRequirements(new ActiveAccountRequirement()));
    foreach (var permission in AccessProfiles.All.SelectMany(x => x.Permissions).Distinct())
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new ActiveAccountRequirement(), new PermissionRequirement(permission)));
});
builder.Services.AddScoped<IAuthorizationHandler, ActiveAccountHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddRazorPages();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("identity-forms", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new()
        {
            PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddHealthChecks();

var app = builder.Build();
if (args.Contains("--initialize-database") || args.Contains("--create-operator") || args.Contains("--provision-owner"))
{
    await DevelopmentSetup.RunAsync(app, args.Contains("--create-operator") || args.Contains("--provision-owner"),
        provisionOnly: args.Contains("--provision-owner"));
    return;
}
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/Identity"))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStaticFiles();
var webDirectory = Path.Combine(app.Environment.WebRootPath, "app");
if (Directory.Exists(webDirectory))
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webDirectory) });
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
app.MapRazorPages().RequireRateLimiting("identity-forms");
app.MapAccountAccess();
app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapGet("/branding/theme.css", (IOptions<InstitutionOptions> options) =>
{
    var theme = options.Value.Theme;
    return Results.Text($":root{{--deep:{theme.Deep};--primary:{theme.Primary};--accent:{theme.Accent};--surface:{theme.Surface};}}", "text/css");
}).AllowAnonymous();
app.MapGet("/api/v1/antiforgery", (Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
    Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken })).RequireAuthorization();
app.MapGet("/api/v1/bootstrap", (IOptions<InstitutionOptions> options, IWebHostEnvironment env) =>
{
    var institution = options.Value;
    // Explicit public projection: never serialize IConfiguration, connection strings or document/tax settings.
    return Results.Ok(new
    {
        institution.DisplayName, institution.ShortName, institution.CampusLabel, institution.Tagline,
        institution.WelcomeMessage, institution.Contact, institution.Assets, institution.Theme,
        institution.Locale, institution.Terms, previewEnabled = env.IsDevelopment()
    });
}).AllowAnonymous();
app.MapGet("/api/v1/session", async (ClaimsPrincipal user, IOptions<InstitutionOptions> options, IdentityDbContext db, CancellationToken ct) =>
{
    var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var permissions = await db.AccountPermissions.AsNoTracking().Where(x => x.UserId == userId)
        .Select(x => x.Permission).ToArrayAsync(ct);
    return Results.Ok(new { userId, username = user.Identity!.Name, schoolId = options.Value.SchoolId, permissions });
})
    .RequireAuthorization("ActiveAccount");
if (app.Environment.IsDevelopment())
    app.MapGet("/api/v1/preview", () => Results.Ok(PreviewData.Create())).AllowAnonymous();
app.Map("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("app/index.html").AllowAnonymous();
app.Run();

public partial class Program;

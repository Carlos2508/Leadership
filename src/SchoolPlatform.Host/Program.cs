using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.Preview;
using SchoolPlatform.Host.SchoolData;

var hostArgs = args.Where(x => x is not "--initialize-database" and not "--create-operator" and not "--provision-owner" and not "--import-people" and not "--import-roster" and not "--migration-assistant").ToArray();
var builder = WebApplication.CreateBuilder(hostArgs);
// Console import errors must not leak SQL exception values through framework logging.
if (args.Contains("--import-roster") || args.Contains("--migration-assistant")) builder.Logging.ClearProviders();
var configDirectory = Path.Combine(AppContext.BaseDirectory, "config");
builder.Configuration.AddJsonFile(Path.Combine(configDirectory, "institution.json"), optional: false, reloadOnChange: false)
    .AddJsonFile(Path.Combine(configDirectory, "institution.local.json"), optional: true, reloadOnChange: false)
    .AddEnvironmentVariables().AddCommandLine(hostArgs);
if (args.Contains("--migration-assistant")) builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["MigrationAssistant:Enabled"] = "true" });
builder.Services.AddSingleton<IValidateOptions<InstitutionOptions>, InstitutionValidator>();
builder.Services.AddOptions<InstitutionOptions>().BindConfiguration("Institution").ValidateOnStart();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SchoolPlatform.Host.Common.BadRequestExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SecurityAudit>();
builder.Services.AddScoped<AccountAccessService>();
builder.Services.AddScoped<AccessRoleService>();
builder.Services.AddScoped<MfaService>();
builder.Services.AddScoped<SignInFlow>();
builder.Services.AddOptions<AccountAccessOptions>().BindConfiguration("AccountAccess")
    .Validate(x => x.ActivationMinutes is >= 1 and <= 4320 && x.RecoveryMinutes is >= 1 and <= 60,
        "Vigencia de códigos fuera del rango permitido.")
    .Validate(x => x.RequireMfaForPermissions.All(p => PermissionCatalog.All.Any(d => d.Key == p)),
        "AccountAccess:RequireMfaForPermissions contiene un permiso desconocido.").ValidateOnStart();
// Resolve the connection lazily from the final configuration: hosts and test factories may add sources after this line.
static string SchoolDatabase(IServiceProvider services) =>
    services.GetRequiredService<IConfiguration>().GetConnectionString("SchoolDatabase")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:SchoolDatabase.");
builder.Services.AddDbContext<IdentityDbContext>((services, options) => options.UseSqlServer(SchoolDatabase(services)));
builder.Services.AddDbContext<SchoolDbContext>((services, options) => options.UseSqlServer(SchoolDatabase(services),
    sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "school")));
builder.Services.AddScoped<SchoolAudit>();
builder.Services.AddScoped<PeopleService>();
builder.Services.AddScoped<SchoolPlatform.Host.LegacyImport.LegacyPeopleImporter>();
builder.Services.AddScoped<RosterAccountProvisioner>();
builder.Services.AddScoped<SchoolPlatform.Host.LegacyImport.InitialRosterImporter>();
builder.Services.AddOptions<SchoolPlatform.Host.LegacyImport.MigrationAssistantOptions>().BindConfiguration("MigrationAssistant");
builder.Services.AddSingleton<SchoolPlatform.Host.LegacyImport.AssistantStorage>();
builder.Services.AddScoped<SchoolPlatform.Host.LegacyImport.IAssistantSource, SchoolPlatform.Host.LegacyImport.AssistantSource>();
builder.Services.AddScoped<SchoolPlatform.Host.LegacyImport.IAssistantBackup, SchoolPlatform.Host.LegacyImport.AssistantBackup>();
builder.Services.AddScoped<SchoolPlatform.Host.LegacyImport.MigrationAssistantService>();
builder.Services.AddScoped<SchoolPlatform.Host.Academic.AcademicService>();
builder.Services.AddScoped<SchoolPlatform.Host.Academic.EnrollmentService>();
builder.Services.AddScoped<SchoolPlatform.Host.Academic.CourseService>();
builder.Services.AddScoped<SchoolPlatform.Host.Finance.FinanceSetupService>();
builder.Services.AddScoped<SchoolPlatform.Host.Finance.ChargeService>();
builder.Services.AddScoped<SchoolPlatform.Host.Finance.PaymentService>();
builder.Services.AddScoped<SchoolPlatform.Host.Finance.ClearanceService>();
builder.Services.AddScoped<SchoolPlatform.Host.Grades.GradeService>();
builder.Services.AddScoped<SchoolPlatform.Host.Common.DashboardService>();
builder.Services.AddScoped<SchoolPlatform.Host.Grades.GradeReportService>();
builder.Services.AddSingleton<SchoolPlatform.Host.Documents.BrandAssets>();
builder.Services.AddSingleton<SchoolPlatform.Host.Documents.PdfDocuments>();
builder.Services.AddOptions<SchoolPlatform.Host.Gate.GateOptions>().Bind(builder.Configuration.GetSection("Gate"))
    .Validate(x => x.DuplicateSeconds is >= 5 and <= 3600 && x.AutoExitAfterMinutes is >= 1 and <= 720 && x.MaxPhotoBytes is > 0 and <= 10 * 1024 * 1024 &&
        (x.PublicUrl.Length == 0 || (Uri.TryCreate(x.PublicUrl, UriKind.Absolute, out var url) && url.Scheme is "https" or "http")),
        "Configuración Gate inválida.")
    .ValidateOnStart();
builder.Services.AddScoped<SchoolPlatform.Host.Gate.GateService>();
builder.Services.AddScoped<SchoolPlatform.Host.Gate.CardService>();
builder.Services.AddScoped<SchoolPlatform.Host.Gate.GateDeviceService>();
builder.Services.AddScoped<SchoolPlatform.Host.Gate.CourseServiceClock>();
builder.Services.AddAuthentication().AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
    SchoolPlatform.Host.Gate.GateDeviceAuthenticationHandler>(SchoolPlatform.Host.Gate.GateDeviceAuthenticationHandler.SchemeName, null);
builder.Services.AddScoped<SchoolPlatform.Host.Learning.LearningService>();
builder.Services.AddScoped<SchoolPlatform.Host.Learning.SubmissionFileService>();
builder.Services.AddOptions<SchoolPlatform.Host.Learning.StorageOptions>().BindConfiguration("Storage")
    .Validate(x => x.MaxFileBytes is > 0 and <= 50 * 1024 * 1024 && x.MaxFilesPerSubmission is > 0 and <= 20 && x.Root.Length > 0,
        "Configuración de almacenamiento inválida.").ValidateOnStart();
builder.Services.AddScoped<SchoolPlatform.Host.Attendance.AttendanceService>();
builder.Services.AddScoped<SchoolPlatform.Host.Communication.CommunicationService>();
builder.Services.AddScoped<SchoolPlatform.Host.Chat.ChatService>();
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = false;
}).AddEntityFrameworkStores<IdentityDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    // Same Identity scheme/SignInFlow, isolated name so opening the local tool does not replace another localhost session.
    options.Cookie.Name = args.Contains("--migration-assistant") ? "SchoolPlatform.MigrationSession" : "SchoolPlatform.Session";
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
    // Registered scanner devices: only their bearer scheme is evaluated (session cookies are ignored on these endpoints).
    options.AddPolicy("GateDevice", policy => policy.AddAuthenticationSchemes(SchoolPlatform.Host.Gate.GateDeviceAuthenticationHandler.SchemeName)
        .RequireClaim(SchoolPlatform.Host.Gate.GateDeviceAuthenticationHandler.DeviceClaim));
    foreach (var permission in PermissionCatalog.All.Select(x => x.Key))
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new ActiveAccountRequirement(), new PermissionRequirement(permission)));
});
builder.Services.AddScoped<IAuthorizationHandler, ActiveAccountHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddRazorPages();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("gate-device", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new()
        {
            PermitLimit = 240, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
    options.AddPolicy("identity-forms", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new()
        {
            PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
    options.AddPolicy("chat-send", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new()
        {
            PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ["application/json", "application/problem+json", "text/plain", "text/csv"];
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(x => x.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(x => x.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.AddHealthChecks().AddCheck<SchoolPlatform.Host.Common.ReadinessCheck>("sql", tags: ["ready"]);

var app = builder.Build();
if (args.Contains("--import-roster"))
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var expectedCatalog = app.Configuration["Roster:Database"];
        var actualCatalog = scope.ServiceProvider.GetRequiredService<SchoolDbContext>().Database.GetDbConnection().Database;
        if (string.IsNullOrWhiteSpace(expectedCatalog) || !string.Equals(expectedCatalog, actualCatalog, StringComparison.OrdinalIgnoreCase))
            throw new SchoolPlatform.Host.LegacyImport.RosterImportException("target-database-confirmation-required");
        var input = app.Configuration["Roster:File"] ?? throw new SchoolPlatform.Host.LegacyImport.RosterImportException("file-required");
        if (new FileInfo(input).Length is 0 or > 20_000_000) throw new SchoolPlatform.Host.LegacyImport.RosterImportException("invalid-file-size");
        var credentials = new SchoolPlatform.Host.LegacyImport.RosterOperator(
            app.Configuration["Roster:Actor"] ?? "",
            Environment.GetEnvironmentVariable("ROSTER_OPERATOR_PASSWORD") ?? "",
            Environment.GetEnvironmentVariable("ROSTER_OPERATOR_TOTP"));
        var report = await scope.ServiceProvider.GetRequiredService<SchoolPlatform.Host.LegacyImport.InitialRosterImporter>()
            .RunAsync(await File.ReadAllBytesAsync(input), app.Configuration["Roster:Mode"] ?? "plan", app.Configuration["Roster:PlanSha256"], credentials);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report, SchoolPlatform.Host.LegacyImport.RosterJson.Options));
        // Explicit reconcile reports subsequent changes; it never undoes activations or business edits.
        if (report.Observations.Length > 0) Environment.ExitCode = 2;
    }
    catch (Exception exception)
    {
        // SQL uniqueness/errors can contain personal values; never print an exception/stack or the input document.
        Console.Error.WriteLine(exception switch
        {
            SchoolPlatform.Host.LegacyImport.RosterImportException expected => expected.Code,
            SchoolPlatform.Host.Common.OperationException expected => expected.Code,
            _ => "roster-operation-failed"
        });
        Environment.ExitCode = 1;
    }
    return;
}
if (args.Contains("--initialize-database") || args.Contains("--create-operator") || args.Contains("--provision-owner"))
{
    await DevelopmentSetup.RunAsync(app, args.Contains("--create-operator") || args.Contains("--provision-owner"),
        provisionOnly: args.Contains("--provision-owner"));
    return;
}
if (args.Contains("--import-people"))
{
    // MIG-001a console tool: never exposed over HTTP. Import:Mode=plan (default) or apply with Import:Sha256 and Import:Actor.
    await using var scope = app.Services.CreateAsyncScope();
    var school = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
    if ((await school.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Aplica primero las migraciones de esta instalación.");
    var file = app.Configuration["Import:File"] ?? throw new InvalidOperationException("Define Import:File con la ruta del staging revisado.");
    var applyImport = string.Equals(app.Configuration["Import:Mode"], "apply", StringComparison.OrdinalIgnoreCase);
    var plan = await scope.ServiceProvider.GetRequiredService<SchoolPlatform.Host.LegacyImport.LegacyPeopleImporter>()
        .RunAsync(await File.ReadAllBytesAsync(file), applyImport, app.Configuration["Import:Sha256"], app.Configuration["Import:Actor"], default);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(plan, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
    return;
}
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // A client that navigates away cancels its SQL command; that is not a server failure worth an error log.
    // Malformed request bodies are client errors answered with 400 by BadRequestExceptionHandler.
    SuppressDiagnosticsCallback = context => context.HttpContext.RequestAborted.IsCancellationRequested ||
        context.Exception is BadHttpRequestException
});
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; script-src 'self'; font-src 'self'; connect-src 'self'; frame-src https://www.google.com; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/Identity"))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});
// Branding images and Identity styles change rarely: cache for an hour, then revalidate.
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = x => x.Context.Response.Headers.CacheControl = "public, max-age=3600" });
var webDirectory = Path.Combine(app.Environment.WebRootPath, "app");
if (Directory.Exists(webDirectory)) app.UseSpaAssets(webDirectory);
// API JSON is compressed on the fly (fastest level). The antiforgery token endpoint and Identity pages are excluded so
// a secret is never compressed together with attacker-influenced input (BREACH).
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/api/v1/antiforgery"),
    branch => branch.UseResponseCompression());
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
app.MapRazorPages().RequireRateLimiting("identity-forms");
app.MapAccountAccess();
app.MapAppAuth();
app.MapPeople();
SchoolPlatform.Host.LegacyImport.MigrationAssistantEndpoints.MapMigrationAssistant(app);
SchoolPlatform.Host.Academic.AcademicEndpoints.MapAcademic(app);
SchoolPlatform.Host.Academic.AcademicEndpoints.MapCourses(app);
SchoolPlatform.Host.Finance.FinanceEndpoints.MapFinance(app);
SchoolPlatform.Host.Grades.GradeEndpoints.MapGrades(app);
SchoolPlatform.Host.Learning.LearningEndpoints.MapLearning(app);
SchoolPlatform.Host.Attendance.AttendanceEndpoints.MapAttendance(app);
SchoolPlatform.Host.Communication.CommunicationEndpoints.MapCommunication(app);
SchoolPlatform.Host.Chat.ChatEndpoints.MapChat(app);
SchoolPlatform.Host.Documents.DocumentEndpoints.MapDocuments(app);
SchoolPlatform.Host.Gate.GateEndpoints.MapGate(app);
SchoolPlatform.Host.Common.DashboardEndpoints.MapDashboard(app);
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
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
        institution.Locale, institution.Terms, institution.Public, previewEnabled = env.IsDevelopment()
    });
}).AllowAnonymous();
app.MapGet("/api/v1/session", async (ClaimsPrincipal user, IOptions<InstitutionOptions> options, IdentityDbContext db,
    SchoolDbContext school, MfaService mfa, CancellationToken ct) =>
{
    var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var permissions = await db.AccountPermissions.AsNoTracking().Where(x => x.UserId == userId)
        .Select(x => x.Permission).ToArrayAsync(ct);
    var twoFactor = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.TwoFactorEnabled).SingleAsync(ct);
    // Permissions that stay unusable until two-step verification is enabled, so the UI can explain instead of failing.
    var mfaPending = twoFactor ? [] : await mfa.RequiredByAsync(userId, ct);
    // Name of the linked person (if any) for the avatar and greeting; accounts without a person show their username.
    var person = await school.AccountLinks.AsNoTracking().Where(x => x.UserId == userId && x.UnlinkedAt == null)
        .Join(school.Persons, l => l.PersonId, p => p.Id, (l, p) => new { p.Id, p.GivenNames, p.FamilyNames, p.PreferredName, p.Status,
            IsStudent = school.Students.Any(s => s.PersonId == p.Id), IsGuardian = school.Guardians.Any(g => g.PersonId == p.Id),
            IsStaff = school.Staff.Any(s => s.PersonId == p.Id) }).FirstOrDefaultAsync(ct);
    var roleLabel = await db.AccountRoleAssignments.Where(a => a.UserId == userId)
        .Join(db.AccessRoles, a => a.RoleKey, r => r.Key, (a, r) => r.Label).FirstOrDefaultAsync(ct);
    return Results.Ok(new
    {
        userId, username = user.Identity!.Name, schoolId = options.Value.SchoolId,
        displayName = person is null ? null : SchoolPlatform.Host.People.PeopleService.Display(person.GivenNames, person.FamilyNames, person.PreferredName),
        studentId = person is { IsStudent: true, Status: SchoolPlatform.Host.People.PersonStatus.Active } ? (Guid?)person.Id : null,
        schoolProfile = person?.IsStudent == true ? "Estudiante" : person?.IsStaff == true ? "Docente y personal" : person?.IsGuardian == true ? "Familia" : roleLabel,
        permissions = permissions.Except(mfaPending).ToArray(), twoFactorEnabled = twoFactor, mfaPending
    });
})
    .RequireAuthorization("ActiveAccount");
if (app.Environment.IsDevelopment())
    app.MapGet("/api/v1/preview", () => Results.Ok(PreviewData.Create())).AllowAnonymous();
app.Map("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("app/index.html", new StaticFileOptions
{
    // The shell must always revalidate so a deployment's new hashed bundles are used immediately.
    OnPrepareResponse = x => x.Context.Response.Headers.CacheControl = "no-cache"
}).AllowAnonymous();
app.Run();

public partial class Program;

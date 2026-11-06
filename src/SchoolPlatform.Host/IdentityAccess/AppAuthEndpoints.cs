using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using SchoolPlatform.Host.Common;

namespace SchoolPlatform.Host.IdentityAccess;

/// <summary>
/// JSON sign-in for the native app (ADR-014). Same cookie session as the web: every business endpoint keeps requiring
/// ActiveAccount plus its permission. Writes need the antiforgery header like any other API call; the anonymous token
/// endpoint lets the app obtain it before signing in. Rate limited with the identity forms policy.
/// </summary>
public static class AppAuthEndpoints
{
    public sealed record LoginRequest([property: Required, StringLength(256)] string Username,
        [property: Required, StringLength(1024)] string Password);
    public sealed record SecondFactorRequest([property: Required, StringLength(32)] string Code, bool Recovery = false);

    public static void MapAppAuth(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").RequireRateLimiting("identity-forms");
        // Explicit opt-in after the complete SignInFlow (including MFA). Same revocable Identity ticket;
        // no password/refresh token is issued. Native clients seal it in biometric Keychain/Keystore storage.
        async Task<IResult> DeviceSession(HttpContext context, SecurityAudit audit, IdentityDbContext db, bool remember, CancellationToken ct)
        {
            var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (!result.Succeeded || result.Principal is null || result.Properties is null) return Results.Unauthorized();
            var properties = result.Properties;
            properties.IsPersistent = remember;
            properties.AllowRefresh = !remember;
            properties.IssuedUtc = DateTimeOffset.UtcNow;
            properties.ExpiresUtc = remember ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddMinutes(30);
            audit.Record(remember ? "DeviceSessionRemembered" : "DeviceSessionForgotten", People.PeopleEndpoints.Actor(context.User), People.PeopleEndpoints.Actor(context.User));
            await db.SaveChangesAsync(ct);
            await context.SignInAsync(IdentityConstants.ApplicationScheme, result.Principal, properties);
            return Results.NoContent();
        }
        auth.MapPost("/remember-device", (HttpContext context, SecurityAudit audit, IdentityDbContext db, CancellationToken ct) => DeviceSession(context, audit, db, true, ct))
            .RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        auth.MapPost("/forget-device", (HttpContext context, SecurityAudit audit, IdentityDbContext db, CancellationToken ct) => DeviceSession(context, audit, db, false, ct))
            .RequireAuthorization("ActiveAccount").AddEndpointFilter<ApiWriteFilter>();
        auth.MapGet("/antiforgery", (IAntiforgery antiforgery, HttpContext context) =>
            Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken })).AllowAnonymous();

        var forms = auth.MapGroup("").AllowAnonymous().AddEndpointFilter<ApiWriteFilter>();
        forms.MapPost("/login", async (LoginRequest request, SignInFlow flow, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
                return Results.Problem(statusCode: 400, title: "Ingresa tu usuario y contraseña.");
            return Outcome(await flow.PasswordAsync(request.Username.Trim(), request.Password, ct));
        });
        forms.MapPost("/login/second-factor", async (SecondFactorRequest request, SignInFlow flow, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Code)) return Results.Problem(statusCode: 400, title: "Escribe el código.");
            var outcome = await flow.SecondFactorAsync(request.Code, request.Recovery, ct);
            return outcome is null
                ? Results.Problem(statusCode: 401, title: "El ingreso venció. Vuelve a escribir tu usuario y contraseña.",
                    extensions: new Dictionary<string, object?> { ["code"] = "second-factor-expired" })
                : outcome == SignInOutcome.Failed
                    ? Results.Problem(statusCode: 401, title: "El código no es válido. Revisa la hora de tu teléfono e intenta con el código actual.",
                        extensions: new Dictionary<string, object?> { ["code"] = "second-factor-failed" })
                    : Outcome(outcome.Value);
        });
        auth.MapPost("/logout", async (ClaimsPrincipal user, SignInFlow flow, CancellationToken ct) =>
        {
            await flow.SignOutAsync(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
            return Results.NoContent();
        }).RequireAuthorization().AddEndpointFilter<ApiWriteFilter>();
    }

    // Failures share one message: the response never reveals whether the username exists or is inactive.
    private static IResult Outcome(SignInOutcome outcome) => outcome switch
    {
        SignInOutcome.SignedIn => Results.Ok(new { status = "signed-in" }),
        SignInOutcome.SecondFactorRequired => Results.Ok(new { status = "second-factor" }),
        SignInOutcome.LockedOut => Results.Problem(statusCode: 423,
            title: "La cuenta se bloqueó temporalmente por intentos fallidos. Espera unos minutos o solicita ayuda al colegio.",
            extensions: new Dictionary<string, object?> { ["code"] = "locked-out" }),
        _ => Results.Problem(statusCode: 401, title: "No pudimos iniciar sesión. Revisa tus datos o solicita ayuda al colegio.",
            extensions: new Dictionary<string, object?> { ["code"] = "sign-in-failed" }),
    };
}

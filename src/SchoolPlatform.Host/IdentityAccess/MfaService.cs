using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;
using SchoolPlatform.Host.Branding;
using SchoolPlatform.Host.Common;

namespace SchoolPlatform.Host.IdentityAccess;

public sealed record MfaStatus(bool Enabled, int RecoveryCodesLeft, string[] RequiredBy);
public sealed record MfaSetup(string SharedKey, string AuthenticatorUri, string QrSvg);
public sealed record MfaCodeRequest(string Code);
public sealed record RecoveryCodes(string[] Codes);

/// <summary>
/// Self-service authenticator (TOTP) enrollment with one-time recovery codes. Codes are shown once; Identity stores
/// them protected. Every change is audited in the same transaction and refreshes the caller's own cookie.
/// </summary>
public sealed class MfaService(IdentityDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    SecurityAudit audit, IOptions<AccountAccessOptions> options, IOptions<InstitutionOptions> institution, UrlEncoder encoder)
{
    public async Task<MfaStatus> StatusAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var user = await Current(principal);
        return new(user.TwoFactorEnabled, user.TwoFactorEnabled ? await users.CountRecoveryCodesAsync(user) : 0,
            await RequiredByAsync(user.Id, ct));
    }

    /// <summary>Permissions held by the account that the installation requires to be protected with MFA.</summary>
    public async Task<string[]> RequiredByAsync(Guid userId, CancellationToken ct)
    {
        var required = options.Value.RequireMfaForPermissions;
        if (required.Length == 0) return [];
        return await db.AccountPermissions.AsNoTracking().Where(x => x.UserId == userId && required.Contains(x.Permission))
            .Select(x => x.Permission).ToArrayAsync(ct);
    }

    public async Task<MfaSetup> SetupAsync(ClaimsPrincipal principal)
    {
        var user = await Current(principal);
        if (user.TwoFactorEnabled) throw new OperationException(409, "mfa-enabled", "La verificación en dos pasos ya está activa.");
        // A fresh key for every enrollment attempt; nothing is enabled until a valid code confirms it.
        Check(await users.ResetAuthenticatorKeyAsync(user));
        // Resetting the key rotates the security stamp; keep the caller's own session valid.
        await signIn.RefreshSignInAsync(user);
        var key = await users.GetAuthenticatorKeyAsync(user) ?? throw new InvalidOperationException("Authenticator key unavailable.");
        var issuer = institution.Value.ShortName;
        var uri = $"otpauth://totp/{encoder.Encode(issuer)}:{encoder.Encode(user.UserName!)}?secret={key}&issuer={encoder.Encode(issuer)}&digits=6";
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(data).GetGraphic(5);
        return new(Format(key), uri, svg);
    }

    public async Task<RecoveryCodes> EnableAsync(ClaimsPrincipal principal, MfaCodeRequest request, CancellationToken ct)
    {
        var user = await Current(principal);
        if (user.TwoFactorEnabled) throw new OperationException(409, "mfa-enabled", "La verificación en dos pasos ya está activa.");
        if (!await ValidTotpAsync(user, request.Code)) throw InvalidCode();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Check(await users.SetTwoFactorEnabledAsync(user, true));
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10) ?? [];
        audit.Record("MfaEnabled", user.Id, user.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await signIn.RefreshSignInAsync(user);
        return new(codes.ToArray());
    }

    public async Task<RecoveryCodes> RegenerateAsync(ClaimsPrincipal principal, MfaCodeRequest request, CancellationToken ct)
    {
        var user = await Current(principal);
        if (!user.TwoFactorEnabled) throw new OperationException(409, "mfa-disabled", "Activa primero la verificación en dos pasos.");
        if (!await ValidTotpAsync(user, request.Code)) throw InvalidCode();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10) ?? [];
        audit.Record("MfaRecoveryCodesRegenerated", user.Id, user.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await signIn.RefreshSignInAsync(user);
        return new(codes.ToArray());
    }

    public async Task DisableAsync(ClaimsPrincipal principal, MfaCodeRequest request, CancellationToken ct)
    {
        var user = await Current(principal);
        if (!user.TwoFactorEnabled) return;
        if ((await RequiredByAsync(user.Id, ct)).Length > 0)
            throw new OperationException(409, "mfa-required", "Tu perfil de acceso requiere la verificación en dos pasos; no puede desactivarse.");
        if (!await ValidTotpAsync(user, request.Code)) throw InvalidCode();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Check(await users.SetTwoFactorEnabledAsync(user, false));
        Check(await users.ResetAuthenticatorKeyAsync(user));
        audit.Record("MfaDisabled", user.Id, user.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await signIn.RefreshSignInAsync(user);
    }

    private async Task<bool> ValidTotpAsync(ApplicationUser user, string? code)
    {
        var value = (code ?? "").Replace(" ", "").Replace("-", "");
        return value.Length == 6 && value.All(char.IsAsciiDigit) &&
            await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, value);
    }

    private async Task<ApplicationUser> Current(ClaimsPrincipal principal) =>
        await users.GetUserAsync(principal) ?? throw new OperationException(401, "unauthenticated", "Tu sesión terminó.");

    private static string Format(string key)
    {
        var text = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4) text.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        return text.ToString().TrimEnd().ToLowerInvariant();
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new OperationException(409, "account-changed", "No pudimos actualizar tu cuenta. Recarga e intenta nuevamente.");
    }

    private static OperationException InvalidCode() =>
        new(400, "invalid-mfa-code", "El código no es válido. Revisa la hora de tu teléfono y escribe el código actual de 6 dígitos.");
}

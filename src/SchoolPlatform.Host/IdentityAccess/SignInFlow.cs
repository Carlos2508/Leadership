using Microsoft.AspNetCore.Identity;

namespace SchoolPlatform.Host.IdentityAccess;

public enum SignInOutcome { SignedIn, SecondFactorRequired, LockedOut, Failed }

/// <summary>
/// Password and second-factor sign-in shared by the web forms and the native app endpoints (ADR-014). Both issue the
/// same Identity session cookie, so permissions, MFA, security stamps and audit stay identical across clients.
/// </summary>
public sealed class SignInFlow(SignInManager<ApplicationUser> signInManager, IdentityDbContext db, SecurityAudit audit)
{
    public async Task<SignInOutcome> PasswordAsync(string username, string password, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await signInManager.UserManager.FindByNameAsync(username);
        // Accounts with two-step verification get a pending second-factor cookie, never a full session.
        // Identity silently skips the second step when a flagged account has no usable provider; refuse instead of
        // granting a password-only session (the account must be reset by a manager).
        var flaggedWithoutAuthenticator = user is { TwoFactorEnabled: true } &&
            await signInManager.UserManager.GetAuthenticatorKeyAsync(user) is null;
        var result = user is { IsActive: true } && !flaggedWithoutAuthenticator
            ? await signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: true)
            : SignInResult.NotAllowed;
        audit.Record(result.Succeeded ? "LoginSucceeded" : result.RequiresTwoFactor ? "LoginSecondFactorRequired"
                : result.IsLockedOut ? "LoginLockedOut" : "LoginFailed",
            result.Succeeded || result.RequiresTwoFactor ? user?.Id : null, user?.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(result);
    }

    /// <summary>Null when there is no pending second step (expired or never started).</summary>
    public async Task<SignInOutcome?> SecondFactorAsync(string rawCode, bool recovery, CancellationToken ct)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return null;
        // Authenticator codes may be typed with spaces; recovery codes keep their dash (Identity compares them exactly).
        var code = recovery ? rawCode.Trim() : rawCode.Replace(" ", "");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = !user.IsActive ? SignInResult.NotAllowed
            : recovery ? await signInManager.TwoFactorRecoveryCodeSignInAsync(code)
            : await signInManager.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false);
        audit.Record(result.Succeeded ? (recovery ? "LoginRecoveryCodeUsed" : "LoginSucceeded")
                : result.IsLockedOut ? "LoginLockedOut" : "LoginSecondFactorFailed",
            result.Succeeded ? user.Id : null, user.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(result);
    }

    public async Task SignOutAsync(Guid userId, CancellationToken ct)
    {
        audit.Record("Logout", userId, userId);
        await db.SaveChangesAsync(ct);
        await signInManager.SignOutAsync();
    }

    private static SignInOutcome Map(SignInResult result) =>
        result.Succeeded ? SignInOutcome.SignedIn
        : result.RequiresTwoFactor ? SignInOutcome.SecondFactorRequired
        : result.IsLockedOut ? SignInOutcome.LockedOut
        : SignInOutcome.Failed;
}

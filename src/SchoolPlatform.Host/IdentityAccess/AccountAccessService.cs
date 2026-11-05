using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace SchoolPlatform.Host.IdentityAccess;

public sealed class AccountAccessService(IdentityDbContext db, UserManager<ApplicationUser> users,
    SecurityAudit audit, TimeProvider time, IOptions<AccountAccessOptions> options)
{
    public async Task<PageResult<AccountSummary>> ListAsync(string? search, int page, CancellationToken ct = default)
    {
        if (page < 1 || page > 10000 || search?.Length > 100) throw Invalid("Consulta inválida.");
        const int pageSize = 20;
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.UserName!.Contains(search.Trim()));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.UserName).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.UserName, x.IsActive, HasPassword = x.PasswordHash != null, x.ConcurrencyStamp }).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var permissions = await db.AccountPermissions.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToArrayAsync(ct);
        var now = time.GetUtcNow();
        var codes = await db.AccessGrants.AsNoTracking().Where(x => ids.Contains(x.UserId) && x.ConsumedAt == null &&
            x.RevokedAt == null && x.ExpiresAt > now).Select(x => new { x.UserId, x.Purpose, x.ExpiresAt }).ToArrayAsync(ct);
        return new(rows.Select(x =>
        {
            var code = codes.Where(c => c.UserId == x.Id).OrderByDescending(c => c.ExpiresAt).FirstOrDefault();
            return new AccountSummary(x.Id, x.UserName!, !x.HasPassword ? "Pending" : x.IsActive ? "Active" : "Suspended",
                AccessProfiles.Resolve(permissions.Where(p => p.UserId == x.Id).Select(p => p.Permission)), x.ConcurrencyStamp!,
                code?.ExpiresAt, code?.Purpose);
        }).ToArray(), total, page, pageSize);
    }

    public async Task<IssueCodeResult> InviteAsync(Guid actorId, InviteRequest request, CancellationToken ct = default)
    {
        var reason = Reason(request.Reason);
        var profile = AccessProfiles.Get(request.Profile);
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 100)
            throw Invalid("El usuario debe contener entre 1 y 100 caracteres.");
        await using var transaction = await BeginOperationAsync(ct);
        await RequireManagerAsync(actorId, ct);
        if (await users.FindByNameAsync(request.Username.Trim()) is not null)
            throw new AccessOperationException(409, "username-taken", "Ese usuario ya existe. Elige otro nombre de usuario.");
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = request.Username.Trim(), IsActive = false };
        Check(await users.CreateAsync(user));
        SetProfile(user.Id, profile);
        var code = await IssueAsync(user, actorId, AccessPurpose.Activation, ct);
        audit.Record("AccountInvited", actorId, user.Id, reason, profile.Key);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return code;
    }

    public async Task ChangeProfileAsync(Guid actorId, Guid userId, AccountChangeRequest request, CancellationToken ct = default)
    {
        var reason = Reason(request.Reason);
        var profile = AccessProfiles.Get(request.Profile ?? "");
        await using var transaction = await BeginOperationAsync(ct);
        await RequireManagerAsync(actorId, ct);
        var user = await TargetAsync(userId, request.Version, ct);
        await ProtectLastManagerAsync(user, user.IsActive, profile.Permissions.Contains(Permissions.ManageAccounts), ct);
        var previous = await db.AccountPermissions.Where(x => x.UserId == userId).ToArrayAsync(ct);
        var desired = profile.Permissions.ToHashSet(StringComparer.Ordinal);
        db.AccountPermissions.RemoveRange(previous.Where(x => !desired.Contains(x.Permission)));
        SetProfile(userId, profile, previous.Select(x => x.Permission).ToHashSet(StringComparer.Ordinal));
        Check(await users.UpdateSecurityStampAsync(user));
        audit.Record("AccessProfileChanged", actorId, userId, reason, profile.Key);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task SetActiveAsync(Guid actorId, Guid userId, AccountChangeRequest request, CancellationToken ct = default)
    {
        var reason = Reason(request.Reason);
        if (request.IsActive is null) throw Invalid("Selecciona el estado de la cuenta.");
        await using var transaction = await BeginOperationAsync(ct);
        await RequireManagerAsync(actorId, ct);
        var user = await TargetAsync(userId, request.Version, ct);
        if (user.PasswordHash is null) throw new AccessOperationException(409, "activation-required", "Esta cuenta debe activarse con su código.");
        var hasManagement = await db.AccountPermissions.AnyAsync(x => x.UserId == userId && x.Permission == Permissions.ManageAccounts, ct);
        await ProtectLastManagerAsync(user, request.IsActive.Value, hasManagement, ct);
        user.IsActive = request.IsActive.Value;
        if (!user.IsActive) await RevokeCodesAsync(userId, ct);
        Check(await users.UpdateSecurityStampAsync(user));
        audit.Record(user.IsActive ? "AccountEnabled" : "AccountSuspended", actorId, userId, reason);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RevokeSessionsAsync(Guid actorId, Guid userId, AccountChangeRequest request, CancellationToken ct = default)
    {
        var reason = Reason(request.Reason);
        await using var transaction = await BeginOperationAsync(ct);
        await RequireManagerAsync(actorId, ct);
        var user = await TargetAsync(userId, request.Version, ct);
        Check(await users.UpdateSecurityStampAsync(user));
        audit.Record("SessionsRevoked", actorId, userId, reason);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IssueCodeResult> IssueCodeAsync(Guid actorId, Guid userId, IssueCodeRequest request, CancellationToken ct = default)
    {
        var reason = Reason(request.VerificationNote);
        if (!request.IdentityVerified) throw Invalid("Debes verificar la identidad del titular antes de entregar un código.");
        if (request.Purpose is not (AccessPurpose.Activation or AccessPurpose.Recovery)) throw Invalid("Tipo de código inválido.");
        await using var transaction = await BeginOperationAsync(ct);
        await RequireManagerAsync(actorId, ct);
        var user = await TargetAsync(userId, request.Version, ct);
        if ((request.Purpose == AccessPurpose.Activation) != (user.PasswordHash is null))
            throw new AccessOperationException(409, "wrong-purpose", "El tipo de código no corresponde al estado actual de la cuenta.");
        if (request.Purpose == AccessPurpose.Recovery && !user.IsActive)
            throw new AccessOperationException(409, "account-suspended", "Revisa y habilita la cuenta antes de recuperar su acceso.");
        var code = await IssueAsync(user, actorId, request.Purpose, ct);
        audit.Record("AccessCodeIssued", actorId, userId, reason, request.Purpose);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return code;
    }

    public async Task CancelCodesAsync(Guid actorId, Guid userId, AccountChangeRequest request, CancellationToken ct = default)
    {
        var reason = Reason(request.Reason);
        await using var transaction = await BeginOperationAsync(ct);
        await RequireManagerAsync(actorId, ct);
        var user = await TargetAsync(userId, request.Version, ct);
        await RevokeCodesAsync(userId, ct);
        Check(await users.UpdateAsync(user));
        audit.Record("AccessCodesRevoked", actorId, userId, reason);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RedeemAsync(string code, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(code) || !Regex.IsMatch(code, @"^[0-9a-f]{32}\.[A-Za-z0-9_-]{43}$")) throw InvalidCode();
        var id = Guid.ParseExact(code[..32], "N");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(code[33..]));
        await using var transaction = await BeginOperationAsync(ct);
        var grant = await db.AccessGrants.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (grant is null || !CryptographicOperations.FixedTimeEquals(hash, grant.SecretHash) ||
            grant.ConsumedAt is not null || grant.RevokedAt is not null || grant.ExpiresAt <= time.GetUtcNow()) throw InvalidCode();
        var user = await db.Users.SingleAsync(x => x.Id == grant.UserId, ct);
        if (grant.Purpose == AccessPurpose.Activation)
        {
            if (user.PasswordHash is not null) throw InvalidCode();
            Check(await users.AddPasswordAsync(user, password));
            user.IsActive = true;
        }
        else
        {
            if (!user.IsActive || user.PasswordHash is null) throw InvalidCode();
            var identityToken = await users.GeneratePasswordResetTokenAsync(user);
            Check(await users.ResetPasswordAsync(user, identityToken, password));
        }
        Check(await users.ResetAccessFailedCountAsync(user));
        Check(await users.SetLockoutEndDateAsync(user, null));
        Check(await users.UpdateSecurityStampAsync(user));
        grant.ConsumedAt = time.GetUtcNow();
        await RevokeCodesAsync(user.Id, ct, except: grant.Id);
        audit.Record(grant.Purpose == AccessPurpose.Activation ? "AccountActivated" : "PasswordRecovered", user.Id, user.Id,
            detail: grant.Purpose);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<Guid> ProvisionOwnerAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 100) throw Invalid("Usuario inválido.");
        await using var transaction = await BeginOperationAsync(ct);
        if (await ActiveManagers().AnyAsync(ct))
            throw new AccessOperationException(409, "owner-exists", "Ya existe un gestor activo. Administra las cuentas desde la plataforma.");
        if (await users.FindByNameAsync(username.Trim()) is not null)
            throw new AccessOperationException(409, "username-taken", "El usuario ya existe; no se modificará su contraseña ni sus permisos.");
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = username.Trim(), IsActive = true };
        Check(await users.CreateAsync(user, password));
        SetProfile(user.Id, AccessProfiles.Get("access-owner"));
        audit.Record("OwnerProvisioned", null, user.Id, "Preparación inicial de esta instalación.", "access-owner");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return user.Id;
    }

    private async Task<IDbContextTransaction> BeginOperationAsync(CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Low-volume account administration: one short SQL lock covers last-manager checks and one-use redemption.
            // No external calls or message delivery run under this lock.
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource=N'SchoolPlatform.AccountAccess',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @result < 0 THROW 51000, 'Account access operation busy', 1;
                """, ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    private async Task RequireManagerAsync(Guid actorId, CancellationToken ct)
    {
        if (!await ActiveManagers().AnyAsync(x => x.Id == actorId, ct))
            throw new AccessOperationException(403, "permission-denied", "No tienes permiso para administrar cuentas.");
    }

    private IQueryable<ApplicationUser> ActiveManagers() => db.Users.Where(x => x.IsActive && x.PasswordHash != null &&
        db.AccountPermissions.Any(p => p.UserId == x.Id && p.Permission == Permissions.ManageAccounts));

    private async Task ProtectLastManagerAsync(ApplicationUser user, bool willBeActive, bool willManage, CancellationToken ct)
    {
        if (user.IsActive && user.PasswordHash is not null && (!willBeActive || !willManage) &&
            await db.AccountPermissions.AnyAsync(x => x.UserId == user.Id && x.Permission == Permissions.ManageAccounts, ct) &&
            await ActiveManagers().CountAsync(ct) <= 1)
            throw new AccessOperationException(409, "last-manager", "Debe quedar al menos un gestor de accesos activo. Activa otro gestor antes de continuar.");
    }

    private async Task<ApplicationUser> TargetAsync(Guid id, string version, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AccessOperationException(404, "account-not-found", "No encontramos esa cuenta.");
        if (string.IsNullOrEmpty(version) || version != user.ConcurrencyStamp)
            throw new AccessOperationException(409, "account-changed", "La cuenta cambió. Recarga la lista antes de continuar.");
        return user;
    }

    private void SetProfile(Guid userId, AccessProfile profile, HashSet<string>? existing = null)
    {
        foreach (var permission in profile.Permissions.Where(x => existing is null || !existing.Contains(x)))
            db.AccountPermissions.Add(new() { UserId = userId, Permission = permission });
    }

    private async Task<IssueCodeResult> IssueAsync(ApplicationUser user, Guid actorId, string purpose, CancellationToken ct)
    {
        await RevokeCodesAsync(user.Id, ct);
        // Save revocations before inserting the next row into the filtered unique index.
        await db.SaveChangesAsync(ct);
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var grant = new AccountAccessGrant
        {
            Id = Guid.NewGuid(), UserId = user.Id, IssuedBy = actorId, Purpose = purpose,
            SecretHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret)), IssuedAt = time.GetUtcNow(),
            ExpiresAt = time.GetUtcNow().AddMinutes(purpose == AccessPurpose.Activation ? options.Value.ActivationMinutes : options.Value.RecoveryMinutes)
        };
        db.AccessGrants.Add(grant);
        Check(await users.UpdateAsync(user));
        return new(user.Id, $"{grant.Id:N}.{secret}", purpose, grant.ExpiresAt);
    }

    private async Task RevokeCodesAsync(Guid userId, CancellationToken ct, Guid? except = null)
    {
        var outstanding = await db.AccessGrants.Where(x => x.UserId == userId && x.ConsumedAt == null &&
            x.RevokedAt == null && (except == null || x.Id != except)).ToArrayAsync(ct);
        foreach (var grant in outstanding) grant.RevokedAt = time.GetUtcNow();
    }

    private static string Reason(string? value)
    {
        var reason = value?.Trim() ?? "";
        if (reason.Length is < 8 or > 500) throw Invalid("Describe el motivo o la verificación realizada, entre 8 y 500 caracteres.");
        return reason;
    }

    private static void Check(IdentityResult result)
    {
        if (result.Succeeded) return;
        if (result.Errors.Any(x => x.Code == "ConcurrencyFailure"))
            throw new AccessOperationException(409, "account-changed", "La cuenta cambió. Recarga e intenta nuevamente.");
        if (result.Errors.Any(x => x.Code.StartsWith("Password", StringComparison.Ordinal)))
            throw Invalid("Usa al menos 12 caracteres con mayúscula, minúscula, número y símbolo.");
        throw Invalid("No pudimos guardar la cuenta. Revisa el usuario; usa letras sin acentos, números, punto, guion o guion bajo.");
    }

    private static AccessOperationException Invalid(string message) => new(400, "invalid-request", message);
    private static AccessOperationException InvalidCode() => new(400, "invalid-code", "El código no es válido, venció o ya fue utilizado. Solicita uno nuevo al colegio.");
}

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Gate;

public sealed record DeviceRequest(string Name);
public sealed record DeviceView(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt, bool Revoked);
/// <summary>The token is shown once (as text and pairing QR) and stored only as a SHA-256 hash.</summary>
public sealed record RegisteredDevice(Guid Id, string Name, string Token, string PairingSvg);

/// <summary>Scanner phones/tablets: registered by attendance.manage, each with its own revocable bearer token (scope: gate scans only).</summary>
public sealed class GateDeviceService(SchoolDbContext db, SchoolAudit audit, TimeProvider time)
{
    public const string TokenPrefix = "gd_";

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<DeviceView[]> ListAsync(CancellationToken ct) =>
        await db.GateDevices.AsNoTracking().OrderBy(x => x.RevokedAt != null).ThenBy(x => x.Name)
            .Select(x => new DeviceView(x.Id, x.Name, x.CreatedAt, x.LastSeenAt, x.RevokedAt != null)).ToArrayAsync(ct);

    public async Task<RegisteredDevice> RegisterAsync(Guid actor, DeviceRequest request, string origin, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AttendanceManage);
        var name = (request.Name ?? "").Trim();
        if (name.Length is < 2 or > 80) throw OperationException.Invalid("Escribe un nombre de 2 a 80 caracteres (por ejemplo «Portón principal»).");
        if (await db.GateDevices.CountAsync(x => x.RevokedAt == null, ct) >= 30)
            throw new OperationException(409, "too-many-devices", "Revoca algún dispositivo antes de registrar otro (máximo 30 activos).");
        var token = TokenPrefix + Base64Url(RandomNumberGenerator.GetBytes(32));
        var device = new GateDevice { Id = Guid.NewGuid(), Name = name, TokenHash = Hash(token), CreatedAt = time.GetUtcNow(), CreatedBy = actor };
        db.GateDevices.Add(device);
        audit.Record("GateDeviceRegistered", actor, "GateDevice", device.Id, "", name);
        await db.SaveChangesAsync(ct);
        // Pairing payload read by the mobile app's scanner: server address and token. Never placed in a URL or logged.
        var pairing = System.Text.Json.JsonSerializer.Serialize(new { v = 1, server = origin, token });
        return new(device.Id, name, token, Documents.Barcodes.QrSvg(pairing));
    }

    public async Task RevokeAsync(Guid actor, Guid id, VoidMarkRequest request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.AttendanceManage);
        var reason = Reasons.Require(request.Reason);
        var device = await db.GateDevices.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw OperationException.NotFound("No encontramos ese dispositivo.");
        if (device.RevokedAt is not null) throw new OperationException(409, "already-revoked", "El dispositivo ya estaba revocado.");
        device.RevokedAt = time.GetUtcNow();
        device.RevokedBy = actor;
        audit.Record("GateDeviceRevoked", actor, "GateDevice", id, reason, device.Name);
        await db.SaveChangesAsync(ct);
    }

    public async Task<GateDevice?> AuthenticateAsync(string token, CancellationToken ct)
    {
        if (!token.StartsWith(TokenPrefix, StringComparison.Ordinal) || token.Length > 80) return null;
        var hash = Hash(token);
        var device = await db.GateDevices.SingleOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAt == null, ct);
        if (device is null) return null;
        var now = time.GetUtcNow();
        if (device.LastSeenAt is null || now - device.LastSeenAt > TimeSpan.FromMinutes(1))
        {
            device.LastSeenAt = now;
            await db.SaveChangesAsync(ct);
        }
        return device;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>"Authorization: Bearer gd_…" for registered scanner devices. Grants only the gate-device claims.</summary>
public sealed class GateDeviceAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, GateDeviceService devices) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "GateDevice";
    public const string DeviceClaim = "gate_device";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return AuthenticateResult.NoResult();
        var device = await devices.AuthenticateAsync(header["Bearer ".Length..].Trim(), Context.RequestAborted);
        if (device is null) return AuthenticateResult.Fail("Dispositivo no autorizado.");
        var identity = new ClaimsIdentity([new Claim(DeviceClaim, device.Id.ToString()), new Claim(ClaimTypes.Name, device.Name)], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

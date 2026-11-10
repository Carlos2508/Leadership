using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Common;

/// <summary>
/// Operational setting of this installation that the administrator can change from the platform (not branding: institutional
/// texts, logos and colours stay in config/institution.json). Each change is audited with its reason.
/// </summary>
public sealed class PlatformSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed record PlatformSettingsView(int ChatAttachmentMaxMb, int ChatAttachmentLimitMb, string[] ChatAttachmentTypes, string? Version);
public sealed record SavePlatformSettings(int ChatAttachmentMaxMb, string? Version, string Reason);

public sealed class PlatformSettingsService(SchoolDbContext db, SchoolAudit audit, TimeProvider time)
{
    public const string ChatAttachmentMaxMbKey = "Chat.AttachmentMaxMb";
    public const int DefaultChatAttachmentMb = 5;
    /// <summary>Upper bound the administrator can choose (request size limits are set for it).</summary>
    public const int ChatAttachmentLimitMb = 25;

    public async Task<int> ChatAttachmentMaxMbAsync(CancellationToken ct)
    {
        var value = await db.PlatformSettings.AsNoTracking().Where(x => x.Key == ChatAttachmentMaxMbKey).Select(x => x.Value).SingleOrDefaultAsync(ct);
        return int.TryParse(value, out var mb) && mb is >= 1 and <= ChatAttachmentLimitMb ? mb : DefaultChatAttachmentMb;
    }

    public async Task<PlatformSettingsView> GetAsync(CancellationToken ct)
    {
        var setting = await db.PlatformSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == ChatAttachmentMaxMbKey, ct);
        return new(await ChatAttachmentMaxMbAsync(ct), ChatAttachmentLimitMb, Chat.ChatAttachmentRules.Extensions,
            setting is null ? null : Versions.Encode(setting.Version));
    }

    public async Task SaveAsync(Guid actor, SavePlatformSettings request, CancellationToken ct)
    {
        await audit.RequireAsync(actor, ct, Permissions.ManageAccounts);
        var reason = Reasons.Require(request.Reason);
        if (request.ChatAttachmentMaxMb is < 1 or > ChatAttachmentLimitMb)
            throw OperationException.Invalid($"El tamaÃ±o mÃ¡ximo de los adjuntos va de 1 a {ChatAttachmentLimitMb} MB.");
        var setting = await db.PlatformSettings.SingleOrDefaultAsync(x => x.Key == ChatAttachmentMaxMbKey, ct);
        if (setting is null)
        {
            if (!string.IsNullOrEmpty(request.Version)) throw OperationException.Changed();
            setting = new PlatformSetting { Key = ChatAttachmentMaxMbKey };
            db.PlatformSettings.Add(setting);
        }
        else if (!setting.Version.AsSpan().SequenceEqual(Versions.Decode(request.Version))) throw OperationException.Changed();
        var previous = setting.Value;
        setting.Value = request.ChatAttachmentMaxMb.ToString(System.Globalization.CultureInfo.InvariantCulture);
        setting.UpdatedAt = time.GetUtcNow();
        setting.UpdatedBy = actor;
        audit.Record("PlatformSettingChanged", actor, "PlatformSetting", Guid.Empty, reason, $"{ChatAttachmentMaxMbKey}:{previous}->{setting.Value}");
        await db.SaveChangesAsync(ct);
    }
}

public static class PlatformSettingsEndpoints
{
    public static void MapPlatformSettings(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/settings").RequireAuthorization(Permissions.ManageAccounts).AddEndpointFilter<ApiWriteFilter>();
        admin.MapGet("", (PlatformSettingsService service, CancellationToken ct) => service.GetAsync(ct));
        admin.MapPost("", async (SavePlatformSettings request, ClaimsPrincipal user, PlatformSettingsService service, CancellationToken ct) =>
        {
            await service.SaveAsync(PeopleEndpoints.Actor(user), request, ct);
            return Results.NoContent();
        });
    }
}

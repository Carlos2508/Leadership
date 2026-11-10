using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.Learning;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Chat;

/// <summary>File sent in a chat message (one per message). The blob lives outside the web root under a random key.</summary>
public sealed class ChatAttachment
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public long Sequence { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }
    public Guid UploadedBy { get; set; }
}

public sealed record ChatAttachmentView(Guid Id, string FileName, string ContentType, long Size);

/// <summary>
/// Allowed chat attachments: documents (PDF, Word, Excel, PowerPoint), images, plain text and ZIP. Every file is checked
/// against its real signature, so a renamed program is rejected; ZIP archives are opened and refused when they contain
/// programs or scripts, are encrypted or expand beyond a safe size. Macro-enabled Office formats are not accepted.
/// </summary>
public static partial class ChatAttachmentRules
{
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] Ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly Dictionary<string, (string ContentType, byte[][] Signatures)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", [[0x25, 0x50, 0x44, 0x46]]),
        [".doc"] = ("application/msword", [Ole]),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [Zip]),
        [".xls"] = ("application/vnd.ms-excel", [Ole]),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [Zip]),
        [".ppt"] = ("application/vnd.ms-powerpoint", [Ole]),
        [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", [Zip]),
        [".png"] = ("image/png", [[0x89, 0x50, 0x4E, 0x47]]),
        [".jpg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".jpeg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".txt"] = ("text/plain", []),
        [".csv"] = ("text/csv", []),
        [".zip"] = ("application/zip", [Zip, [0x50, 0x4B, 0x05, 0x06]]),
    };

    /// <summary>Programs, scripts, shortcuts and installers never travel inside a ZIP.</summary>
    private static readonly HashSet<string> Dangerous = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".com", ".bat", ".cmd", ".msi", ".msp", ".scr", ".pif", ".cpl", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse",
        ".wsf", ".wsh", ".hta", ".jar", ".sh", ".apk", ".app", ".lnk", ".reg", ".inf", ".sys", ".docm", ".xlsm", ".pptm", ".dotm",
        ".xltm", ".iso", ".img", ".vhd", ".vhdx",
    };

    public static string[] Extensions => [.. Allowed.Keys];

    /// <summary>Content type when the file is acceptable; throws with a clear reason otherwise.</summary>
    public static string Validate(string fileName, byte[] bytes, long maxBytes)
    {
        var extension = Path.GetExtension(fileName);
        if (!Allowed.TryGetValue(extension, out var type))
            throw OperationException.Invalid($"Tipo de archivo no permitido. Puedes enviar: {string.Join(", ", Allowed.Keys)}.");
        if (bytes.LongLength is 0 || bytes.LongLength > maxBytes)
            throw OperationException.Invalid($"El archivo debe pesar como máximo {maxBytes / 1024 / 1024} MB.");
        if (type.Signatures.Length > 0 && !type.Signatures.Any(sig => bytes.AsSpan().StartsWith(sig)))
            throw OperationException.Invalid("El contenido del archivo no corresponde a su tipo.");
        if (type.Signatures.Length == 0 && bytes.Contains((byte)0))
            throw OperationException.Invalid("El archivo de texto contiene datos binarios.");
        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)) InspectZip(bytes);
        return type.ContentType;
    }

    private static void InspectZip(byte[] bytes)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            long expanded = 0;
            foreach (var entry in archive.Entries)
            {
                expanded += entry.Length;
                if (Dangerous.Contains(Path.GetExtension(entry.Name)))
                    throw OperationException.Invalid($"El ZIP contiene un archivo no permitido ({entry.Name}).");
                if (entry.Length > 0 && IsEncrypted(entry))
                    throw OperationException.Invalid("El ZIP está protegido con contraseña y no se puede revisar.");
                if (expanded > 200L * 1024 * 1024 || archive.Entries.Count > 2000)
                    throw OperationException.Invalid("El ZIP es demasiado grande al descomprimirse.");
            }
        }
        catch (InvalidDataException)
        {
            throw OperationException.Invalid("El ZIP está dañado o no se puede leer.");
        }
    }

    private static bool IsEncrypted(ZipArchiveEntry entry)
    {
        // ZipArchiveEntry has no public flag; encrypted entries fail to open for reading.
        try { using var stream = entry.Open(); return false; }
        catch (InvalidDataException) { return true; }
        catch (NotSupportedException) { return true; }
    }

    public static string SafeName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "").Trim();
        name = UnsafeChars().Replace(name, "_");
        if (name.Length is 0 or > 150 || name.StartsWith('.')) throw OperationException.Invalid("El nombre del archivo no es válido (máximo 150 caracteres).");
        return name;
    }

    [GeneratedRegex(@"[^\p{L}\p{Nd} ._\-()]")] private static partial Regex UnsafeChars();
}

/// <summary>Stores and serves chat attachments; who may send or read them is decided by <see cref="ChatService"/>.</summary>
public sealed class ChatAttachmentService(ChatService chat, SchoolDbContext db, PlatformSettingsService settings, IOptions<StorageOptions> storage,
    IWebHostEnvironment environment)
{
    public async Task<ChatMessageView> SendAsync(Guid actor, Guid year, Guid id, Guid clientMessageId, string? caption, IFormFile file, CancellationToken ct)
    {
        var name = ChatAttachmentRules.SafeName(file.FileName);
        var max = await settings.ChatAttachmentMaxMbAsync(ct) * 1024L * 1024;
        if (file.Length > max) throw OperationException.Invalid($"El archivo debe pesar como máximo {max / 1024 / 1024} MB.");
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var type = ChatAttachmentRules.Validate(name, bytes, max);
        var key = $"chat/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}";
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, ct);
        try
        {
            var attachment = new ChatAttachment
            {
                Id = Guid.NewGuid(), FileName = name, ContentType = type, Size = bytes.LongLength, Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                StorageKey = key
            };
            var (view, stored) = await chat.SendWithAttachmentAsync(actor, year, id, clientMessageId, caption, attachment, ct);
            if (!stored) File.Delete(path); // repeated attempt: the first upload is the one kept
            return view;
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    /// <summary>Null when the account cannot read the conversation (respond 404).</summary>
    public async Task<FileDownload?> DownloadAsync(Guid actor, Guid attachmentId, CancellationToken ct)
    {
        var row = await (from a in db.ChatAttachments.AsNoTracking()
                         where a.Id == attachmentId
                         join c in db.ChatConversations on a.ConversationId equals c.Id
                         select new { Attachment = a, c.AcademicYearId }).SingleOrDefaultAsync(ct);
        if (row is null || !await chat.CanReadAsync(actor, row.AcademicYearId, row.Attachment.ConversationId, ct)) return null;
        var path = PathFor(row.Attachment.StorageKey);
        return File.Exists(path) ? new(path, row.Attachment.FileName, row.Attachment.ContentType) : null;
    }

    private string PathFor(string key)
    {
        var root = Path.GetFullPath(Path.IsPathRooted(storage.Value.Root) ? storage.Value.Root : Path.Combine(environment.ContentRootPath, storage.Value.Root));
        var full = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid storage key.");
        return full;
    }
}

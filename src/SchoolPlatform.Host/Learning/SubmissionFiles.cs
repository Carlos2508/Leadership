using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.People;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.Learning;

public sealed class StorageOptions
{
    /// <summary>Private folder outside wwwroot. Relative paths are resolved from the content root.</summary>
    public string Root { get; set; } = "App_Data/files";
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxFilesPerSubmission { get; set; } = 5;
}

// Attachment of a submission. The blob lives under a random storage key; names are never used as paths.
public sealed class SubmissionFile
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }
    public Guid UploadedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed record FileView(Guid Id, string FileName, string ContentType, long Size, DateTimeOffset UploadedAt);
public sealed record FileDownload(string Path, string FileName, string ContentType);

/// <summary>
/// Private submission attachments: allow-listed types verified by signature, size limits, storage outside the web root,
/// and downloads authorized per resource (the student, or staff who can view the course). No antivirus is integrated yet;
/// files are always served as attachments with nosniff.
/// </summary>
public sealed partial class SubmissionFileService(SchoolDbContext db, SchoolAudit audit, TimeProvider time, CourseService courses,
    PeopleService people, IOptions<StorageOptions> options, IWebHostEnvironment environment)
{
    private static readonly Dictionary<string, (string ContentType, byte[][] Signatures)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", [[0x25, 0x50, 0x44, 0x46]]),
        [".png"] = ("image/png", [[0x89, 0x50, 0x4E, 0x47]]),
        [".jpg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".jpeg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [[0x50, 0x4B, 0x03, 0x04]]),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [[0x50, 0x4B, 0x03, 0x04]]),
        [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", [[0x50, 0x4B, 0x03, 0x04]]),
        [".txt"] = ("text/plain", []),
    };

    public static string AllowedExtensions => string.Join(", ", Allowed.Keys);

    public async Task<FileView[]> ListAsync(Guid submissionId, CancellationToken ct) =>
        await db.SubmissionFiles.AsNoTracking().Where(x => x.SubmissionId == submissionId && x.DeletedAt == null)
            .OrderBy(x => x.UploadedAt).Select(x => new FileView(x.Id, x.FileName, x.ContentType, x.Size, x.UploadedAt)).ToArrayAsync(ct);

    public async Task<FileView> UploadAsync(Guid userId, Guid assignmentId, IFormFile file, CancellationToken ct)
    {
        var submission = await EditableSubmissionAsync(userId, assignmentId, ct);
        var name = SafeName(file.FileName);
        var extension = Path.GetExtension(name);
        if (!Allowed.TryGetValue(extension, out var type))
            throw OperationException.Invalid($"Tipo de archivo no permitido. Usa: {AllowedExtensions}.");
        if (file.Length is 0 || file.Length > options.Value.MaxFileBytes)
            throw OperationException.Invalid($"El archivo debe pesar entre 1 byte y {options.Value.MaxFileBytes / 1024 / 1024} MB.");
        if (await db.SubmissionFiles.CountAsync(x => x.SubmissionId == submission.Id && x.DeletedAt == null, ct) >= options.Value.MaxFilesPerSubmission)
            throw new OperationException(409, "too-many-files", $"Puedes adjuntar como máximo {options.Value.MaxFilesPerSubmission} archivos.");

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        // The declared extension must match the real content signature (no renamed executables or scripts).
        if (type.Signatures.Length > 0 && !type.Signatures.Any(sig => bytes.AsSpan().StartsWith(sig)))
            throw OperationException.Invalid("El contenido del archivo no corresponde a su tipo.");
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) && bytes.Contains((byte)0))
            throw OperationException.Invalid("El archivo de texto contiene datos binarios.");

        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}";
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, ct);
        var record = new SubmissionFile
        {
            Id = Guid.NewGuid(), SubmissionId = submission.Id, FileName = name, ContentType = type.ContentType, Size = bytes.LongLength,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), StorageKey = key, UploadedAt = time.GetUtcNow(), UploadedBy = userId
        };
        db.SubmissionFiles.Add(record);
        audit.Record("SubmissionFileUploaded", userId, "Assignment", assignmentId, detail: extension.ToLowerInvariant());
        try { await db.SaveChangesAsync(ct); }
        catch
        {
            File.Delete(path); // no orphan blobs when the metadata could not be stored
            throw;
        }
        return new(record.Id, record.FileName, record.ContentType, record.Size, record.UploadedAt);
    }

    public async Task DeleteAsync(Guid userId, Guid assignmentId, Guid fileId, CancellationToken ct)
    {
        var submission = await EditableSubmissionAsync(userId, assignmentId, ct);
        var file = await db.SubmissionFiles.SingleOrDefaultAsync(x => x.Id == fileId && x.SubmissionId == submission.Id && x.DeletedAt == null, ct)
            ?? throw OperationException.NotFound("No encontramos ese archivo.");
        // Soft delete keeps the record and blob for review history; it simply stops being part of the submission.
        file.DeletedAt = time.GetUtcNow();
        audit.Record("SubmissionFileRemoved", userId, "Assignment", assignmentId);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Null when the account may not read the file (respond 404).</summary>
    public async Task<FileDownload?> DownloadAsync(Guid userId, Guid fileId, CancellationToken ct)
    {
        var row = await (from f in db.SubmissionFiles.AsNoTracking()
                         where f.Id == fileId && f.DeletedAt == null
                         join s in db.Submissions on f.SubmissionId equals s.Id
                         join a in db.Assignments on s.AssignmentId equals a.Id
                         select new { File = f, s.StudentId, a.CourseId }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var self = await people.StudentAccessAsync(userId, row.StudentId, ct) is { IsSelf: true };
        if (!self && !await courses.CanViewAsync(userId, row.CourseId, ct)) return null;
        var path = PathFor(row.File.StorageKey);
        return File.Exists(path) ? new(path, row.File.FileName, row.File.ContentType) : null;
    }

    private async Task<Submission> EditableSubmissionAsync(Guid userId, Guid assignmentId, CancellationToken ct)
    {
        var personId = await db.AccountLinks.AsNoTracking().Where(l => l.UserId == userId && l.UnlinkedAt == null)
            .Select(l => (Guid?)l.PersonId).SingleOrDefaultAsync(ct);
        var assignment = await db.Assignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assignmentId, ct);
        var submission = personId is null || assignment is null ? null
            : await db.Submissions.SingleOrDefaultAsync(x => x.AssignmentId == assignmentId && x.StudentId == personId, ct);
        if (submission is null || await people.StudentAccessAsync(userId, submission.StudentId, ct) is not { IsSelf: true })
            throw OperationException.NotFound("Primero guarda el texto de tu entrega; luego podrás adjuntar archivos.");
        if (assignment!.Status != AssignmentStatus.Published || submission.Status == SubmissionStatus.Reviewed ||
            (time.GetUtcNow() > assignment.DueAt && !assignment.AllowLate))
            throw new OperationException(409, "submission-locked", "La entrega ya no admite cambios.");
        return submission;
    }

    private string PathFor(string key)
    {
        var root = Path.GetFullPath(Path.IsPathRooted(options.Value.Root) ? options.Value.Root : Path.Combine(environment.ContentRootPath, options.Value.Root));
        var full = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid storage key.");
        return full;
    }

    private static string SafeName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "").Trim();
        name = UnsafeChars().Replace(name, "_");
        if (name.Length is 0 or > 150 || name.StartsWith('.')) throw OperationException.Invalid("El nombre del archivo no es válido (máximo 150 caracteres).");
        return name;
    }

    [GeneratedRegex(@"[^\p{L}\p{Nd} ._\-()]")] private static partial Regex UnsafeChars();
}

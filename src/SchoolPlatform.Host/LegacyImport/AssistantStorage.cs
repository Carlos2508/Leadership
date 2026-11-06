using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Common;

namespace SchoolPlatform.Host.LegacyImport;

public sealed record AssistantRun(Guid Owner, AssistantReview Review, string? PlanSha256, string? RoleKey, string? BackupPath);

public sealed class AssistantStorage(IWebHostEnvironment environment, IOptions<MigrationAssistantOptions> options)
{
    private string Root => Path.GetFullPath(options.Value.StorageRoot, environment.ContentRootPath);
    private string DirectoryFor(Guid owner, Guid run) => Path.Combine(Root, owner.ToString("N"), run.ToString("N"));

    public string Create(Guid owner, Guid run)
    {
        var directory = DirectoryFor(owner, run);
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsWindows())
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(directory).SetAccessControl(security);
        }
        else File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return directory;
    }

    public string RawPath(Guid owner, Guid run) => Path.Combine(DirectoryFor(owner, run), "raw.json");
    public string PackagePath(Guid owner, Guid run) => Path.Combine(DirectoryFor(owner, run), "package.json");
    private string StatePath(Guid owner, Guid run) => Path.Combine(DirectoryFor(owner, run), "state.json");
    public async Task SaveAsync(AssistantRun state, CancellationToken ct)
    {
        var path = StatePath(state.Owner, state.Review.RunId);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(state, AssistantJson.Options), ct);
        File.Move(temporary, path, true);
    }

    public async Task<AssistantRun> LoadAsync(Guid owner, Guid run, CancellationToken ct)
    {
        var path = StatePath(owner, run);
        if (!File.Exists(path)) throw OperationException.NotFound("No encontramos tu revisión. Vuelve a leer el origen.");
        var state = JsonSerializer.Deserialize<AssistantRun>(await File.ReadAllTextAsync(path, ct), AssistantJson.Options)!;
        if (state.Owner != owner || state.Review.RunId != run) throw OperationException.NotFound("No encontramos esa revisión.");
        return state;
    }

    public async Task<FileStream> LockAsync(Guid owner, Guid run, CancellationToken ct)
    {
        if (!File.Exists(StatePath(owner, run))) throw OperationException.NotFound("No encontramos tu revisión.");
        var path = Path.Combine(DirectoryFor(owner, run), "operation.lock");
        for (var attempt = 0; attempt < 1200; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { await Task.Delay(100, ct); }
        }
        throw new OperationException(409, "assistant-busy", "La revisión está siendo usada en otra ventana. Espera un momento.");
    }

    public async Task<AssistantReview[]> RecentAsync(Guid owner, CancellationToken ct)
    {
        var root = Path.Combine(Root, owner.ToString("N"));
        if (!Directory.Exists(root)) return [];
        var result = new List<AssistantReview>();
        foreach (var directory in Directory.GetDirectories(root).OrderByDescending(Directory.GetLastWriteTimeUtc).Take(5))
            if (Guid.TryParseExact(Path.GetFileName(directory), "N", out var run) && File.Exists(StatePath(owner, run)))
                result.Add((await LoadAsync(owner, run, ct)).Review);
        return result.ToArray();
    }
}

public static class AssistantJson
{
    public static readonly JsonSerializerOptions Options = Create();
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 24,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };
        options.Converters.Add(new ScalarStringConverter());
        return options;
    }
    private sealed class ScalarStringConverter : System.Text.Json.Serialization.JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => ReadNumber(ref reader),
            JsonTokenType.Null => null,
            _ => throw new JsonException()
        };
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
        private static string ReadNumber(ref Utf8JsonReader reader) { using var document = JsonDocument.ParseValue(ref reader); return document.RootElement.GetRawText(); }
    }
}

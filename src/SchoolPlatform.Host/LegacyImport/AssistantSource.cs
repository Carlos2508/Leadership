using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SchoolPlatform.Host.Common;

namespace SchoolPlatform.Host.LegacyImport;

public interface IAssistantSource
{
    Task<AssistantInspect> InspectAsync(AssistantSourceConnection? connection, CancellationToken ct);
    Task ReadAsync(string yearId, string outputFile, AssistantSourceConnection? connection, CancellationToken ct);
}

public sealed class AssistantSource(IWebHostEnvironment environment, IOptions<MigrationAssistantOptions> options) : IAssistantSource
{
    public async Task<AssistantInspect> InspectAsync(AssistantSourceConnection? connection, CancellationToken ct)
    {
        var output = await PhpAsync(["--inspect"], connection, ct);
        return JsonSerializer.Deserialize<AssistantInspect>(output, AssistantJson.Options) ?? throw Failure();
    }

    public async Task ReadAsync(string yearId, string outputFile, AssistantSourceConnection? connection, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(yearId, @"^[1-9][0-9]{0,9}$")) throw OperationException.Invalid("Elige un ciclo de origen.");
        await PhpAsync(["--year=" + yearId, "--source=sys-escolar", "--output=" + outputFile], connection, ct);
    }

    private async Task<string> PhpAsync(string[] arguments, AssistantSourceConnection? connection, CancellationToken ct)
    {
        var start = new ProcessStartInfo(options.Value.PhpPath)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.GetFullPath(options.Value.ExporterPath, environment.ContentRootPath));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        // Ignore inherited credentials. Auto-detection stays in the child process and never returns secrets.
        foreach (var key in new[] { "LEGACY_DSN", "LEGACY_DB_USER", "LEGACY_DB_PASSWORD" }) start.Environment.Remove(key);
        if (connection is null) start.ArgumentList.Add("--config-file=" + options.Value.LegacyConfigPath);
        else
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(connection.Host ?? "", @"^[a-zA-Z0-9._-]+$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(connection.Database ?? "", @"^[a-zA-Z0-9_-]+$") || connection.Port is < 1 or > 65535)
                throw OperationException.Invalid("Revisa servidor, base y puerto del origen.");
            start.Environment["LEGACY_DSN"] = $"mysql:host={connection.Host};port={connection.Port};dbname={connection.Database};charset=utf8mb4";
            start.Environment["LEGACY_DB_USER"] = connection.Username ?? "";
            start.Environment["LEGACY_DB_PASSWORD"] = connection.Password ?? "";
        }
        using var process = Process.Start(start) ?? throw Failure();
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct); // Drain; never return/log PDO errors or credentials.
        try { await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromMinutes(2), ct); }
        catch { if (!process.HasExited) process.Kill(true); throw Failure(); }
        var text = await output;
        _ = await error;
        if (process.ExitCode != 0 || text.Length > 2000000) throw Failure();
        return text;
    }

    private static OperationException Failure() => new(400, "source-unavailable", "No se pudo leer el origen. Comprueba que MariaDB esté encendido o usa la conexión manual.");
}

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Common;
using SchoolPlatform.Host.SchoolData;

namespace SchoolPlatform.Host.LegacyImport;

public interface IAssistantBackup { Task<string> CreateVerifiedAsync(CancellationToken ct); }

public sealed class AssistantBackup(SchoolDbContext db) : IAssistantBackup
{
    public async Task<string> CreateVerifiedAsync(CancellationToken ct)
    {
        // Separate connection: never perform a backup inside the import transaction or IAM lock.
        await using var connection = new SqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync(ct);
        await using var pathCommand = new SqlCommand("SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'))", connection);
        var root = await pathCommand.ExecuteScalarAsync(ct) as string;
        if (string.IsNullOrWhiteSpace(root)) throw new OperationException(409, "backup-unavailable", "SQL Server no tiene una carpeta de respaldo disponible. La importación no se ejecutó.");
        var name = System.Text.RegularExpressions.Regex.Replace(connection.Database, @"[^a-zA-Z0-9_-]", "_");
        var path = root.TrimEnd('\\', '/') + (root.Contains('\\') ? "\\" : "/") + name + "_migration_" + Guid.NewGuid().ToString("N") + ".bak";
        var identifier = new SqlCommandBuilder().QuoteIdentifier(connection.Database);
        await using var command = new SqlCommand($"BACKUP DATABASE {identifier} TO DISK = @path WITH COPY_ONLY, CHECKSUM; RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;", connection)
        { CommandTimeout = 180 };
        command.Parameters.AddWithValue("@path", path);
        try { await command.ExecuteNonQueryAsync(ct); }
        catch (SqlException) { throw new OperationException(409, "backup-unavailable", "No se pudo crear y verificar el respaldo automático. La importación no se ejecutó."); }
        return path;
    }
}

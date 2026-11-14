using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Creates and restores backups using SQLite's online backup API, which is safe to use
/// while the database is open (unlike copying the file directly). WAL mode is respected.
/// </summary>
public sealed class BackupService(AppDbContext db) : IBackupService
{
    public async Task<BackupResult> CreateBackupAsync(string destinationFolder, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationFolder);
        var path = Path.Combine(
            destinationFolder,
            $"highland-milkbar-backup-{DateTime.Now:yyyyMMddHHmmss}.db");

        await using var destination = new SqliteConnection($"Data Source={path};Pooling=False");
        await destination.OpenAsync(cancellationToken);

        var source = (SqliteConnection)db.Database.GetDbConnection();
        await OpenIfClosedAsync(source, cancellationToken);
        source.BackupDatabase(destination);

        var info = new FileInfo(path);
        return new BackupResult
        {
            Path = path,
            SizeBytes = info.Exists ? info.Length : 0,
            CreatedAt = DateTime.Now
        };
    }

    public async Task RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException("Backup file was not found.", backupFilePath);

        await using var backup = new SqliteConnection($"Data Source={backupFilePath};Pooling=False");
        await backup.OpenAsync(cancellationToken);

        if (!await IsIntegrityOkAsync(backup, cancellationToken))
            throw new InvalidOperationException("The backup file failed the SQLite integrity check.");

        // Always keep a safety copy of the current data before overwriting it.
        var safetyFolder = Path.Combine(Path.GetDirectoryName(backupFilePath)!, "pre-restore");
        await CreateBackupAsync(safetyFolder, cancellationToken);

        var live = (SqliteConnection)db.Database.GetDbConnection();
        await OpenIfClosedAsync(live, cancellationToken);
        backup.BackupDatabase(live);
    }

    public async Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await OpenIfClosedAsync(connection, cancellationToken);
        return await IsIntegrityOkAsync(connection, cancellationToken);
    }

    private static async Task<bool> IsIntegrityOkAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return string.Equals(result?.ToString(), "ok", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task OpenIfClosedAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
    }
}
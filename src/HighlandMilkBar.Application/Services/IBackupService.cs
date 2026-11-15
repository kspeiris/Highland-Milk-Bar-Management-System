using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Backup creation, verification and restore. Extends the STEP 02 implementation:
/// every backup is produced through SQLite's online backup API (WAL-safe) and is
/// not reported successful until it has passed integrity and checksum verification.
/// </summary>
public interface IBackupService
{
    /// <summary>Legacy folder-based backup (STEP 02). Returns the created file's basic details.</summary>
    Task<BackupResult> CreateBackupAsync(string destinationFolder, CancellationToken cancellationToken = default);

    /// <summary>Creates a verified backup of the given type and writes a sidecar manifest.</summary>
    Task<BackupOperationResult> CreateBackupAsync(BackupType type, string destinationFolder, CancellationToken cancellationToken = default);

    /// <summary>Legacy in-place restore used by earlier tests. Verifies and safety-backs up first.</summary>
    Task RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default);

    /// <summary>Full safe restore: verify, pre-restore safety backup, in-place replace, migrate, verify.</summary>
    Task<BackupOperationResult> RestoreBackupAsync(string backupFilePath, CancellationToken cancellationToken = default);

    /// <summary>Legacy live-database integrity check (STEP 02).</summary>
    Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default);

    /// <summary>Detailed verification of a backup file (existence, integrity, FK, checksum, schema).</summary>
    Task<BackupVerificationDto> VerifyBackupAsync(string backupFilePath, CancellationToken cancellationToken = default);
}

/// <summary>Read-only backup history, settings and maintenance operations.</summary>
public interface IBackupQueryService
{
    Task<List<BackupInfoDto>> ListBackupsAsync(string? folder, CancellationToken cancellationToken = default);
    Task<BackupStorageInfoDto> GetStorageInfoAsync(string? folder, CancellationToken cancellationToken = default);
    Task<BackupSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(BackupSettingsDto settings, CancellationToken cancellationToken = default);
    Task<BackupRetentionResultDto> RunRetentionAsync(string? folder, CancellationToken cancellationToken = default);

    /// <summary>Deletes a backup file and its manifest. Protected backups are never auto-deleted; this is explicit.</summary>
    Task DeleteBackupAsync(string backupFilePath, CancellationToken cancellationToken = default);
}
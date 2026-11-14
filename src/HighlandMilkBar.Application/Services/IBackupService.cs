using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

public interface IBackupService
{
    Task<BackupResult> CreateBackupAsync(string destinationFolder, CancellationToken cancellationToken = default);
    Task RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default);
    Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default);
}
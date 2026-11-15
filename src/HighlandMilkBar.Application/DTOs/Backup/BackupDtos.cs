namespace HighlandMilkBar.Application.DTOs;

/// <summary>Kinds of backup the application can produce.</summary>
public enum BackupType
{
    Manual,
    Automatic,
    PreRestore
}

/// <summary>Automatic-backup schedule frequencies.</summary>
public enum BackupFrequency
{
    Disabled,
    Daily,
    Weekly
}

/// <summary>Result of a single verification check.</summary>
public enum BackupCheckStatus
{
    Pass,
    Fail,
    NotAvailable,
    Unknown
}

/// <summary>
/// Sidecar metadata written next to every backup created by this application.
/// Legacy backups without a manifest are still verifiable; the checksum check
/// is simply reported as unavailable for them.
/// </summary>
public sealed class BackupManifestDto
{
    public string BackupFile { get; set; } = string.Empty;
    public BackupType BackupType { get; set; } = BackupType.Manual;
    public DateTime CreatedAtUtc { get; set; }
    public string ApplicationVersion { get; set; } = string.Empty;
    public string? SchemaVersion { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public string ChecksumAlgorithm { get; set; } = "SHA256";
    public string Checksum { get; set; } = string.Empty;
    public BackupCheckStatus IntegrityCheck { get; set; } = BackupCheckStatus.Unknown;
    public BackupCheckStatus ForeignKeyCheck { get; set; } = BackupCheckStatus.Unknown;
    public string? OperationId { get; set; }
}

/// <summary>A backup file as shown in the history grid.</summary>
public sealed class BackupInfoDto
{
    public string FileName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public BackupType Type { get; set; } = BackupType.Manual;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
    public string ApplicationVersion { get; set; } = string.Empty;
    public string? SchemaVersion { get; set; }
    public bool HasManifest { get; set; }
    public bool IsLegacy => !HasManifest;
    public BackupCheckStatus ChecksumStatus { get; set; } = BackupCheckStatus.NotAvailable;
    public BackupCheckStatus IntegrityStatus { get; set; } = BackupCheckStatus.Unknown;
    /// <summary>Protected backups (manual / pre-restore) are never auto-deleted.</summary>
    public bool IsProtected => Type != BackupType.Automatic;
    public bool EligibleForRetention => Type == BackupType.Automatic;
}

/// <summary>Detailed verification outcome for a single backup file.</summary>
public sealed class BackupVerificationDto
{
    public string FileName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool FileExists { get; set; }
    public long FileSizeBytes { get; set; }
    public bool ManifestPresent { get; set; }
    public bool IsLegacy => !ManifestPresent;

    public BackupCheckStatus FileExistsCheck { get; set; } = BackupCheckStatus.Unknown;
    public BackupCheckStatus FileSizeCheck { get; set; } = BackupCheckStatus.Unknown;
    public BackupCheckStatus SqliteIntegrityCheck { get; set; } = BackupCheckStatus.Unknown;
    public BackupCheckStatus ForeignKeyCheck { get; set; } = BackupCheckStatus.Unknown;
    public BackupCheckStatus ChecksumCheck { get; set; } = BackupCheckStatus.Unknown;
    public BackupCheckStatus SchemaCompatibilityCheck { get; set; } = BackupCheckStatus.Unknown;

    public bool OverallValid { get; set; }
    public List<string> Messages { get; set; } = new();
    public List<string> ForeignKeyViolations { get; set; } = new();

    public List<string> MigrationIds { get; set; } = new();
    public string? SchemaVersion { get; set; }
    public BackupType? Type { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>Persistence settings for automatic backup and retention.</summary>
public sealed class BackupSettingsDto
{
    public bool AutomaticBackupEnabled { get; set; }
    public BackupFrequency Frequency { get; set; } = BackupFrequency.Disabled;
    public TimeSpan BackupTime { get; set; } = new(22, 0, 0);
    public DayOfWeek Weekday { get; set; } = DayOfWeek.Sunday;
    public string Location { get; set; } = string.Empty;
    public int RetentionCount { get; set; } = 14;
    public int MaxBackupAgeDays { get; set; } = 30;
    public int MinimumValidBackups { get; set; } = 3;
    public DateTime? LastSuccessUtc { get; set; }
    public DateTime? LastAttemptUtc { get; set; }
}

/// <summary>Summary of backup storage in a folder.</summary>
public sealed class BackupStorageInfoDto
{
    public string Location { get; set; } = string.Empty;
    public int BackupCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public long FreeSpaceBytes { get; set; }
    public bool LocationExists { get; set; }
    public bool LocationWritable { get; set; }
}

/// <summary>Outcome of a retention cleanup pass.</summary>
public sealed class BackupRetentionResultDto
{
    public int Evaluated { get; set; }
    public int Deleted { get; set; }
    public List<string> DeletedFiles { get; set; } = new();
    public List<string> KeptProtected { get; set; } = new();
    public List<string> Failures { get; set; } = new();
}

/// <summary>Rich result of a backup creation, including verification.</summary>
public sealed class BackupOperationResult
{
    public bool Success { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public BackupType Type { get; set; } = BackupType.Manual;
    public string? ErrorMessage { get; set; }
    public BackupVerificationDto? Verification { get; set; }
}

/// <summary>Details shown before a restore is confirmed.</summary>
public sealed class RestorePreviewDto
{
    public bool CanRestore { get; set; }
    public BackupInfoDto? Backup { get; set; }
    public BackupVerificationDto? Verification { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
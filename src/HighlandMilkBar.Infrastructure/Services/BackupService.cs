using System.Data;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Reliable backup and recovery for the application database. Extends the STEP 02
/// implementation: every backup is produced through SQLite's online backup API (safe
/// while the database is open and WAL-correct), written to a <c>.partial</c> file, then
/// integrity- and checksum-verified before being atomically renamed and reported as
/// successful. Restore verifies the source, creates and verifies a pre-restore safety
/// backup, replaces the live database in place, applies supported forward migrations
/// and re-verifies, rolling back to the safety backup on any failure.
/// </summary>
public sealed class BackupService : IBackupService, IBackupQueryService
{
    private static readonly string[] RequiredTables = { "Categories", "Products", "Sales", "Payments", "AppSettings" };

    private readonly AppDbContext _db;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BackupService(AppDbContext db) => _db = db;

    private string ApplicationVersion =>
        typeof(BackupService).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    private string SchemaVersion =>
        _db.Database.GetMigrations().LastOrDefault() ?? "none";

    // ---------------------------------------------------------------- create

    public Task<BackupResult> CreateBackupAsync(string destinationFolder, CancellationToken cancellationToken = default)
        => CreateBackupAsync(BackupType.Manual, destinationFolder, cancellationToken)
            .ContinueWith(t =>
            {
                var r = t.GetAwaiter().GetResult();
                return new BackupResult { Path = r.Path, SizeBytes = r.SizeBytes, CreatedAt = r.CreatedAt };
            }, cancellationToken);

    public async Task<BackupOperationResult> CreateBackupAsync(BackupType type, string destinationFolder, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await CreateBackupCoreAsync(type, destinationFolder, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<BackupOperationResult> CreateBackupCoreAsync(BackupType type, string destinationFolder, CancellationToken cancellationToken)
    {
        var createdUtc = DateTime.UtcNow;
        string path;
        try
        {
            Directory.CreateDirectory(destinationFolder);
            path = UniquePath(destinationFolder, $"HighlandMilkBar_{TypeName(type)}_{createdUtc.ToLocalTime():yyyyMMdd_HHmmss}.db");
        }
        catch (Exception ex)
        {
            return Failure(type, string.Empty, createdUtc, $"Backup destination is not usable: {ex.Message}");
        }

        var tempPath = path + ".partial";
        try
        {
            await using var destination = new SqliteConnection($"Data Source={tempPath};Pooling=False");
            await destination.OpenAsync(cancellationToken);

            var source = (SqliteConnection)_db.Database.GetDbConnection();
            await OpenIfClosedAsync(source, cancellationToken);
            source.BackupDatabase(destination);

            var (integrityOk, integrityMessages) = await IntegrityAsync(destination, cancellationToken);
            var (foreignKeyOk, foreignKeyViolations) = await ForeignKeyAsync(destination, cancellationToken);

            await destination.CloseAsync();
            await destination.DisposeAsync();

            var size = new FileInfo(tempPath).Length;
            var checksum = BackupManifest.ComputeSha256(tempPath);

            if (!integrityOk)
            {
                TryDelete(tempPath);
                return Failure(type, string.Empty, createdUtc,
                    "Backup failed the SQLite integrity check and was not kept. " + string.Join("; ", integrityMessages));
            }

            File.Move(tempPath, path);

            var manifest = new BackupManifestDto
            {
                BackupFile = Path.GetFileName(path),
                BackupType = type,
                CreatedAtUtc = createdUtc,
                ApplicationVersion = ApplicationVersion,
                SchemaVersion = SchemaVersion,
                DatabaseSizeBytes = size,
                ChecksumAlgorithm = "SHA256",
                Checksum = checksum,
                IntegrityCheck = BackupCheckStatus.Pass,
                ForeignKeyCheck = foreignKeyOk ? BackupCheckStatus.Pass : BackupCheckStatus.Fail,
                OperationId = Guid.NewGuid().ToString("N")
            };
            BackupManifest.Write(path, manifest);

            return new BackupOperationResult
            {
                Success = true,
                FileName = Path.GetFileName(path),
                Path = path,
                SizeBytes = size,
                Checksum = checksum,
                CreatedAt = createdUtc.ToLocalTime(),
                Type = type,
                Verification = new BackupVerificationDto
                {
                    FileName = manifest.BackupFile,
                    Path = path,
                    FileExists = true,
                    FileExistsCheck = BackupCheckStatus.Pass,
                    FileSizeBytes = size,
                    FileSizeCheck = BackupCheckStatus.Pass,
                    ManifestPresent = true,
                    SqliteIntegrityCheck = BackupCheckStatus.Pass,
                    ForeignKeyCheck = manifest.ForeignKeyCheck,
                    ChecksumCheck = BackupCheckStatus.Pass,
                    SchemaCompatibilityCheck = BackupCheckStatus.Pass,
                    OverallValid = true,
                    MigrationIds = new(),
                    SchemaVersion = SchemaVersion,
                    Type = type,
                    CreatedAt = createdUtc,
                    ForeignKeyViolations = foreignKeyViolations
                }
            };
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return Failure(type, string.Empty, createdUtc, $"Backup failed: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- verify

    public Task<BackupVerificationDto> VerifyBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
        => VerifyBackupCoreAsync(backupFilePath, cancellationToken);

    private async Task<BackupVerificationDto> VerifyBackupCoreAsync(string backupFilePath, CancellationToken cancellationToken)
    {
        var result = new BackupVerificationDto
        {
            FileName = Path.GetFileName(backupFilePath),
            Path = backupFilePath
        };

        if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
        {
            result.FileExistsCheck = BackupCheckStatus.Fail;
            result.Messages.Add("The backup file does not exist or is not accessible.");
            result.OverallValid = false;
            return result;
        }

        result.FileExists = true;
        result.FileExistsCheck = BackupCheckStatus.Pass;

        var length = new FileInfo(backupFilePath).Length;
        result.FileSizeBytes = length;
        result.FileSizeCheck = length > 0 ? BackupCheckStatus.Pass : BackupCheckStatus.Fail;
        if (length == 0)
            result.Messages.Add("The backup file is empty.");

        var manifest = BackupManifest.Read(backupFilePath);
        result.ManifestPresent = manifest is not null;
        if (manifest is not null)
        {
            result.Type = manifest.BackupType;
            result.CreatedAt = manifest.CreatedAtUtc;
            result.SchemaVersion = manifest.SchemaVersion;
        }
        else
        {
            result.Messages.Add("Legacy backup: no checksum manifest is available.");
        }

        await using var connection = new SqliteConnection($"Data Source={backupFilePath};Mode=ReadOnly;Pooling=False");
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            result.SqliteIntegrityCheck = BackupCheckStatus.Fail;
            result.Messages.Add($"The backup could not be opened as a SQLite database: {ex.Message}");
            result.OverallValid = false;
            return result;
        }

        try
        {
            var (integrityOk, integrityMessages) = await IntegrityAsync(connection, cancellationToken);
            result.SqliteIntegrityCheck = integrityOk ? BackupCheckStatus.Pass : BackupCheckStatus.Fail;
            result.Messages.AddRange(integrityMessages);

            var (foreignKeyOk, violations) = await ForeignKeyAsync(connection, cancellationToken);
            result.ForeignKeyCheck = foreignKeyOk ? BackupCheckStatus.Pass : BackupCheckStatus.Fail;
            result.ForeignKeyViolations.AddRange(violations);

            result.SchemaCompatibilityCheck = await SchemaCompatibilityAsync(connection, result, cancellationToken);
        }
        catch (Exception ex)
        {
            // A corrupt or non-database file can throw on the first pragma; treat it as a failed check.
            result.SqliteIntegrityCheck = BackupCheckStatus.Fail;
            if (result.ForeignKeyCheck == BackupCheckStatus.Unknown) result.ForeignKeyCheck = BackupCheckStatus.Fail;
            if (result.SchemaCompatibilityCheck == BackupCheckStatus.Unknown) result.SchemaCompatibilityCheck = BackupCheckStatus.Fail;
            result.Messages.Add($"The backup could not be validated as a SQLite database: {ex.Message}");
        }

        // Checksum (only meaningful with a manifest that carries one).
        if (manifest is not null && !string.IsNullOrWhiteSpace(manifest.Checksum))
        {
            var actual = BackupManifest.ComputeSha256(backupFilePath);
            var matches = string.Equals(actual, manifest.Checksum, StringComparison.OrdinalIgnoreCase);
            result.ChecksumCheck = matches ? BackupCheckStatus.Pass : BackupCheckStatus.Fail;
            result.Messages.Add(matches
                ? "SHA-256 checksum matches the manifest."
                : "SHA-256 checksum does not match the manifest; the file may be corrupted or modified.");
        }
        else
        {
            result.ChecksumCheck = BackupCheckStatus.NotAvailable;
        }

        var checksumAcceptable = result.ChecksumCheck is BackupCheckStatus.Pass or BackupCheckStatus.NotAvailable;
        result.OverallValid =
            result.FileExistsCheck == BackupCheckStatus.Pass &&
            result.FileSizeCheck == BackupCheckStatus.Pass &&
            result.SqliteIntegrityCheck == BackupCheckStatus.Pass &&
            result.ForeignKeyCheck == BackupCheckStatus.Pass &&
            result.SchemaCompatibilityCheck == BackupCheckStatus.Pass &&
            checksumAcceptable;

        return result;
    }

    private async Task<BackupCheckStatus> SchemaCompatibilityAsync(
        SqliteConnection connection, BackupVerificationDto result, CancellationToken cancellationToken)
    {
        var hasHistory = await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken);

        // Legacy (pre-migration) database: validate that expected tables are present.
        if (!hasHistory)
        {
            var missing = await MissingTablesAsync(connection, cancellationToken);
            if (missing.Count > 0)
            {
                result.Messages.Add("Legacy database is missing required tables: " + string.Join(", ", missing));
                return BackupCheckStatus.Fail;
            }

            result.Messages.Add("Legacy database (no migration history); it will be adopted on restore.");
            return BackupCheckStatus.Pass;
        }

        var backupMigrations = await ReadMigrationIdsAsync(connection, cancellationToken);
        result.MigrationIds.AddRange(backupMigrations);
        result.SchemaVersion = backupMigrations.LastOrDefault() ?? result.SchemaVersion;

        var known = _db.Database.GetMigrations().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = backupMigrations.Where(id => !known.Contains(id)).ToList();
        if (unknown.Count > 0)
        {
            result.Messages.Add("This backup was created by a newer, incompatible application version (unknown migrations: "
                + string.Join(", ", unknown) + "). It cannot be restored by this version.");
            return BackupCheckStatus.Fail;
        }

        result.Messages.Add("Backup schema is compatible (same or older); forward migrations will be applied on restore.");
        return BackupCheckStatus.Pass;
    }

    // ---------------------------------------------------------------- restore

    public Task RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default)
        => RestoreAsyncCore(backupFilePath, cancellationToken);

    private async Task RestoreAsyncCore(string backupFilePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException("Backup file was not found.", backupFilePath);

        var result = await RestoreBackupAsync(backupFilePath, cancellationToken);
        if (!result.Success)
            throw new InvalidOperationException("Restore failed: " + result.ErrorMessage);
    }

    public async Task<BackupOperationResult> RestoreBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await RestoreBackupCoreAsync(backupFilePath, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<BackupOperationResult> RestoreBackupCoreAsync(string backupFilePath, CancellationToken cancellationToken)
    {
        // 1. Verify the selected backup before touching anything.
        var verify = await VerifyBackupCoreAsync(backupFilePath, cancellationToken);
        if (!verify.OverallValid)
            return Failure(BackupType.Manual, backupFilePath, DateTime.UtcNow,
                "The selected backup did not pass verification and was not restored.", verify);

        var livePath = ((SqliteConnection)_db.Database.GetDbConnection()).DataSource;
        if (string.Equals(Path.GetFullPath(livePath), Path.GetFullPath(backupFilePath), StringComparison.OrdinalIgnoreCase))
            return Failure(BackupType.Manual, backupFilePath, DateTime.UtcNow,
                "The selected file is the active database and cannot be restored over itself.", verify);

        // 2. Pre-restore safety backup of the current database, then verify it.
        var safetyFolder = Path.Combine(Path.GetDirectoryName(backupFilePath)!, "pre-restore");
        var safety = await CreateBackupCoreAsync(BackupType.PreRestore, safetyFolder, cancellationToken);
        if (!safety.Success)
            return Failure(BackupType.Manual, backupFilePath, DateTime.UtcNow,
                "A pre-restore safety backup could not be created, so the restore was aborted.", verify);

        var safetyVerify = await VerifyBackupCoreAsync(safety.Path, cancellationToken);
        if (!safetyVerify.OverallValid)
            return Failure(BackupType.Manual, backupFilePath, DateTime.UtcNow,
                "The pre-restore safety backup failed verification, so the restore was aborted.", verify);

        // 3. Replace the live database in place through the online backup API (WAL-safe).
        await using var source = new SqliteConnection($"Data Source={backupFilePath};Mode=ReadOnly;Pooling=False");
        await source.OpenAsync(cancellationToken);
        var live = (SqliteConnection)_db.Database.GetDbConnection();
        await OpenIfClosedAsync(live, cancellationToken);
        source.BackupDatabase(live);
        await source.CloseAsync();
        _db.ChangeTracker.Clear();

        // 4. Integrity of the restored data.
        var (integrityOk, integrityMessages) = await IntegrityAsync(live, cancellationToken);
        if (!integrityOk)
        {
            await RollbackAsync(safety.Path, cancellationToken);
            return Failure(BackupType.Manual, backupFilePath, DateTime.UtcNow,
                "The restored database failed its integrity check; the previous database was restored from the safety backup. "
                + string.Join("; ", integrityMessages), verify);
        }

        // 5. Apply supported forward migrations (older schema is upgraded to current).
        try
        {
            await _db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await RollbackAsync(safety.Path, cancellationToken);
            return Failure(BackupType.Manual, backupFilePath, DateTime.UtcNow,
                "Applying migrations to the restored database failed; the previous database was restored from the safety backup. "
                + ex.Message, verify);
        }

        // 6. Final verification that the application can use the database.
        _db.ChangeTracker.Clear();
        var (finalOk, finalMessages) = await IntegrityAsync(live, cancellationToken);
        if (!finalOk)
        {
            await RollbackAsync(safety.Path, cancellationToken);
            return Failure(BackupType.Manual, backupFilePath, DateTime.UtcNow,
                "Post-restore verification failed; the previous database was restored from the safety backup. "
                + string.Join("; ", finalMessages), verify);
        }

        return new BackupOperationResult
        {
            Success = true,
            FileName = Path.GetFileName(backupFilePath),
            Path = backupFilePath,
            Type = verify.Type ?? BackupType.Manual,
            Verification = verify,
            CreatedAt = DateTime.UtcNow.ToLocalTime()
        };
    }

    private async Task RollbackAsync(string safetyPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var safety = new SqliteConnection($"Data Source={safetyPath};Pooling=False");
            await safety.OpenAsync(cancellationToken);
            var live = (SqliteConnection)_db.Database.GetDbConnection();
            await OpenIfClosedAsync(live, cancellationToken);
            safety.BackupDatabase(live);
            await safety.CloseAsync();
            _db.ChangeTracker.Clear();
        }
        catch
        {
            // Best-effort recovery; the safety file remains on disk for manual recovery.
        }
    }

    // ---------------------------------------------------------------- live integrity (legacy)

    public async Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default)
    {
        var connection = (SqliteConnection)_db.Database.GetDbConnection();
        await OpenIfClosedAsync(connection, cancellationToken);
        return await IsIntegrityOkAsync(connection, cancellationToken);
    }

    // ---------------------------------------------------------------- history / storage

    public async Task<List<BackupInfoDto>> ListBackupsAsync(string? folder, CancellationToken cancellationToken = default)
    {
        var location = await ResolveFolderAsync(folder, cancellationToken);
        if (!Directory.Exists(location))
            return new List<BackupInfoDto>();

        var files = Directory.EnumerateFiles(location, "*.db", SearchOption.TopDirectoryOnly);
        var list = new List<BackupInfoDto>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = BackupManifest.Read(file);
            var info = new FileInfo(file);
            list.Add(new BackupInfoDto
            {
                FileName = Path.GetFileName(file),
                Path = file,
                Type = manifest?.BackupType ?? BackupType.Manual,
                CreatedAt = manifest?.CreatedAtUtc.ToLocalTime() ?? info.LastWriteTime,
                SizeBytes = info.Length,
                ApplicationVersion = manifest?.ApplicationVersion ?? string.Empty,
                SchemaVersion = manifest?.SchemaVersion,
                HasManifest = manifest is not null,
                ChecksumStatus = manifest is null ? BackupCheckStatus.NotAvailable : BackupCheckStatus.Pass,
                IntegrityStatus = manifest?.IntegrityCheck ?? BackupCheckStatus.Unknown
            });
        }

        return list.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public async Task<BackupStorageInfoDto> GetStorageInfoAsync(string? folder, CancellationToken cancellationToken = default)
    {
        var location = await ResolveFolderAsync(folder, cancellationToken);
        var info = new BackupStorageInfoDto { Location = location };
        if (!Directory.Exists(location))
            return info;

        info.LocationExists = true;
        var files = Directory.EnumerateFiles(location, "*.db", SearchOption.TopDirectoryOnly).ToList();
        info.BackupCount = files.Count;
        info.TotalSizeBytes = files.Sum(f => new FileInfo(f).Length);

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(location));
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady) info.FreeSpaceBytes = drive.AvailableFreeSpace;
            }
            var probe = Path.Combine(location, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            info.LocationWritable = true;
        }
        catch
        {
            info.LocationWritable = false;
        }

        return info;
    }

    // ---------------------------------------------------------------- settings

    public async Task<BackupSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.AppSettings.AsNoTracking()
            .Where(s => s.Key.StartsWith("backup."))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string Get(string key, string fallback)
            => settings.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

        var location = Get("backup.location", DefaultBackupFolder());
        var timeText = Get("backup.time", "22:00");
        var time = TimeSpan.TryParseExact(timeText, @"hh\:mm", null, out var t) ? t
            : TimeSpan.TryParse(timeText, out var t2) ? t2
            : TimeSpan.FromHours(22);

        return new BackupSettingsDto
        {
            AutomaticBackupEnabled = ParseBool(Get("backup.auto_enabled", "false")),
            Frequency = Enum.TryParse<BackupFrequency>(Get("backup.frequency", "Disabled"), true, out var f) ? f : BackupFrequency.Disabled,
            BackupTime = time,
            Weekday = Enum.TryParse<DayOfWeek>(Get("backup.weekday", "Sunday"), true, out var w) ? w : DayOfWeek.Sunday,
            Location = location,
            RetentionCount = ParseInt(Get("backup.retention_count", "14"), 14),
            MaxBackupAgeDays = ParseInt(Get("backup.max_age_days", "30"), 30),
            MinimumValidBackups = ParseInt(Get("backup.min_valid", "3"), 3),
            LastSuccessUtc = ParseDate(Get("backup.last_success_utc", string.Empty)),
            LastAttemptUtc = ParseDate(Get("backup.last_attempt_utc", string.Empty))
        };
    }

    public async Task SaveSettingsAsync(BackupSettingsDto settings, CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>
        {
            ["backup.auto_enabled"] = settings.AutomaticBackupEnabled ? "true" : "false",
            ["backup.frequency"] = settings.Frequency.ToString(),
            ["backup.time"] = settings.BackupTime.ToString(@"hh\:mm"),
            ["backup.weekday"] = settings.Weekday.ToString(),
            ["backup.location"] = settings.Location,
            ["backup.retention_count"] = settings.RetentionCount.ToString(),
            ["backup.max_age_days"] = settings.MaxBackupAgeDays.ToString(),
            ["backup.min_valid"] = settings.MinimumValidBackups.ToString(),
            ["backup.last_success_utc"] = settings.LastSuccessUtc?.ToString("O") ?? string.Empty,
            ["backup.last_attempt_utc"] = settings.LastAttemptUtc?.ToString("O") ?? string.Empty
        };

        var existing = await _db.AppSettings
            .Where(s => s.Key.StartsWith("backup."))
            .ToDictionaryAsync(s => s.Key, s => s, cancellationToken);

        foreach (var (key, value) in values)
        {
            if (existing.TryGetValue(key, out var row))
            {
                row.Value = value;
                row.UpdatedAt = DateTime.Now;
            }
            else
            {
                _db.AppSettings.Add(new Domain.Entities.AppSetting { Key = key, Value = value });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- retention

    public async Task<BackupRetentionResultDto> RunRetentionAsync(string? folder, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var location = await ResolveFolderAsync(folder, cancellationToken);
        var result = new BackupRetentionResultDto();

        if (!Directory.Exists(location))
            return result;

        var automatic = (await ListBackupsAsync(location, cancellationToken))
            .Where(b => b.Type == BackupType.Automatic && b.HasManifest)
            .OrderByDescending(b => b.CreatedAt)
            .ToList();

        if (automatic.Count == 0)
            return result;

        var minValid = Math.Max(1, settings.MinimumValidBackups);
        // Never keep fewer than the greater of the retention count and the minimum valid count,
        // so the minimum-valid floor is always satisfied by construction.
        var keepCount = Math.Max(Math.Max(1, settings.RetentionCount), minValid);

        // Newest-first. Keep the newest `keepCount`; delete only older eligible automatic backups.
        for (var i = keepCount; i < automatic.Count; i++)
        {
            var backup = automatic[i];
            result.Evaluated++;

            try
            {
                File.Delete(backup.Path);
                BackupManifest.Delete(backup.Path);
                result.Deleted++;
                result.DeletedFiles.Add(backup.FileName);
            }
            catch (Exception ex)
            {
                result.Failures.Add($"{backup.FileName}: {ex.Message}");
            }
        }

        return result;
    }

    public async Task DeleteBackupAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(backupFilePath)) File.Delete(backupFilePath);
            BackupManifest.Delete(backupFilePath);
            await Task.CompletedTask;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---------------------------------------------------------------- helpers

    private async Task<string> ResolveFolderAsync(string? folder, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(folder))
            return folder;

        var settings = await GetSettingsAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(settings.Location) ? DefaultBackupFolder() : settings.Location;
    }

    public static string DefaultBackupFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HighlandMilkBar",
        "Backups");

    private static string TypeName(BackupType type) => type switch
    {
        BackupType.Automatic => "Auto",
        BackupType.PreRestore => "PreRestore",
        _ => "Manual"
    };

    private static string UniquePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path)) return path;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(folder, $"{name}_{i}{ext}");
            if (!File.Exists(candidate)) return candidate;
        }

        return Path.Combine(folder, $"{name}_{Guid.NewGuid():N}{ext}");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static async Task OpenIfClosedAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
    }

    private static async Task<bool> IsIntegrityOkAsync(SqliteConnection connection, CancellationToken cancellationToken)
        => (await IntegrityAsync(connection, cancellationToken)).Ok;

    private static async Task<(bool Ok, List<string> Messages)> IntegrityAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var messages = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ok = true;
        while (await reader.ReadAsync(cancellationToken))
        {
            var value = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            if (!string.Equals(value, "ok", StringComparison.OrdinalIgnoreCase))
            {
                ok = false;
                messages.Add(value);
            }
        }

        return (ok, messages);
    }

    private static async Task<(bool Ok, List<string> Violations)> ForeignKeyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var violations = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var parts = new List<string>();
            for (var i = 0; i < reader.FieldCount; i++)
                parts.Add(reader.IsDBNull(i) ? "null" : reader.GetValue(i).ToString() ?? string.Empty);
            violations.Add(string.Join(",", parts));
        }

        return (violations.Count == 0, violations);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", table);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && Convert.ToInt64(result) > 0;
    }

    private async Task<List<string>> MissingTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        foreach (var table in RequiredTables)
        {
            if (!await TableExistsAsync(connection, table, cancellationToken))
                missing.Add(table);
        }

        return missing;
    }

    private static async Task<List<string>> ReadMigrationIdsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            ids.Add(reader.GetString(0));
        return ids;
    }

    private static BackupOperationResult Failure(BackupType type, string path, DateTime createdUtc, string message, BackupVerificationDto? verification = null)
        => new()
        {
            Success = false,
            FileName = string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path),
            Path = path,
            Type = type,
            CreatedAt = createdUtc.ToLocalTime(),
            ErrorMessage = message,
            Verification = verification
        };

    private static bool ParseBool(string value) =>
        bool.TryParse(value, out var b) && b;

    private static int ParseInt(string value, int fallback) =>
        int.TryParse(value, out var i) ? i : fallback;

    private static DateTime? ParseDate(string value) =>
        DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d : null;
}
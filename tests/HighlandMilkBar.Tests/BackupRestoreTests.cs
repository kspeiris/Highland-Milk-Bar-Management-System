using System.Text;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using HighlandMilkBar.Infrastructure.Services.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class BackupRestoreTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private BackupService _backup = null!;
    private string _folder = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _backup = new BackupService(Db);
        _folder = Path.Combine(Path.GetTempPath(), $"hmb-bk-{Guid.NewGuid():N}");
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        return _db.DisposeAsync();
    }

    private void NewFolder()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        Directory.CreateDirectory(_folder);
    }

    // ------------------------------------------------------------ create + verify

    [Fact]
    public async Task ManualBackup_CreatesVerifiedFileWithManifest()
    {
        await _db.AddCategoryAsync("BkCat");

        var result = await _backup.CreateBackupAsync(BackupType.Manual, _folder);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(result.Path));
        Assert.True(File.Exists(result.Path + ".manifest.json"));
        Assert.Equal(64, result.Checksum.Length);
        Assert.Equal("Manual", System.IO.Path.GetFileName(result.Path).Split('_')[1]);

        var verify = await _backup.VerifyBackupAsync(result.Path);
        Assert.True(verify.OverallValid);
        Assert.Equal(BackupCheckStatus.Pass, verify.SqliteIntegrityCheck);
        Assert.Equal(BackupCheckStatus.Pass, verify.ForeignKeyCheck);
        Assert.Equal(BackupCheckStatus.Pass, verify.ChecksumCheck);
        Assert.Equal(BackupCheckStatus.Pass, verify.SchemaCompatibilityCheck);
    }

    [Fact]
    public async Task Verify_MissingFile_IsInvalid()
    {
        var verify = await _backup.VerifyBackupAsync(Path.Combine(_folder, "nope.db"));
        Assert.False(verify.OverallValid);
        Assert.False(verify.FileExists);
        Assert.Equal(BackupCheckStatus.Fail, verify.FileExistsCheck);
    }

    [Fact]
    public async Task Verify_EmptyFile_IsInvalid()
    {
        NewFolder();
        var path = Path.Combine(_folder, "empty.db");
        await File.WriteAllTextAsync(path, string.Empty);

        var verify = await _backup.VerifyBackupAsync(path);
        Assert.False(verify.OverallValid);
        Assert.Equal(BackupCheckStatus.Fail, verify.FileSizeCheck);
    }

    [Fact]
    public async Task Verify_CorruptedFile_IsInvalid()
    {
        NewFolder();
        var path = Path.Combine(_folder, "corrupt.db");
        await File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes("this is not a sqlite database"));

        var verify = await _backup.VerifyBackupAsync(path);
        Assert.False(verify.OverallValid);
        Assert.Equal(BackupCheckStatus.Fail, verify.SqliteIntegrityCheck);
    }

    [Fact]
    public async Task Verify_ModifiedChecksum_IsInvalid()
    {
        var result = await _backup.CreateBackupAsync(BackupType.Manual, _folder);
        var manifestPath = result.Path + ".manifest.json";
        var text = await File.ReadAllTextAsync(manifestPath);
        const string marker = "\"checksum\": \"";
        var idx = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var tampered = text[..idx] + new string('0', 64) + text[(idx + 64)..];
        await File.WriteAllTextAsync(manifestPath, tampered);

        var verify = await _backup.VerifyBackupAsync(result.Path);
        Assert.False(verify.OverallValid);
        Assert.Equal(BackupCheckStatus.Fail, verify.ChecksumCheck);
    }

    [Fact]
    public async Task Verify_LegacyBackup_NoManifest_ChecksumNotAvailable()
    {
        var result = await _backup.CreateBackupAsync(BackupType.Manual, _folder);
        var legacy = Path.Combine(_folder, "HighlandMilkBar_Manual_20260101_000000.db");
        File.Copy(result.Path, legacy);
        File.Delete(result.Path + ".manifest.json");

        var verify = await _backup.VerifyBackupAsync(legacy);
        Assert.True(verify.OverallValid);
        Assert.True(verify.IsLegacy);
        Assert.Equal(BackupCheckStatus.NotAvailable, verify.ChecksumCheck);
    }

    [Fact]
    public async Task Verify_NewerSchema_IsRejected()
    {
        var result = await _backup.CreateBackupAsync(BackupType.Manual, _folder);

        // Inject an unknown (future) migration into a copy to simulate a newer schema.
        await using (var conn = new SqliteConnection($"Data Source={result.Path};Pooling=False"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('99991231235959_FutureMigration','10.0.0');";
            await cmd.ExecuteNonQueryAsync();
        }

        var verify = await _backup.VerifyBackupAsync(result.Path);
        Assert.False(verify.OverallValid);
        Assert.Equal(BackupCheckStatus.Fail, verify.SchemaCompatibilityCheck);
        Assert.Contains(verify.Messages, m => m.Contains("newer"));
    }

    // ------------------------------------------------------------ restore

    [Fact]
    public async Task Restore_RoundTripsData_AndKeepsSafetyBackup()
    {
        var original = await _db.AddCategoryAsync("RestoreCat");
        var result = await _backup.CreateBackupAsync(BackupType.Manual, _folder);

        Db.Categories.Remove(original);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        await _db.AddCategoryAsync("NewerCat");
        Assert.True(await Db.Categories.AnyAsync(c => c.Name == "NewerCat"));

        var restore = await _backup.RestoreBackupAsync(result.Path);
        Assert.True(restore.Success, restore.ErrorMessage);

        Db.ChangeTracker.Clear();
        Assert.True(await Db.Categories.AnyAsync(c => c.Name == "RestoreCat"));
        Assert.False(await Db.Categories.AnyAsync(c => c.Name == "NewerCat"));

        // The pre-restore safety backup must exist, verify, and still contain the newer record.
        var safety = await _backup.ListBackupsAsync(Path.Combine(_folder, "pre-restore"));
        var preRestore = safety.Single(b => b.Type == BackupType.PreRestore);
        var safetyVerify = await _backup.VerifyBackupAsync(preRestore.Path);
        Assert.True(safetyVerify.OverallValid);

        await using var conn = new SqliteConnection($"Data Source={preRestore.Path};Mode=ReadOnly;Pooling=False");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Categories WHERE Name='NewerCat';";
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Restore_RejectsCorruptedSource_AndLeavesLiveDataUntouched()
    {
        await _db.AddCategoryAsync("KeepCat");
        var result = await _backup.CreateBackupAsync(BackupType.Manual, _folder);

        await _db.AddCategoryAsync("NewCat");
        await File.WriteAllBytesAsync(result.Path, Encoding.ASCII.GetBytes("broken"));

        var restore = await _backup.RestoreBackupAsync(result.Path);
        Assert.False(restore.Success);

        Db.ChangeTracker.Clear();
        Assert.True(await Db.Categories.AnyAsync(c => c.Name == "NewCat"));
    }

    [Fact]
    public async Task Restore_RejectsNewerSchema()
    {
        var result = await _backup.CreateBackupAsync(BackupType.Manual, _folder);
        await using (var conn = new SqliteConnection($"Data Source={result.Path};Pooling=False"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('99991231235959_FutureMigration','10.0.0');";
            await cmd.ExecuteNonQueryAsync();
        }

        var restore = await _backup.RestoreBackupAsync(result.Path);
        Assert.False(restore.Success);
    }

    [Fact]
    public async Task Restore_RejectsActiveDatabaseAsSource()
    {
        var livePath = ((SqliteConnection)Db.Database.GetDbConnection()).DataSource;
        var restore = await _backup.RestoreBackupAsync(livePath);
        Assert.False(restore.Success);
    }

    // ------------------------------------------------------------ WAL

    [Fact]
    public async Task Backup_WithWalMode_ContainsCommittedData()
    {
        var live = (SqliteConnection)Db.Database.GetDbConnection();
        if (live.State != System.Data.ConnectionState.Open) await live.OpenAsync();
        await using (var pragma = live.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL;";
            await pragma.ExecuteNonQueryAsync();
        }

        await _db.AddCategoryAsync("WalCat");
        var result = await _backup.CreateBackupAsync(BackupType.Manual, _folder);

        var verify = await _backup.VerifyBackupAsync(result.Path);
        Assert.True(verify.OverallValid);

        await using var backupConn = new SqliteConnection($"Data Source={result.Path};Mode=ReadOnly;Pooling=False");
        await backupConn.OpenAsync();
        await using var cmd = backupConn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Categories WHERE Name='WalCat';";
        Assert.Equal(1, Convert.ToInt64(await cmd.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task ConcurrentBackups_ProduceDistinctFiles()
    {
        var t1 = _backup.CreateBackupAsync(BackupType.Manual, _folder);
        var t2 = _backup.CreateBackupAsync(BackupType.Manual, _folder);
        var results = await Task.WhenAll(t1, t2);

        Assert.True(results.All(r => r.Success));
        Assert.NotEqual(results[0].Path, results[1].Path);
        Assert.Equal(2, Directory.GetFiles(_folder, "*.db").Length);
    }

    // ------------------------------------------------------------ retention

    [Fact]
    public async Task Retention_DeletesOnlyEligibleAutomaticBackups()
    {
        NewFolder();
        await _backup.SaveSettingsAsync(new BackupSettingsDto
        {
            Location = _folder,
            RetentionCount = 2,
            MinimumValidBackups = 1,
            MaxBackupAgeDays = 30,
            AutomaticBackupEnabled = true
        });

        for (var i = 0; i < 5; i++)
            await _backup.CreateBackupAsync(BackupType.Automatic, _folder);

        var manual = await _backup.CreateBackupAsync(BackupType.Manual, _folder);
        var preRestore = await _backup.CreateBackupAsync(BackupType.PreRestore, _folder);

        var retention = await _backup.RunRetentionAsync(_folder);

        Assert.Equal(3, retention.Deleted);
        Assert.True(File.Exists(manual.Path));
        Assert.True(File.Exists(preRestore.Path));
        Assert.Equal(4, Directory.GetFiles(_folder, "*.db").Length); // 2 auto + manual + pre-restore
    }

    // ------------------------------------------------------------ settings

    [Fact]
    public async Task Settings_RoundTripThroughStore()
    {
        var settings = new BackupSettingsDto
        {
            AutomaticBackupEnabled = true,
            Frequency = BackupFrequency.Weekly,
            BackupTime = new TimeSpan(21, 30, 0),
            Weekday = DayOfWeek.Monday,
            Location = @"C:\Backups\HMB",
            RetentionCount = 10,
            MaxBackupAgeDays = 45,
            MinimumValidBackups = 5
        };

        await _backup.SaveSettingsAsync(settings);
        var loaded = await _backup.GetSettingsAsync();

        Assert.Equal(settings.AutomaticBackupEnabled, loaded.AutomaticBackupEnabled);
        Assert.Equal(BackupFrequency.Weekly, loaded.Frequency);
        Assert.Equal(settings.BackupTime, loaded.BackupTime);
        Assert.Equal(DayOfWeek.Monday, loaded.Weekday);
        Assert.Equal(settings.Location, loaded.Location);
        Assert.Equal(10, loaded.RetentionCount);
        Assert.Equal(45, loaded.MaxBackupAgeDays);
        Assert.Equal(5, loaded.MinimumValidBackups);
    }

    // ------------------------------------------------------------ scheduler

    [Fact]
    public async Task Scheduler_Daily_RunsOncePerPeriod()
    {
        NewFolder();
        var settings = new BackupSettingsDto
        {
            AutomaticBackupEnabled = true,
            Frequency = BackupFrequency.Daily,
            BackupTime = new TimeSpan(22, 0, 0),
            Location = _folder
        };
        await _backup.SaveSettingsAsync(settings);

        var clock = new MutableTimeProvider();
        var scheduler = new BackupSchedulerService(_backup, _backup, clock);

        clock.Set(DateTime.Today.AddHours(22).AddMinutes(30));
        Assert.True(await scheduler.TickAsync()); // due -> one backup
        Assert.Single(Directory.GetFiles(_folder, "*.db"));
        Assert.NotNull((await _backup.GetSettingsAsync()).LastSuccessUtc);

        clock.Set(DateTime.Today.AddHours(23));
        Assert.False(await scheduler.TickAsync()); // same period -> no duplicate
        Assert.Single(Directory.GetFiles(_folder, "*.db"));

        clock.Set(DateTime.Today.AddDays(1).AddHours(22).AddMinutes(30));
        Assert.True(await scheduler.TickAsync()); // next day -> runs
        Assert.Equal(2, Directory.GetFiles(_folder, "*.db").Length);
    }

    [Fact]
    public async Task Scheduler_Disabled_DoesNotRun()
    {
        NewFolder();
        await _backup.SaveSettingsAsync(new BackupSettingsDto
        {
            AutomaticBackupEnabled = false,
            Frequency = BackupFrequency.Daily,
            BackupTime = new TimeSpan(22, 0, 0),
            Location = _folder
        });

        var scheduler = new BackupSchedulerService(_backup, _backup, new MutableTimeProvider(DateTime.Today.AddHours(22).AddMinutes(30)));
        Assert.False(await scheduler.TickAsync());
        Assert.Empty(Directory.GetFiles(_folder, "*.db"));
    }

    [Fact]
    public async Task Scheduler_Weekly_RunsOnConfiguredDay()
    {
        NewFolder();
        await _backup.SaveSettingsAsync(new BackupSettingsDto
        {
            AutomaticBackupEnabled = true,
            Frequency = BackupFrequency.Weekly,
            BackupTime = new TimeSpan(22, 0, 0),
            Weekday = DayOfWeek.Monday,
            Location = _folder
        });

        // The next Monday at the scheduled time.
        var monday = DateTime.Today;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);
        monday = monday.AddHours(22).AddMinutes(30);
        var clock = new MutableTimeProvider(monday);
        var scheduler = new BackupSchedulerService(_backup, _backup, clock);

        Assert.True(await scheduler.TickAsync());
        Assert.Single(Directory.GetFiles(_folder, "*.db"));
        Assert.NotNull((await _backup.GetSettingsAsync()).LastSuccessUtc);
    }

    [Fact]
    public async Task Scheduler_FailedBackup_DoesNotUpdateLastSuccess()
    {
        // Use a file path as the "folder" so directory creation fails.
        var filePath = Path.Combine(Path.GetTempPath(), $"hmb-notdir-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(filePath, "x");
        try
        {
            await _backup.SaveSettingsAsync(new BackupSettingsDto
            {
                AutomaticBackupEnabled = true,
                Frequency = BackupFrequency.Daily,
                BackupTime = new TimeSpan(22, 0, 0),
                Location = filePath
            });

            var clock = new MutableTimeProvider(DateTime.Today.AddHours(22).AddMinutes(30));
            var scheduler = new BackupSchedulerService(_backup, _backup, clock);

            var attempted = await scheduler.TickAsync();
            var settings = await _backup.GetSettingsAsync();

            Assert.True(attempted);          // an attempt was made
            Assert.Null(settings.LastSuccessUtc); // but success was not recorded
            Assert.NotNull(settings.LastAttemptUtc);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utc;
        public MutableTimeProvider() : this(DateTime.Now) { }
        public MutableTimeProvider(DateTime local) => _utc = new DateTimeOffset(local);
        public void Set(DateTime local) => _utc = new DateTimeOffset(local);
        public override DateTimeOffset GetUtcNow() => _utc;
    }
}
# Database Setup — Highland Milk Bar Management System

## 1. Prerequisites

- Windows 10/11
- **.NET 10 SDK** (not only the runtime)
- EF Core CLI (installed as a local tool in this repo — see below)

## 2. EF Core tools (local)

The repository pins `dotnet-ef` in `dotnet-tools.json` so migrations are reproducible:

```powershell
dotnet tool restore          # installs dotnet-ef 10.0.15 from the manifest
dotnet dotnet-ef --version   # verify
```

All `dotnet ef` commands must be run as `dotnet dotnet-ef` (local tool) from the repo root.

## 3. Build

```powershell
dotnet build .\HighlandMilkBar.sln
```

## 4. Database location

Runtime database (per Windows user):

```
%LOCALAPPDATA%\HighlandMilkBar\highland-milkbar.db
```

The WPF app creates the folder if needed. The database file is excluded from Git
(`.gitignore` has `*.db`).

## 5. How the schema is created

The application does **not** call `EnsureCreated` or `EnsureDeleted`. On startup
`DbInitializer.InitializeAsync` runs:

1. `Database.MigrateAsync()` — applies all pending migrations (creates the DB on first run).
2. Idempotent seed of units, categories, starter products, expense categories and default
   settings (only inserts missing reference rows).

This never deletes or recreates user data.

## 6. Working with migrations

```powershell
# Add a new migration after changing entities/configurations
dotnet dotnet-ef migrations add <Name> `
  --project .\src\HighlandMilkBar.Infrastructure\HighlandMilkBar.Infrastructure.csproj `
  --startup-project .\src\HighlandMilkBar.Infrastructure\HighlandMilkBar.Infrastructure.csproj `
  --output-dir Data\Migrations

# Remove the most recent migration (only if not yet applied/shared)
dotnet dotnet-ef migrations remove --project .\src\HighlandMilkBar.Infrastructure\HighlandMilkBar.Infrastructure.csproj

# Verify the model matches the last migration
dotnet dotnet-ef migrations has-pending-model-changes --project .\src\HighlandMilkBar.Infrastructure\HighlandMilkBar.Infrastructure.csproj

# Generate SQL for review (offline)
dotnet dotnet-ef migrations script --project .\src\HighlandMilkBar.Infrastructure\HighlandMilkBar.Infrastructure.csproj
```

`DesignTimeDbContextFactory` lets `dotnet ef` build the context without launching WPF.

## 7. Run the application

```powershell
.\run.ps1
```

or

```powershell
dotnet run --project .\src\HighlandMilkBar.WPF\HighlandMilkBar.WPF.csproj
```

## 8. Run the tests

```powershell
dotnet test .\tests\HighlandMilkBar.Tests\HighlandMilkBar.Tests.csproj
```

Tests use a **real temporary SQLite database** created by applying the real migrations
(not the InMemory provider), so relational behaviour is exercised.

## 9. Backup and restore (services)

Implemented in `Infrastructure/Services/BackupService.cs` using SQLite's online backup API
(WAL-safe — safe while the DB is open):

```csharp
await backupService.CreateBackupAsync(@"D:\HighlandBackups");   // creates timestamped .db
await backupService.VerifyIntegrityAsync();                     // PRAGMA integrity_check
await backupService.RestoreAsync(@"D:\HighlandBackups\highland-milkbar-backup-YYYYMMDDHHMMSS.db");
```

Restore always:
1. Verifies the backup file with `PRAGMA integrity_check`.
2. Creates an automatic **pre-restore safety backup**.
3. Overwrites the live database from the backup.

## 10. Troubleshooting

- **"The process cannot access the file …dll"** during build: the WPF app is still running.
  Close it, or `Get-Process HighlandMilkBar | Stop-Process -Force`, then rebuild.
- **"PendingModelChanges"**: the model changed without a migration. Add a migration (section 6).
- **Missing `dotnet-ef`**: run `dotnet tool restore` from the repo root.
- **Reset a test/dev database**: delete the `.db` file; it is recreated by migrations on next
  start. Never do this on a database that holds live business data.
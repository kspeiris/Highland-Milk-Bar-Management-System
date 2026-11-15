# Security Review

Practical security review for a local, single-user Windows desktop application (Step 15). This is a code-and-configuration review, **not** a penetration test.

## Dependency review

`dotnet list package --vulnerable --include-transitive` reports **no vulnerable packages** across Domain, Application, Infrastructure, WPF and Tests. Direct dependencies: EF Core (SQLite), ClosedXML (Excel export), xUnit (tests). No duplicate/obsolete libraries observed.

## Input validation

- Product, customer, supplier, expense and financial inputs are validated in the service layer (required fields, numeric ranges, uniqueness, email format, status transitions).
- Numeric/decimal values are validated (non-negative where required; discount/margin bounds enforced).
- Backup time (`HH:mm`) and retention values are validated in `BackupSettingsViewModel`.

## File operations

- **CSV formula injection:** cells beginning with `=`, `+`, `-`, `@`, tab or CR are prefixed with `'`; covered by `CsvExport_EscapesAndPreventsFormulaInjection`.
- **Export filenames:** sanitized (invalid characters replaced); destination chosen via `SaveFileDialog`.
- **Backup destination:** chosen via folder dialog; validated (creatable, writable) before saving settings. Backups are written outside the install directory by default (`%LOCALAPPDATA%\HighlandMilkBar\Backups`).
- **Restore source:** verified before use; the active database is rejected as a restore source; corrupted/newer-schema sources are rejected. Covered by `BackupRestoreTests`.
- **Retention:** only deletes inside the managed backup folder; never touches unrelated files.

## Database operations

- All access via EF Core LINQ (parameterized). No raw SQL string concatenation.
- Transactions wrap multi-step operations (checkout, receiving, payments); failures roll back.
- Migrations are forward-only and preserve data; no `EnsureDeleted`.

## Logging

- `startup.log` records startup exceptions only; no customer PII or secrets are logged.
- Backup manifests record checksums/versions, not business data.

## Known limitations

- No authentication/authorization (single-user desktop app); physical access to the machine implies access to the data.
- No encryption at rest of the SQLite file; protect via OS-level disk encryption if required.
- No penetration test or runtime fuzzing performed.
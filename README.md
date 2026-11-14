# Highland Milk Bar Management System — Starter 0.1

A Windows desktop starter application for a single-user Highland Milk Bar. This package gives the project a runnable WPF shell, a local SQLite database, a first dashboard, and a working starter product catalogue. The other workspaces are intentionally shown as roadmap placeholders so they can be implemented in controlled phases with your coding agent.

## Technology

- C# / .NET 10 (Windows desktop)
- WPF + XAML
- MVVM-style view models and command binding
- Clean Architecture solution split: Domain, Application, Infrastructure, WPF
- Entity Framework Core 10 + SQLite
- Local-first operation; no internet connection is needed after NuGet dependencies have been restored

## Requirements

- Windows 10 or Windows 11
- .NET 10 SDK (not only the runtime)
- Visual Studio 2022/2026 with the .NET desktop development workload, or Visual Studio Code with the C# Dev Kit
- Internet access for the first NuGet restore

## Run the application

### Option 1 — PowerShell

1. Extract the ZIP file.
2. Open the extracted `HighlandMilkBar` folder.
3. Right-click `run.ps1` and choose **Run with PowerShell**, or open PowerShell in the folder and run:

   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\run.ps1
   ```

### Option 2 — Visual Studio

1. Open `HighlandMilkBar.sln`.
2. Set `HighlandMilkBar.WPF` as the startup project if it is not already selected.
3. Restore NuGet packages.
4. Run with `F5`.

### Option 3 — .NET CLI

```powershell
dotnet restore .\HighlandMilkBar.sln
dotnet run --project .\src\HighlandMilkBar.WPF\HighlandMilkBar.WPF.csproj
```

## What works in this starter

- Branded WPF application shell with sidebar navigation and a hybrid POS/ERP visual direction.
- Responsive desktop layout with dashboard KPI cards and quick-action navigation.
- SQLite database created automatically on first launch.
- Starter categories and clearly-placeholder product catalogue entries are seeded on first launch.
- Dashboard metrics read from local data: today's completed sales revenue, today's completed transactions, active products and low-stock products.
- Product catalogue reads from SQLite, searches by product name/SKU/category, and supports adding a product.
- Product validation for required SKU/name, unique SKU, active category and non-negative prices/quantities.
- Product prices and quantity fields use LKR and decimal quantities to support units such as litres, kilograms and individual units.

## What is intentionally not implemented yet

POS checkout, barcode workflow, receipt printing, sale returns, purchase receiving, supplier/customer CRUD, batch-level FIFO, recipe-based ingredient deductions, expense entry, full profit-and-loss reporting, PDF/Excel export, backup/restore, and installer packaging are documented in `docs/PRODUCT_REQUIREMENTS.md` and represented by navigation placeholders. Do not use this starter for live financial operations until those workflows are implemented and tested.

## Database location

The database is stored outside the project folder so that publishing or updating the app does not overwrite business data:

`%LOCALAPPDATA%\HighlandMilkBar\highland-milkbar.db`

This folder should be included in the future backup/restore workflow. Do not manually delete it if you need to preserve data.

## Starter catalogue data

The first run inserts example Highland-style catalogue names and broad categories. Seed prices and opening stock are zero; the application does not claim these are official Highland prices or actual outlet quantities. Edit them to match the outlet before any live use. The seed runs only when the database has no categories.

## Project structure

```text
HighlandMilkBar/
├── HighlandMilkBar.sln
├── docs/
│   ├── PRODUCT_REQUIREMENTS.md
│   ├── DESIGN_SYSTEM.md
│   └── CODING_AGENT_HANDOFF.md
├── src/
│   ├── HighlandMilkBar.Domain/
│   ├── HighlandMilkBar.Application/
│   ├── HighlandMilkBar.Infrastructure/
│   └── HighlandMilkBar.WPF/
└── run.ps1
```

## Important implementation note

The initial database uses `EnsureCreated` to get the first-run prototype running without a migration command. Before making schema changes for production, move the database lifecycle to EF Core migrations and provide a tested upgrade path. Never change or delete a user's live database during an app update.

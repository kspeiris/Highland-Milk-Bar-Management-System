# Export Format Rules

All formats are produced from the same `ReportResult` (columns + typed rows + summary). See `REPORTING_AND_EXPORT.md` for the pipeline.

## CSV (native)

- UTF-8 encoded (with BOM for spreadsheet compatibility).
- Header row from column names; comma-delimited; fields quoted when they contain a comma, quote or newline; embedded quotes doubled.
- Numbers written as plain decimals (no currency symbols); dates as `yyyy-MM-dd`.
- **Formula-injection protection:** any text cell beginning with `=`, `+`, `-`, `@`, tab or carriage return is prefixed with a single quote.

## Excel (.xlsx, ClosedXML — MIT)

- **Summary** sheet: business name, report title, period, generated timestamp, and key metrics.
- **Details** sheet: header row (bold), one row per record, frozen header, auto-filter, auto-sized columns.
- Real typed cells: currency/number as decimals (`#,##0.00`), integers as `0`, percentages as `0.0"%"`, dates as `yyyy-mm-dd`.

## PDF (dependency-free `SimplePdf`)

- Valid PDF 1.4, A4 portrait, Helvetica.
- Header: business name, report title, period, generated timestamp; summary metrics; detail table.
- Pagination with repeated table headers; footer with generated-by line and "Page X of Y".
- Non-ASCII characters are replaced with `?` (ASCII-only content stream).

## Filename

`{Title}_{period}.{ext}` with invalid filename characters replaced by `_`. The user chooses the destination via a Save dialog; existing files are overwritten only after confirmation.

## Read-only

Export never modifies business records; a test asserts row counts are unchanged after report generation and export.
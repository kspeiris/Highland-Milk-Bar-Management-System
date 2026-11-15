using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using HighlandMilkBar.Application.DTOs.Reports;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.Infrastructure.Services.Reports;

/// <summary>
/// Exports a generated <see cref="ReportResult"/> to CSV, Excel (.xlsx) or PDF.
/// The same dataset powers every format, so preview and exports always agree.
/// </summary>
public sealed class ReportExportService : IReportExportService
{
    public Task ExportAsync(ReportResult report, ReportExportFormat format, string path, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        switch (format)
        {
            case ReportExportFormat.Csv:
                WriteCsv(report, path);
                break;
            case ReportExportFormat.Excel:
                WriteExcel(report, path);
                break;
            case ReportExportFormat.Pdf:
                WritePdf(report, path);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
        return Task.CompletedTask;
    }

    // ---------------- CSV ----------------

    private static void WriteCsv(ReportResult report, string path)
    {
        var sb = new StringBuilder();
        foreach (var column in report.Columns)
            sb.Append(EscapeCsv(SanitizeText(column.Header))).Append(',');

        // remove trailing comma / newline handling
        if (sb.Length > 0) sb.Length--; // drop last comma
        sb.Append("\r\n");

        foreach (var row in report.Rows)
        {
            for (var i = 0; i < report.Columns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var value = i < row.Values.Count ? row.Values[i] : null;
                sb.Append(EscapeCsv(FormatForCsv(report.Columns[i], value)));
            }
            sb.Append("\r\n");
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string FormatForCsv(ReportColumn column, object? value)
    {
        if (value is null) return string.Empty;
        return column.Type switch
        {
            ReportCellType.Currency => ((decimal)Convert.ToDecimal(value, CultureInfo.InvariantCulture)).ToString("0.00", CultureInfo.InvariantCulture),
            ReportCellType.Number => ((decimal)Convert.ToDecimal(value, CultureInfo.InvariantCulture)).ToString("0.###", CultureInfo.InvariantCulture),
            ReportCellType.Integer => Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            ReportCellType.Percent => ((decimal)Convert.ToDecimal(value, CultureInfo.InvariantCulture)).ToString("0.0", CultureInfo.InvariantCulture),
            ReportCellType.Date => value is DateTime d ? d.ToString("yyyy-MM-dd") : value.ToString() ?? string.Empty,
            _ => SanitizeText(value.ToString() ?? string.Empty)
        };
    }

    private static string EscapeCsv(string text)
    {
        // Prevent spreadsheet formula injection for text values.
        if (text.Length > 0 && (text[0] is '=' or '+' or '-' or '@' or '\t' or '\r'))
            text = "'" + text;

        if (text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        return text;
    }

    // ---------------- Excel ----------------

    private static void WriteExcel(ReportResult report, string path)
    {
        using var workbook = new XLWorkbook();

        var summary = workbook.AddWorksheet("Summary");
        summary.Cell(1, 1).Value = "HIGHLAND MILK BAR";
        summary.Cell(2, 1).Value = report.Title;
        summary.Cell(3, 1).Value = "Reporting period";
        summary.Cell(3, 2).Value = report.PeriodLabel;
        summary.Cell(4, 1).Value = "Generated";
        summary.Cell(4, 2).Value = report.GeneratedAt;
        summary.Cell(4, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";

        var row = 6;
        foreach (var item in report.Summary)
        {
            summary.Cell(row, 1).Value = item.Label;
            summary.Cell(row, 2).Value = item.Value;
            row++;
        }
        summary.Columns().AdjustToContents();

        var details = workbook.AddWorksheet("Details");
        for (var c = 0; c < report.Columns.Count; c++)
        {
            var cell = details.Cell(1, c + 1);
            cell.Value = report.Columns[c].Header;
            cell.Style.Font.Bold = true;
        }

        for (var r = 0; r < report.Rows.Count; r++)
        {
            var values = report.Rows[r].Values;
            for (var c = 0; c < report.Columns.Count; c++)
            {
                var cell = details.Cell(r + 2, c + 1);
                var value = c < values.Count ? values[c] : null;
                ApplyCell(cell, report.Columns[c].Type, value);
            }
        }

        details.SheetView.FreezeRows(1);
        if (report.Rows.Count > 0)
            details.Range(1, 1, report.Rows.Count + 1, report.Columns.Count).SetAutoFilter();
        details.Columns().AdjustToContents();

        workbook.SaveAs(path);
    }

    private static void ApplyCell(IXLCell cell, ReportCellType type, object? value)
    {
        if (value is null) return;
        switch (type)
        {
            case ReportCellType.Currency:
                cell.Value = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case ReportCellType.Number:
                cell.Value = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case ReportCellType.Integer:
                cell.Value = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                cell.Style.NumberFormat.Format = "0";
                break;
            case ReportCellType.Percent:
                cell.Value = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                cell.Style.NumberFormat.Format = "0.0\"%\"";
                break;
            case ReportCellType.Date:
                if (value is DateTime d) { cell.Value = d; cell.Style.DateFormat.Format = "yyyy-mm-dd"; }
                else cell.Value = value.ToString() ?? string.Empty;
                break;
            default:
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }

    // ---------------- PDF (dependency-free) ----------------

    private static void WritePdf(ReportResult report, string path)
    {
        var doc = new SimplePdf();
        doc.Begin();

        doc.Title("HIGHLAND MILK BAR");
        doc.Subtitle(report.Title);
        doc.Line($"Reporting period: {report.PeriodLabel}");
        doc.Line($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm}");
        doc.Gap(6);

        foreach (var item in report.Summary)
            doc.Line($"{item.Label}: {item.Value}");
        doc.Gap(10);

        doc.Table(report.Columns.Select(c => c.Header).ToList(), BuildPdfRows(report));
        doc.End();

        File.WriteAllBytes(path, doc.ToBytes());
    }

    private static List<IReadOnlyList<string>> BuildPdfRows(ReportResult report)
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (var r in report.Rows)
        {
            var cells = new List<string>();
            for (var c = 0; c < report.Columns.Count; c++)
            {
                var value = c < r.Values.Count ? r.Values[c] : null;
                cells.Add(FormatForPdf(report.Columns[c], value));
            }
            rows.Add(cells);
        }
        return rows;
    }

    private static string FormatForPdf(ReportColumn column, object? value)
    {
        if (value is null) return string.Empty;
        return column.Type switch
        {
            ReportCellType.Currency => ((decimal)Convert.ToDecimal(value, CultureInfo.InvariantCulture)).ToString("N2", CultureInfo.InvariantCulture),
            ReportCellType.Number => ((decimal)Convert.ToDecimal(value, CultureInfo.InvariantCulture)).ToString("N2", CultureInfo.InvariantCulture),
            ReportCellType.Integer => Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString("N0", CultureInfo.InvariantCulture),
            ReportCellType.Percent => ((decimal)Convert.ToDecimal(value, CultureInfo.InvariantCulture)).ToString("N1", CultureInfo.InvariantCulture) + "%",
            ReportCellType.Date => value is DateTime d ? d.ToString("yyyy-MM-dd") : value.ToString() ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string SanitizeText(string text)
        => new(text.Select(ch => ch < 32 && ch != '\t' ? ' ' : ch).ToArray());
}
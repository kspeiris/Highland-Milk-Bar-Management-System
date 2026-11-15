using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs.Reports;
using HighlandMilkBar.Application.Services;
using Microsoft.Win32;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Report Centre: pick a report, set a period, generate a preview, export or print.</summary>
public sealed class ReportCentreViewModel : ObservableObject
{
    private readonly IReportCatalogService _catalog;
    private readonly IReportQueryService _query;
    private readonly IReportExportService _export;

    private ReportDefinition? _selectedReport;
    private EnumOption<ReportingPreset>? _selectedPreset;
    private string _fromDate = string.Empty;
    private string _toDate = string.Empty;
    private string _statusMessage = "Choose a report and select Generate.";
    private string _rowCountText = string.Empty;
    private bool _isBusy;
    private string? _error;
    private bool _hasSummary;
    private ReportResult? _result;

    public ReportCentreViewModel(IReportCatalogService catalog, IReportQueryService query, IReportExportService export)
    {
        _catalog = catalog;
        _query = query;
        _export = export;

        Reports = new ObservableCollection<ReportDefinition>(catalog.GetCatalog());
        PeriodOptions =
        [
            new EnumOption<ReportingPreset>("This month", ReportingPreset.ThisMonth),
            new EnumOption<ReportingPreset>("Last month", ReportingPreset.LastMonth),
            new EnumOption<ReportingPreset>("This year", ReportingPreset.ThisYear),
            new EnumOption<ReportingPreset>("Today", ReportingPreset.Today)
        ];
        SelectedPreset = PeriodOptions[0];
        SelectedReport = Reports.FirstOrDefault();

        GenerateCommand = new RelayCommand(_ => _ = GenerateAsync(), _ => SelectedReport is not null);
        ExportPdfCommand = new RelayCommand(_ => _ = ExportAsync(ReportExportFormat.Pdf), _ => _result is not null);
        ExportExcelCommand = new RelayCommand(_ => _ = ExportAsync(ReportExportFormat.Excel), _ => _result is not null);
        ExportCsvCommand = new RelayCommand(_ => _ = ExportAsync(ReportExportFormat.Csv), _ => _result is not null);
        PrintCommand = new RelayCommand(_ => Print(), _ => _result is not null);
    }

    public ObservableCollection<ReportDefinition> Reports { get; }
    public ObservableCollection<EnumOption<ReportingPreset>> PeriodOptions { get; }
    public ObservableCollection<ReportSummaryItem> Summary { get; } = new();

    public RelayCommand GenerateCommand { get; }
    public RelayCommand ExportPdfCommand { get; }
    public RelayCommand ExportExcelCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand PrintCommand { get; }

    public ReportDefinition? SelectedReport
    {
        get => _selectedReport;
        set { if (SetProperty(ref _selectedReport, value)) GenerateCommand.RaiseCanExecuteChanged(); }
    }

    public EnumOption<ReportingPreset>? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value) && value?.Value is { } preset)
            {
                var period = ReportingPeriod.FromPreset(preset);
                FromDate = period.Start.ToString("yyyy-MM-dd");
                ToDate = period.EndExclusive.AddDays(-1).ToString("yyyy-MM-dd");
            }
        }
    }

    public string FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public string ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string RowCountText { get => _rowCountText; private set => SetProperty(ref _rowCountText, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string? Error { get => _error; private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(Error);
    public bool HasSummary { get => _hasSummary; private set => SetProperty(ref _hasSummary, value); }

    public DataTable Preview { get; private set; } = new();

    public async Task GenerateAsync()
    {
        if (SelectedReport is not { } report || IsBusy) return;
        IsBusy = true;
        Error = null;
        try
        {
            var period = ResolvePeriod();
            _result = await _query.GenerateAsync(report.Key, period);

            Summary.Clear();
            foreach (var item in _result.Summary) Summary.Add(item);
            HasSummary = Summary.Count > 0;

            Preview = BuildTable(_result);
            RowCountText = $"{_result.Rows.Count} row(s)";
            StatusMessage = $"{_result.Title} generated for {_result.PeriodLabel}.";
            OnPropertyChanged(nameof(Preview));

            ExportPdfCommand.RaiseCanExecuteChanged();
            ExportExcelCommand.RaiseCanExecuteChanged();
            ExportCsvCommand.RaiseCanExecuteChanged();
            PrintCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            StatusMessage = "Report generation failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ReportingPeriod ResolvePeriod()
    {
        if (DateTime.TryParse(FromDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out var from) &&
            DateTime.TryParse(ToDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out var to) &&
            to >= from)
            return ReportingPeriod.Custom(from, to.AddDays(1));

        return ReportingPeriod.FromPreset(SelectedPreset?.Value ?? ReportingPreset.ThisMonth);
    }

    private async Task ExportAsync(ReportExportFormat format)
    {
        if (_result is not { } result) return;
        var (filter, ext) = format switch
        {
            ReportExportFormat.Pdf => ("PDF (*.pdf)|*.pdf", ".pdf"),
            ReportExportFormat.Excel => ("Excel (*.xlsx)|*.xlsx", ".xlsx"),
            _ => ("CSV (*.csv)|*.csv", ".csv")
        };

        var dialog = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = ext,
            FileName = BuildFileName(result, ext)
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await _export.ExportAsync(result, format, dialog.FileName);
            StatusMessage = $"Exported {result.Title} to {Path.GetFileName(dialog.FileName)}.";
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            StatusMessage = "Export failed.";
        }
    }

    private void Print()
    {
        if (_result is not { } result) return;
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;

        var document = BuildFlowDocument(result);
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, result.Title);
        StatusMessage = $"Sent {result.Title} to the printer.";
    }

    private static string BuildFileName(ReportResult result, string ext)
    {
        var period = result.PeriodLabel.Replace(' ', '_');
        var name = $"{result.Title}_{period}{ext}";
        return SanitizeFileName(name);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private static DataTable BuildTable(ReportResult result)
    {
        var table = new DataTable(result.Title);
        foreach (var column in result.Columns)
            table.Columns.Add(column.Header, ToClrType(column.Type));

        foreach (var row in result.Rows)
        {
            var values = new object?[result.Columns.Count];
            for (var i = 0; i < result.Columns.Count; i++)
            {
                var raw = i < row.Values.Count ? row.Values[i] : null;
                values[i] = ConvertValue(raw, result.Columns[i].Type);
            }
            table.Rows.Add(values);
        }
        return table;
    }

    private static Type ToClrType(ReportCellType type) => type switch
    {
        ReportCellType.Integer => typeof(int),
        ReportCellType.Number or ReportCellType.Currency or ReportCellType.Percent => typeof(decimal),
        ReportCellType.Date => typeof(DateTime),
        _ => typeof(string)
    };

    private static object? ConvertValue(object? value, ReportCellType type)
    {
        if (value is null or DBNull) return null;
        return type switch
        {
            ReportCellType.Integer => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            ReportCellType.Number or ReportCellType.Currency or ReportCellType.Percent => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            ReportCellType.Date => value is DateTime d ? d : DateTime.TryParse(value.ToString(), out var parsed) ? parsed : (object?)null,
            _ => value.ToString()
        };
    }

    private static FlowDocument BuildFlowDocument(ReportResult result)
    {
        var doc = new FlowDocument { PagePadding = new Thickness(40) };

        doc.Blocks.Add(new Paragraph(new Run("HIGHLAND MILK BAR") { FontSize = 20, FontWeight = FontWeights.Bold }));
        doc.Blocks.Add(new Paragraph(new Run(result.Title) { FontSize = 16, FontWeight = FontWeights.Bold }));
        doc.Blocks.Add(new Paragraph(new Run($"Reporting period: {result.PeriodLabel}")));
        doc.Blocks.Add(new Paragraph(new Run($"Generated: {result.GeneratedAt:yyyy-MM-dd HH:mm}")));

        if (result.Summary.Count > 0)
        {
            var summary = new Paragraph();
            foreach (var item in result.Summary)
                summary.Inlines.Add(new Run($"{item.Label}: {item.Value}    ") { FontWeight = FontWeights.SemiBold });
            doc.Blocks.Add(summary);
        }

        if (result.Columns.Count > 0 && result.Rows.Count > 0)
        {
            var table = new Table { CellSpacing = 0 };
            var rowGroup = new TableRowGroup();
            var header = new TableRow();
            foreach (var column in result.Columns)
            {
                var cell = new TableCell(new Paragraph(new Run(column.Header) { FontWeight = FontWeights.Bold }));
                header.Cells.Add(cell);
            }
            rowGroup.Rows.Add(header);

            foreach (var row in result.Rows)
            {
                var tableRow = new TableRow();
                for (var i = 0; i < result.Columns.Count; i++)
                {
                    var raw = i < row.Values.Count ? row.Values[i] : null;
                    var text = result.Columns[i].Type switch
                    {
                        ReportCellType.Currency => ((decimal)Convert.ToDecimal(raw ?? 0m, CultureInfo.InvariantCulture)).ToString("N2"),
                        ReportCellType.Date => raw is DateTime d ? d.ToString("yyyy-MM-dd") : raw?.ToString() ?? string.Empty,
                        _ => raw?.ToString() ?? string.Empty
                    };
                    tableRow.Cells.Add(new TableCell(new Paragraph(new Run(text))));
                }
                rowGroup.Rows.Add(tableRow);
            }

            for (var i = 0; i < result.Columns.Count; i++)
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.RowGroups.Add(rowGroup);
            doc.Blocks.Add(table);
        }

        return doc;
    }
}
namespace HighlandMilkBar.Application.DTOs.Reports;

/// <summary>Report grouping categories.</summary>
public enum ReportCategory
{
    Sales,
    Products,
    Inventory,
    Purchases,
    Customers,
    Expenses,
    Financial,
    MilkBar,
    Analytics
}

/// <summary>How a column's values are formatted in exports.</summary>
public enum ReportCellType
{
    Text,
    Integer,
    Number,
    Currency,
    Percent,
    Date
}

/// <summary>A report column definition.</summary>
public sealed class ReportColumn
{
    public string Header { get; init; } = string.Empty;
    public ReportCellType Type { get; init; } = ReportCellType.Text;
}

/// <summary>One report row (values aligned to the columns).</summary>
public sealed class ReportRow
{
    public IReadOnlyList<object?> Values { get; init; } = Array.Empty<object?>();
}

/// <summary>A headline metric shown above the detail table.</summary>
public sealed class ReportSummaryItem
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

/// <summary>A generated report dataset — powers preview and every export format.</summary>
public sealed class ReportResult
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string PeriodLabel { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    public IReadOnlyList<ReportSummaryItem> Summary { get; init; } = Array.Empty<ReportSummaryItem>();
    public IReadOnlyList<ReportColumn> Columns { get; init; } = Array.Empty<ReportColumn>();
    public IReadOnlyList<ReportRow> Rows { get; init; } = Array.Empty<ReportRow>();
}

/// <summary>A report the user can choose from the catalogue.</summary>
public sealed class ReportDefinition
{
    public string Key { get; init; } = string.Empty;
    public ReportCategory Category { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool RequiresPeriod { get; init; }
}

/// <summary>Supported export formats.</summary>
public enum ReportExportFormat
{
    Pdf,
    Excel,
    Csv
}
using HighlandMilkBar.Application.DTOs.Reports;

namespace HighlandMilkBar.Application.Services;

/// <summary>The list of reports the application can generate.</summary>
public interface IReportCatalogService
{
    IReadOnlyList<ReportDefinition> GetCatalog();
}

/// <summary>Builds a report dataset by reusing the existing domain query services.</summary>
public interface IReportQueryService
{
    Task<ReportResult> GenerateAsync(string reportKey, Common.ReportingPeriod period, CancellationToken cancellationToken = default);
}

/// <summary>Exports a generated report dataset to PDF, Excel or CSV. Read-only.</summary>
public interface IReportExportService
{
    Task ExportAsync(ReportResult report, ReportExportFormat format, string path, CancellationToken cancellationToken = default);
}
using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Read-only business intelligence layer. Composes the existing domain query services
/// (financial, inventory, customer, milk bar, purchase, expense) with a few database-side
/// sales/product aggregates that no single service exposes. It never writes to any ledger.
/// </summary>
public interface IBusinessAnalyticsQueryService
{
    Task<BusinessOverviewDto> GetOverviewAsync(ReportingPeriod period, CancellationToken cancellationToken = default);

    Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(ReportingPeriod period, CancellationToken cancellationToken = default);

    Task<ProductAnalyticsDto> GetProductAnalyticsAsync(ReportingPeriod period, CancellationToken cancellationToken = default);

    Task<InventoryOverviewDto> GetInventoryAnalyticsAsync(CancellationToken cancellationToken = default);

    Task<CustomerAnalyticsDto> GetCustomerAnalyticsAsync(CancellationToken cancellationToken = default);

    Task<MilkBarOverviewDto> GetMilkBarAnalyticsAsync(CancellationToken cancellationToken = default);

    Task<PurchaseDashboardDto> GetPurchaseAnalyticsAsync(CancellationToken cancellationToken = default);

    Task<ExpenseReportDto> GetExpenseAnalyticsAsync(ReportingPeriod period, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BusinessAlertDto>> GetBusinessAlertsAsync(ReportingPeriod period, CancellationToken cancellationToken = default);
}
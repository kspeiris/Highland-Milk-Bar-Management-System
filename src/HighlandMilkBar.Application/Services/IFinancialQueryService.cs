using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Read-only financial reporting. All figures are derived from the existing sales,
/// sales returns, supplier payments and expense ledgers — this layer never writes to
/// those ledgers, so it cannot alter sales, purchases, inventory or cash balances.
/// </summary>
public interface IFinancialQueryService
{
    /// <summary>Profit-and-loss + cash + position summary for the inclusive/exclusive date window.</summary>
    Task<FinancialSummaryDto> GetSummaryAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Cash-flow statement (cash-basis) for the window.</summary>
    Task<CashFlowDto> GetCashFlowAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Sales-tender payment-mix (all methods) for the window.</summary>
    Task<IReadOnlyList<PaymentBreakdownDto>> GetSalesPaymentBreakdownAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Supplier-payment mix (all methods) for the window.</summary>
    Task<IReadOnlyList<PaymentBreakdownDto>> GetSupplierPaymentBreakdownAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Expense payment-mix (all methods) for the window.</summary>
    Task<IReadOnlyList<PaymentBreakdownDto>> GetExpensePaymentBreakdownAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Outstanding payables and ageing as at now.</summary>
    Task<SupplierLiabilityDto> GetSupplierLiabilitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>Supplier statement: received value, charges, payments and closing balance for one supplier.</summary>
    Task<SupplierLiabilityLineDto> GetSupplierStatementAsync(int supplierId, CancellationToken cancellationToken = default);

    /// <summary>Trailing monthly performance series (oldest first).</summary>
    Task<IReadOnlyList<FinancialSeriesPointDto>> GetMonthlySeriesAsync(int months = 12, CancellationToken cancellationToken = default);

    /// <summary>Cash-session reconciliation for open and recently closed sessions (newest first).</summary>
    Task<IReadOnlyList<CashReconciliationDto>> GetCashReconciliationAsync(CancellationToken cancellationToken = default);
}
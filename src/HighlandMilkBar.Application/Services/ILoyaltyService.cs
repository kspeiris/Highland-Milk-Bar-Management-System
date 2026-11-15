using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Loyalty points configuration and ledger. Award/reversal methods are called from
/// <see cref="ISalesService"/> inside the existing checkout/return transaction so the ledger
/// stays consistent with the sale.
/// </summary>
public interface ILoyaltyService
{
    Task<LoyaltySettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task UpdateSettingsAsync(LoyaltyUpdateSettingsRequest request, CancellationToken cancellationToken = default);

    Task<LoyaltyBalanceDto> GetBalanceAsync(int customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoyaltyTransactionDto>> GetHistoryAsync(int customerId, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Awards points for a completed sale. Must be called inside the checkout transaction
    /// (same <c>DbContext</c>) so the ledger and sale commit together. Does nothing for
    /// walk-in sales, disabled loyalty, non-qualifying amounts or a sale already awarded.
    /// </summary>
    Task AwardForSaleAsync(int saleId, int customerId, decimal saleTotal, string receiptNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses points for a return against a sale. Must be called inside the return
    /// transaction. Reverses a proportional share of the originally earned points (floored),
    /// or all remaining points on a full return, and never drives the balance negative.
    /// </summary>
    Task ReverseForReturnAsync(
        int saleId,
        int customerId,
        decimal saleTotal,
        decimal returnedAmount,
        bool isFullReturn,
        int salesReturnId,
        string returnNumber,
        CancellationToken cancellationToken = default);
}
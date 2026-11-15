using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Purchase workflow: create and edit drafts, receive goods into inventory, cancel, and
/// query purchase data. Receiving uses the shared <see cref="IInventoryService"/> so a
/// received purchase is the single source of inventory batches and stock movements — there
/// is no separate purchase stock system (PUR-08/09/10/11/18).
/// </summary>
public interface IPurchaseService
{
    /// <summary>
    /// Receives a purchase: creates inventory batches and stock movements and updates
    /// product stock atomically. Only a received (non-draft) purchase increases stock.
    /// </summary>
    Task<ReceivePurchaseResult> ReceiveAsync(int purchaseId, CancellationToken cancellationToken = default);

    Task<PagedResult<PurchaseRowDto>> SearchAsync(PurchaseFilter filter, CancellationToken cancellationToken = default);

    Task<PurchaseDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a draft purchase. Does not touch stock (PUR-08).</summary>
    Task<PurchaseDetailDto> CreateAsync(CreatePurchaseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a draft purchase. Rejected once received or cancelled.</summary>
    Task<PurchaseDetailDto> UpdateAsync(UpdatePurchaseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Cancels a draft or received purchase without ever deleting history.</summary>
    Task CancelAsync(int id, CancellationToken cancellationToken = default);

    Task<PurchaseDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}

/// <summary>Outstanding balances, purchase price history and purchase reports.</summary>
public interface IPurchaseQueryService
{
    Task<IReadOnlyList<OutstandingBalanceDto>> GetOutstandingAsync(
        int? supplierId, bool onlyOverdue, CancellationToken cancellationToken = default);

    Task<PriceHistoryDto> GetPriceHistoryAsync(int productId, int? supplierId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PriceHistoryRowDto>> GetProductPurchaseHistoryAsync(
        int productId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}
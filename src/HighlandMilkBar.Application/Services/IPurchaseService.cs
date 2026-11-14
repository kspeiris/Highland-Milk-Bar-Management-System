using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

public interface IPurchaseService
{
    /// <summary>
    /// Receives a purchase: creates inventory batches and stock movements and updates
    /// product stock atomically. Only a received (non-draft) purchase increases stock.
    /// </summary>
    Task<ReceivePurchaseResult> ReceiveAsync(int purchaseId, CancellationToken cancellationToken = default);
}
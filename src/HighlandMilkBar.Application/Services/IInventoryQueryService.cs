using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Read-only queries that back the inventory screens. Kept separate from
/// <see cref="IInventoryService"/> so commands (which mutate stock) stay distinct from queries.
/// </summary>
public interface IInventoryQueryService
{
    Task<InventoryOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<StockRowDto>> GetStockAsync(StockFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<BatchRowDto>> GetBatchesAsync(BatchFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<MovementRowDto>> GetMovementsAsync(MovementFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpiryRowDto>> GetNearExpiryAsync(int withinDays, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpiryRowDto>> GetExpiredAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LowStockRowDto>> GetLowStockAsync(CancellationToken cancellationToken = default);
}
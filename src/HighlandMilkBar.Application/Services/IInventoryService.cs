using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Applies and traces all stock changes. Every quantity change produces a
/// <see cref="StockMovement"/> and consumption produces a <see cref="StockAllocation"/>
/// so batch-level FIFO/FEFO behaviour is auditable.
/// </summary>
public interface IInventoryService
{
    Task<InventoryBatch> ReceiveAsync(StockReceiptRequest request, CancellationToken cancellationToken = default);

    Task<AllocationResult> AllocateAsync(
        int productId,
        decimal quantity,
        StockMovementType movementType,
        MovementReferenceType? referenceType,
        int? referenceId,
        int? saleItemId,
        AllocationPolicy policy,
        CancellationToken cancellationToken = default);

    Task<StockAdjustment> AdjustAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default);

    Task<decimal> GetAvailableStockAsync(int productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BatchDto>> GetBatchesAsync(int productId, CancellationToken cancellationToken = default);
}
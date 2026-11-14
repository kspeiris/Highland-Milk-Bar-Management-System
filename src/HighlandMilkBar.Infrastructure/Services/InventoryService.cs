using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

public sealed class InventoryService(AppDbContext db) : IInventoryService
{
    public async Task<InventoryBatch> ReceiveAsync(StockReceiptRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
            throw new ArgumentException("Received quantity must be greater than zero.", nameof(request));
        if (request.UnitCost < 0)
            throw new ArgumentException("Unit cost cannot be negative.", nameof(request));

        var product = await GetProductAsync(request.ProductId, cancellationToken);

        var isPurchase = request.PurchaseItemId.HasValue;
        var batch = new InventoryBatch
        {
            ProductId = product.Id,
            SupplierId = request.SupplierId,
            PurchaseItemId = request.PurchaseItemId,
            BatchNumber = string.IsNullOrWhiteSpace(request.BatchNumber)
                ? $"B{DateTime.Now:yyyyMMddHHmmssfff}"
                : request.BatchNumber.Trim(),
            ManufacturedDate = request.ManufacturedDate,
            ExpiryDate = request.ExpiryDate,
            QuantityReceived = request.Quantity,
            QuantityRemaining = request.Quantity,
            UnitCost = request.UnitCost,
            ReceivedAt = DateTime.Now,
            IsActive = true,
            Notes = request.Note
        };
        db.InventoryBatches.Add(batch);

        product.CurrentStock += request.Quantity;
        product.UpdatedAt = DateTime.Now;

        var movement = new StockMovement
        {
            ProductId = product.Id,
            InventoryBatch = batch,
            MovementType = isPurchase ? StockMovementType.Purchase : StockMovementType.Opening,
            QuantityChange = request.Quantity,
            UnitCostSnapshot = request.UnitCost,
            BalanceAfter = batch.QuantityRemaining,
            ReferenceType = isPurchase ? MovementReferenceType.Purchase : MovementReferenceType.Opening,
            ReferenceId = request.PurchaseItemId,
            Note = request.Note
        };
        db.StockMovements.Add(movement);

        await db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<AllocationResult> AllocateAsync(
        int productId,
        decimal quantity,
        StockMovementType movementType,
        MovementReferenceType? referenceType,
        int? referenceId,
        int? saleItemId,
        AllocationPolicy policy,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
            throw new ArgumentException("Allocation quantity must be greater than zero.", nameof(quantity));

        var product = await GetProductAsync(productId, cancellationToken);
        if (!product.TrackInventory)
            throw new InvalidOperationException($"Product '{product.Name}' is not inventory-tracked and cannot be allocated.");

        var today = DateTime.Today;
        var candidates = await db.InventoryBatches
            .Where(b => b.ProductId == productId && b.IsActive && b.QuantityRemaining > 0)
            .ToListAsync(cancellationToken);

        // Expired stock is never sellable (DBR-03).
        candidates = candidates
            .Where(b => b.ExpiryDate == null || b.ExpiryDate.Value.Date >= today)
            .ToList();

        IOrderedEnumerable<InventoryBatch> ordered = policy == AllocationPolicy.Fefo
            ? candidates
                .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(b => b.ExpiryDate)
                .ThenBy(b => b.ReceivedAt)
                .ThenBy(b => b.Id)
            : candidates
                .OrderBy(b => b.ReceivedAt)
                .ThenBy(b => b.ExpiryDate)
                .ThenBy(b => b.Id);

        var remaining = quantity;
        var totalCost = 0m;
        var allocations = new List<(InventoryBatch Batch, decimal Quantity, decimal UnitCost)>();

        foreach (var batch in ordered)
        {
            if (remaining <= 0) break;
            var take = Math.Min(remaining, batch.QuantityRemaining);
            if (take <= 0) continue;

            batch.QuantityRemaining -= take;
            remaining -= take;
            totalCost += take * batch.UnitCost;
            allocations.Add((batch, take, batch.UnitCost));
        }

        if (remaining > 0)
        {
            foreach (var (batch, take, _) in allocations)
                batch.QuantityRemaining += take;
            throw new InvalidOperationException(
                $"Insufficient stock for '{product.Name}'. Requested {quantity:0.###}, available {quantity - remaining:0.###}.");
        }

        product.CurrentStock -= quantity;
        product.UpdatedAt = DateTime.Now;

        var movements = new List<StockMovement>();
        foreach (var (batch, take, unitCost) in allocations)
        {
            var movement = new StockMovement
            {
                ProductId = productId,
                InventoryBatchId = batch.Id,
                MovementType = movementType,
                QuantityChange = -take,
                UnitCostSnapshot = unitCost,
                BalanceAfter = batch.QuantityRemaining,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                CreatedAt = DateTime.Now
            };
            db.StockMovements.Add(movement);
            movements.Add(movement);
        }

        await db.SaveChangesAsync(cancellationToken);

        var lines = new List<AllocationLine>();
        for (var i = 0; i < allocations.Count; i++)
        {
            var (batch, take, unitCost) = allocations[i];
            db.StockAllocations.Add(new StockAllocation
            {
                ProductId = productId,
                InventoryBatchId = batch.Id,
                SaleItemId = saleItemId,
                MovementType = movementType,
                Quantity = take,
                UnitCostSnapshot = unitCost,
                StockMovementId = movements[i].Id,
                CreatedAt = DateTime.Now
            });
            lines.Add(new AllocationLine
            {
                InventoryBatchId = batch.Id,
                BatchNumber = batch.BatchNumber,
                Quantity = take,
                UnitCost = unitCost
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return new AllocationResult
        {
            QuantityAllocated = quantity,
            TotalCost = totalCost,
            Lines = lines
        };
    }

    public async Task<StockAdjustment> AdjustAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReferenceNumber))
            throw new ArgumentException("A reference number is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A reason is required.", nameof(request));
        if (request.Lines.Count == 0)
            throw new ArgumentException("At least one adjustment line is required.", nameof(request));
        if (await db.StockAdjustments.AnyAsync(a => a.ReferenceNumber == request.ReferenceNumber, cancellationToken))
            throw new InvalidOperationException("That adjustment reference number is already in use.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var adjustment = new StockAdjustment
        {
            ReferenceNumber = request.ReferenceNumber.Trim(),
            Reason = request.Reason.Trim(),
            Notes = request.Notes,
            AdjustmentDate = DateTime.Today,
            Status = StockAdjustmentStatus.Applied,
            CreatedAt = DateTime.Now
        };
        db.StockAdjustments.Add(adjustment);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in request.Lines)
        {
            if (line.QuantityChange == 0)
                throw new ArgumentException("Adjustment quantity cannot be zero.", nameof(request));

            var product = await GetProductAsync(line.ProductId, cancellationToken);
            var movementType = line.QuantityChange > 0 ? StockMovementType.AdjustmentIn : StockMovementType.AdjustmentOut;
            var unitCost = line.UnitCost;

            InventoryBatch? batch = null;
            if (line.InventoryBatchId.HasValue)
            {
                batch = await db.InventoryBatches.FirstOrDefaultAsync(
                    b => b.Id == line.InventoryBatchId.Value && b.ProductId == line.ProductId, cancellationToken)
                    ?? throw new InvalidOperationException("The selected batch does not belong to the product.");
                batch.QuantityRemaining += line.QuantityChange;
                if (batch.QuantityRemaining < 0)
                    throw new InvalidOperationException($"Batch '{batch.BatchNumber}' cannot go negative.");
                unitCost = line.UnitCost != 0 ? line.UnitCost : batch.UnitCost;
                batch.UnitCost = unitCost;
            }

            product.CurrentStock += line.QuantityChange;
            if (product.CurrentStock < 0)
                throw new InvalidOperationException($"Product '{product.Name}' cannot go negative.");
            product.UpdatedAt = DateTime.Now;

            db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                InventoryBatchId = batch?.Id,
                MovementType = movementType,
                QuantityChange = line.QuantityChange,
                UnitCostSnapshot = unitCost,
                BalanceAfter = product.CurrentStock,
                ReferenceType = MovementReferenceType.Adjustment,
                ReferenceId = adjustment.Id,
                Note = line.Note ?? request.Reason,
                CreatedAt = DateTime.Now
            });

            adjustment.Items.Add(new StockAdjustmentItem
            {
                ProductId = product.Id,
                InventoryBatchId = batch?.Id,
                QuantityChange = line.QuantityChange,
                UnitCostSnapshot = unitCost,
                Note = line.Note
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return adjustment;
    }

    public async Task<decimal> GetAvailableStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await GetProductAsync(productId, cancellationToken);
        return product.CurrentStock;
    }

    public async Task<IReadOnlyList<BatchDto>> GetBatchesAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await db.InventoryBatches.AsNoTracking()
            .Where(b => b.ProductId == productId)
            .OrderByDescending(b => b.ReceivedAt)
            .Select(b => new BatchDto
            {
                Id = b.Id,
                ProductId = b.ProductId,
                BatchNumber = b.BatchNumber,
                ExpiryDate = b.ExpiryDate,
                QuantityRemaining = b.QuantityRemaining,
                UnitCost = b.UnitCost
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<Product> GetProductAsync(int productId, CancellationToken cancellationToken)
    {
        return await db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {productId} was not found.");
    }
}
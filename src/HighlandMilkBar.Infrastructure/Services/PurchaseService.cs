using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

public sealed class PurchaseService(AppDbContext db, IInventoryService inventory) : IPurchaseService
{
    public async Task<ReceivePurchaseResult> ReceiveAsync(int purchaseId, CancellationToken cancellationToken = default)
    {
        var purchase = await db.Purchases
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == purchaseId, cancellationToken)
            ?? throw new InvalidOperationException($"Purchase {purchaseId} was not found.");

        if (purchase.Status == PurchaseStatus.Cancelled)
            throw new InvalidOperationException("A cancelled purchase cannot be received.");
        if (purchase.Status == PurchaseStatus.Received || purchase.ReceivedAt != null)
            throw new InvalidOperationException("This purchase has already been received.");
        if (purchase.Items.Count == 0)
            throw new InvalidOperationException("A purchase must have at least one item before it can be received.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var batchesCreated = 0;
        var totalQuantity = 0m;

        foreach (var item in purchase.Items)
        {
            var quantity = item.ReceivedQuantity != 0 ? item.ReceivedQuantity : item.Quantity;
            if (quantity <= 0) continue;

            await inventory.ReceiveAsync(new StockReceiptRequest
            {
                ProductId = item.ProductId,
                BatchNumber = item.BatchNumber ?? string.Empty,
                Quantity = quantity,
                UnitCost = item.UnitCost,
                ManufacturedDate = item.ManufacturedDate,
                ExpiryDate = item.ExpiryDate,
                SupplierId = purchase.SupplierId,
                PurchaseItemId = item.Id,
                Note = $"Purchase {purchase.InvoiceNumber}"
            }, cancellationToken);

            item.ReceivedQuantity = quantity;
            batchesCreated++;
            totalQuantity += quantity;
        }

        purchase.Status = purchase.AmountPaid >= purchase.TotalAmount && purchase.TotalAmount > 0
            ? PurchaseStatus.Paid
            : PurchaseStatus.Received;
        purchase.ReceivedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ReceivePurchaseResult
        {
            PurchaseId = purchase.Id,
            BatchesCreated = batchesCreated,
            MovementsRecorded = batchesCreated,
            TotalQuantityReceived = totalQuantity
        };
    }
}
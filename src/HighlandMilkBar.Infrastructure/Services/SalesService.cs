using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

public sealed class SalesService(AppDbContext db, IInventoryService inventory) : ISalesService
{
    public async Task<SaleResult> CheckoutAsync(SaleCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
            throw new ArgumentException("A sale must contain at least one item.", nameof(request));
        if (request.Payments.Count == 0)
            throw new ArgumentException("A completed sale must record at least one payment.", nameof(request));

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var subtotal = 0m;
        var linePlans = new List<(SaleLineRequest Line, Product? Product, decimal UnitPrice, decimal LineTotal)>();
        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException("Sale item quantity must be greater than zero.", nameof(request));
            products.TryGetValue(line.ProductId, out var product);
            var unitPrice = line.UnitPrice;
            if (unitPrice <= 0 && product != null) unitPrice = product.SellingPrice;
            var lineTotal = line.Quantity * unitPrice - line.DiscountAmount;
            if (lineTotal < 0)
                throw new ArgumentException("A sale line cannot be negative after discount.", nameof(request));
            subtotal += lineTotal;
            linePlans.Add((line, product, unitPrice, lineTotal));
        }

        var total = subtotal - request.OrderDiscount;
        if (total < 0)
            throw new ArgumentException("Order discount cannot exceed the sale subtotal.", nameof(request));

        var amountPaid = request.Payments.Sum(p => p.Amount);
        if (Math.Abs(amountPaid - total) > 0.001m)
            throw new InvalidOperationException(
                $"Payment total {amountPaid:0.00} does not match the sale total {total:0.00}.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var sale = new Sale
        {
            ReceiptNumber = await GenerateSaleReceiptNumberAsync(cancellationToken),
            SoldAt = DateTime.Now,
            CustomerId = request.CustomerId,
            CashSessionId = request.CashSessionId,
            Subtotal = subtotal,
            DiscountAmount = request.OrderDiscount,
            TaxAmount = 0m,
            TotalAmount = total,
            AmountPaid = amountPaid,
            Status = SaleStatus.Completed,
            Notes = request.Notes,
            CreatedAt = DateTime.Now
        };

        foreach (var plan in linePlans)
        {
            sale.Items.Add(new SaleItem
            {
                ProductId = plan.Product?.Id,
                ItemNameSnapshot = plan.Line.ItemNameSnapshot ?? plan.Product?.Name ?? "Item",
                Quantity = plan.Line.Quantity,
                UnitPrice = plan.UnitPrice,
                UnitCostSnapshot = plan.Product?.CostPrice ?? 0m,
                DiscountAmount = plan.Line.DiscountAmount,
                LineTotal = plan.LineTotal
            });
        }

        foreach (var payment in request.Payments)
        {
            if (payment.Amount <= 0)
                throw new ArgumentException("Payment amount must be greater than zero.", nameof(request));
            sale.Payments.Add(new Payment
            {
                Method = payment.Method,
                Amount = payment.Amount,
                PaidAt = DateTime.Now,
                Reference = payment.Reference
            });
        }

        db.Sales.Add(sale);
        await db.SaveChangesAsync(cancellationToken);

        var costOfGoodsSold = 0m;
        foreach (var saleItem in sale.Items)
        {
            var product = saleItem.ProductId.HasValue ? products.GetValueOrDefault(saleItem.ProductId.Value) : null;

            if (product != null && product.TrackInventory)
            {
                var allocation = await inventory.AllocateAsync(
                    product.Id, saleItem.Quantity, StockMovementType.Sale,
                    MovementReferenceType.Sale, sale.Id, saleItem.Id, request.AllocationPolicy, cancellationToken);

                saleItem.UnitCostSnapshot = saleItem.Quantity == 0 ? 0 : allocation.TotalCost / saleItem.Quantity;
                costOfGoodsSold += allocation.TotalCost;
            }
            else if (product != null && product.Type == ProductType.PreparedItem)
            {
                var recipe = await GetActiveRecipeAsync(product.Id, cancellationToken);
                if (recipe != null)
                {
                    saleItem.RecipeId = recipe.Id;
                    var recipeCost = await ConsumeRecipeAsync(
                        recipe, saleItem.Quantity, saleItem.Id, request.AllocationPolicy, cancellationToken);
                    saleItem.UnitCostSnapshot = saleItem.Quantity == 0 ? 0 : recipeCost / saleItem.Quantity;
                    costOfGoodsSold += recipeCost;
                }
                else
                {
                    costOfGoodsSold += saleItem.Quantity * saleItem.UnitCostSnapshot;
                }
            }
            else
            {
                costOfGoodsSold += saleItem.Quantity * saleItem.UnitCostSnapshot;
            }
        }

        sale.CostOfGoodsSold = costOfGoodsSold;
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new SaleResult
        {
            SaleId = sale.Id,
            ReceiptNumber = sale.ReceiptNumber,
            Subtotal = sale.Subtotal,
            DiscountAmount = sale.DiscountAmount,
            TotalAmount = sale.TotalAmount,
            CostOfGoodsSold = sale.CostOfGoodsSold,
            AmountPaid = sale.AmountPaid,
            Change = sale.AmountPaid - sale.TotalAmount
        };
    }

    public async Task<SalesReturnResult> ProcessReturnAsync(SalesReturnRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
            throw new ArgumentException("A return must contain at least one line.", nameof(request));

        var sale = await db.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken)
            ?? throw new InvalidOperationException($"Sale {request.SaleId} was not found.");

        if (sale.Status is SaleStatus.Voided or SaleStatus.Returned)
            throw new InvalidOperationException("This sale cannot be returned in its current state.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var salesReturn = new SalesReturn
        {
            ReturnNumber = await GenerateReturnNumberAsync(cancellationToken),
            SaleId = sale.Id,
            CustomerId = sale.CustomerId,
            ReturnedAt = DateTime.Now,
            Reason = request.Reason,
            Status = SalesReturnStatus.Completed
        };
        db.SalesReturns.Add(salesReturn);
        await db.SaveChangesAsync(cancellationToken);

        var totalAmount = 0m;
        var anyRestock = false;

        foreach (var line in request.Lines)
        {
            var saleItem = sale.Items.FirstOrDefault(i => i.Id == line.SaleItemId)
                ?? throw new InvalidOperationException("Return line references an item that is not part of the sale.");

            var alreadyReturned = await db.SalesReturnItems
                .Where(ri => ri.SaleItemId == saleItem.Id
                    && ri.SalesReturn!.Status == SalesReturnStatus.Completed)
                .SumAsync(ri => ri.Quantity, cancellationToken);

            if (line.Quantity <= 0 || line.Quantity > saleItem.Quantity - alreadyReturned)
                throw new InvalidOperationException(
                    $"Return quantity for '{saleItem.ItemNameSnapshot}' exceeds the remaining returnable quantity.");

            var lineTotal = line.Quantity * saleItem.UnitPrice;
            totalAmount += lineTotal;

            db.SalesReturnItems.Add(new SalesReturnItem
            {
                SalesReturnId = salesReturn.Id,
                SaleItemId = saleItem.Id,
                ProductId = saleItem.ProductId,
                ItemNameSnapshot = saleItem.ItemNameSnapshot,
                Quantity = line.Quantity,
                UnitPrice = saleItem.UnitPrice,
                Condition = line.Condition,
                LineTotal = lineTotal
            });

            if (line.Condition == ReturnCondition.Restock && saleItem.ProductId.HasValue)
            {
                var product = await db.Products.FirstAsync(p => p.Id == saleItem.ProductId.Value, cancellationToken);
                if (product.TrackInventory)
                {
                    await RestockAsync(saleItem, line.Quantity, salesReturn.Id, product, cancellationToken);
                    anyRestock = true;
                }
            }
        }

        salesReturn.TotalAmount = totalAmount;
        salesReturn.Restocked = anyRestock;

        var totalReturnedQty = request.Lines.Sum(l => l.Quantity);
        var soldQty = sale.Items.Sum(i => i.Quantity);
        sale.Status = totalReturnedQty >= soldQty ? SaleStatus.Returned : SaleStatus.PartiallyReturned;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SalesReturnResult
        {
            SalesReturnId = salesReturn.Id,
            ReturnNumber = salesReturn.ReturnNumber,
            TotalAmount = salesReturn.TotalAmount,
            Restocked = salesReturn.Restocked
        };
    }

    private async Task RestockAsync(SaleItem saleItem, decimal quantity, int salesReturnId, Product product, CancellationToken cancellationToken)
    {
        var remaining = quantity;

        // Return to the original sale batches first so batch balances stay consistent.
        var saleAllocations = await db.StockAllocations
            .Where(a => a.SaleItemId == saleItem.Id && a.MovementType == StockMovementType.Sale)
            .OrderByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        foreach (var allocation in saleAllocations)
        {
            if (remaining <= 0) break;
            var take = Math.Min(remaining, allocation.Quantity);
            if (take <= 0) continue;

            var batch = await db.InventoryBatches.FirstAsync(b => b.Id == allocation.InventoryBatchId, cancellationToken);
            batch.QuantityRemaining += take;
            remaining -= take;

            db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                InventoryBatchId = batch.Id,
                MovementType = StockMovementType.SaleReturn,
                QuantityChange = take,
                UnitCostSnapshot = allocation.UnitCostSnapshot,
                BalanceAfter = batch.QuantityRemaining,
                ReferenceType = MovementReferenceType.SaleReturn,
                ReferenceId = salesReturnId,
                CreatedAt = DateTime.Now
            });
        }

        product.CurrentStock += quantity;
        product.UpdatedAt = DateTime.Now;

        if (remaining > 0)
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                MovementType = StockMovementType.SaleReturn,
                QuantityChange = remaining,
                UnitCostSnapshot = saleItem.UnitCostSnapshot,
                BalanceAfter = product.CurrentStock,
                ReferenceType = MovementReferenceType.SaleReturn,
                ReferenceId = salesReturnId,
                CreatedAt = DateTime.Now
            });
        }
    }

    private async Task<decimal> ConsumeRecipeAsync(Recipe recipe, decimal servings, int saleItemId, AllocationPolicy policy, CancellationToken cancellationToken)
    {
        var yield = recipe.YieldQuantity <= 0 ? 1 : recipe.YieldQuantity;
        var totalCost = 0m;

        foreach (var item in recipe.Items)
        {
            var required = item.QuantityRequired / yield * servings;
            if (required <= 0) continue;

            var ingredient = item.IngredientProduct;
            if (ingredient != null && ingredient.TrackInventory)
            {
                var allocation = await inventory.AllocateAsync(
                    ingredient.Id, required, StockMovementType.RecipeConsumption,
                    MovementReferenceType.Recipe, recipe.Id, saleItemId, policy, cancellationToken);
                totalCost += allocation.TotalCost;
            }
            else
            {
                totalCost += required * (ingredient?.CostPrice ?? 0m);
            }
        }

        return totalCost;
    }

    private async Task<Recipe?> GetActiveRecipeAsync(int menuProductId, CancellationToken cancellationToken)
    {
        return await db.Recipes
            .Include(r => r.Items)
                .ThenInclude(i => i.IngredientProduct)
            .Where(r => r.MenuProductId == menuProductId && r.IsActive)
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> GenerateSaleReceiptNumberAsync(CancellationToken cancellationToken)
    {
        const string prefix = "R";
        var date = DateTime.Now.ToString("yyyyMMdd");
        var count = await db.Sales.CountAsync(s => s.ReceiptNumber.StartsWith(prefix + date), cancellationToken);
        string candidate;
        do
        {
            count++;
            candidate = $"{prefix}{date}-{count:D4}";
        }
        while (await db.Sales.AnyAsync(s => s.ReceiptNumber == candidate, cancellationToken));
        return candidate;
    }

    private async Task<string> GenerateReturnNumberAsync(CancellationToken cancellationToken)
    {
        const string prefix = "SR";
        var date = DateTime.Now.ToString("yyyyMMdd");
        var count = await db.SalesReturns.CountAsync(s => s.ReturnNumber.StartsWith(prefix + date), cancellationToken);
        string candidate;
        do
        {
            count++;
            candidate = $"{prefix}{date}-{count:D4}";
        }
        while (await db.SalesReturns.AnyAsync(s => s.ReturnNumber == candidate, cancellationToken));
        return candidate;
    }
}
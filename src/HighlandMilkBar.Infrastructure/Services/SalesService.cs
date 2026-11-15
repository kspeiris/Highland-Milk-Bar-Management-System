using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

public sealed class SalesService(AppDbContext db, IInventoryService inventory) : ISalesService, ISalesQueryService
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

        if (request.OrderDiscount < 0)
            throw new ArgumentException("Order discount cannot be negative.", nameof(request));

        var subtotal = 0m;
        var lineDiscountTotal = 0m;
        var linePlans = new List<(SaleLineRequest Line, Product Product, decimal UnitPrice, decimal LineTotal)>();
        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException("Sale item quantity must be greater than zero.", nameof(request));
            if (line.DiscountAmount < 0)
                throw new ArgumentException("A sale line discount cannot be negative.", nameof(request));

            // POS-01: only active, existing products may be sold.
            if (!products.TryGetValue(line.ProductId, out var product) || product is null)
                throw new InvalidOperationException($"Product {line.ProductId} was not found and cannot be sold.");
            if (!product.IsActive)
                throw new InvalidOperationException($"Product '{product.Name}' is inactive and cannot be sold.");

            var unitPrice = line.UnitPrice;
            if (unitPrice <= 0) unitPrice = product.SellingPrice;
            var gross = line.Quantity * unitPrice;
            if (line.DiscountAmount > gross)
                throw new ArgumentException($"Discount for '{product.Name}' exceeds the line amount.", nameof(request));
            var lineTotal = gross - line.DiscountAmount;
            if (lineTotal < 0)
                throw new ArgumentException("A sale line cannot be negative after discount.", nameof(request));
            subtotal += gross;
            lineDiscountTotal += line.DiscountAmount;
            linePlans.Add((line, product, unitPrice, lineTotal));
        }

        var discountTotal = lineDiscountTotal + request.OrderDiscount;
        var total = subtotal - discountTotal;
        if (total < 0)
            throw new ArgumentException("Discounts cannot exceed the sale subtotal.", nameof(request));

        // POS-09/POS-10: non-cash tenders cannot exceed the total; only cash may overpay (change).
        var nonCashPaid = request.Payments.Where(p => p.Method != PaymentMethod.Cash).Sum(p => p.Amount);
        if (nonCashPaid > total + 0.001m)
            throw new InvalidOperationException("Card and other non-cash payments cannot exceed the sale total.");

        var amountPaid = request.Payments.Sum(p => p.Amount);
        if (amountPaid < total - 0.001m)
            throw new InvalidOperationException(
                $"Payment total {amountPaid:0.00} is less than the sale total {total:0.00}.");

        var change = amountPaid - total;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var sale = new Sale
        {
            ReceiptNumber = await GenerateSaleReceiptNumberAsync(cancellationToken),
            SoldAt = DateTime.Now,
            CustomerId = request.CustomerId,
            CashSessionId = request.CashSessionId,
            Subtotal = subtotal,
            DiscountAmount = discountTotal,
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
                    // POS: a recipe-based prepared item cannot be sold without a valid active recipe.
                    throw new InvalidOperationException(
                        $"Product '{saleItem.ItemNameSnapshot}' is a prepared item with no active recipe and cannot be sold.");
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
            Change = change
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

    // ===== ISalesQueryService =====

    public async Task<PagedResult<SaleListRowDto>> SearchAsync(SaleSearchFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new SaleSearchFilter();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 200 ? 20 : filter.PageSize;

        var query = db.Sales.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var like = $"%{EscapeLike(filter.Search)}%";
            query = query.Where(s => EF.Functions.Like(s.ReceiptNumber, like));
        }
        if (filter.From is { } from)
            query = query.Where(s => s.SoldAt >= from);
        if (filter.To is { } to)
            query = query.Where(s => s.SoldAt <= to);
        if (filter.Status is { } status)
            query = query.Where(s => s.Status == status);
        if (filter.PaymentMethod is { } method)
            query = query.Where(s => s.Payments.Any(p => p.Method == method));

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(s => s.SoldAt)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.ReceiptNumber,
                s.SoldAt,
                s.TotalAmount,
                s.DiscountAmount,
                s.Status
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Id).ToList();

        var quantities = await db.SaleItems.AsNoTracking()
            .Where(i => ids.Contains(i.SaleId))
            .Select(i => new { i.SaleId, i.Quantity })
            .ToListAsync(cancellationToken);

        var methods = await db.Payments.AsNoTracking()
            .Where(p => ids.Contains(p.SaleId))
            .Select(p => new { p.SaleId, p.Method })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new SaleListRowDto
        {
            Id = r.Id,
            ReceiptNumber = r.ReceiptNumber,
            SoldAt = r.SoldAt,
            ItemCount = quantities.Where(q => q.SaleId == r.Id).Sum(q => q.Quantity),
            PaymentSummary = string.Join(", ",
                methods.Where(m => m.SaleId == r.Id).Select(m => m.Method).Distinct().Select(FormatPaymentMethod)),
            TotalAmount = r.TotalAmount,
            DiscountAmount = r.DiscountAmount,
            Status = r.Status,
            StatusText = FormatStatus(r.Status)
        }).ToList();

        return new PagedResult<SaleListRowDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<SaleDetailDto?> GetByIdAsync(int saleId, CancellationToken cancellationToken = default)
    {
        var sale = await LoadDetailAsync(s => s.Id == saleId, cancellationToken);
        return sale is null ? null : MapDetail(sale);
    }

    public async Task<SaleDetailDto?> GetByReceiptNumberAsync(string receiptNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber)) return null;
        var trimmed = receiptNumber.Trim();
        var sale = await LoadDetailAsync(s => s.ReceiptNumber == trimmed, cancellationToken);
        return sale is null ? null : MapDetail(sale);
    }

    public async Task<IReadOnlyList<CustomerOptionDto>> SearchCustomersAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = db.Customers.AsNoTracking().Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{EscapeLike(search)}%";
            query = query.Where(c => EF.Functions.Like(c.Name, like)
                || (c.Phone != null && EF.Functions.Like(c.Phone, like)));
        }

        return await query
            .OrderBy(c => c.Name)
            .Take(50)
            .Select(c => new CustomerOptionDto { Id = c.Id, Name = c.Name, Phone = c.Phone })
            .ToListAsync(cancellationToken);
    }

    private async Task<Sale?> LoadDetailAsync(System.Linq.Expressions.Expression<Func<Sale, bool>> predicate, CancellationToken cancellationToken)
    {
        return await db.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .Include(s => s.SalesReturns)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    private static SaleDetailDto MapDetail(Sale sale)
    {
        return new SaleDetailDto
        {
            Id = sale.Id,
            ReceiptNumber = sale.ReceiptNumber,
            SoldAt = sale.SoldAt,
            CustomerId = sale.CustomerId,
            CustomerName = sale.Customer?.Name,
            Subtotal = sale.Subtotal,
            DiscountAmount = sale.DiscountAmount,
            TaxAmount = sale.TaxAmount,
            TotalAmount = sale.TotalAmount,
            CostOfGoodsSold = sale.CostOfGoodsSold,
            AmountPaid = sale.AmountPaid,
            Change = sale.AmountPaid - sale.TotalAmount,
            Status = sale.Status,
            StatusText = FormatStatus(sale.Status),
            Notes = sale.Notes,
            Items = sale.Items
                .OrderBy(i => i.Id)
                .Select(i => new SaleDetailItemDto
                {
                    SaleItemId = i.Id,
                    ProductId = i.ProductId,
                    ItemNameSnapshot = i.ItemNameSnapshot,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    UnitCostSnapshot = i.UnitCostSnapshot,
                    DiscountAmount = i.DiscountAmount,
                    LineTotal = i.LineTotal,
                    IsRecipeItem = i.RecipeId.HasValue
                }).ToList(),
            Payments = sale.Payments
                .OrderBy(p => p.Id)
                .Select(p => new SaleDetailPaymentDto
                {
                    Method = p.Method,
                    MethodText = FormatPaymentMethod(p.Method),
                    Amount = p.Amount,
                    Reference = p.Reference,
                    PaidAt = p.PaidAt
                }).ToList(),
            Returns = sale.SalesReturns
                .OrderBy(r => r.Id)
                .Select(r => new SaleDetailReturnDto
                {
                    SalesReturnId = r.Id,
                    ReturnNumber = r.ReturnNumber,
                    ReturnedAt = r.ReturnedAt,
                    TotalAmount = r.TotalAmount,
                    Restocked = r.Restocked,
                    StatusText = FormatStatus(r.Status)
                }).ToList()
        };
    }

    private static string FormatPaymentMethod(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Card => "Card",
        PaymentMethod.BankTransfer => "Bank Transfer",
        _ => "Other"
    };

    private static string FormatStatus(SaleStatus status) => status switch
    {
        SaleStatus.Completed => "Completed",
        SaleStatus.Voided => "Voided",
        SaleStatus.PartiallyReturned => "Partially returned",
        SaleStatus.Returned => "Returned",
        _ => status.ToString()
    };

    private static string FormatStatus(SalesReturnStatus status) => status switch
    {
        SalesReturnStatus.Completed => "Completed",
        SalesReturnStatus.Voided => "Voided",
        _ => status.ToString()
    };

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

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
                // Normalise the recipe unit into the ingredient's stock unit before allocating.
                var stockUnit = string.IsNullOrWhiteSpace(ingredient.Unit) ? item.Unit : ingredient.Unit;
                var converted = required;
                if (!UnitConversion.TryConvert(item.Unit, stockUnit, required, out converted, out _))
                {
                    // Recipes created through the validated editor are always compatible; this
                    // fallback preserves behaviour for legacy data created outside that path.
                    converted = required;
                }

                var allocation = await inventory.AllocateAsync(
                    ingredient.Id, converted, StockMovementType.RecipeConsumption,
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
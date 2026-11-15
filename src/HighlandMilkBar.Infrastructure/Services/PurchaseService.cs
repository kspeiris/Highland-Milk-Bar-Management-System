using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Purchase workflow and reporting. A purchase is a supplier invoice and its full goods
/// receipt. Creating a draft never touches stock; receiving is transactional and delegates
/// to the shared <see cref="IInventoryService"/> so batches and stock movements are the single
/// source of truth (PUR-08/09/10/11/18). There is no separate purchase stock system.
/// </summary>
public sealed class PurchaseService(AppDbContext db, IInventoryService inventory)
    : IPurchaseService, IPurchaseQueryService
{
    public async Task<PurchaseDetailDto> CreateAsync(CreatePurchaseRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateHeaderAsync(request.SupplierId, request.InvoiceNumber, null, cancellationToken);
        var lines = await PrepareLinesAsync(request.Items, cancellationToken);
        if (lines.Count == 0) throw new InvalidOperationException("A purchase must contain at least one item (PUR-02).");

        var subtotal = PurchaseFinancials.Subtotal(lines.Select(l => l.LineTotal));
        var total = PurchaseFinancials.GrandTotal(subtotal, request.DiscountAmount, request.AdditionalCharges);

        var purchase = new Purchase
        {
            PurchaseNumber = await NextPurchaseNumberAsync(cancellationToken),
            InvoiceNumber = Clean(request.InvoiceNumber) ?? string.Empty,
            SupplierId = request.SupplierId,
            PurchaseDate = request.PurchaseDate,
            DueDate = request.DueDate,
            DiscountAmount = request.DiscountAmount,
            AdditionalCharges = request.AdditionalCharges,
            Subtotal = subtotal,
            TotalAmount = total,
            AmountPaid = 0m,
            Status = PurchaseStatus.Draft,
            PaymentStatus = PaymentStatus.Unpaid,
            Notes = Clean(request.Notes),
            CreatedAt = DateTime.Now
        };
        foreach (var line in lines)
            purchase.Items.Add(new PurchaseItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                Unit = line.Unit,
                UnitCost = line.UnitCost,
                DiscountAmount = line.DiscountAmount,
                LineTotal = line.LineTotal,
                BatchNumber = line.BatchNumber,
                ExpiryDate = line.ExpiryDate
            });

        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(purchase.Id, cancellationToken))!;
    }

    public async Task<PurchaseDetailDto> UpdateAsync(UpdatePurchaseRequest request, CancellationToken cancellationToken = default)
    {
        var purchase = await db.Purchases
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Purchase {request.Id} was not found.");

        if (purchase.Status != PurchaseStatus.Draft)
            throw new InvalidOperationException("Only draft purchases can be edited.");

        await ValidateHeaderAsync(request.SupplierId, request.InvoiceNumber, request.Id, cancellationToken);
        var lines = await PrepareLinesAsync(request.Items, cancellationToken);
        if (lines.Count == 0) throw new InvalidOperationException("A purchase must contain at least one item (PUR-02).");

        var subtotal = PurchaseFinancials.Subtotal(lines.Select(l => l.LineTotal));
        var total = PurchaseFinancials.GrandTotal(subtotal, request.DiscountAmount, request.AdditionalCharges);

        purchase.SupplierId = request.SupplierId;
        purchase.InvoiceNumber = Clean(request.InvoiceNumber) ?? string.Empty;
        purchase.PurchaseDate = request.PurchaseDate;
        purchase.DueDate = request.DueDate;
        purchase.DiscountAmount = request.DiscountAmount;
        purchase.AdditionalCharges = request.AdditionalCharges;
        purchase.Subtotal = subtotal;
        purchase.TotalAmount = total;
        purchase.Notes = Clean(request.Notes);

        db.PurchaseItems.RemoveRange(purchase.Items);
        purchase.Items.Clear();
        foreach (var line in lines)
            purchase.Items.Add(new PurchaseItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                Unit = line.Unit,
                UnitCost = line.UnitCost,
                DiscountAmount = line.DiscountAmount,
                LineTotal = line.LineTotal,
                BatchNumber = line.BatchNumber,
                ExpiryDate = line.ExpiryDate
            });

        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(purchase.Id, cancellationToken))!;
    }

    public async Task CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var purchase = await db.Purchases.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Purchase {id} was not found.");

        if (purchase.Status == PurchaseStatus.Received)
            throw new InvalidOperationException(
                "A received purchase cannot be cancelled. It has already affected inventory; use a return/credit note instead.");

        if (purchase.Status == PurchaseStatus.Cancelled) return;

        purchase.Status = PurchaseStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReceivePurchaseResult> ReceiveAsync(int purchaseId, CancellationToken cancellationToken = default)
    {
        var purchase = await db.Purchases
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == purchaseId, cancellationToken)
            ?? throw new InvalidOperationException($"Purchase {purchaseId} was not found.");

        if (purchase.Status == PurchaseStatus.Cancelled)
            throw new InvalidOperationException("A cancelled purchase cannot be received (PUR-15).");
        if (purchase.Status == PurchaseStatus.Received || purchase.ReceivedAt != null)
            throw new InvalidOperationException("This purchase has already been received (PUR-11).");
        if (purchase.SupplierId is null)
            throw new InvalidOperationException("A purchase must have a valid supplier before it can be received (PUR-01).");
        if (purchase.Items.Count == 0)
            throw new InvalidOperationException("A purchase must have at least one item before it can be received (PUR-02).");

        var receivedDate = DateTime.Today;

        // Validate every line before any write (PUR-04/05/06/07).
        foreach (var item in purchase.Items)
        {
            var product = item.Product ?? throw new InvalidOperationException("A purchase line is missing its product.");
            if (item.Quantity <= 0)
                throw new InvalidOperationException($"Line for '{product.Name}' must have a positive quantity (PUR-04).");
            if (item.UnitCost < 0)
                throw new InvalidOperationException($"Line for '{product.Name}' cannot have a negative cost (PUR-05).");
            if (!product.TrackInventory)
                throw new InvalidOperationException($"Product '{product.Name}' is not inventory-tracked and cannot be received.");
            if (string.IsNullOrWhiteSpace(item.BatchNumber))
                throw new InvalidOperationException($"A batch number is required for '{product.Name}' (PUR-06).");
            if (product.RequiresExpiryDate && item.ExpiryDate is null)
                throw new InvalidOperationException($"An expiry date is required for '{product.Name}' (PUR-07).");
            if (item.ExpiryDate is not null && item.ExpiryDate.Value.Date < receivedDate)
                throw new InvalidOperationException($"Expiry date for '{product.Name}' cannot be earlier than the received date.");
        }

        // Spread purchase-level charges/discounts across lines so batch unit cost is landed cost.
        var items = purchase.Items.ToList();
        var allocations = AllocateLineCharges(purchase);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var batchesCreated = 0;
        var totalQuantity = 0m;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            await inventory.ReceiveAsync(new StockReceiptRequest
            {
                ProductId = item.ProductId,
                BatchNumber = item.BatchNumber ?? string.Empty,
                Quantity = item.Quantity,
                UnitCost = allocations[i].AdjustedUnitCost,
                ManufacturedDate = item.ManufacturedDate,
                ExpiryDate = item.ExpiryDate,
                SupplierId = purchase.SupplierId,
                PurchaseItemId = item.Id,
                ReceivedDate = receivedDate,
                Note = $"Purchase {purchase.PurchaseNumber}"
            }, cancellationToken);

            item.ReceivedQuantity = item.Quantity;
            batchesCreated++;
            totalQuantity += item.Quantity;
        }

        purchase.Status = PurchaseStatus.Received;
        purchase.ReceivedAt = DateTime.Now;
        purchase.ReceivedDate = receivedDate;
        purchase.PaymentStatus = PurchaseFinancials.ResolvePaymentStatus(purchase.TotalAmount, purchase.AmountPaid);
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

    public async Task<PagedResult<PurchaseRowDto>> SearchAsync(PurchaseFilter filter, CancellationToken cancellationToken = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 50 : filter.PageSize;

        var query = db.Purchases.AsNoTracking().Include(p => p.Supplier).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.PurchaseNumber, $"%{term}%") ||
                (p.InvoiceNumber != null && EF.Functions.Like(p.InvoiceNumber, $"%{term}%")) ||
                (p.Supplier != null && EF.Functions.Like(p.Supplier.Name, $"%{term}%")));
        }
        if (filter.SupplierId.HasValue) query = query.Where(p => p.SupplierId == filter.SupplierId.Value);
        if (filter.ReceiveStatus.HasValue) query = query.Where(p => p.Status == filter.ReceiveStatus.Value);
        if (filter.PaymentStatus.HasValue) query = query.Where(p => p.PaymentStatus == filter.PaymentStatus.Value);
        if (filter.From.HasValue) query = query.Where(p => p.PurchaseDate >= filter.From.Value);
        if (filter.To.HasValue) query = query.Where(p => p.PurchaseDate < filter.To.Value.AddDays(1));

        var total = await query.CountAsync(cancellationToken);

        var entities = await query
            .Include(p => p.Items)
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(ToRow).ToList();
        return new PagedResult<PurchaseRowDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<PurchaseDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var p = await db.Purchases.AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p is null) return null;

        var paymentEntities = await db.SupplierPayments.AsNoTracking()
            .Where(pay => pay.PurchaseId == id)
            .OrderByDescending(pay => pay.PaidAt)
            .ToListAsync(cancellationToken);
        var payments = paymentEntities.Select(pay => new SupplierPaymentDto
        {
            Id = pay.Id,
            PurchaseId = pay.PurchaseId,
            PurchaseNumber = p.PurchaseNumber,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier != null ? p.Supplier.Name : string.Empty,
            Amount = pay.Amount,
            Method = pay.Method,
            PaidAt = pay.PaidAt,
            Reference = pay.Reference,
            Notes = pay.Notes
        }).ToList();

        var items = p.Items.Select(i => new PurchaseItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? string.Empty,
            ProductSku = i.Product?.Sku ?? string.Empty,
            Quantity = i.Quantity,
            Unit = i.Unit ?? i.Product?.Unit ?? string.Empty,
            UnitCost = i.UnitCost,
            DiscountAmount = i.DiscountAmount,
            LineTotal = i.LineTotal,
            BatchNumber = i.BatchNumber,
            ExpiryDate = i.ExpiryDate,
            RequiresExpiryDate = i.Product?.RequiresExpiryDate ?? false,
            TrackInventory = i.Product?.TrackInventory ?? false
        }).ToList();

        var isReceived = p.Status == PurchaseStatus.Received;

        return new PurchaseDetailDto
        {
            Id = p.Id,
            PurchaseNumber = p.PurchaseNumber,
            InvoiceNumber = p.InvoiceNumber,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier?.Name ?? string.Empty,
            PurchaseDate = p.PurchaseDate,
            DueDate = p.DueDate,
            ReceivedDate = p.ReceivedDate,
            Subtotal = p.Subtotal,
            DiscountAmount = p.DiscountAmount,
            AdditionalCharges = p.AdditionalCharges,
            TotalAmount = p.TotalAmount,
            AmountPaid = p.AmountPaid,
            Outstanding = isReceived ? PurchaseFinancials.Outstanding(p.TotalAmount, p.AmountPaid) : 0m,
            ReceiveStatus = p.Status,
            PaymentStatus = p.PaymentStatus,
            CanReceive = p.Status == PurchaseStatus.Draft,
            CanEdit = p.Status == PurchaseStatus.Draft,
            Notes = p.Notes,
            Items = items,
            Payments = payments
        };
    }

    // ---------- Dashboard ----------

    public async Task<PurchaseDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var received = db.Purchases.AsNoTracking().Where(p => p.Status == PurchaseStatus.Received);

        var totalReceived = await received.CountAsync(cancellationToken);
        var purchaseValue = await received.SumAsync(p => (decimal?)p.TotalAmount, cancellationToken) ?? 0m;
        var receivedIds = db.Purchases.AsNoTracking().Where(p => p.Status == PurchaseStatus.Received).Select(p => p.Id);
        var totalPaid = await db.SupplierPayments.AsNoTracking()
            .Where(pay => receivedIds.Contains(pay.PurchaseId))
            .SumAsync(pay => (decimal?)pay.Amount, cancellationToken) ?? 0m;
        var activeSuppliers = await db.Suppliers.CountAsync(s => s.IsActive, cancellationToken);
        var paymentsThisMonth = await db.SupplierPayments.AsNoTracking()
            .Where(pay => pay.PaidAt >= monthStart)
            .SumAsync(pay => (decimal?)pay.Amount, cancellationToken) ?? 0m;
        var purchasesThisMonth = await received.CountAsync(p => p.PurchaseDate >= monthStart, cancellationToken);
        var purchasesThisMonthValue = await received
            .Where(p => p.PurchaseDate >= monthStart)
            .SumAsync(p => (decimal?)p.TotalAmount, cancellationToken) ?? 0m;

        var unpaid = await received.CountAsync(p => p.PaymentStatus == PaymentStatus.Unpaid, cancellationToken);
        var partially = await received.CountAsync(p => p.PaymentStatus == PaymentStatus.PartiallyPaid, cancellationToken);
        var paid = await received.CountAsync(p => p.PaymentStatus == PaymentStatus.Paid, cancellationToken);

        var trend = await BuildMonthlyTrendAsync(today, cancellationToken);
        var bySupplier = await BuildBySupplierAsync(cancellationToken);
        var byCategory = await BuildByCategoryAsync(cancellationToken);
        var topPurchased = await BuildTopPurchasedAsync(cancellationToken);
        var recent = await BuildRecentAsync(cancellationToken);

        return new PurchaseDashboardDto
        {
            TotalReceivedPurchases = totalReceived,
            PurchaseValue = purchaseValue,
            ActiveSuppliers = activeSuppliers,
            OutstandingPayments = purchaseValue - totalPaid,
            PaymentsThisMonth = paymentsThisMonth,
            PurchasesThisMonth = purchasesThisMonth,
            PurchasesThisMonthValue = purchasesThisMonthValue,
            UnpaidPurchases = unpaid,
            PartiallyPaidPurchases = partially,
            PaidPurchases = paid,
            MonthlyTrend = trend,
            BySupplier = bySupplier,
            ByCategory = byCategory,
            TopPurchased = topPurchased,
            RecentPurchases = recent
        };
    }

    private async Task<List<MonthlySpendDto>> BuildMonthlyTrendAsync(DateTime today, CancellationToken cancellationToken)
    {
        var start = today.AddMonths(-5);
        var first = new DateTime(start.Year, start.Month, 1);
        var rows = await db.Purchases.AsNoTracking()
            .Where(p => p.Status == PurchaseStatus.Received && p.PurchaseDate >= first)
            .Select(p => new { p.PurchaseDate, p.TotalAmount })
            .ToListAsync(cancellationToken);

        var months = new List<MonthlySpendDto>();
        for (var i = 0; i < 6; i++)
        {
            var m = first.AddMonths(i);
            var inMonth = rows.Where(r => r.PurchaseDate.Year == m.Year && r.PurchaseDate.Month == m.Month).ToList();
            months.Add(new MonthlySpendDto
            {
                Year = m.Year,
                Month = m.Month,
                Label = m.ToString("MMM yy"),
                Value = inMonth.Sum(r => r.TotalAmount),
                Purchases = inMonth.Count
            });
        }
        return months;
    }

    private async Task<List<SupplierSpendDto>> BuildBySupplierAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Purchases.AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => p.Status == PurchaseStatus.Received && p.SupplierId != null)
            .Select(p => new { SupplierName = p.Supplier != null ? p.Supplier.Name : string.Empty, p.TotalAmount })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.SupplierName)
            .Select(g => new SupplierSpendDto
            {
                SupplierName = g.Key,
                Value = g.Sum(x => x.TotalAmount),
                Purchases = g.Count()
            })
            .OrderByDescending(x => x.Value)
            .ToList();
    }

    private async Task<List<CategorySpendDto>> BuildByCategoryAsync(CancellationToken cancellationToken)
    {
        var rows = await db.PurchaseItems.AsNoTracking()
            .Include(i => i.Product)
            .Include(i => i.Purchase)
            .Where(i => i.Purchase != null && i.Purchase.Status == PurchaseStatus.Received)
            .Select(i => new { Category = i.Product != null && i.Product.Category != null ? i.Product.Category.Name : "Uncategorised", i.LineTotal })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.Category)
            .Select(g => new CategorySpendDto { CategoryName = g.Key, Value = g.Sum(x => x.LineTotal) })
            .OrderByDescending(x => x.Value)
            .ToList();
    }

    private async Task<List<TopPurchasedDto>> BuildTopPurchasedAsync(CancellationToken cancellationToken)
    {
        var rows = await db.PurchaseItems.AsNoTracking()
            .Include(i => i.Product)
            .Include(i => i.Purchase)
            .Where(i => i.Purchase != null && i.Purchase.Status == PurchaseStatus.Received)
            .Select(i => new
            {
                i.ProductId,
                Name = i.Product != null ? i.Product.Name : string.Empty,
                Sku = i.Product != null ? i.Product.Sku : string.Empty,
                i.Quantity,
                i.LineTotal
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new { r.ProductId, r.Name, r.Sku })
            .Select(g => new TopPurchasedDto
            {
                Name = g.Key.Name,
                Sku = g.Key.Sku,
                Quantity = g.Sum(x => x.Quantity),
                Value = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.Value)
            .Take(8)
            .ToList();
    }

    private async Task<List<PurchaseRowDto>> BuildRecentAsync(CancellationToken cancellationToken)
    {
        var entities = await db.Purchases.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Items)
            .Where(p => p.Status == PurchaseStatus.Received)
            .OrderByDescending(p => p.ReceivedAt ?? p.PurchaseDate)
            .ThenByDescending(p => p.Id)
            .Take(8)
            .ToListAsync(cancellationToken);
        return entities.Select(ToRow).ToList();
    }

    // ---------- Queries: outstanding + price history ----------

    public async Task<IReadOnlyList<OutstandingBalanceDto>> GetOutstandingAsync(
        int? supplierId, bool onlyOverdue, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var query = db.Purchases.AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => p.Status == PurchaseStatus.Received && p.AmountPaid < p.TotalAmount);

        if (supplierId.HasValue) query = query.Where(p => p.SupplierId == supplierId.Value);
        if (onlyOverdue) query = query.Where(p => p.DueDate != null && p.DueDate.Value.Date < today);

        var rows = await query
            .OrderBy(p => p.DueDate ?? DateTime.MaxValue)
            .ThenBy(p => p.PurchaseDate)
            .Select(p => new OutstandingBalanceDto
            {
                SupplierId = p.SupplierId,
                PurchaseId = p.Id,
                SupplierName = p.Supplier != null ? p.Supplier.Name : string.Empty,
                PurchaseNumber = p.PurchaseNumber,
                PurchaseDate = p.PurchaseDate,
                DueDate = p.DueDate,
                TotalAmount = p.TotalAmount,
                AmountPaid = p.AmountPaid,
                Outstanding = p.TotalAmount - p.AmountPaid,
                IsOverdue = p.DueDate != null && p.DueDate.Value.Date < today
            })
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<PriceHistoryDto> GetPriceHistoryAsync(int productId, int? supplierId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {productId} was not found.");

        var items = await LoadPriceHistoryItemsAsync(productId, supplierId, null, null, cancellationToken);
        var rows = items.Select(i => new PriceHistoryRowDto
        {
            PurchaseId = i.PurchaseId,
            PurchaseNumber = i.PurchaseNumber,
            InvoiceNumber = i.InvoiceNumber,
            SupplierId = i.SupplierId,
            SupplierName = i.SupplierName,
            PurchaseDate = i.PurchaseDate,
            Quantity = i.Quantity,
            Unit = string.IsNullOrWhiteSpace(i.Unit) ? product.Unit : i.Unit,
            UnitCost = i.UnitCost
        }).ToList();

        return new PriceHistoryDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            LatestCost = rows.Count > 0 ? rows[0].UnitCost : null,
            LowestCost = rows.Count > 0 ? rows.Min(r => r.UnitCost) : null,
            HighestCost = rows.Count > 0 ? rows.Max(r => r.UnitCost) : null,
            PurchaseCount = rows.Count,
            Rows = rows
        };
    }

    public async Task<IReadOnlyList<PriceHistoryRowDto>> GetProductPurchaseHistoryAsync(
        int productId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {productId} was not found.");

        var items = await LoadPriceHistoryItemsAsync(productId, null, from, to, cancellationToken);
        return items.Select(i => new PriceHistoryRowDto
        {
            PurchaseId = i.PurchaseId,
            PurchaseNumber = i.PurchaseNumber,
            InvoiceNumber = i.InvoiceNumber,
            SupplierId = i.SupplierId,
            SupplierName = i.SupplierName,
            PurchaseDate = i.PurchaseDate,
            Quantity = i.Quantity,
            Unit = string.IsNullOrWhiteSpace(i.Unit) ? product.Unit : i.Unit,
            UnitCost = i.UnitCost
        }).ToList();
    }

    private sealed record PriceHistoryItem(
        int PurchaseId, string PurchaseNumber, string? InvoiceNumber, int? SupplierId,
        string SupplierName, DateTime PurchaseDate, decimal Quantity, string? Unit, decimal UnitCost);

    private async Task<List<PriceHistoryItem>> LoadPriceHistoryItemsAsync(
        int productId, int? supplierId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var rows = await db.PurchaseItems.AsNoTracking()
            .Where(i => i.ProductId == productId)
            .Select(i => new
            {
                PurchaseId = i.Purchase != null ? i.Purchase.Id : 0,
                PurchaseNumber = i.Purchase != null ? i.Purchase.PurchaseNumber : string.Empty,
                InvoiceNumber = i.Purchase != null ? i.Purchase.InvoiceNumber : null,
                SupplierId = i.Purchase != null ? i.Purchase.SupplierId : null,
                SupplierName = i.Purchase != null && i.Purchase.Supplier != null ? i.Purchase.Supplier.Name : string.Empty,
                PurchaseDate = i.Purchase != null ? (DateTime?)i.Purchase.PurchaseDate : null,
                Status = i.Purchase != null ? (PurchaseStatus?)i.Purchase.Status : null,
                i.Quantity,
                i.Unit,
                i.UnitCost
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => r.Status == PurchaseStatus.Received)
            .Where(r => !supplierId.HasValue || r.SupplierId == supplierId.Value)
            .Where(r => !from.HasValue || (r.PurchaseDate.HasValue && r.PurchaseDate.Value >= from.Value))
            .Where(r => !to.HasValue || (r.PurchaseDate.HasValue && r.PurchaseDate.Value < to.Value.AddDays(1)))
            .OrderByDescending(r => r.PurchaseDate)
            .Select(r => new PriceHistoryItem(
                r.PurchaseId,
                r.PurchaseNumber,
                r.InvoiceNumber,
                r.SupplierId,
                r.SupplierName,
                r.PurchaseDate ?? default,
                r.Quantity,
                r.Unit,
                r.UnitCost))
            .ToList();
    }

    // ---------- helpers ----------

    private sealed record PreparedLine(
        int ProductId, decimal Quantity, string Unit, decimal UnitCost,
        decimal DiscountAmount, decimal LineTotal, string? BatchNumber, DateTime? ExpiryDate);

    private async Task<List<PreparedLine>> PrepareLinesAsync(
        IReadOnlyList<PurchaseLineRequest> requestLines, CancellationToken cancellationToken)
    {
        var productIds = requestLines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var result = new List<PreparedLine>();
        foreach (var line in requestLines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new InvalidOperationException($"Product {line.ProductId} was not found.");
            if (!product.IsActive)
                throw new InvalidOperationException($"Product '{product.Name}' is inactive and cannot be purchased.");
            if (!product.TrackInventory)
                throw new InvalidOperationException($"Product '{product.Name}' is not inventory-tracked and cannot be purchased.");
            if (line.Quantity <= 0)
                throw new InvalidOperationException($"Quantity for '{product.Name}' must be greater than zero (PUR-04).");
            if (line.UnitCost < 0)
                throw new InvalidOperationException($"Cost for '{product.Name}' cannot be negative (PUR-05).");
            if (line.DiscountAmount < 0)
                throw new InvalidOperationException($"Discount for '{product.Name}' cannot be negative.");

            var lineTotal = PurchaseFinancials.LineTotal(line.Quantity, line.UnitCost, line.DiscountAmount);
            result.Add(new PreparedLine(
                product.Id,
                line.Quantity,
                string.IsNullOrWhiteSpace(line.Unit) ? product.Unit : line.Unit.Trim(),
                line.UnitCost,
                line.DiscountAmount,
                lineTotal,
                Clean(line.BatchNumber),
                line.ExpiryDate));
        }
        return result;
    }

    private async Task ValidateHeaderAsync(int supplierId, string? invoiceNumber, int? excludePurchaseId, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken)
            ?? throw new InvalidOperationException("A valid supplier is required (PUR-01).");
        if (!supplier.IsActive)
            throw new InvalidOperationException($"Supplier '{supplier.Name}' is inactive and cannot be used for new purchases (PUR-03).");

        var invoice = Clean(invoiceNumber);
        if (invoice is not null)
        {
            var clash = await db.Purchases.AnyAsync(
                p => p.SupplierId == supplierId && p.InvoiceNumber == invoice
                     && (excludePurchaseId == null || p.Id != excludePurchaseId.Value),
                cancellationToken);
            if (clash)
                throw new InvalidOperationException($"Invoice '{invoice}' already exists for this supplier (PUR-12).");
        }
    }

    /// <summary>
    /// Allocates the net purchase-level adjustment (additional charges − purchase discount)
    /// across lines proportionally to each line's net value, then derives a landed unit cost.
    /// </summary>
    private static IReadOnlyList<(decimal AllocatedCharges, decimal AdjustedUnitCost)> AllocateLineCharges(Purchase purchase)
    {
        var items = purchase.Items.ToList();
        var weights = items
            .Select(i => PurchaseFinancials.LineTotal(i.Quantity, i.UnitCost, i.DiscountAmount))
            .ToList();
        var pool = purchase.AdditionalCharges - purchase.DiscountAmount;
        var allocated = PurchaseFinancials.AllocateProportionally(weights, pool);

        var result = new List<(decimal, decimal)>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var unitCost = PurchaseFinancials.AdjustedUnitCost(weights[i], allocated[i], item.Quantity);
            result.Add((allocated[i], unitCost));
        }
        return result;
    }

    private async Task<string> NextPurchaseNumberAsync(CancellationToken cancellationToken)
    {
        const string prefix = "PUR-";
        var numbers = await db.Purchases
            .Where(p => p.PurchaseNumber.StartsWith(prefix))
            .Select(p => p.PurchaseNumber)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var n in numbers)
            if (int.TryParse(n.AsSpan(prefix.Length), out var v) && v > max)
                max = v;
        return $"{prefix}{max + 1:D8}";
    }

    private PurchaseRowDto ToRow(Purchase p)
    {
        var isReceived = p.Status == PurchaseStatus.Received;
        return new PurchaseRowDto
        {
            Id = p.Id,
            PurchaseNumber = p.PurchaseNumber,
            InvoiceNumber = p.InvoiceNumber,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier?.Name ?? string.Empty,
            PurchaseDate = p.PurchaseDate,
            ReceivedDate = p.ReceivedDate,
            TotalAmount = p.TotalAmount,
            AmountPaid = p.AmountPaid,
            Outstanding = isReceived ? PurchaseFinancials.Outstanding(p.TotalAmount, p.AmountPaid) : 0m,
            ReceiveStatus = p.Status,
            PaymentStatus = p.PaymentStatus,
            ItemCount = p.Items.Count
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
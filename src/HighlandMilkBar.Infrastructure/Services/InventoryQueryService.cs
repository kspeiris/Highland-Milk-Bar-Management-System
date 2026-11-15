using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Database-backed read queries for the inventory screens. Stock is derived from batch
/// records (the source of truth) rather than the cached Product.CurrentStock column.
/// </summary>
public sealed class InventoryQueryService(AppDbContext db) : IInventoryQueryService
{
    private sealed class BatchInfo
    {
        public int BatchId { get; init; }
        public int ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string Sku { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public string BatchNumber { get; init; } = string.Empty;
        public DateTime? ExpiryDate { get; init; }
        public DateTime ReceivedAt { get; init; }
        public decimal QuantityRemaining { get; init; }
        public decimal UnitCost { get; init; }
        public bool RequiresExpiry { get; init; }
    }

    public async Task<InventoryOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var products = await LoadTrackedProductsAsync(cancellationToken);
        var batches = await LoadBatchesAsync(cancellationToken);

        var rows = BuildStockRows(products, batches, today);

        var physicalValue = batches.Sum(b => b.QuantityRemaining * b.UnitCost);
        var sellableBatches = batches.Where(b => ExpiryRules.IsSellable(b.ExpiryDate, today)).ToList();
        var sellableValue = sellableBatches.Sum(b => b.QuantityRemaining * b.UnitCost);

        var expiringSoon = batches.Count(b => IsNearExpiry(b.ExpiryDate, today));
        var expiredBatches = batches.Where(b => ExpiryRules.IsExpired(b.ExpiryDate, today)).ToList();
        var expiredQty = expiredBatches.Sum(b => b.QuantityRemaining);

        var byCategory = rows
            .Where(r => r.PhysicalStock > 0)
            .GroupBy(r => r.CategoryName)
            .Select(g => new CategoryStockDto
            {
                CategoryName = g.Key,
                Quantity = g.Sum(x => x.PhysicalStock),
                Value = batches.Where(b => b.CategoryName == g.Key).Sum(b => b.QuantityRemaining * b.UnitCost)
            })
            .OrderByDescending(c => c.Quantity)
            .ToList();

        var valueByCategory = batches
            .Where(b => b.QuantityRemaining > 0)
            .GroupBy(b => b.CategoryName)
            .Select(g => new CategoryStockDto { CategoryName = g.Key, Quantity = g.Sum(x => x.QuantityRemaining), Value = g.Sum(x => x.QuantityRemaining * x.UnitCost) })
            .OrderByDescending(c => c.Value)
            .ToList();

        var topStocked = rows
            .Where(r => r.PhysicalStock > 0)
            .OrderByDescending(r => r.PhysicalStock)
            .Take(5)
            .Select(r => new TopStockedDto { Name = r.Name, Sku = r.Sku, Quantity = r.PhysicalStock })
            .ToList();

        var lowStockAlerts = rows
            .Where(r => r.Status == StockStatus.LowStock)
            .OrderBy(r => r.Shortfall)
            .Take(5)
            .Select(r => new InventoryAlertDto
            {
                ProductName = r.Name,
                Sku = r.Sku,
                Detail = $"Remaining sellable stock: {r.SellableStock:0.###}. Reorder level: {r.ReorderLevel:0.###}."
            })
            .ToList();

        var expiryAlerts = expiredBatches
            .OrderBy(b => b.ExpiryDate)
            .Take(5)
            .Select(b => new InventoryAlertDto
            {
                ProductName = b.ProductName,
                Sku = b.Sku,
                Detail = $"Batch {b.BatchNumber}. Expired {b.ExpiryDate:dd MMM yyyy}. Quantity: {b.QuantityRemaining:0.###}."
            })
            .ToList();

        return new InventoryOverviewDto
        {
            TrackedProducts = products.Count,
            LowStockProducts = rows.Count(r => r.Status == StockStatus.LowStock),
            ExpiringSoonBatches = expiringSoon,
            ExpiredBatches = expiredBatches.Count,
            ExpiredQuantity = expiredQty,
            TotalPhysicalStock = rows.Sum(r => r.PhysicalStock),
            TotalSellableStock = rows.Sum(r => r.SellableStock),
            PhysicalStockValue = physicalValue,
            SellableStockValue = sellableValue,
            TotalActiveBatches = batches.Count(b => b.QuantityRemaining > 0),
            StockByCategory = byCategory,
            ValueByCategory = valueByCategory,
            TopStocked = topStocked,
            LowStockAlerts = lowStockAlerts,
            ExpiryAlerts = expiryAlerts
        };
    }

    public async Task<PagedResult<StockRowDto>> GetStockAsync(StockFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new StockFilter();
        var today = DateTime.Today;
        var products = await LoadTrackedProductsAsync(cancellationToken);
        var batches = await LoadBatchesAsync(cancellationToken);

        var rows = BuildStockRows(products, batches, today).AsEnumerable();

        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search;
            rows = rows.Where(r => r.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || r.Sku.Contains(term, StringComparison.OrdinalIgnoreCase)
                || r.CategoryName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (filter.CategoryId is { } categoryId) rows = rows.Where(r => r.CategoryId == categoryId);
        if (filter.Status is { } status) rows = rows.Where(r => r.Status == status);

        var list = rows.OrderBy(r => r.Name).ToList();
        return Page(list, filter.Page, filter.PageSize);
    }

    public async Task<PagedResult<BatchRowDto>> GetBatchesAsync(BatchFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new BatchFilter();
        var today = DateTime.Today;

        var query = db.InventoryBatches.AsNoTracking()
            .Where(b => b.Product != null && b.Product.TrackInventory)
            .Select(b => new BatchRowDto
            {
                Id = b.Id,
                ProductId = b.ProductId,
                ProductName = b.Product!.Name,
                BatchNumber = b.BatchNumber,
                ManufacturedDate = b.ManufacturedDate,
                ExpiryDate = b.ExpiryDate,
                ReceivedAt = b.ReceivedAt,
                QuantityReceived = b.QuantityReceived,
                QuantityRemaining = b.QuantityRemaining,
                UnitCost = b.UnitCost,
                IsActive = b.IsActive
            })
            .AsQueryable();

        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{EscapeLike(search)}%";
            query = query.Where(b => EF.Functions.Like(b.BatchNumber, like) || EF.Functions.Like(b.ProductName, like));
        }
        if (filter.ProductId is { } productId) query = query.Where(b => b.ProductId == productId);

        // Expiry filtering is date-only; evaluate in memory after materialising the (small) batch set.
        var all = await query.OrderByDescending(b => b.ReceivedAt).ThenByDescending(b => b.Id).ToListAsync(cancellationToken);

        var enriched = all.Select(b => EnrichBatch(b, today)).ToList();
        enriched = filter.Expiry switch
        {
            ExpiryFilter.Expired => enriched.Where(b => b.IsExpired).ToList(),
            ExpiryFilter.NearExpiry => enriched.Where(b => IsNearExpiry(b.ExpiryDate, today)).ToList(),
            _ => enriched
        };

        return Page(enriched, filter.Page, filter.PageSize);
    }

    public async Task<PagedResult<MovementRowDto>> GetMovementsAsync(MovementFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new MovementFilter();

        var query = db.StockMovements.AsNoTracking()
            .Where(m => m.Product != null)
            .Select(m => new MovementRowDto
            {
                Id = m.Id,
                CreatedAt = m.CreatedAt,
                ProductId = m.ProductId,
                ProductName = m.Product!.Name,
                Sku = m.Product.Sku,
                BatchNumber = m.InventoryBatch != null ? m.InventoryBatch.BatchNumber : null,
                MovementType = m.MovementType.ToString(),
                QuantityChange = m.QuantityChange,
                BalanceAfter = m.BalanceAfter,
                UnitCost = m.UnitCostSnapshot,
                Reference = m.ReferenceType != null ? m.ReferenceType.ToString() : null,
                Note = m.Note
            })
            .AsQueryable();

        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{EscapeLike(search)}%";
            query = query.Where(m => EF.Functions.Like(m.ProductName, like) || EF.Functions.Like(m.Sku, like)
                || (m.BatchNumber != null && EF.Functions.Like(m.BatchNumber, like)));
        }
        if (filter.ProductId is { } productId) query = query.Where(m => m.ProductId == productId);
        if (filter.MovementType is { } type) query = query.Where(m => m.MovementType == type.ToString());
        if (filter.From is { } from) query = query.Where(m => m.CreatedAt >= from);
        if (filter.To is { } to) query = query.Where(m => m.CreatedAt <= to.AddDays(1).AddTicks(-1));

        var list = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).ToListAsync(cancellationToken);
        return Page(list, filter.Page, filter.PageSize);
    }

    public async Task<IReadOnlyList<ExpiryRowDto>> GetNearExpiryAsync(int withinDays, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var horizon = today.AddDays(withinDays <= 0 ? ExpiryRules.UpcomingDays : withinDays);

        var batches = await LoadBatchesAsync(cancellationToken);
        return batches
            .Where(b => b.ExpiryDate != null && b.ExpiryDate.Value.Date >= today && b.ExpiryDate.Value.Date <= horizon && b.QuantityRemaining > 0)
            .OrderBy(b => b.ExpiryDate)
            .Select(b => ToExpiryRow(b, today))
            .ToList();
    }

    public async Task<IReadOnlyList<ExpiryRowDto>> GetExpiredAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var batches = await LoadBatchesAsync(cancellationToken);
        return batches
            .Where(b => ExpiryRules.IsExpired(b.ExpiryDate, today) && b.QuantityRemaining > 0)
            .OrderBy(b => b.ExpiryDate)
            .Select(b => ToExpiryRow(b, today))
            .ToList();
    }

    public async Task<IReadOnlyList<LowStockRowDto>> GetLowStockAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var products = await LoadTrackedProductsAsync(cancellationToken);
        var batches = await LoadBatchesAsync(cancellationToken);
        return BuildStockRows(products, batches, today)
            .Where(r => r.SellableStock <= r.ReorderLevel)
            .OrderBy(r => r.Shortfall)
            .Select(r => new LowStockRowDto
            {
                ProductId = r.ProductId,
                Name = r.Name,
                Sku = r.Sku,
                CategoryName = r.CategoryName,
                SellableStock = r.SellableStock,
                ReorderLevel = r.ReorderLevel,
                Shortfall = r.Shortfall
            })
            .ToList();
    }

    // ---- helpers ----

    private async Task<List<ProductInfo>> LoadTrackedProductsAsync(CancellationToken cancellationToken)
    {
        return await db.Products.AsNoTracking()
            .Where(p => p.TrackInventory)
            .Select(p => new ProductInfo
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category != null ? p.Category.Name : "Uncategorised",
                CategoryId = p.CategoryId,
                ReorderLevel = p.ReorderLevel,
                RequiresExpiry = p.RequiresExpiryDate
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<BatchInfo>> LoadBatchesAsync(CancellationToken cancellationToken)
    {
        return await db.InventoryBatches.AsNoTracking()
            .Where(b => b.IsActive && b.Product != null && b.Product.TrackInventory)
            .Select(b => new BatchInfo
            {
                BatchId = b.Id,
                ProductId = b.ProductId,
                ProductName = b.Product!.Name,
                Sku = b.Product.Sku,
                CategoryName = b.Product.Category != null ? b.Product.Category.Name : "Uncategorised",
                BatchNumber = b.BatchNumber,
                ExpiryDate = b.ExpiryDate,
                ReceivedAt = b.ReceivedAt,
                QuantityRemaining = b.QuantityRemaining,
                UnitCost = b.UnitCost,
                RequiresExpiry = b.Product.RequiresExpiryDate
            })
            .ToListAsync(cancellationToken);
    }

    private static List<StockRowDto> BuildStockRows(List<ProductInfo> products, List<BatchInfo> batches, DateTime today)
    {
        var byProduct = batches.GroupBy(b => b.ProductId).ToDictionary(g => g.Key, g => g.ToList());

        return products.Select(p =>
        {
            var list = byProduct.TryGetValue(p.Id, out var b) ? b : new List<BatchInfo>();
            var physical = list.Sum(x => x.QuantityRemaining);
            var expired = list.Where(x => ExpiryRules.IsExpired(x.ExpiryDate, today)).Sum(x => x.QuantityRemaining);
            var sellable = physical - expired;
            var status = sellable <= 0 ? StockStatus.OutOfStock
                : sellable <= p.ReorderLevel ? StockStatus.LowStock
                : StockStatus.InStock;
            return new StockRowDto
            {
                ProductId = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryName = p.CategoryName,
                CategoryId = p.CategoryId,
                PhysicalStock = physical,
                SellableStock = sellable,
                ExpiredStock = expired,
                ReorderLevel = p.ReorderLevel,
                Shortfall = sellable <= p.ReorderLevel ? p.ReorderLevel - sellable : 0m,
                Status = status,
                RequiresExpiryDate = p.RequiresExpiry
            };
        }).ToList();
    }

    private static BatchRowDto EnrichBatch(BatchRowDto b, DateTime today)
    {
        var days = ExpiryRules.DaysRemaining(b.ExpiryDate, today);
        return new BatchRowDto
        {
            Id = b.Id,
            ProductId = b.ProductId,
            ProductName = b.ProductName,
            BatchNumber = b.BatchNumber,
            ManufacturedDate = b.ManufacturedDate,
            ExpiryDate = b.ExpiryDate,
            ReceivedAt = b.ReceivedAt,
            QuantityReceived = b.QuantityReceived,
            QuantityRemaining = b.QuantityRemaining,
            UnitCost = b.UnitCost,
            DaysRemaining = days == int.MaxValue ? 0 : days,
            ExpiryStatus = ExpiryRules.Status(b.ExpiryDate, today),
            IsExpired = ExpiryRules.IsExpired(b.ExpiryDate, today),
            Value = b.QuantityRemaining * b.UnitCost,
            IsActive = b.IsActive
        };
    }

    private static ExpiryRowDto ToExpiryRow(BatchInfo b, DateTime today)
    {
        var days = ExpiryRules.DaysRemaining(b.ExpiryDate, today);
        return new ExpiryRowDto
        {
            BatchId = b.BatchId,
            ProductName = b.ProductName,
            Sku = b.Sku,
            BatchNumber = b.BatchNumber,
            ExpiryDate = b.ExpiryDate,
            QuantityRemaining = b.QuantityRemaining,
            UnitCost = b.UnitCost,
            Value = b.QuantityRemaining * b.UnitCost,
            DaysRemaining = days == int.MaxValue ? 0 : days,
            ExpiryStatus = ExpiryRules.Status(b.ExpiryDate, today)
        };
    }

    private static bool IsNearExpiry(DateTime? expiry, DateTime today)
        => expiry != null && expiry.Value.Date >= today && expiry.Value.Date <= today.AddDays(ExpiryRules.UpcomingDays);

    private static PagedResult<T> Page<T>(List<T> items, int page, int pageSize)
    {
        var p = page < 1 ? 1 : page;
        var size = pageSize is < 1 or > 200 ? 20 : pageSize;
        return new PagedResult<T>
        {
            Items = items.Skip((p - 1) * size).Take(size).ToList(),
            TotalCount = items.Count,
            Page = p,
            PageSize = size
        };
    }

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private sealed class ProductInfo
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Sku { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public int CategoryId { get; init; }
        public decimal ReorderLevel { get; init; }
        public bool RequiresExpiry { get; init; }
    }
}
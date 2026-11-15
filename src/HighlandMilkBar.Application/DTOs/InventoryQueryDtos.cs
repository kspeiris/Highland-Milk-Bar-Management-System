using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

/// <summary>Derived sellability state of a product's stock.</summary>
public enum StockStatus
{
    InStock,
    LowStock,
    OutOfStock
}

/// <summary>Expiry bucket used for filtering batches.</summary>
public enum ExpiryFilter
{
    All,
    NearExpiry,
    Expired
}

/// <summary>
/// Date-only expiry rules shared by queries and tests. A batch expiring today is still
/// sellable until the end of that business date; it becomes expired the following day.
/// </summary>
public static class ExpiryRules
{
    public const int UpcomingDays = 30;
    public const int WarningDays = 7;

    public static bool IsExpired(DateTime? expiry, DateTime today)
        => expiry.HasValue && expiry.Value.Date < today.Date;

    public static bool IsSellable(DateTime? expiry, DateTime today)
        => expiry is null || expiry.Value.Date >= today.Date;

    public static int DaysRemaining(DateTime? expiry, DateTime today)
        => expiry.HasValue ? (int)(expiry.Value.Date - today.Date).TotalDays : int.MaxValue;

    /// <summary>Normal / Upcoming / Warning / ExpiresToday / Expired / NoExpiry.</summary>
    public static string Status(DateTime? expiry, DateTime today)
    {
        if (expiry is null) return "No expiry";
        var days = DaysRemaining(expiry, today);
        if (days < 0) return "Expired";
        if (days == 0) return "Expires today";
        if (days <= WarningDays) return "Warning";
        if (days <= UpcomingDays) return "Upcoming";
        return "Normal";
    }
}

/// <summary>Per-category quantity and value breakdown.</summary>
public sealed class CategoryStockDto
{
    public string CategoryName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Value { get; init; }
}

public sealed class TopStockedDto
{
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
}

/// <summary>A low-stock or expiry alert shown on the overview.</summary>
public sealed class InventoryAlertDto
{
    public string ProductName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

/// <summary>KPIs and breakdowns for the inventory overview.</summary>
public sealed class InventoryOverviewDto
{
    public int TrackedProducts { get; init; }
    public int LowStockProducts { get; init; }
    public int ExpiringSoonBatches { get; init; }
    public int ExpiredBatches { get; init; }
    public decimal ExpiredQuantity { get; init; }
    public decimal TotalPhysicalStock { get; init; }
    public decimal TotalSellableStock { get; init; }
    public decimal PhysicalStockValue { get; init; }
    public decimal SellableStockValue { get; init; }
    public int TotalActiveBatches { get; init; }
    public IReadOnlyList<CategoryStockDto> StockByCategory { get; init; } = Array.Empty<CategoryStockDto>();
    public IReadOnlyList<CategoryStockDto> ValueByCategory { get; init; } = Array.Empty<CategoryStockDto>();
    public IReadOnlyList<TopStockedDto> TopStocked { get; init; } = Array.Empty<TopStockedDto>();
    public IReadOnlyList<InventoryAlertDto> LowStockAlerts { get; init; } = Array.Empty<InventoryAlertDto>();
    public IReadOnlyList<InventoryAlertDto> ExpiryAlerts { get; init; } = Array.Empty<InventoryAlertDto>();
}

/// <summary>One product row in the stock overview.</summary>
public sealed class StockRowDto
{
    public int ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public decimal PhysicalStock { get; init; }
    public decimal SellableStock { get; init; }
    public decimal ExpiredStock { get; init; }
    public decimal ReorderLevel { get; init; }
    public decimal Shortfall { get; init; }
    public StockStatus Status { get; init; }
    public bool RequiresExpiryDate { get; init; }

    public string StatusText => Status switch
    {
        StockStatus.InStock => "In stock",
        StockStatus.LowStock => "Low stock",
        _ => "Out of stock"
    };
}

public sealed class StockFilter
{
    public string? Search { get; init; }
    public int? CategoryId { get; init; }
    public StockStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>One batch row in batch management.</summary>
public sealed class BatchRowDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string BatchNumber { get; init; } = string.Empty;
    public DateTime? ManufacturedDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public DateTime ReceivedAt { get; init; }
    public decimal QuantityReceived { get; init; }
    public decimal QuantityRemaining { get; init; }
    public decimal UnitCost { get; init; }
    public decimal Value { get; init; }
    public int DaysRemaining { get; init; }
    public string ExpiryStatus { get; init; } = string.Empty;
    public bool IsExpired { get; init; }
    public bool IsActive { get; init; }
}

public sealed class BatchFilter
{
    public string? Search { get; init; }
    public int? ProductId { get; init; }
    public ExpiryFilter Expiry { get; init; } = ExpiryFilter.All;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>One row of the immutable stock movement ledger.</summary>
public sealed class MovementRowDto
{
    public int Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string? BatchNumber { get; init; }
    public string MovementType { get; init; } = string.Empty;
    public decimal QuantityChange { get; init; }
    public decimal BalanceAfter { get; init; }
    public decimal UnitCost { get; init; }
    public string? Reference { get; init; }
    public string? Note { get; init; }
}

public sealed class MovementFilter
{
    public string? Search { get; init; }
    public int? ProductId { get; init; }
    public StockMovementType? MovementType { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>One batch in the near-expiry / expired lists.</summary>
public sealed class ExpiryRowDto
{
    public int BatchId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string BatchNumber { get; init; } = string.Empty;
    public DateTime? ExpiryDate { get; init; }
    public decimal QuantityRemaining { get; init; }
    public decimal UnitCost { get; init; }
    public decimal Value { get; init; }
    public int DaysRemaining { get; init; }
    public string ExpiryStatus { get; init; } = string.Empty;
}

/// <summary>One row in the low-stock list.</summary>
public sealed class LowStockRowDto
{
    public int ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal SellableStock { get; init; }
    public decimal ReorderLevel { get; init; }
    public decimal Shortfall { get; init; }
}
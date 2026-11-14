using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

/// <summary>A measurement unit available for products (from the Units table).</summary>
public sealed class UnitDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public UnitKind Kind { get; init; }
}

/// <summary>Row model for the product listing grid.</summary>
public sealed class ProductDto
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public ProductType Type { get; init; }
    public string TypeName { get; set; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal CostPrice { get; init; }
    public decimal SellingPrice { get; init; }
    public decimal CurrentStock { get; init; }
    public decimal ReorderLevel { get; init; }
    public bool TrackInventory { get; init; }
    public bool IsActive { get; init; }

    public string StatusText => IsActive ? "Active" : "Inactive";
}

/// <summary>Full product record for the details view and edit form.</summary>
public sealed class ProductDetailDto
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public ProductType Type { get; init; }
    public string TypeName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public int? BaseUnitId { get; init; }
    public decimal CostPrice { get; init; }
    public decimal SellingPrice { get; init; }
    public decimal CurrentStock { get; init; }
    public decimal ReorderLevel { get; init; }
    public bool TrackInventory { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>Filter, sort and paging options for a product search.</summary>
public sealed class ProductFilter
{
    public string? Search { get; init; }
    public int? CategoryId { get; init; }
    public ProductType? Type { get; init; }
    public bool? IsActive { get; init; }

    /// <summary>One of: name, sku, category, sellingprice, stock (default: name).</summary>
    public string SortBy { get; init; } = "name";
    public bool SortDescending { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>A single page of results plus paging metadata.</summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Summary counters shown above the product grid.</summary>
public sealed class ProductStatsDto
{
    public int TotalProducts { get; init; }
    public int ActiveProducts { get; init; }
    public int InactiveProducts { get; init; }
    public int Categories { get; init; }
}

/// <summary>Request to create a new product.</summary>
public sealed record CreateProductRequest
{
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public int CategoryId { get; init; }
    public ProductType Type { get; init; } = ProductType.Packaged;
    public string Unit { get; init; } = string.Empty;
    public int? BaseUnitId { get; init; }
    public decimal CostPrice { get; init; }
    public decimal SellingPrice { get; init; }
    public decimal OpeningStock { get; init; }
    public decimal ReorderLevel { get; init; }
    public bool TrackInventory { get; init; } = true;
    public bool IsActive { get; init; } = true;
}

/// <summary>Request to update an existing product. Stock is changed via inventory, not here.</summary>
public sealed record UpdateProductRequest
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public int CategoryId { get; init; }
    public ProductType Type { get; init; } = ProductType.Packaged;
    public string Unit { get; init; } = string.Empty;
    public decimal CostPrice { get; init; }
    public decimal SellingPrice { get; init; }
    public decimal ReorderLevel { get; init; }
    public bool TrackInventory { get; init; }
    public bool IsActive { get; init; } = true;
}

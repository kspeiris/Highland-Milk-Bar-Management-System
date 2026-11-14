using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

public sealed class CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class ProductDto
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal CostPrice { get; init; }
    public decimal SellingPrice { get; init; }
    public decimal CurrentStock { get; init; }
    public decimal ReorderLevel { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CreateProductRequest
{
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public ProductType Type { get; init; }
    public string Unit { get; init; } = "Unit";
    public decimal CostPrice { get; init; }
    public decimal SellingPrice { get; init; }
    public decimal OpeningStock { get; init; }
    public decimal ReorderLevel { get; init; }
    public bool TrackInventory { get; init; } = true;
}

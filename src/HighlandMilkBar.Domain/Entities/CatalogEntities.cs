using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public ProductType Type { get; set; } = ProductType.Packaged;

    /// <summary>Display unit (for example "Bottle", "Scoop").</summary>
    public string Unit { get; set; } = "Unit";

    /// <summary>The unit stock quantities are held in (the base unit).</summary>
    public int? BaseUnitId { get; set; }
    public Unit? BaseUnit { get; set; }

    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool TrackInventory { get; set; }

    /// <summary>When true, received batches must carry an expiry date and are subject to expiry rules.</summary>
    public bool RequiresExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ProductUnit> ProductUnits { get; set; } = new List<ProductUnit>();
    public ICollection<InventoryBatch> InventoryBatches { get; set; } = new List<InventoryBatch>();
}

/// <summary>A reusable measurement unit (litre, gram, bottle, scoop...).</summary>
public class Unit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public UnitKind Kind { get; set; } = UnitKind.Count;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ProductUnit> ProductUnits { get; set; } = new List<ProductUnit>();
}

/// <summary>
/// Defines how one product can be measured in an alternative unit. The base unit has
/// a factor of 1; an alternative unit stores how many base units it represents
/// (for example 1 Litre = 1000 ml when the base unit is millilitre).
/// </summary>
public class ProductUnit
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int UnitId { get; set; }
    public Unit? Unit { get; set; }

    /// <summary>How many base units one of this unit represents (base unit = 1).</summary>
    public decimal FactorToBase { get; set; } = 1;
    public bool IsBaseUnit { get; set; }
}

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
namespace HighlandMilkBar.Application.DTOs;

// ===== Milk Bar overview =====

public sealed class CategoryCountDto
{
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class RecipeCostRankDto
{
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal CostPerServing { get; init; }
}

public sealed class MenuMarginRankDto
{
    public string Name { get; init; } = string.Empty;
    public decimal SellingPrice { get; init; }
    public decimal Cost { get; init; }
    public decimal MarginPercent { get; init; }
}

public sealed class IngredientCostShareDto
{
    public string Name { get; init; } = string.Empty;
    public decimal TotalCost { get; init; }
    public decimal SharePercent { get; set; }
}

/// <summary>Severity of an operational alert on the Milk Bar dashboard.</summary>
public enum MilkBarAlertSeverity
{
    Info,
    Warning,
    Critical
}

public sealed class MilkBarAlertDto
{
    public string Type { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public MilkBarAlertSeverity Severity { get; init; }
}

public sealed class MilkBarOverviewDto
{
    public int TotalMenuItems { get; init; }
    public int ActiveRecipes { get; init; }
    public int TotalIngredientsUsed { get; init; }
    public int LowStockIngredients { get; init; }
    public decimal AverageRecipeCost { get; init; }
    public decimal AverageMarginPercent { get; init; }

    public IReadOnlyList<CategoryCountDto> MenuByCategory { get; init; } = new List<CategoryCountDto>();
    public IReadOnlyList<RecipeCostRankDto> HighestCostRecipes { get; init; } = new List<RecipeCostRankDto>();
    public IReadOnlyList<MenuMarginRankDto> HighestMarginProducts { get; init; } = new List<MenuMarginRankDto>();
    public IReadOnlyList<MenuMarginRankDto> LowestMarginProducts { get; init; } = new List<MenuMarginRankDto>();
    public IReadOnlyList<IngredientCostShareDto> IngredientCostBreakdown { get; init; } = new List<IngredientCostShareDto>();
    public IReadOnlyList<MilkBarAlertDto> Alerts { get; init; } = new List<MilkBarAlertDto>();
}

// ===== Menu management =====

public sealed class MenuFilter
{
    public string? Search { get; init; }
    public int? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class MenuItemDto
{
    public int ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal SellingPrice { get; init; }
    public bool IsActive { get; init; }

    public int? RecipeId { get; init; }
    public bool HasActiveRecipe { get; init; }
    public decimal EstimatedCost { get; init; }
    public decimal EstimatedProfit { get; init; }
    public decimal EstimatedMarginPercent { get; init; }
    public bool AllIngredientsAvailable { get; init; }

    public string StatusText => IsActive ? "Active" : "Inactive";
    public string RecipeStatus => HasActiveRecipe ? "Recipe ready" : "No recipe";
}

// ===== Ingredient management =====

public sealed class IngredientFilter
{
    public string? Search { get; init; }
    public int? CategoryId { get; init; }
    public StockStatus? Stock { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class IngredientDto
{
    public int ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal SellableStock { get; init; }
    public decimal ReorderLevel { get; init; }
    public decimal UnitCost { get; init; }
    public int ActiveRecipeCount { get; init; }
    public bool IsActive { get; init; }

    public StockStatus Status { get; init; }
    public string StatusText => Status switch
    {
        StockStatus.InStock => "In stock",
        StockStatus.LowStock => "Low stock",
        _ => "Out of stock"
    };
}
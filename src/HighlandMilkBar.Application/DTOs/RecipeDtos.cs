namespace HighlandMilkBar.Application.DTOs;

// ===== Costing =====

/// <summary>A single ingredient line inside a recipe cost breakdown.</summary>
public sealed class RecipeCostLine
{
    public int IngredientProductId { get; init; }
    public string IngredientName { get; init; } = string.Empty;

    /// <summary>Quantity expressed in the ingredient's stock unit (after any unit conversion).</summary>
    public decimal Quantity { get; init; }

    /// <summary>Stock unit symbol the quantity is expressed in (for example "ml", "g", "pcs").</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>Cost per one stock unit of the ingredient.</summary>
    public decimal UnitCost { get; init; }

    public decimal LineCost { get; init; }
    public bool InStock { get; init; }

    /// <summary>The quantity as entered on the recipe (in the recipe's own unit).</summary>
    public decimal RecipeQuantity { get; init; }

    /// <summary>The recipe's own unit symbol for this line.</summary>
    public string RecipeUnit { get; init; } = string.Empty;

    public decimal AvailableStock { get; init; }
    public decimal SharePercent { get; set; }
}

/// <summary>Full estimated cost breakdown for a recipe.</summary>
public sealed class RecipeCostBreakdown
{
    public int RecipeId { get; init; }
    public decimal Servings { get; init; }
    public decimal CostPerServing { get; init; }
    public decimal TotalCost { get; init; }
    public bool AllIngredientsAvailable { get; init; }
    public IReadOnlyList<RecipeCostLine> Lines { get; init; } = new List<RecipeCostLine>();

    // Profitability context (based on the current menu product selling price).
    public decimal SellingPrice { get; init; }
    public decimal GrossProfit { get; init; }
    public decimal MarginPercent { get; init; }
}

// ===== Recipe list / detail =====

public sealed class RecipeFilter
{
    public string? Search { get; init; }
    public int? MenuCategoryId { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class RecipeSummaryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int MenuProductId { get; init; }
    public string MenuProductName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal YieldQuantity { get; init; }
    public string YieldUnit { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int ItemCount { get; init; }
    public decimal EstimatedCostPerServing { get; init; }
    public decimal SellingPrice { get; init; }
    public decimal EstimatedGrossProfit { get; init; }
    public decimal EstimatedMarginPercent { get; init; }
    public bool AllIngredientsAvailable { get; init; }
    public string AvailabilityText { get; init; } = string.Empty;

    public string YieldText => $"{YieldQuantity:0.###} {YieldUnit}";
    public string StatusText => IsActive ? "Active" : "Inactive";
}

public sealed class RecipeDetailDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int MenuProductId { get; init; }
    public string MenuProductName { get; init; } = string.Empty;
    public int? MenuCategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal YieldQuantity { get; init; }
    public string YieldUnit { get; init; } = string.Empty;
    public int Version { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public decimal SellingPrice { get; init; }

    public IReadOnlyList<RecipeDetailItemDto> Items { get; init; } = new List<RecipeDetailItemDto>();
    public RecipeCostBreakdown Cost { get; init; } = new RecipeCostBreakdown();
    public RecipeAvailabilityDto Availability { get; init; } = new RecipeAvailabilityDto();
}

public sealed class RecipeDetailItemDto
{
    public int IngredientProductId { get; init; }
    public string IngredientName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public decimal QuantityRequired { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal UnitCost { get; init; }
    public decimal LineCost { get; init; }
    public decimal AvailableStock { get; init; }
    public string StockUnit { get; init; } = string.Empty;
    public bool InStock { get; init; }
}

// ===== Commands =====

public sealed class CreateRecipeRequest
{
    public string Name { get; init; } = string.Empty;
    public int MenuProductId { get; init; }
    public decimal YieldQuantity { get; init; } = 1;
    public string YieldUnit { get; init; } = "Portion";
    public bool IsActive { get; init; } = true;
    public List<RecipeLineRequest> Items { get; init; } = new List<RecipeLineRequest>();
}

public sealed class UpdateRecipeRequest
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int MenuProductId { get; init; }
    public decimal YieldQuantity { get; init; } = 1;
    public string YieldUnit { get; init; } = "Portion";
    public bool IsActive { get; init; } = true;
    public List<RecipeLineRequest> Items { get; init; } = new List<RecipeLineRequest>();
}

public sealed class RecipeLineRequest
{
    public int IngredientProductId { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
}

// ===== Ingredient availability =====

public sealed class IngredientAvailabilityLine
{
    public int IngredientProductId { get; init; }
    public string IngredientName { get; init; } = string.Empty;
    public decimal RequiredPerServing { get; init; }
    public decimal RequiredTotal { get; init; }
    public decimal AvailableStock { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal Shortage { get; init; }
    public bool IsAvailable { get; init; }

    /// <summary>How many servings this ingredient alone could support (int.MaxValue when unlimited).</summary>
    public decimal MaxServings { get; init; }
    public bool IsUnlimited { get; init; }

    public string StatusText => IsAvailable ? "Available" : "Shortage";
}

public sealed class RecipeAvailabilityDto
{
    public int RecipeId { get; init; }
    public decimal MaxServings { get; init; }
    public bool IsUnlimited { get; init; }
    public bool AllAvailable { get; init; }
    public IReadOnlyList<IngredientAvailabilityLine> Lines { get; init; } = new List<IngredientAvailabilityLine>();
}

// ===== Pricing =====

public sealed class PricingRecommendationDto
{
    public int RecipeId { get; init; }
    public decimal CostPerServing { get; init; }
    public decimal CurrentSellingPrice { get; init; }
    public decimal TargetMarginPercent { get; init; }
    public decimal RecommendedPrice { get; init; }
    public decimal RecommendedPriceRounded { get; init; }
    public decimal CurrentMarginPercent { get; init; }
    public decimal RecommendedProfit { get; init; }
    public decimal RoundingIncrement { get; init; }
    public bool MeetsTarget { get; init; }
    public string RecommendationText { get; init; } = string.Empty;
}
namespace HighlandMilkBar.Application.DTOs;

public sealed class RecipeCostLine
{
    public int IngredientProductId { get; init; }
    public string IngredientName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal UnitCost { get; init; }
    public decimal LineCost { get; init; }
    public bool InStock { get; init; }
}

public sealed class RecipeCostBreakdown
{
    public int RecipeId { get; init; }
    public decimal Servings { get; init; }
    public decimal CostPerServing { get; init; }
    public decimal TotalCost { get; init; }
    public bool AllIngredientsAvailable { get; init; }
    public IReadOnlyList<RecipeCostLine> Lines { get; init; } = new List<RecipeCostLine>();
}
namespace HighlandMilkBar.Domain.Entities;

public class Recipe
{
    public int Id { get; set; }
    public int MenuProductId { get; set; }
    public Product? MenuProduct { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal YieldQuantity { get; set; } = 1;
    public string YieldUnit { get; set; } = "Portion";
    public bool IsActive { get; set; } = true;
    public ICollection<RecipeItem> Items { get; set; } = new List<RecipeItem>();
}

public class RecipeItem
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int IngredientProductId { get; set; }
    public Product? IngredientProduct { get; set; }
    public decimal QuantityRequired { get; set; }
    public string Unit { get; set; } = "Unit";
}

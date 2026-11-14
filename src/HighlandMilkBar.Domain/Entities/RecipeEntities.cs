namespace HighlandMilkBar.Domain.Entities;

/// <summary>
/// A milk-bar recipe that turns ingredient products into a prepared menu item.
/// One menu product can have multiple recipe versions over time.
/// </summary>
public class Recipe
{
    public int Id { get; set; }
    public int MenuProductId { get; set; }
    public Product? MenuProduct { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>How many portions a single preparation of this recipe yields.</summary>
    public decimal YieldQuantity { get; set; } = 1;

    /// <summary>Display unit of the yield (for example "Portion", "Glass").</summary>
    public string YieldUnit { get; set; } = "Portion";

    /// <summary>Monotonic version number so historic sales keep their original recipe.</summary>
    public int Version { get; set; } = 1;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
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

    /// <summary>Unit the ingredient quantity is expressed in (for example "ml", "g", "scoop").</summary>
    public string Unit { get; set; } = "Unit";

    public int SortOrder { get; set; }
}
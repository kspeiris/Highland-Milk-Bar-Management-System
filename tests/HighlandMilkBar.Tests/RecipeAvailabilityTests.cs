using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;

namespace HighlandMilkBar.Tests;

public sealed class RecipeAvailabilityTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private RecipeService Recipes => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<int> CategoryAsync() => (await _db.AddCategoryAsync("Test Milkshakes")).Id;

    private async Task<(int RecipeId, Product Milk, Product Ice, Product Syrup)> RecipeAsync(int cat, decimal yield = 1)
    {
        var milk = await _db.AddIngredientAsync("ING-MILK", "Fresh Milk", "ml", 0.25m, cat);
        var ice = await _db.AddIngredientAsync("ING-ICE", "Ice Cream", "scoop", 35m, cat);
        var syrup = await _db.AddIngredientAsync("ING-SYRUP", "Chocolate Syrup", "ml", 0.8m, cat);
        var menu = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 250m);
        var recipe = await _db.AddRecipeAsync("Shake", menu.Id, yield, "Portion", true,
            (milk.Id, 200m, "ml"),
            (ice.Id, 2m, "scoop"),
            (syrup.Id, 20m, "ml"));
        return (recipe.Id, milk, ice, syrup);
    }

    private void Batch(int productId, decimal qty, DateTime? expiry = null, string? number = null)
    {
        _db.AddBatch(productId, number ?? "B" + Guid.NewGuid().ToString("N")[..6], qty, 1m, DateTime.Today, expiry);
    }

    [Fact]
    public async Task MaxServings_IsLimitedByTightestIngredient()
    {
        var cat = await CategoryAsync();
        var (recipeId, milk, ice, syrup) = await RecipeAsync(cat);
        Batch(milk.Id, 5000);   // 25 servings
        Batch(ice.Id, 30);      // 15 servings
        Batch(syrup.Id, 100);   // 5 servings
        await Db.SaveChangesAsync();

        var a = await Recipes.GetAvailabilityAsync(recipeId);

        Assert.Equal(5, a.MaxServings);
        Assert.True(a.AllAvailable);
        Assert.False(a.IsUnlimited);
    }

    [Fact]
    public async Task ExpiredBatches_AreExcludedFromAvailability()
    {
        var cat = await CategoryAsync();
        var (recipeId, milk, ice, syrup) = await RecipeAsync(cat);
        Batch(milk.Id, 5000);
        Batch(milk.Id, 1000, expiry: DateTime.Today.AddDays(-1)); // expired, ignored
        Batch(ice.Id, 30);
        Batch(syrup.Id, 100);
        await Db.SaveChangesAsync();

        var a = await Recipes.GetAvailabilityAsync(recipeId);
        var milkLine = a.Lines.Single(l => l.IngredientProductId == milk.Id);

        Assert.Equal(5000m, milkLine.AvailableStock); // expired 1000 excluded
        Assert.Equal(5, a.MaxServings);
    }

    [Fact]
    public async Task ZeroAvailable_ReportsShortageAndZeroServings()
    {
        var cat = await CategoryAsync();
        var (recipeId, milk, ice, syrup) = await RecipeAsync(cat);
        Batch(milk.Id, 5000);
        Batch(ice.Id, 30);
        // no syrup batch
        await Db.SaveChangesAsync();

        var a = await Recipes.GetAvailabilityAsync(recipeId);
        var syrupLine = a.Lines.Single(l => l.IngredientProductId == syrup.Id);

        Assert.Equal(0, a.MaxServings);
        Assert.False(a.AllAvailable);
        Assert.False(syrupLine.IsAvailable);
        Assert.Equal(20m, syrupLine.Shortage);
    }

    [Fact]
    public async Task NonTrackedIngredient_IsUnlimitedAndIgnored()
    {
        var cat = await CategoryAsync();
        var milk = await _db.AddIngredientAsync("ING-MILK", "Milk", "ml", 0.25m, cat);
        var cup = await _db.AddProductAsync("ING-CUP", "Cup", cat, type: Domain.Enums.ProductType.Packaged, track: false);
        var menu = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 250m);
        var recipe = await _db.AddRecipeAsync("Shake", menu.Id, 1, "Portion", true,
            (milk.Id, 200m, "ml"), (cup.Id, 1m, "pcs"));
        Batch(milk.Id, 1000); // 5 servings
        await Db.SaveChangesAsync();

        var a = await Recipes.GetAvailabilityAsync(recipe.Id);

        Assert.Equal(5, a.MaxServings); // cup does not constrain
        var cupLine = a.Lines.Single(l => l.IngredientProductId == cup.Id);
        Assert.True(cupLine.IsUnlimited);
        Assert.True(cupLine.IsAvailable);
    }

    [Fact]
    public async Task Unlimited_WhenNoTrackedConstraints()
    {
        var cat = await CategoryAsync();
        var cup = await _db.AddProductAsync("ING-CUP", "Cup", cat, type: Domain.Enums.ProductType.Packaged, track: false);
        var menu = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 250m);
        var recipe = await _db.AddRecipeAsync("Shake", menu.Id, 1, "Portion", true, (cup.Id, 1m, "pcs"));

        var a = await Recipes.GetAvailabilityAsync(recipe.Id);

        Assert.True(a.IsUnlimited);
    }
}
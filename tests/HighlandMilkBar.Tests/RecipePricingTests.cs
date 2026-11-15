using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class RecipePricingTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private RecipeService Recipes => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    /// <summary>A recipe whose cost per serving is exactly LKR 100 (200 ml milk @ 0.50).</summary>
    private async Task<(int MenuId, int RecipeId)> RecipeAsync(decimal price = 250m)
    {
        var cat = (await _db.AddCategoryAsync("Test Milkshakes")).Id;
        var milk = await _db.AddIngredientAsync("ING-MILK", "Fresh Milk", "ml", 0.5m, cat);
        var menu = await _db.AddMenuItemAsync("MB-1", "Milkshake", cat, price);
        var recipe = await _db.AddRecipeAsync("Milkshake", menu.Id, 1, "Portion", true, (milk.Id, 200m, "ml"));
        return (menu.Id, recipe.Id);
    }

    [Fact]
    public async Task Recommends_ForFortyPercentMargin()
    {
        var (_, recipeId) = await RecipeAsync(price: 120m); // 16.7% current margin, below target

        var p = await Recipes.CalculatePricingAsync(recipeId, 40m, 0m);

        Assert.Equal(100m, p.CostPerServing);
        Assert.Equal(166.67m, p.RecommendedPrice);       // 100 / 0.6
        Assert.Equal(166.67m, p.RecommendedPriceRounded);
        Assert.False(p.MeetsTarget);
    }

    [Fact]
    public async Task Applies_RoundingIncrement()
    {
        var (_, recipeId) = await RecipeAsync();

        var p = await Recipes.CalculatePricingAsync(recipeId, 40m, 5m);

        Assert.Equal(170m, p.RecommendedPriceRounded);   // ceil(166.67 / 5) * 5
    }

    [Fact]
    public async Task Rejects_HundredPercentMargin()
    {
        var (_, recipeId) = await RecipeAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => Recipes.CalculatePricingAsync(recipeId, 100m, 0m));
    }

    [Fact]
    public async Task Recommendation_DoesNotChangeProductPrice()
    {
        var (menuId, recipeId) = await RecipeAsync(price: 250m);
        var before = (await Db.Products.FindAsync(menuId))!.SellingPrice;

        await Recipes.CalculatePricingAsync(recipeId, 40m, 5m);

        var after = (await Db.Products.AsNoTracking().FirstAsync(p => p.Id == menuId)).SellingPrice;
        Assert.Equal(before, after);
        Assert.Equal(250m, after);
    }
}
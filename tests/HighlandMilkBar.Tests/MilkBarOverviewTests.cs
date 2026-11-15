using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class MilkBarOverviewTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private RecipeService Recipes => new(Db);
    private MilkBarService MilkBar => new(Db, new RecipeService(Db));

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Overview_ReportsRealCounts()
    {
        var cat = (await _db.AddCategoryAsync("Test Milkshakes")).Id;
        var milk = await _db.AddIngredientAsync("ING-MILK", "Fresh Milk", "ml", 0.5m, cat);
        var sugar = await _db.AddIngredientAsync("ING-SUGAR", "Sugar", "g", 0.3m, cat);
        var shake = await _db.AddMenuItemAsync("MB-1", "Chocolate Shake", cat, 250m);
        var latte = await _db.AddMenuItemAsync("MB-2", "Latte", cat, 200m);
        await _db.AddRecipeAsync("Chocolate Shake", shake.Id, 1, "Portion", true,
            (milk.Id, 200m, "ml"), (sugar.Id, 10m, "g"));

        _db.AddBatch(milk.Id, "B1", 5000, 0.5m, DateTime.Today, DateTime.Today.AddDays(30));
        // sugar has no batch -> low stock (reorder 0, sellable 0)
        await Db.SaveChangesAsync();

        var overview = await MilkBar.GetOverviewAsync();

        Assert.True(overview.TotalMenuItems >= 2);      // two test items (plus any seeded prepared products)
        Assert.Equal(1, overview.ActiveRecipes);
        Assert.Equal(2, overview.TotalIngredientsUsed); // milk + sugar
        Assert.Equal(1, overview.LowStockIngredients);  // sugar
        Assert.Contains(overview.Alerts, a => a.Type == "Low ingredient stock");
    }

    [Fact]
    public async Task MenuItems_IncludeCostAndMargin()
    {
        var cat = (await _db.AddCategoryAsync("Test Milkshakes")).Id;
        var milk = await _db.AddIngredientAsync("ING-MILK", "Fresh Milk", "ml", 0.5m, cat);
        var shake = await _db.AddMenuItemAsync("MB-1", "Chocolate Shake", cat, 250m);
        await _db.AddMenuItemAsync("MB-2", "Latte", cat, 200m);
        await _db.AddRecipeAsync("Chocolate Shake", shake.Id, 1, "Portion", true, (milk.Id, 200m, "ml"));
        await Db.SaveChangesAsync();

        var page = await MilkBar.GetMenuItemsAsync(new MenuFilter { CategoryId = cat });

        Assert.Equal(2, page.TotalCount);
        var withRecipe = page.Items.Single(i => i.ProductId == shake.Id);
        Assert.True(withRecipe.HasActiveRecipe);
        Assert.Equal(100m, withRecipe.EstimatedCost);
        Assert.Equal(60m, Math.Round(withRecipe.EstimatedMarginPercent, 3));
    }

    [Fact]
    public async Task Ingredients_ReportSellableStock()
    {
        var cat = (await _db.AddCategoryAsync("Test Milkshakes")).Id;
        var milk = await _db.AddIngredientAsync("ING-MILK", "Fresh Milk", "ml", 0.5m, cat);
        var shake = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 250m);
        await _db.AddRecipeAsync("Shake", shake.Id, 1, "Portion", true, (milk.Id, 200m, "ml"));
        _db.AddBatch(milk.Id, "B1", 5000, 0.5m, DateTime.Today, DateTime.Today.AddDays(30));
        _db.AddBatch(milk.Id, "B2", 1000, 0.5m, DateTime.Today, DateTime.Today.AddDays(-1)); // expired
        await Db.SaveChangesAsync();

        var page = await MilkBar.GetIngredientsAsync(new IngredientFilter());

        var row = page.Items.Single(i => i.ProductId == milk.Id);
        Assert.Equal(5000m, row.SellableStock); // expired batch excluded
        Assert.Equal(StockStatus.InStock, row.Status);
        Assert.Equal(1, row.ActiveRecipeCount);
    }
}
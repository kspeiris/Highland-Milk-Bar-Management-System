using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class RecipeCostingTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private RecipeService Recipes => new(Db);
    private InventoryService Inventory => new(Db);
    private SalesService Sales => new(Db, Inventory, new LoyaltyService(Db));

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<int> CategoryAsync() => (await _db.AddCategoryAsync("Test Milkshakes")).Id;

    /// <summary>Builds the classic Chocolate Milkshake recipe (cost 151 at the example prices).</summary>
    private async Task<(int MenuId, int RecipeId, Product Milk)> ChocolateRecipeAsync(int cat, decimal milkCost = 0.25m, decimal price = 250m)
    {
        var milk = await _db.AddIngredientAsync("ING-MILK", "Fresh Milk", "ml", milkCost, cat);
        var iceCream = await _db.AddIngredientAsync("ING-ICE", "Vanilla Ice Cream", "scoop", 35m, cat);
        var syrup = await _db.AddIngredientAsync("ING-SYRUP", "Chocolate Syrup", "ml", 0.8m, cat);
        var sugar = await _db.AddIngredientAsync("ING-SUGAR", "Sugar", "g", 0.3m, cat);
        var cup = await _db.AddIngredientAsync("ING-CUP", "Milkshake Cup", "pcs", 12m, cat);
        var menu = await _db.AddMenuItemAsync("MB-TEST-MS", "Chocolate Milkshake", cat, price);

        var recipe = await _db.AddRecipeAsync("Chocolate Milkshake", menu.Id, 1, "Portion", true,
            (milk.Id, 200m, "ml"),
            (iceCream.Id, 2m, "scoop"),
            (syrup.Id, 20m, "ml"),
            (sugar.Id, 10m, "g"),
            (cup.Id, 1m, "pcs"));

        return (menu.Id, recipe.Id, milk);
    }

    [Fact]
    public async Task TotalCost_MatchesSumOfLineCosts()
    {
        var cat = await CategoryAsync();
        var (_, recipeId, _) = await ChocolateRecipeAsync(cat);

        var cost = await Recipes.CalculateCostAsync(recipeId, 1);

        Assert.Equal(151m, cost.TotalCost);
        Assert.Equal(151m, cost.Lines.Sum(l => l.LineCost));
        Assert.Equal(151m, cost.CostPerServing);
    }

    [Fact]
    public async Task CostPerServing_ScalesWithServings()
    {
        var cat = await CategoryAsync();
        var (_, recipeId, _) = await ChocolateRecipeAsync(cat);

        var cost = await Recipes.CalculateCostAsync(recipeId, 2);

        Assert.Equal(302m, cost.TotalCost);
        Assert.Equal(151m, cost.CostPerServing);
    }

    [Fact]
    public async Task GrossProfit_And_Margin_AreCorrect()
    {
        var cat = await CategoryAsync();
        var (_, recipeId, _) = await ChocolateRecipeAsync(cat, price: 250m);

        var cost = await Recipes.CalculateCostAsync(recipeId, 1);

        Assert.Equal(250m, cost.SellingPrice);
        Assert.Equal(99m, cost.GrossProfit);
        Assert.Equal(39.6m, Math.Round(cost.MarginPercent, 3));
    }

    [Fact]
    public async Task ZeroSellingPrice_MarginIsZeroWithoutDivideByZero()
    {
        var cat = await CategoryAsync();
        var (_, recipeId, _) = await ChocolateRecipeAsync(cat, price: 0m);

        var cost = await Recipes.CalculateCostAsync(recipeId, 1);

        Assert.Equal(0m, cost.MarginPercent);
        Assert.Equal(-151m, cost.GrossProfit);
    }

    [Fact]
    public async Task CostUpdates_WhenIngredientCostChanges()
    {
        var cat = await CategoryAsync();
        var (_, recipeId, milk) = await ChocolateRecipeAsync(cat, milkCost: 0.25m);

        var before = await Recipes.CalculateCostAsync(recipeId, 1);
        Assert.Equal(151m, before.TotalCost);

        milk.CostPrice = 0.5m; // +0.25 * 200ml = +50
        await Db.SaveChangesAsync();

        var after = await Recipes.CalculateCostAsync(recipeId, 1);
        Assert.Equal(201m, after.TotalCost);
    }

    [Fact]
    public async Task CompletedSaleCogs_IsPreservedAfterRecipeEdit()
    {
        var cat = await CategoryAsync();
        var (menuId, recipeId, _) = await ChocolateRecipeAsync(cat);

        // Give every ingredient real batch stock so the recipe can be consumed.
        var ingredients = await Db.Products.Where(p => p.Type == ProductType.Ingredient).ToListAsync();
        foreach (var ing in ingredients)
            _db.AddBatch(ing.Id, "B-" + ing.Sku, 100000, ing.CostPrice, DateTime.Today, DateTime.Today.AddDays(60));
        await Db.SaveChangesAsync();

        var sale = await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = menuId, Quantity = 1, UnitPrice = 250m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 250m } }
        });
        var originalCogs = (await Db.Sales.FindAsync(sale.SaleId))!.CostOfGoodsSold;
        Assert.Equal(151m, originalCogs);

        // Edit the recipe to require more milk.
        var current = await Db.Recipes.Include(r => r.Items).FirstAsync(r => r.Id == recipeId);
        await Recipes.UpdateAsync(new UpdateRecipeRequest
        {
            Id = recipeId,
            Name = current.Name,
            MenuProductId = current.MenuProductId,
            YieldQuantity = 1,
            YieldUnit = "Portion",
            IsActive = true,
            Items = current.Items.Select(i => new RecipeLineRequest { IngredientProductId = i.IngredientProductId, Quantity = i.QuantityRequired + 100m, Unit = i.Unit }).ToList()
        });

        var saleAfter = await Db.Sales.AsNoTracking().FirstAsync(s => s.Id == sale.SaleId);
        Assert.Equal(originalCogs, saleAfter.CostOfGoodsSold);
        var item = await Db.SaleItems.AsNoTracking().FirstAsync(i => i.SaleId == sale.SaleId);
        Assert.Equal(151m, item.UnitCostSnapshot * item.Quantity);
    }
}
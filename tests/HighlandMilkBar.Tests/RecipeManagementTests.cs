using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class RecipeManagementTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private RecipeService Recipes => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<int> CategoryAsync(string name = "Test Milkshakes")
    {
        var c = await _db.AddCategoryAsync(name);
        return c.Id;
    }

    [Fact]
    public async Task Create_ValidRecipe_PersistsWithItems()
    {
        var cat = await CategoryAsync();
        var menu = await _db.AddMenuItemAsync("MB-1", "Chocolate Milkshake", cat, 250m);
        var milk = await _db.AddIngredientAsync("ING-MILK", "Fresh Milk", "ml", 0.25m, cat);
        var syrup = await _db.AddIngredientAsync("ING-SYRUP", "Chocolate Syrup", "ml", 0.8m, cat);

        var result = await Recipes.CreateAsync(new CreateRecipeRequest
        {
            Name = "Chocolate Milkshake",
            MenuProductId = menu.Id,
            YieldQuantity = 1,
            YieldUnit = "Portion",
            IsActive = true,
            Items = new List<RecipeLineRequest>
            {
                new() { IngredientProductId = milk.Id, Quantity = 200, Unit = "ml" },
                new() { IngredientProductId = syrup.Id, Quantity = 20, Unit = "ml" }
            }
        });

        Assert.Equal("Chocolate Milkshake", result.Name);
        Assert.Equal(2, result.ItemCount);
        Assert.True(result.IsActive);
        Assert.Equal(2, await Db.RecipeItems.CountAsync());
    }

    [Fact]
    public async Task Update_ReplacesItems()
    {
        var cat = await CategoryAsync();
        var menu = await _db.AddMenuItemAsync("MB-1", "Latte", cat, 200m);
        var milk = await _db.AddIngredientAsync("ING-MILK", "Milk", "ml", 0.25m, cat);
        var sugar = await _db.AddIngredientAsync("ING-SUGAR", "Sugar", "g", 0.3m, cat);

        var recipe = await _db.AddRecipeAsync("Latte", menu.Id, 1, "Portion", true, (milk.Id, 150m, "ml"));

        var updated = await Recipes.UpdateAsync(new UpdateRecipeRequest
        {
            Id = recipe.Id,
            Name = "Latte",
            MenuProductId = menu.Id,
            YieldQuantity = 1,
            YieldUnit = "Portion",
            IsActive = true,
            Items = new List<RecipeLineRequest>
            {
                new() { IngredientProductId = milk.Id, Quantity = 180, Unit = "ml" },
                new() { IngredientProductId = sugar.Id, Quantity = 10, Unit = "g" }
            }
        });

        Assert.Equal(2, updated.ItemCount);
        var items = await Db.RecipeItems.Where(i => i.RecipeId == recipe.Id).ToListAsync();
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Create_SecondActiveRecipe_DeactivatesFirst()
    {
        var cat = await CategoryAsync();
        var menu = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 200m);
        var milk = await _db.AddIngredientAsync("ING-MILK", "Milk", "ml", 0.25m, cat);

        var first = await _db.AddRecipeAsync("Shake v1", menu.Id, 1, "Portion", true, (milk.Id, 200m, "ml"));
        var second = await Recipes.CreateAsync(new CreateRecipeRequest
        {
            Name = "Shake v2",
            MenuProductId = menu.Id,
            YieldQuantity = 1,
            YieldUnit = "Portion",
            IsActive = true,
            Items = new List<RecipeLineRequest> { new() { IngredientProductId = milk.Id, Quantity = 220, Unit = "ml" } }
        });

        Assert.True(second.IsActive);
        var firstReloaded = await Db.Recipes.FindAsync(first.Id);
        Assert.False(firstReloaded!.IsActive);
        Assert.Equal(1, await Db.Recipes.CountAsync(r => r.MenuProductId == menu.Id && r.IsActive));
    }

    [Fact]
    public async Task SetActive_DeactivatesOthers()
    {
        var cat = await CategoryAsync();
        var menu = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 200m);
        var milk = await _db.AddIngredientAsync("ING-MILK", "Milk", "ml", 0.25m, cat);
        var a = await _db.AddRecipeAsync("A", menu.Id, 1, "Portion", true, (milk.Id, 200m, "ml"));
        var b = await _db.AddRecipeAsync("B", menu.Id, 1, "Portion", false, (milk.Id, 210m, "ml"));

        await Recipes.SetActiveAsync(b.Id, true);

        Assert.False((await Db.Recipes.FindAsync(a.Id))!.IsActive);
        Assert.True((await Db.Recipes.FindAsync(b.Id))!.IsActive);
    }

    [Fact]
    public async Task Create_RejectsEmptyRecipe()
    {
        var cat = await CategoryAsync();
        var menu = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 200m);

        await Assert.ThrowsAsync<ArgumentException>(() => Recipes.CreateAsync(new CreateRecipeRequest
        {
            Name = "Empty",
            MenuProductId = menu.Id,
            Items = new List<RecipeLineRequest>()
        }));
    }

    [Fact]
    public async Task Create_RejectsInvalidYield()
    {
        var cat = await CategoryAsync();
        var menu = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 200m);
        var milk = await _db.AddIngredientAsync("ING-MILK", "Milk", "ml", 0.25m, cat);

        await Assert.ThrowsAsync<ArgumentException>(() => Recipes.CreateAsync(new CreateRecipeRequest
        {
            Name = "Bad",
            MenuProductId = menu.Id,
            YieldQuantity = 0,
            Items = new List<RecipeLineRequest> { new() { IngredientProductId = milk.Id, Quantity = 100, Unit = "ml" } }
        }));
    }

    [Fact]
    public async Task Create_RejectsInactiveIngredient()
    {
        var cat = await CategoryAsync();
        var menu = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 200m);
        var milk = await _db.AddIngredientAsync("ING-MILK", "Milk", "ml", 0.25m, cat, active: false);

        await Assert.ThrowsAsync<ArgumentException>(() => Recipes.CreateAsync(new CreateRecipeRequest
        {
            Name = "Bad",
            MenuProductId = menu.Id,
            Items = new List<RecipeLineRequest> { new() { IngredientProductId = milk.Id, Quantity = 100, Unit = "ml" } }
        }));
    }

    [Fact]
    public async Task Create_RejectsNonInventoryIngredient()
    {
        var cat = await CategoryAsync();
        var menu = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 200m);
        var notTracked = await _db.AddProductAsync("PKG-1", "Bottled Water", cat, type: Domain.Enums.ProductType.Packaged, track: false);

        await Assert.ThrowsAsync<ArgumentException>(() => Recipes.CreateAsync(new CreateRecipeRequest
        {
            Name = "Bad",
            MenuProductId = menu.Id,
            Items = new List<RecipeLineRequest> { new() { IngredientProductId = notTracked.Id, Quantity = 1, Unit = "pcs" } }
        }));
    }

    [Fact]
    public async Task Create_RejectsDuplicateIngredient()
    {
        var cat = await CategoryAsync();
        var menu = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 200m);
        var milk = await _db.AddIngredientAsync("ING-MILK", "Milk", "ml", 0.25m, cat);

        await Assert.ThrowsAsync<ArgumentException>(() => Recipes.CreateAsync(new CreateRecipeRequest
        {
            Name = "Dup",
            MenuProductId = menu.Id,
            Items = new List<RecipeLineRequest>
            {
                new() { IngredientProductId = milk.Id, Quantity = 100, Unit = "ml" },
                new() { IngredientProductId = milk.Id, Quantity = 50, Unit = "ml" }
            }
        }));
    }

    [Fact]
    public async Task Create_RejectsIncompatibleUnit()
    {
        var cat = await CategoryAsync();
        var menu = await _db.AddMenuItemAsync("MB-1", "Shake", cat, 200m);
        var milk = await _db.AddIngredientAsync("ING-MILK", "Milk", "ml", 0.25m, cat);

        await Assert.ThrowsAsync<ArgumentException>(() => Recipes.CreateAsync(new CreateRecipeRequest
        {
            Name = "Bad unit",
            MenuProductId = menu.Id,
            Items = new List<RecipeLineRequest> { new() { IngredientProductId = milk.Id, Quantity = 100, Unit = "g" } }
        }));
    }
}
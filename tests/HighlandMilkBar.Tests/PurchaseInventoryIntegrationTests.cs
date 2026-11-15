using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

/// <summary>
/// Verifies the three critical Step 08 integrations: received purchase → inventory batches,
/// and received ingredient cost → RecipeService weighted-average costing. No separate stock system.
/// </summary>
public sealed class PurchaseInventoryIntegrationTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private PurchaseService Purchases => new(Db, new InventoryService(Db));
    private SupplierService Suppliers => new(Db);
    private RecipeService Recipes => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task ReceivedPurchase_AppearsInInventoryStock()
    {
        var cat = await _db.AddCategoryAsync("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat.Id);
        var supplier = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Dairy" });
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 100, UnitCost = 180, BatchNumber = "FM-101", ExpiryDate = DateTime.Today.AddDays(10) }]
        });

        await Purchases.ReceiveAsync(purchase.Id);

        var stock = (await Db.Products.FindAsync(product.Id))!.CurrentStock;
        Assert.Equal(100m, stock);
        var batch = await Db.InventoryBatches.SingleAsync();
        Assert.Equal("FM-101", batch.BatchNumber);
        Assert.Equal(180m, batch.UnitCost);
    }

    [Fact]
    public async Task ReceivedIngredient_DrivesRecipeCosting()
    {
        var cat = await _db.AddCategoryAsync("Cat");
        // Ingredient priced at 0 to prove the batch (purchase) cost is what the recipe uses.
        var ingredient = await _db.AddIngredientAsync("ING-MLK", "Fresh Milk (ingredient)", "L", 0m, cat.Id);
        var menu = await _db.AddMenuItemAsync("MB-MS-1", "Milkshake", cat.Id, 500m);
        var recipe = await _db.AddRecipeAsync("Milkshake", menu.Id, 1m, "Glass", true, (ingredient.Id, 0.2m, "L"));

        var supplier = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Dairy" });
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = ingredient.Id, Quantity = 100, Unit = "L", UnitCost = 200m, BatchNumber = "MLK-1" }]
        });
        await Purchases.ReceiveAsync(purchase.Id);

        var breakdown = await Recipes.CalculateCostAsync(recipe.Id, 1m);
        var line = Assert.Single(breakdown.Lines);
        Assert.Equal(200m, line.UnitCost);   // weighted-average now reflects the received batch
        Assert.Equal(40m, line.LineCost);    // 0.2 L × 200
    }

    [Fact]
    public async Task RestartConsistency_NoDuplicateStockAfterReinitialise()
    {
        var cat = await _db.AddCategoryAsync("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat.Id);
        var supplier = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Dairy" });
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 100, UnitCost = 180, BatchNumber = "FM-1" }]
        });
        await Purchases.ReceiveAsync(purchase.Id);

        // Simulate an application restart re-running schema init on the same file.
        await DbInitializer.InitializeAsync(Db);

        var stock = (await Db.Products.FindAsync(product.Id))!.CurrentStock;
        Assert.Equal(100m, stock);
        Assert.Single(await Db.InventoryBatches.ToListAsync());
    }
}
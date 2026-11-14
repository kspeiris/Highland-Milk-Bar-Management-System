using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class DatabaseFoundationTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Initialization_SeedsReferenceData()
    {
        Assert.True(await Db.Categories.AnyAsync());
        Assert.True(await Db.Units.AnyAsync());
        Assert.True(await Db.ExpenseCategories.AnyAsync());
        Assert.True(await Db.AppSettings.AnyAsync(s => s.Key == "currency.code"));
    }

    [Fact]
    public async Task UniqueConstraints_AreEnforced()
    {
        var category = await _db.AddCategoryAsync("Test Unique Category");
        await _db.AddProductAsync("SKU-1", "Cola", category.Id);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => _db.AddProductAsync("SKU-1", "Cola 500ml", category.Id));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => _db.AddCategoryAsync("Test Unique Category"));
    }

    [Fact]
    public async Task ReceiveStock_CreatesBatchMovementAndUpdatesStock()
    {
        var category = await _db.AddCategoryAsync("Test Fresh Milk");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk 1L", category.Id, stock: 0m);
        var inventory = new InventoryService(Db);

        var batch = await inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = product.Id,
            BatchNumber = "B-001",
            Quantity = 50m,
            UnitCost = 10m
        });

        Assert.Equal(50m, batch.QuantityRemaining);
        Assert.Equal(50m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.True(await Db.StockMovements.AnyAsync(m =>
            m.ProductId == product.Id && m.MovementType == StockMovementType.Opening));
    }

    [Fact]
    public async Task FifoAllocation_ConsumesOldestReceivedFirst()
    {
        var category = await _db.AddCategoryAsync("Test Fresh Milk");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk 1L", category.Id, stock: 0m);

        _db.AddBatch(product.Id, "B1", 50m, 10m, DateTime.Today.AddDays(-2), DateTime.Today.AddDays(5));
        _db.AddBatch(product.Id, "B2", 40m, 12m, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(8));
        product.CurrentStock = 90m;
        await Db.SaveChangesAsync();

        var inventory = new InventoryService(Db);
        var result = await inventory.AllocateAsync(
            product.Id, 60m, StockMovementType.Sale, MovementReferenceType.Sale, null, null, AllocationPolicy.Fifo);

        Assert.Equal(60m, result.QuantityAllocated);
        Assert.Equal(0m, (await Batch("B1")).QuantityRemaining);
        Assert.Equal(30m, (await Batch("B2")).QuantityRemaining);
        Assert.Equal(30m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
    }

    [Fact]
    public async Task FefoAllocation_ConsumesEarliestExpiryFirst()
    {
        var category = await _db.AddCategoryAsync("Test Fresh Milk");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk 1L", category.Id, stock: 0m);

        // B1 was received earlier but expires later; B2 expires sooner and must be consumed first.
        _db.AddBatch(product.Id, "B1", 50m, 10m, DateTime.Today.AddDays(-2), DateTime.Today.AddDays(5));
        _db.AddBatch(product.Id, "B2", 40m, 12m, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(2));
        product.CurrentStock = 90m;
        await Db.SaveChangesAsync();

        var inventory = new InventoryService(Db);
        var result = await inventory.AllocateAsync(
            product.Id, 60m, StockMovementType.Sale, MovementReferenceType.Sale, null, null, AllocationPolicy.Fefo);

        Assert.Equal(60m, result.QuantityAllocated);
        Assert.Equal(30m, (await Batch("B1")).QuantityRemaining);
        Assert.Equal(0m, (await Batch("B2")).QuantityRemaining);
        Assert.Equal(30m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
    }

    [Fact]
    public async Task ExpiredBatch_IsNotAllocated()
    {
        var category = await _db.AddCategoryAsync("Test Fresh Milk");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk 1L", category.Id, stock: 50m);

        _db.AddBatch(product.Id, "OLD", 50m, 10m, DateTime.Today.AddDays(-10), DateTime.Today.AddDays(-1));
        await Db.SaveChangesAsync();

        var inventory = new InventoryService(Db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => inventory.AllocateAsync(
            product.Id, 10m, StockMovementType.Sale, MovementReferenceType.Sale, null, null, AllocationPolicy.Fefo));

        Assert.Equal(50m, (await Batch("OLD")).QuantityRemaining);
        Assert.Equal(50m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
    }

    [Fact]
    public async Task InsufficientStock_Throws()
    {
        var category = await _db.AddCategoryAsync("Test Fresh Milk");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk 1L", category.Id, stock: 5m);
        _db.AddBatch(product.Id, "B1", 5m, 10m, DateTime.Today.AddDays(-1), null);
        await Db.SaveChangesAsync();

        var inventory = new InventoryService(Db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => inventory.AllocateAsync(
            product.Id, 10m, StockMovementType.Sale, MovementReferenceType.Sale, null, null, AllocationPolicy.Fifo));
    }

    [Fact]
    public async Task PurchaseReceiving_CreatesBatchAndMovement()
    {
        var category = await _db.AddCategoryAsync("Test Ingredients");
        var product = await _db.AddProductAsync("ING-1", "Chocolate Syrup", category.Id, stock: 0m, cost: 5m);
        var supplier = new Supplier { Name = "Ceylon Supplies" };
        Db.Suppliers.Add(supplier);
        await Db.SaveChangesAsync();

        var purchase = new Purchase
        {
            InvoiceNumber = "INV-1001",
            SupplierId = supplier.Id,
            PurchaseDate = DateTime.Today,
            Subtotal = 50m,
            TotalAmount = 50m,
            AmountPaid = 50m,
            Status = PurchaseStatus.Draft
        };
        purchase.Items.Add(new PurchaseItem { ProductId = product.Id, Quantity = 10m, UnitCost = 5m, LineTotal = 50m });
        Db.Purchases.Add(purchase);
        await Db.SaveChangesAsync();

        var service = new PurchaseService(Db, new InventoryService(Db));
        var result = await service.ReceiveAsync(purchase.Id);

        Assert.Equal(1, result.BatchesCreated);
        Assert.Equal(10m, result.TotalQuantityReceived);
        Assert.Equal(10m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.True(await Db.StockMovements.AnyAsync(m =>
            m.ProductId == product.Id && m.MovementType == StockMovementType.Purchase));
    }

    [Fact]
    public async Task RecipeConsumption_DeductsIngredientStock()
    {
        var category = await _db.AddCategoryAsync("Test Menu");
        var ingredient = await _db.AddProductAsync("ING-MLK", "Fresh Milk (ingredient)", category.Id,
            type: ProductType.Ingredient, track: true, cost: 0.05m, stock: 0m);
        var menu = await _db.AddProductAsync("MB-MS-1", "Chocolate Milkshake", category.Id,
            type: ProductType.PreparedItem, track: false, cost: 0m, price: 500m);

        var inventory = new InventoryService(Db);
        await inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = ingredient.Id, BatchNumber = "MLK-1", Quantity = 1000m, UnitCost = 0.05m
        });

        var recipe = new Recipe
        {
            MenuProductId = menu.Id,
            Name = "Chocolate Milkshake",
            YieldQuantity = 1m,
            YieldUnit = "Glass",
            IsActive = true
        };
        recipe.Items.Add(new RecipeItem { IngredientProductId = ingredient.Id, QuantityRequired = 200m, Unit = "ml" });
        Db.Recipes.Add(recipe);
        await Db.SaveChangesAsync();

        var sales = new SalesService(Db, inventory);
        var result = await sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest>
            {
                new() { ProductId = menu.Id, Quantity = 1m, UnitPrice = 500m }
            },
            Payments = new List<PaymentRequest>
            {
                new() { Method = PaymentMethod.Cash, Amount = 500m }
            }
        });

        Assert.Equal(800m, (await Db.Products.FindAsync(ingredient.Id))!.CurrentStock);
        Assert.True(await Db.StockMovements.AnyAsync(m =>
            m.ProductId == ingredient.Id && m.MovementType == StockMovementType.RecipeConsumption));
        Assert.Equal(10m, result.CostOfGoodsSold);
    }

    [Fact]
    public async Task Checkout_SupportsSplitPayments()
    {
        var category = await _db.AddCategoryAsync("Packaged");
        var product = await _db.AddProductAsync("P-1", "Yoghurt", category.Id, stock: 10m, price: 100m);
        _db.AddBatch(product.Id, "B1", 10m, 40m, DateTime.Today.AddDays(-1), null);
        await Db.SaveChangesAsync();

        var sales = new SalesService(Db, new InventoryService(Db));
        var result = await sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest>
            {
                new() { ProductId = product.Id, Quantity = 2m, UnitPrice = 100m }
            },
            Payments = new List<PaymentRequest>
            {
                new() { Method = PaymentMethod.Cash, Amount = 120m },
                new() { Method = PaymentMethod.Card, Amount = 80m }
            }
        });

        Assert.Equal(200m, result.TotalAmount);
        Assert.Equal(200m, result.AmountPaid);
        Assert.Equal(0m, result.Change);
        Assert.Equal(2, await Db.Payments.CountAsync(p => p.SaleId == result.SaleId));
        Assert.Equal(80m, result.CostOfGoodsSold);
    }

    [Fact]
    public async Task Checkout_RollsBack_WhenPaymentDoesNotMatchTotal()
    {
        var category = await _db.AddCategoryAsync("Packaged");
        var product = await _db.AddProductAsync("P-1", "Yoghurt", category.Id, stock: 10m, price: 100m);
        _db.AddBatch(product.Id, "B1", 10m, 40m, DateTime.Today.AddDays(-1), null);
        await Db.SaveChangesAsync();

        var sales = new SalesService(Db, new InventoryService(Db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest>
            {
                new() { ProductId = product.Id, Quantity = 2m, UnitPrice = 100m }
            },
            Payments = new List<PaymentRequest>
            {
                new() { Method = PaymentMethod.Cash, Amount = 100m }
            }
        }));

        Assert.Equal(0, await Db.Sales.CountAsync());
        Assert.Equal(10m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
    }

    [Fact]
    public async Task Checkout_RollsBack_WhenStockIsInsufficient()
    {
        var category = await _db.AddCategoryAsync("Packaged");
        var product = await _db.AddProductAsync("P-1", "Yoghurt", category.Id, stock: 5m, price: 100m);
        _db.AddBatch(product.Id, "B1", 5m, 40m, DateTime.Today.AddDays(-1), null);
        await Db.SaveChangesAsync();

        var sales = new SalesService(Db, new InventoryService(Db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest>
            {
                new() { ProductId = product.Id, Quantity = 10m, UnitPrice = 100m }
            },
            Payments = new List<PaymentRequest>
            {
                new() { Method = PaymentMethod.Cash, Amount = 1000m }
            }
        }));

        Db.ChangeTracker.Clear();
        Assert.Equal(0, await Db.Sales.CountAsync());
        Assert.Equal(5m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.Equal(5m, (await Db.InventoryBatches.SingleAsync(b => b.BatchNumber == "B1")).QuantityRemaining);
    }

    [Fact]
    public async Task SalesReturn_WithRestock_IncreasesStock()
    {
        var category = await _db.AddCategoryAsync("Packaged");
        var product = await _db.AddProductAsync("P-1", "Yoghurt", category.Id, stock: 10m, price: 100m);
        _db.AddBatch(product.Id, "B1", 10m, 40m, DateTime.Today.AddDays(-1), null);
        await Db.SaveChangesAsync();

        var sales = new SalesService(Db, new InventoryService(Db));
        var sale = await sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest>
            {
                new() { ProductId = product.Id, Quantity = 3m, UnitPrice = 100m }
            },
            Payments = new List<PaymentRequest>
            {
                new() { Method = PaymentMethod.Cash, Amount = 300m }
            }
        });
        Assert.Equal(7m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);

        var saleItem = await Db.SaleItems.SingleAsync(i => i.SaleId == sale.SaleId);
        var returnResult = await sales.ProcessReturnAsync(new SalesReturnRequest
        {
            SaleId = sale.SaleId,
            Reason = "Customer changed mind",
            Lines = new List<ReturnLineRequest>
            {
                new() { SaleItemId = saleItem.Id, Quantity = 3m, Condition = ReturnCondition.Restock }
            }
        });

        Assert.True(returnResult.Restocked);
        Assert.Equal(300m, returnResult.TotalAmount);
        Assert.Equal(10m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.Equal(SaleStatus.Returned, (await Db.Sales.FindAsync(sale.SaleId))!.Status);
    }

    [Fact]
    public async Task BackupAndRestore_RoundTripsData()
    {
        var category = await _db.AddCategoryAsync("BackupCategory");
        var backup = new BackupService(Db);
        var folder = Path.Combine(Path.GetTempPath(), $"hmb-backup-{Guid.NewGuid():N}");

        try
        {
            var result = await backup.CreateBackupAsync(folder);
            Assert.True(File.Exists(result.Path));

            Db.Categories.Remove(category);
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            Assert.False(await Db.Categories.AnyAsync(c => c.Name == "BackupCategory"));

            await backup.RestoreAsync(result.Path);
            Db.ChangeTracker.Clear();
            Assert.True(await Db.Categories.AnyAsync(c => c.Name == "BackupCategory"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private async Task<InventoryBatch> Batch(string batchNumber)
    {
        return await Db.InventoryBatches.SingleAsync(b => b.BatchNumber == batchNumber);
    }
}
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class SalesServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private InventoryService Inventory => new(Db);
    private SalesService Sales => new(Db, Inventory, new LoyaltyService(Db));

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<int> CategoryAsync(string name = "Cat")
    {
        var c = await _db.AddCategoryAsync(name);
        return c.Id;
    }

    private async Task<Product> ProductAsync(
        int categoryId,
        string sku = "P-1",
        string name = "Fresh Milk",
        decimal price = 20m,
        decimal cost = 10m,
        bool track = true,
        ProductType type = ProductType.Packaged,
        bool active = true)
    {
        var p = await _db.AddProductAsync(sku, name, categoryId, type: type, track: track, cost: cost, price: price);
        p.IsActive = active;
        await Db.SaveChangesAsync();
        return p;
    }

    private async Task StockAsync(int productId, string batch, decimal qty, decimal unitCost,
        DateTime? received = null, DateTime? expiry = null)
    {
        await Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = productId,
            BatchNumber = batch,
            Quantity = qty,
            UnitCost = unitCost,
            ReceivedDate = received ?? DateTime.Today,
            ExpiryDate = expiry ?? DateTime.Today.AddDays(30)
        });
    }

    private static SaleCheckoutRequest CashSale(params (int ProductId, decimal Qty, decimal Price)[] lines)
        => new()
        {
            Items = lines.Select(l => new SaleLineRequest { ProductId = l.ProductId, Quantity = l.Qty, UnitPrice = l.Price }).ToList(),
            Payments = new List<PaymentRequest>
            {
                new() { Method = PaymentMethod.Cash, Amount = lines.Sum(l => l.Qty * l.Price) }
            }
        };

    // ---- Product sales ----

    [Fact]
    public async Task Checkout_PackagedSale_DeductsStockAndRecordsMovement()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat);
        await StockAsync(p.Id, "B1", 10m, 100m);

        var result = await Sales.CheckoutAsync(CashSale((p.Id, 3m, 20m)));

        Assert.Equal(60m, result.TotalAmount);
        Assert.Equal(0m, result.Change);
        Assert.Equal(7m, (await Db.Products.FindAsync(p.Id))!.CurrentStock);
        Assert.True(await Db.StockMovements.AnyAsync(m => m.MovementType == StockMovementType.Sale && m.ProductId == p.Id));
    }

    [Fact]
    public async Task Checkout_MultipleItems_CalculatesTotal()
    {
        var cat = await CategoryAsync();
        var a = await ProductAsync(cat, "A", "Milk", price: 250m);
        var b = await ProductAsync(cat, "B", "Yoghurt", price: 120m);
        await StockAsync(a.Id, "BA", 10m, 100m);
        await StockAsync(b.Id, "BB", 10m, 50m);

        var req = new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest>
            {
                new() { ProductId = a.Id, Quantity = 2m, UnitPrice = 250m },
                new() { ProductId = b.Id, Quantity = 3m, UnitPrice = 120m }
            },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 860m } }
        };

        var result = await Sales.CheckoutAsync(req);
        Assert.Equal(860m, result.TotalAmount);
        Assert.Equal(8m, (await Db.Products.FindAsync(a.Id))!.CurrentStock);
        Assert.Equal(7m, (await Db.Products.FindAsync(b.Id))!.CurrentStock);
    }

    [Fact]
    public async Task Checkout_RejectsInactiveProduct()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat, active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.CheckoutAsync(CashSale((p.Id, 1m, 20m))));
    }

    [Fact]
    public async Task Checkout_RejectsUnknownProduct()
    {
        var cat = await CategoryAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.CheckoutAsync(CashSale((9999, 1m, 20m))));
    }

    [Fact]
    public async Task Checkout_RejectsInsufficientStock_AndPersistsNothing()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat);
        await StockAsync(p.Id, "B1", 2m, 100m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.CheckoutAsync(CashSale((p.Id, 5m, 20m))));

        Assert.Equal(0, await Db.Sales.CountAsync());
        Assert.Equal(2m, (await Db.Products.FindAsync(p.Id))!.CurrentStock);
    }

    [Fact]
    public async Task Checkout_RejectsExpiredOnlyStock()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat);
        // Insert an already-expired batch directly (bypasses receipt validation).
        _db.AddBatch(p.Id, "OLD", 10m, 100m, DateTime.Today.AddDays(-10), DateTime.Today.AddDays(-1));
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.CheckoutAsync(CashSale((p.Id, 1m, 20m))));
    }

    [Fact]
    public async Task Checkout_Fefo_ConsumesEarliestExpiryFirst()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat);
        _db.AddBatch(p.Id, "LATE", 10m, 100m, DateTime.Today.AddDays(-5), DateTime.Today.AddDays(60));
        _db.AddBatch(p.Id, "EARLY", 10m, 100m, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(10));
        await Db.SaveChangesAsync();

        await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = p.Id, Quantity = 5m, UnitPrice = 20m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 100m } },
            AllocationPolicy = AllocationPolicy.Fefo
        });

        Assert.Equal(5m, (await Db.InventoryBatches.SingleAsync(b => b.BatchNumber == "EARLY"))!.QuantityRemaining);
        Assert.Equal(10m, (await Db.InventoryBatches.SingleAsync(b => b.BatchNumber == "LATE"))!.QuantityRemaining);
    }

    [Fact]
    public async Task Checkout_Fifo_ConsumesOldestReceivedFirst()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat);
        _db.AddBatch(p.Id, "EARLYEXP", 10m, 100m, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(10));
        _db.AddBatch(p.Id, "OLDREC", 10m, 100m, DateTime.Today.AddDays(-5), DateTime.Today.AddDays(60));
        await Db.SaveChangesAsync();

        await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = p.Id, Quantity = 5m, UnitPrice = 20m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 100m } },
            AllocationPolicy = AllocationPolicy.Fifo
        });

        Assert.Equal(5m, (await Db.InventoryBatches.SingleAsync(b => b.BatchNumber == "OLDREC"))!.QuantityRemaining);
        Assert.Equal(10m, (await Db.InventoryBatches.SingleAsync(b => b.BatchNumber == "EARLYEXP"))!.QuantityRemaining);
    }

    [Fact]
    public async Task Checkout_UniqueInvoiceNumbers()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat);
        await StockAsync(p.Id, "B1", 10m, 100m);

        var r1 = await Sales.CheckoutAsync(CashSale((p.Id, 1m, 20m)));
        var r2 = await Sales.CheckoutAsync(CashSale((p.Id, 1m, 20m)));

        Assert.NotEqual(r1.ReceiptNumber, r2.ReceiptNumber);
    }

    // ---- Recipe-based sales ----

    private async Task<(Product Menu, Product Ingredient)> RecipeSetupAsync(int servings, decimal ingredientRequired, decimal ingredientStock)
    {
        var cat = await CategoryAsync();
        var ingredient = await ProductAsync(cat, "ING", "Fresh Milk (ingredient)", price: 0m, cost: 5m, track: true);
        var menu = await ProductAsync(cat, "MENU", "Chocolate Milkshake", price: 350m, cost: 0m, track: false, type: ProductType.PreparedItem);

        if (ingredientStock > 0)
            await StockAsync(ingredient.Id, "IB", ingredientStock, 5m);

        var recipe = new Recipe
        {
            MenuProductId = menu.Id,
            Name = "Chocolate Milkshake v1",
            YieldQuantity = 1,
            Version = 1,
            IsActive = true,
            Items = new List<RecipeItem>
            {
                new() { IngredientProductId = ingredient.Id, QuantityRequired = ingredientRequired, Unit = "ml" }
            }
        };
        Db.Recipes.Add(recipe);
        await Db.SaveChangesAsync();
        return (menu, ingredient);
    }

    [Fact]
    public async Task Checkout_RecipeSale_DeductsIngredients()
    {
        var (menu, ingredient) = await RecipeSetupAsync(servings: 1, ingredientRequired: 200m, ingredientStock: 1000m);

        var result = await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = menu.Id, Quantity = 2m, UnitPrice = 350m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 700m } }
        });

        Assert.Equal(700m, result.TotalAmount);
        Assert.Equal(600m, (await Db.Products.FindAsync(ingredient.Id))!.CurrentStock); // 1000 - 2*200
        Assert.True(await Db.StockMovements.AnyAsync(m => m.MovementType == StockMovementType.RecipeConsumption));
    }

    [Fact]
    public async Task Checkout_RecipeSale_RejectsInsufficientIngredient_AndPersistsNothing()
    {
        var (menu, _) = await RecipeSetupAsync(servings: 1, ingredientRequired: 200m, ingredientStock: 100m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = menu.Id, Quantity = 1m, UnitPrice = 350m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 350m } }
        }));

        Assert.Equal(0, await Db.Sales.CountAsync());
    }

    [Fact]
    public async Task Checkout_PreparedItemWithoutRecipe_IsRejected()
    {
        var cat = await CategoryAsync();
        var menu = await ProductAsync(cat, "NOREC", "Ice Cream Cup", price: 300m, track: false, type: ProductType.PreparedItem);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.CheckoutAsync(CashSale((menu.Id, 1m, 300m))));
        Assert.Equal(0, await Db.Sales.CountAsync());
    }

    // ---- Cart & discounts ----

    [Fact]
    public async Task Checkout_LineAndOrderDiscount_CalculatesCorrectly()
    {
        var cat = await CategoryAsync();
        var a = await ProductAsync(cat, "A", "Milk", price: 250m);
        var b = await ProductAsync(cat, "B", "Yoghurt", price: 120m);
        await StockAsync(a.Id, "BA", 10m, 100m);
        await StockAsync(b.Id, "BB", 10m, 50m);

        var req = new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest>
            {
                new() { ProductId = a.Id, Quantity = 2m, UnitPrice = 250m, DiscountAmount = 50m },
                new() { ProductId = b.Id, Quantity = 3m, UnitPrice = 120m, DiscountAmount = 0m }
            },
            OrderDiscount = 50m,
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 760m } }
        };

        var result = await Sales.CheckoutAsync(req);
        Assert.Equal(860m, result.Subtotal);       // gross: 500 + 360
        Assert.Equal(100m, result.DiscountAmount); // 50 line + 50 order
        Assert.Equal(760m, result.TotalAmount);
    }

    [Fact]
    public async Task Checkout_RejectsNegativeAndExcessiveDiscounts()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat, price: 100m);
        await StockAsync(p.Id, "B", 10m, 50m);

        await Assert.ThrowsAsync<ArgumentException>(() => Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = p.Id, Quantity = 1m, UnitPrice = 100m, DiscountAmount = 150m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 0m } }
        }));

        await Assert.ThrowsAsync<ArgumentException>(() => Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = p.Id, Quantity = 1m, UnitPrice = 100m } },
            OrderDiscount = 200m,
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 100m } }
        }));
    }

    // ---- Payments ----

    [Fact]
    public async Task Checkout_CashPayment_ComputesChange()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat, price: 20m);
        await StockAsync(p.Id, "B", 10m, 10m);

        var result = await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = p.Id, Quantity = 1m, UnitPrice = 20m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 50m } }
        });

        Assert.Equal(20m, result.TotalAmount);
        Assert.Equal(30m, result.Change);
    }

    [Fact]
    public async Task Checkout_CardPayment_HasNoChange()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat, price: 20m);
        await StockAsync(p.Id, "B", 10m, 10m);

        var result = await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = p.Id, Quantity = 1m, UnitPrice = 20m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Card, Amount = 20m } }
        });

        Assert.Equal(0m, result.Change);
    }

    [Fact]
    public async Task Checkout_SplitPayment_RecordsBoth()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat, price: 20m);
        await StockAsync(p.Id, "B", 10m, 10m);

        var result = await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = p.Id, Quantity = 1m, UnitPrice = 20m } },
            Payments = new List<PaymentRequest>
            {
                new() { Method = PaymentMethod.Cash, Amount = 5m },
                new() { Method = PaymentMethod.Card, Amount = 15m }
            }
        });

        Assert.Equal(0m, result.Change);
        Assert.Equal(2, await Db.Payments.CountAsync(p => p.SaleId == result.SaleId));
    }

    [Fact]
    public async Task Checkout_RejectsInsufficientPayment()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat, price: 20m);
        await StockAsync(p.Id, "B", 10m, 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = p.Id, Quantity = 1m, UnitPrice = 20m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 10m } }
        }));
    }

    [Fact]
    public async Task Checkout_RejectsCardOverpayment()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat, price: 20m);
        await StockAsync(p.Id, "B", 10m, 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = p.Id, Quantity = 1m, UnitPrice = 20m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Card, Amount = 30m } }
        }));
    }

    [Fact]
    public async Task Checkout_RejectsEmptyCart()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest>(),
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 0m } }
        }));
    }

    // ---- Returns ----

    private async Task<(int SaleId, SaleDetailDto Detail, int ItemId)> CompletedSaleAsync(decimal qty = 3m, decimal price = 20m)
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat, price: price);
        await StockAsync(p.Id, "B", 100m, 10m);
        var result = await Sales.CheckoutAsync(CashSale((p.Id, qty, price)));
        var detail = await Sales.GetByIdAsync(result.SaleId);
        return (result.SaleId, detail!, detail!.Items.Single().SaleItemId);
    }

    [Fact]
    public async Task Return_Full_RestocksAndMarksReturned()
    {
        var (saleId, detail, itemId) = await CompletedSaleAsync();
        var productId = detail.Items.Single().ProductId!.Value;
        var stockBefore = (await Db.Products.FindAsync(productId))!.CurrentStock;

        var result = await Sales.ProcessReturnAsync(new SalesReturnRequest
        {
            SaleId = saleId,
            Reason = "Customer changed mind",
            Lines = new List<ReturnLineRequest> { new() { SaleItemId = itemId, Quantity = 3m, Condition = ReturnCondition.Restock } }
        });

        Assert.True(result.Restocked);
        Assert.Equal(stockBefore + 3m, (await Db.Products.FindAsync(productId))!.CurrentStock);
        Assert.Equal(SaleStatus.Returned, (await Db.Sales.FindAsync(saleId))!.Status);
    }

    [Fact]
    public async Task Return_Partial_KeepsPartiallyReturnedStatus()
    {
        var (saleId, _, itemId) = await CompletedSaleAsync(qty: 5m);

        var result = await Sales.ProcessReturnAsync(new SalesReturnRequest
        {
            SaleId = saleId,
            Lines = new List<ReturnLineRequest> { new() { SaleItemId = itemId, Quantity = 2m, Condition = ReturnCondition.Restock } }
        });

        Assert.Equal(2m * 20m, result.TotalAmount);
        Assert.Equal(SaleStatus.PartiallyReturned, (await Db.Sales.FindAsync(saleId))!.Status);
    }

    [Fact]
    public async Task Return_RejectsQuantityExceedingOriginal()
    {
        var (saleId, _, itemId) = await CompletedSaleAsync(qty: 3m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.ProcessReturnAsync(new SalesReturnRequest
        {
            SaleId = saleId,
            Lines = new List<ReturnLineRequest> { new() { SaleItemId = itemId, Quantity = 5m, Condition = ReturnCondition.Restock } }
        }));
    }

    [Fact]
    public async Task Return_RejectsDuplicateReturnBeyondReturnedQuantity()
    {
        var (saleId, _, itemId) = await CompletedSaleAsync(qty: 3m);

        await Sales.ProcessReturnAsync(new SalesReturnRequest
        {
            SaleId = saleId,
            Lines = new List<ReturnLineRequest> { new() { SaleItemId = itemId, Quantity = 3m, Condition = ReturnCondition.Restock } }
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.ProcessReturnAsync(new SalesReturnRequest
        {
            SaleId = saleId,
            Lines = new List<ReturnLineRequest> { new() { SaleItemId = itemId, Quantity = 1m, Condition = ReturnCondition.Restock } }
        }));
    }

    [Fact]
    public async Task Return_Damaged_DoesNotRestock()
    {
        var (saleId, detail, itemId) = await CompletedSaleAsync();
        var productId = detail.Items.Single().ProductId!.Value;
        var stockBefore = (await Db.Products.FindAsync(productId))!.CurrentStock;

        var result = await Sales.ProcessReturnAsync(new SalesReturnRequest
        {
            SaleId = saleId,
            Lines = new List<ReturnLineRequest> { new() { SaleItemId = itemId, Quantity = 2m, Condition = ReturnCondition.Damaged } }
        });

        Assert.False(result.Restocked);
        Assert.Equal(stockBefore, (await Db.Products.FindAsync(productId))!.CurrentStock);
    }

    // ---- Query / history / details ----

    [Fact]
    public async Task SearchAsync_FindsSaleByReceiptNumber()
    {
        var (saleId, _, _) = await CompletedSaleAsync();
        var detail = await Sales.GetByIdAsync(saleId);

        var page = await Sales.SearchAsync(new SaleSearchFilter { Search = detail!.ReceiptNumber });
        Assert.Single(page.Items);
        Assert.Equal(detail.ReceiptNumber, page.Items[0].ReceiptNumber);
    }

    [Fact]
    public async Task SearchAsync_FiltersByPaymentMethod()
    {
        var cat = await CategoryAsync();
        var p = await ProductAsync(cat);
        await StockAsync(p.Id, "B", 10m, 10m);

        await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = p.Id, Quantity = 1m, UnitPrice = 20m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Card, Amount = 20m } }
        });

        var cash = await Sales.SearchAsync(new SaleSearchFilter { PaymentMethod = PaymentMethod.Cash });
        var card = await Sales.SearchAsync(new SaleSearchFilter { PaymentMethod = PaymentMethod.Card });
        Assert.Empty(cash.Items);
        Assert.Single(card.Items);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsItemsPaymentsAndReturns()
    {
        var (saleId, _, itemId) = await CompletedSaleAsync(qty: 4m);
        await Sales.ProcessReturnAsync(new SalesReturnRequest
        {
            SaleId = saleId,
            Lines = new List<ReturnLineRequest> { new() { SaleItemId = itemId, Quantity = 1m, Condition = ReturnCondition.Restock } }
        });

        var detail = await Sales.GetByIdAsync(saleId);
        Assert.NotNull(detail);
        Assert.Single(detail!.Items);
        Assert.Single(detail.Payments);
        Assert.Single(detail.Returns);
        Assert.Equal(4m, detail.Items[0].Quantity);
    }
}
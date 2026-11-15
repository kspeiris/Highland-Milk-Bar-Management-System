using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

/// <summary>Purchase price history: preserved costs, supplier comparison, filters (PUR-14).</summary>
public sealed class PurchasePriceHistoryTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private PurchaseService Purchases => new(Db, new InventoryService(Db));
    private SupplierService Suppliers => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<(Product Product, int SupplierA, int SupplierB)> SetupAsync()
    {
        var cat = await _db.AddCategoryAsync("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat.Id);
        var a = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Supplier A" });
        var b = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Supplier B" });
        return (product, a.Id, b.Id);
    }

    private async Task<int> ReceiveAsync(int productId, int supplierId, decimal unitCost, DateTime date, string batch)
    {
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplierId,
            PurchaseDate = date,
            Items = [new PurchaseLineRequest { ProductId = productId, Quantity = 10, UnitCost = unitCost, BatchNumber = batch }]
        });
        await Purchases.ReceiveAsync(purchase.Id);
        return purchase.Id;
    }

    [Fact]
    public async Task History_PreservesAllPurchasePrices()
    {
        var (product, a, b) = await SetupAsync();
        await ReceiveAsync(product.Id, a, 175m, DateTime.Today.AddDays(-14), "B1");
        await ReceiveAsync(product.Id, a, 178m, DateTime.Today.AddDays(-7), "B2");
        await ReceiveAsync(product.Id, b, 182m, DateTime.Today.AddDays(-3), "B3");

        var history = await Purchases.GetPriceHistoryAsync(product.Id, null);

        Assert.Equal(3, history.PurchaseCount);
        Assert.Equal(175m, history.LowestCost);
        Assert.Equal(182m, history.HighestCost);
        Assert.Equal(182m, history.LatestCost); // most recent first
    }

    [Fact]
    public async Task History_ComparesSuppliers()
    {
        var (product, a, b) = await SetupAsync();
        await ReceiveAsync(product.Id, a, 175m, DateTime.Today.AddDays(-5), "B1");
        await ReceiveAsync(product.Id, b, 182m, DateTime.Today.AddDays(-3), "B2");

        var aRows = await Purchases.GetPriceHistoryAsync(product.Id, a);
        var bRows = await Purchases.GetPriceHistoryAsync(product.Id, b);

        Assert.Single(aRows.Rows);
        Assert.Single(bRows.Rows);
        Assert.Equal(175m, aRows.Rows[0].UnitCost);
        Assert.Equal(182m, bRows.Rows[0].UnitCost);
    }

    [Fact]
    public async Task History_FiltersByDateRange()
    {
        var (product, a, _) = await SetupAsync();
        await ReceiveAsync(product.Id, a, 175m, DateTime.Today.AddDays(-20), "B1");
        await ReceiveAsync(product.Id, a, 180m, DateTime.Today.AddDays(-2), "B2");

        var recent = await Purchases.GetProductPurchaseHistoryAsync(product.Id, DateTime.Today.AddDays(-5), DateTime.Today);

        Assert.Single(recent);
        Assert.Equal(180m, recent[0].UnitCost);
    }

    [Fact]
    public async Task History_ExcludesDraftPurchases()
    {
        var (product, a, _) = await SetupAsync();
        await ReceiveAsync(product.Id, a, 175m, DateTime.Today, "B1");

        var draft = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = a,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 999m }]
        });

        var history = await Purchases.GetPriceHistoryAsync(product.Id, null);
        Assert.Single(history.Rows); // draft (999) not counted
        Assert.DoesNotContain(history.Rows, r => r.PurchaseId == draft.Id);
    }
}
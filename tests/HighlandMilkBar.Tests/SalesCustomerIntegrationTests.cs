using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class SalesCustomerIntegrationTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private InventoryService Inventory => new(Db);
    private LoyaltyService Loyalty => new(Db);
    private SalesService Sales => new(Db, Inventory, Loyalty);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<int> CategoryAsync() => (await _db.AddCategoryAsync("Cat")).Id;

    private async Task<Product> ProductAsync(int categoryId, decimal price = 500m)
    {
        var p = await _db.AddProductAsync("P-1", "Milk", categoryId, track: true, price: price);
        await Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = p.Id,
            BatchNumber = "B-1",
            Quantity = 100,
            UnitCost = 100m,
            ReceivedDate = DateTime.Today,
            ExpiryDate = DateTime.Today.AddDays(30)
        });
        return p;
    }

    private SaleCheckoutRequest SaleFor(int? customerId, int productId, decimal price, decimal qty = 1m)
        => new()
        {
            CustomerId = customerId,
            Items = new List<SaleLineRequest> { new() { ProductId = productId, Quantity = qty, UnitPrice = price } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price * qty } }
        };

    [Fact]
    public async Task Checkout_WithCustomer_PersistsAssociationAndAwardsLoyalty()
    {
        var categoryId = await CategoryAsync();
        var product = await ProductAsync(categoryId, price: 500m);
        var customer = await _db.AddCustomerAsync("Buyer", "0770000001");

        var result = await Sales.CheckoutAsync(SaleFor(customer.Id, product.Id, 500m));

        var sale = await Db.Sales.SingleAsync(s => s.Id == result.SaleId);
        Assert.Equal(customer.Id, sale.CustomerId);

        var balance = await Loyalty.GetBalanceAsync(customer.Id);
        Assert.Equal(5, balance.Balance); // 500 / 100 * 1
    }

    [Fact]
    public async Task Checkout_WalkIn_NoCustomerNoLoyalty()
    {
        var categoryId = await CategoryAsync();
        var product = await ProductAsync(categoryId, price: 500m);

        var result = await Sales.CheckoutAsync(SaleFor(null, product.Id, 500m));

        var sale = await Db.Sales.SingleAsync(s => s.Id == result.SaleId);
        Assert.Null(sale.CustomerId);
        Assert.Empty(Db.LoyaltyTransactions);
    }

    [Fact]
    public async Task Checkout_InactiveCustomer_Rejected()
    {
        var categoryId = await CategoryAsync();
        var product = await ProductAsync(categoryId, price: 500m);
        var customer = await _db.AddCustomerAsync("Inactive", "0770000001", active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.CheckoutAsync(SaleFor(customer.Id, product.Id, 500m)));
        Assert.Empty(Db.Sales);
    }

    [Fact]
    public async Task Checkout_InvalidCustomer_Rejected()
    {
        var categoryId = await CategoryAsync();
        var product = await ProductAsync(categoryId, price: 500m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Sales.CheckoutAsync(SaleFor(9999, product.Id, 500m)));
        Assert.Empty(Db.Sales);
    }

    [Fact]
    public async Task Checkout_StillDeductsInventory()
    {
        var categoryId = await CategoryAsync();
        var product = await ProductAsync(categoryId, price: 500m);
        var before = (await Db.Products.SingleAsync(p => p.Id == product.Id)).CurrentStock;

        await Sales.CheckoutAsync(SaleFor(null, product.Id, 500m));

        var after = (await Db.Products.SingleAsync(p => p.Id == product.Id)).CurrentStock;
        Assert.Equal(before - 1m, after);
    }
}
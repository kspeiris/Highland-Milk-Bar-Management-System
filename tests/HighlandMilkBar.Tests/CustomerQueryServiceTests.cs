using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;

namespace HighlandMilkBar.Tests;

public sealed class CustomerQueryServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private CustomerQueryService Query => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Search_ByCode_Name_Phone_Email()
    {
        var a = await _db.AddCustomerAsync("Alice Johnson", "0771111111", email: "alice@example.com");
        await _db.AddCustomerAsync("Bob Smith", "0772222222", email: "bob@example.com");

        Assert.Single((await Query.SearchAsync(new CustomerSearchFilter { Search = a.CustomerCode })).Items);
        Assert.Single((await Query.SearchAsync(new CustomerSearchFilter { Search = "Alice" })).Items);
        Assert.Single((await Query.SearchAsync(new CustomerSearchFilter { Search = "0772222222" })).Items);
        Assert.Single((await Query.SearchAsync(new CustomerSearchFilter { Search = "alice@example.com" })).Items);
    }

    [Fact]
    public async Task Search_FilterByTypeAndStatus()
    {
        await _db.AddCustomerAsync("Business One", "0770000001", CustomerType.Business, active: true);
        await _db.AddCustomerAsync("Individual One", "0770000002", CustomerType.Individual, active: false);

        var business = await Query.SearchAsync(new CustomerSearchFilter { Type = CustomerType.Business });
        Assert.Single(business.Items);
        Assert.Equal(CustomerType.Business, business.Items[0].Type);

        var inactive = await Query.SearchAsync(new CustomerSearchFilter { IsActive = false });
        Assert.Single(inactive.Items);
    }

    [Fact]
    public async Task Search_SortBySpending_OrdersDescending()
    {
        var low = await _db.AddCustomerAsync("Low", "0770000001");
        var high = await _db.AddCustomerAsync("High", "0770000002");
        _db.AddSale(low.Id, 100m);
        _db.AddSale(high.Id, 5000m);

        var result = await Query.SearchAsync(new CustomerSearchFilter { Sort = CustomerSort.Spending });
        Assert.Equal(high.Id, result.Items[0].Id);
        Assert.Equal(5000m, result.Items[0].TotalSpending);
    }

    [Fact]
    public async Task Search_Pagination_ReturnsPageSizeAndTotal()
    {
        for (var i = 0; i < 20; i++)
            await _db.AddCustomerAsync($"Customer {i:D2}", $"07700{i:D5}");

        var page1 = await Query.SearchAsync(new CustomerSearchFilter { Page = 1, PageSize = 15 });
        Assert.Equal(15, page1.Items.Count);
        Assert.Equal(20, page1.TotalCount);

        var page2 = await Query.SearchAsync(new CustomerSearchFilter { Page = 2, PageSize = 15 });
        Assert.Equal(5, page2.Items.Count);
    }

    [Fact]
    public async Task Profile_ComputesSpendingAverageAndLastPurchase()
    {
        var customer = await _db.AddCustomerAsync("Spender", "0770000001");
        _db.AddSale(customer.Id, 1000m, SaleStatus.Completed, new DateTime(2026, 1, 10));
        _db.AddSale(customer.Id, 3000m, SaleStatus.Completed, new DateTime(2026, 2, 10));
        _db.AddSale(customer.Id, 9999m, SaleStatus.Voided, new DateTime(2026, 3, 1));

        var profile = await Query.GetProfileAsync(customer.Id);

        Assert.NotNull(profile);
        Assert.Equal(2, profile!.TotalPurchases);
        Assert.Equal(4000m, profile.TotalSpending);
        Assert.Equal(2000m, profile.AveragePurchaseValue);
        Assert.Equal(new DateTime(2026, 2, 10), profile.LastPurchase);
    }

    [Fact]
    public async Task PurchaseHistory_ExcludesVoidedSales()
    {
        var customer = await _db.AddCustomerAsync("Historic", "0770000001");
        _db.AddSale(customer.Id, 500m, SaleStatus.Completed);
        _db.AddSale(customer.Id, 700m, SaleStatus.Voided);

        var history = await Query.GetPurchaseHistoryAsync(customer.Id);
        Assert.Single(history);
        Assert.Equal(500m, history[0].TotalAmount);
        Assert.Equal("Completed", history[0].StatusText);
    }

    [Fact]
    public async Task PurchasedProducts_AggregatesQuantityAndSpend()
    {
        var customer = await _db.AddCustomerAsync("Buyer", "0770000001");
        var sale = _db.AddSale(customer.Id, 500m);
        var item = Db.SaleItems.First(i => i.SaleId == sale.Id);
        item.Quantity = 2;
        item.LineTotal = 500m;
        await Db.SaveChangesAsync();

        var products = await Query.GetPurchasedProductsAsync(customer.Id);
        Assert.Single(products);
        Assert.Equal(2m, products[0].TotalQuantity);
        Assert.Equal(500m, products[0].TotalSpent);
    }

    [Fact]
    public async Task Stats_CountsRepeatCustomersAndCustomerSales()
    {
        var repeat = await _db.AddCustomerAsync("Repeat", "0770000001");
        _db.AddSale(repeat.Id, 100m);
        _db.AddSale(repeat.Id, 200m);
        var single = await _db.AddCustomerAsync("Single", "0770000002");
        _db.AddSale(single.Id, 300m);
        _db.AddSale(null, 999m); // walk-in excluded

        var stats = await Query.GetStatsAsync();

        Assert.Equal(2, stats.TotalCustomers);
        Assert.Equal(1, stats.RepeatCustomers);
        Assert.Equal(600m, stats.TotalCustomerSales);
    }
}
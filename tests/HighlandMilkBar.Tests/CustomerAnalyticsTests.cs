using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;

namespace HighlandMilkBar.Tests;

public sealed class CustomerAnalyticsTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private CustomerQueryService Query => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private int Count(CustomerAnalyticsDto data, CustomerSegment segment)
        => data.SegmentCounts.Single(s => s.Segment == segment).Count;

    [Fact]
    public async Task Metrics_UseRealTransactionData()
    {
        var now = DateTime.Now;
        var repeat = await _db.AddCustomerAsync("Repeat", "0770000001", createdAt: now.AddDays(-40));
        _db.AddSale(repeat.Id, 100m, soldAt: now.AddDays(-30));
        _db.AddSale(repeat.Id, 200m, soldAt: now.AddDays(-5));
        var single = await _db.AddCustomerAsync("Single", "0770000002", createdAt: now.AddDays(-10));
        _db.AddSale(single.Id, 300m, soldAt: now.AddDays(-3));
        _db.AddSale(null, 999m); // walk-in excluded

        var data = await Query.GetAnalyticsAsync();

        Assert.Equal(2, data.TotalCustomers);
        Assert.Equal(2, data.ActiveCustomers);
        Assert.Equal(1, data.NewThisMonth);
        Assert.Equal(1, data.RepeatCustomers);
        Assert.Equal(600m, data.TotalCustomerSales);
    }

    [Fact]
    public async Task TopCustomers_AreRankedBySpendingAndFrequency()
    {
        var big = await _db.AddCustomerAsync("Big", "0770000001");
        _db.AddSale(big.Id, 10000m);
        var small = await _db.AddCustomerAsync("Small", "0770000002");
        _db.AddSale(small.Id, 100m);

        var data = await Query.GetAnalyticsAsync();

        Assert.Equal(big.Id, data.TopCustomersBySpending[0].Id);
        Assert.Equal(10000m, data.TopCustomersBySpending[0].TotalSpending);
        Assert.Equal(2, data.TopCustomersByFrequency.Count);
    }

    [Fact]
    public async Task InactiveCustomers_HaveNoRecentPurchase()
    {
        var active = await _db.AddCustomerAsync("Active", "0770000001");
        _db.AddSale(active.Id, 100m, soldAt: DateTime.Now.AddDays(-5));
        var stale = await _db.AddCustomerAsync("Stale", "0770000002");
        _db.AddSale(stale.Id, 100m, soldAt: DateTime.Now.AddDays(-200));

        var data = await Query.GetAnalyticsAsync();

        Assert.Single(data.InactiveCustomers);
        Assert.Equal(stale.Id, data.InactiveCustomers[0].Id);
    }

    [Fact]
    public async Task Segmentation_AssignsExpectedLabels()
    {
        var now = DateTime.Now;

        var regular = await _db.AddCustomerAsync("Regular", "0770000001");
        for (var i = 0; i < 5; i++) _db.AddSale(regular.Id, 100m, soldAt: now.AddDays(-i - 1));

        var oneTime = await _db.AddCustomerAsync("OneTime", "0770000002");
        _db.AddSale(oneTime.Id, 100m, soldAt: now.AddDays(-2));

        var highValue = await _db.AddCustomerAsync("HighValue", "0770000003");
        _db.AddSale(highValue.Id, 60000m, soldAt: now.AddDays(-1));

        var noPurchase = await _db.AddCustomerAsync("NoPurchase", "0770000004");

        var data = await Query.GetAnalyticsAsync();

        // Regular customer qualifies as Regular (5 purchases).
        Assert.Equal(1, Count(data, CustomerSegment.Regular));

        // One-time customers (oneTime and highValue each have a single purchase).
        Assert.Equal(2, Count(data, CustomerSegment.OneTime));
        Assert.True(Count(data, CustomerSegment.New) >= 1);

        // High-value threshold default 50000.
        Assert.Equal(1, Count(data, CustomerSegment.HighValue));

        // Zero-purchase customer is in no segment: New=3 (regular, oneTime, highValue),
        // Regular=1, HighValue=1, OneTime=2, Inactive=0.
        Assert.Equal(3, Count(data, CustomerSegment.New));
        Assert.Equal(1, Count(data, CustomerSegment.Regular));
        Assert.Equal(1, Count(data, CustomerSegment.HighValue));
        Assert.Equal(0, Count(data, CustomerSegment.Inactive));
        Assert.Equal(2, Count(data, CustomerSegment.OneTime));
    }

    [Fact]
    public async Task Analytics_ExcludeCancelledAndVoidedSales()
    {
        var customer = await _db.AddCustomerAsync("Mixed", "0770000001");
        _db.AddSale(customer.Id, 1000m, SaleStatus.Completed);
        _db.AddSale(customer.Id, 5000m, SaleStatus.Voided);

        var data = await Query.GetAnalyticsAsync();
        Assert.Equal(1000m, data.TotalCustomerSales);
    }
}
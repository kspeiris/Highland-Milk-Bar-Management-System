using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;

namespace HighlandMilkBar.Tests;

public sealed class LoyaltyServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private LoyaltyService Loyalty => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private (int SaleId, int CustomerId) SaleFor(int customerId, decimal total)
    {
        var sale = _db.AddSale(customerId, total);
        return (sale.Id, customerId);
    }

    private SalesReturn AddReturn(int saleId)
    {
        var ret = new SalesReturn
        {
            ReturnNumber = $"RET-{Guid.NewGuid():N}"[..10],
            SaleId = saleId,
            Status = SalesReturnStatus.Completed
        };
        Db.SalesReturns.Add(ret);
        Db.SaveChanges();
        return ret;
    }

    [Fact]
    public async Task Award_ForEligibleSale_ErnsPoints()
    {
        var customer = await _db.AddCustomerAsync("Loyal", "0770000001");
        var (saleId, customerId) = SaleFor(customer.Id, 2500m);

        await Loyalty.AwardForSaleAsync(saleId, customerId, 2500m, "RCP-1");

        var balance = await Loyalty.GetBalanceAsync(customerId);
        Assert.Equal(25, balance.Balance);
        Assert.Equal(25, balance.TotalEarned);
    }

    [Fact]
    public async Task Award_WalkInSale_ErnsNothing()
    {
        var sale = _db.AddSale(null, 2500m);
        await Loyalty.AwardForSaleAsync(sale.Id, 0, 2500m, "RCP-1");
        Assert.Empty(Db.LoyaltyTransactions);
    }

    [Fact]
    public async Task Award_BelowMinimum_ErnsNothing()
    {
        var customer = await _db.AddCustomerAsync("Small", "0770000001");
        var (saleId, customerId) = SaleFor(customer.Id, 400m);

        await Loyalty.AwardForSaleAsync(saleId, customerId, 400m, "RCP-1");
        Assert.Empty(Db.LoyaltyTransactions);
    }

    [Fact]
    public async Task Award_WhenDisabled_ErnsNothing()
    {
        await Loyalty.UpdateSettingsAsync(new LoyaltyUpdateSettingsRequest { Enabled = false, PointsPerHundred = 1m, MinimumQualifyingAmount = 500m });
        var customer = await _db.AddCustomerAsync("Off", "0770000001");
        var (saleId, customerId) = SaleFor(customer.Id, 2500m);

        await Loyalty.AwardForSaleAsync(saleId, customerId, 2500m, "RCP-1");
        Assert.Empty(Db.LoyaltyTransactions);
    }

    [Fact]
    public async Task Award_TwiceForSameSale_DoesNotDuplicate()
    {
        var customer = await _db.AddCustomerAsync("Dup", "0770000001");
        var (saleId, customerId) = SaleFor(customer.Id, 2500m);

        await Loyalty.AwardForSaleAsync(saleId, customerId, 2500m, "RCP-1");
        await Loyalty.AwardForSaleAsync(saleId, customerId, 2500m, "RCP-1");

        var balance = await Loyalty.GetBalanceAsync(customerId);
        Assert.Equal(25, balance.Balance);
        Assert.Single(Db.LoyaltyTransactions);
    }

    [Fact]
    public async Task Reverse_FullReturn_ReversesAllEarned()
    {
        var customer = await _db.AddCustomerAsync("Full", "0770000001");
        var (saleId, customerId) = SaleFor(customer.Id, 2500m);
        await Loyalty.AwardForSaleAsync(saleId, customerId, 2500m, "RCP-1");
        var ret = AddReturn(saleId);

        await Loyalty.ReverseForReturnAsync(saleId, customerId, 2500m, 2500m, isFullReturn: true, ret.Id, ret.ReturnNumber);

        var balance = await Loyalty.GetBalanceAsync(customerId);
        Assert.Equal(0, balance.Balance);
        Assert.Equal(25, balance.TotalEarned);
        Assert.Equal(25, balance.TotalReversed);
    }

    [Fact]
    public async Task Reverse_PartialReturn_ReversesProportionalPoints()
    {
        var customer = await _db.AddCustomerAsync("Partial", "0770000001");
        var (saleId, customerId) = SaleFor(customer.Id, 2500m);
        await Loyalty.AwardForSaleAsync(saleId, customerId, 2500m, "RCP-1");
        var ret1 = AddReturn(saleId);

        await Loyalty.ReverseForReturnAsync(saleId, customerId, 2500m, 1250m, isFullReturn: false, ret1.Id, ret1.ReturnNumber);
        var afterPartial = await Loyalty.GetBalanceAsync(customerId);
        Assert.Equal(13, afterPartial.Balance); // floor(25 * 0.5) = 12 reversed

        var ret2 = AddReturn(saleId);
        await Loyalty.ReverseForReturnAsync(saleId, customerId, 2500m, 1250m, isFullReturn: true, ret2.Id, ret2.ReturnNumber);
        var afterFull = await Loyalty.GetBalanceAsync(customerId);
        Assert.Equal(0, afterFull.Balance);
    }

    [Fact]
    public async Task Settings_RoundTripPersists()
    {
        await Loyalty.UpdateSettingsAsync(new LoyaltyUpdateSettingsRequest
        {
            PointsPerHundred = 2m,
            MinimumQualifyingAmount = 1000m,
            Enabled = true
        });

        var settings = await Loyalty.GetSettingsAsync();
        Assert.Equal(2m, settings.PointsPerHundred);
        Assert.Equal(1000m, settings.MinimumQualifyingAmount);
        Assert.True(settings.Enabled);
    }

    [Fact]
    public void Calculator_FloorsPoints()
    {
        Assert.Equal(25, LoyaltyCalculator.PointsForSale(2500m, 1m, 500m, true));
        Assert.Equal(25, LoyaltyCalculator.PointsForSale(2550m, 1m, 500m, true)); // 25.5 -> 25
        Assert.Equal(0, LoyaltyCalculator.PointsForSale(400m, 1m, 500m, true));
        Assert.Equal(12, LoyaltyCalculator.PointsToReverse(25, 2500m, 1250m));
    }
}
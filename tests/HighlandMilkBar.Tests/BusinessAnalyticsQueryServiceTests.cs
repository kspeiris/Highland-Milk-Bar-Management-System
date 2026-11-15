using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class BusinessAnalyticsQueryServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;

    private BusinessAnalyticsQueryService BuildService()
    {
        var inventory = new InventoryService(Db);
        var recipe = new RecipeService(Db);
        return new BusinessAnalyticsQueryService(
            Db,
            new FinancialQueryService(Db),
            new InventoryQueryService(Db),
            new CustomerQueryService(Db),
            new MilkBarService(Db, recipe),
            new PurchaseService(Db, inventory),
            new ExpenseService(Db));
    }

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<(int CategoryId, int ProductId)> AddCategoryProduct(string name, decimal cost, decimal price, decimal stock, decimal reorder, bool track = true)
    {
        var category = await _db.AddCategoryAsync(name);
        var product = await _db.AddProductAsync($"SKU-{name}", name, category.Id, track: track, cost: cost, price: price, stock: stock);
        product.ReorderLevel = reorder;
        Db.SaveChanges();
        return (category.Id, product.Id);
    }

    private Sale AddSale(decimal total, DateTime soldAt, SaleStatus status = SaleStatus.Completed,
        int? productId = null, string itemName = "Item", decimal qty = 1m, decimal unitPrice = 0m, decimal unitCost = 0m,
        PaymentMethod method = PaymentMethod.Cash)
    {
        var sale = new Sale
        {
            ReceiptNumber = $"RCP-{Guid.NewGuid():N}"[..12],
            Subtotal = total,
            TotalAmount = total,
            AmountPaid = total,
            CostOfGoodsSold = qty * unitCost,
            Status = status,
            SoldAt = soldAt
        };
        sale.Items.Add(new SaleItem
        {
            ProductId = productId,
            ItemNameSnapshot = itemName,
            Quantity = qty,
            UnitPrice = unitPrice == 0m ? total : unitPrice,
            UnitCostSnapshot = unitCost,
            LineTotal = total
        });
        sale.Payments.Add(new Payment { Method = method, Amount = total, PaidAt = soldAt });
        Db.Sales.Add(sale);
        Db.SaveChanges();
        return sale;
    }

    private void AddReturn(int saleId, decimal total, DateTime when)
    {
        Db.SalesReturns.Add(new SalesReturn { ReturnNumber = $"RET-{Guid.NewGuid():N}"[..12], SaleId = saleId, TotalAmount = total, ReturnedAt = when });
        Db.SaveChanges();
    }

    [Fact]
    public async Task Overview_UsesFinancialService_ForKpis()
    {
        var (cat, prod) = await AddCategoryProduct("Milk", 40m, 100m, 50m, 5m);
        AddSale(1000m, DateTime.Now, productId: prod, itemName: "Milk", qty: 5m, unitPrice: 200m, unitCost: 40m);
        await AddExpenseAsync(150m, DateTime.Now);

        var overview = await BuildService().GetOverviewAsync(ReportingPeriod.FromPreset(ReportingPreset.ThisMonth));

        var summary = await new FinancialQueryService(Db).GetSummaryAsync(overview.Start, overview.EndExclusive);
        Assert.Equal(summary.NetSales, overview.Financial.NetSales);
        Assert.Equal(summary.GrossProfit, overview.Financial.GrossProfit);
        Assert.Equal(1, overview.Orders);
        Assert.Equal(5m, overview.UnitsSold);
        Assert.Equal(1000m, overview.AverageOrderValue);
        Assert.Single(overview.TopProducts);
        Assert.Equal("Milk", overview.TopProducts[0].Name);
        Assert.Equal(150m, overview.Financial.OperatingExpenses);
    }

    [Fact]
    public async Task Overview_ComparesWithPreviousPeriod()
    {
        AddSale(1000m, DateTime.Now.AddMonths(-1));
        AddSale(800m, DateTime.Now);

        var overview = await BuildService().GetOverviewAsync(ReportingPeriod.FromPreset(ReportingPreset.ThisMonth));

        Assert.Equal(800m, overview.Financial.NetSales);
        Assert.Equal(1000m, overview.NetSalesComparison.Previous);
        Assert.Equal(-20m, overview.NetSalesComparison.PercentChange);
        Assert.False(overview.NetSalesComparison.IsIncrease);
    }

    [Fact]
    public async Task SalesAnalytics_ExcludesVoided_And_SubtractsReturns()
    {
        var (cat, prod) = await AddCategoryProduct("Milk", 40m, 100m, 50m, 5m);
        var sale = AddSale(500m, DateTime.Now, productId: prod, itemName: "Milk", qty: 5m, unitPrice: 100m);
        AddSale(300m, DateTime.Now, SaleStatus.Voided, productId: prod, itemName: "Milk");
        AddReturn(sale.Id, 50m, DateTime.Now);

        var analytics = await BuildService().GetSalesAnalyticsAsync(ReportingPeriod.FromPreset(ReportingPreset.ThisMonth));

        Assert.Equal(450m, analytics.NetSales); // 500 - 50 return, voided excluded
        Assert.Equal(1, analytics.Orders);
        Assert.Equal(5m, analytics.UnitsSold);
        Assert.Equal("Milk", analytics.ByCategory.Single().CategoryName);
    }

    [Fact]
    public async Task ProductAnalytics_TopSellers_And_NoSale()
    {
        var (_, selling) = await AddCategoryProduct("Selling", 10m, 50m, 10m, 2m);
        var (_, noSaleActive) = await AddCategoryProduct("NoSaleActive", 10m, 50m, 10m, 2m);
        var (_, inactive) = await AddCategoryProduct("Inactive", 10m, 50m, 10m, 2m);
        var product = await Db.Products.FindAsync(inactive);
        product!.IsActive = false;
        Db.SaveChanges();

        AddSale(200m, DateTime.Now, productId: selling, itemName: "Selling", qty: 4m, unitPrice: 50m, unitCost: 10m);

        var products = await BuildService().GetProductAnalyticsAsync(ReportingPeriod.FromPreset(ReportingPreset.ThisMonth));

        Assert.Contains(products.TopSellers, p => p.Name == "Selling" && p.UnitsSold == 4m);
        Assert.Contains(products.NoSale, p => p.Name == "NoSaleActive");
        Assert.DoesNotContain(products.NoSale, p => p.Name == "Inactive");
        Assert.Contains(products.HighProfit, p => p.Name == "Selling");
    }

    [Fact]
    public async Task Alerts_IncludeLowStock()
    {
        var (_, product) = await AddCategoryProduct("Perishable", 10m, 50m, 2m, 5m);
        _db.AddBatch(product, "B-1", 2, 10, DateTime.Now, null);
        await Db.SaveChangesAsync();

        var alerts = await BuildService().GetBusinessAlertsAsync(ReportingPeriod.FromPreset(ReportingPreset.ThisMonth));

        Assert.Contains(alerts, a => a.Category == BusinessAlertCategory.Inventory);
    }

    [Fact]
    public async Task Alerts_IncludeSalesDecline()
    {
        AddSale(1000m, DateTime.Now.AddMonths(-1));
        AddSale(100m, DateTime.Now);

        var alerts = await BuildService().GetBusinessAlertsAsync(ReportingPeriod.FromPreset(ReportingPreset.ThisMonth));

        Assert.Contains(alerts, a => a.Type == "Sales.Decline");
    }

    [Fact]
    public async Task Alerts_HaveStableKeys()
    {
        var svc = BuildService();
        var period = ReportingPeriod.FromPreset(ReportingPreset.ThisMonth);
        var first = await svc.GetBusinessAlertsAsync(period);
        var second = await svc.GetBusinessAlertsAsync(period);

        Assert.Equal(
            first.Select(a => a.Key).OrderBy(k => k),
            second.Select(a => a.Key).OrderBy(k => k));
    }

    [Fact]
    public async Task Analytics_IsReadOnly()
    {
        var (cat, prod) = await AddCategoryProduct("Milk", 40m, 100m, 50m, 5m);
        AddSale(500m, DateTime.Now, productId: prod, itemName: "Milk");
        var sales = await Db.Sales.CountAsync();
        var products = await Db.Products.CountAsync();
        var payments = await Db.Payments.CountAsync();

        var svc = BuildService();
        await svc.GetOverviewAsync(ReportingPeriod.FromPreset(ReportingPreset.ThisMonth));
        await svc.GetSalesAnalyticsAsync(ReportingPeriod.FromPreset(ReportingPreset.ThisMonth));
        await svc.GetProductAnalyticsAsync(ReportingPeriod.FromPreset(ReportingPreset.ThisMonth));
        await svc.GetBusinessAlertsAsync(ReportingPeriod.FromPreset(ReportingPreset.ThisMonth));

        Assert.Equal(sales, await Db.Sales.CountAsync());
        Assert.Equal(products, await Db.Products.CountAsync());
        Assert.Equal(payments, await Db.Payments.CountAsync());
    }

    private async Task AddExpenseAsync(decimal amount, DateTime date)
    {
        Db.Expenses.Add(new Expense { Description = "Test", Amount = amount, ExpenseDate = date, PaymentMethod = PaymentMethod.Cash });
        await Db.SaveChangesAsync();
    }
}
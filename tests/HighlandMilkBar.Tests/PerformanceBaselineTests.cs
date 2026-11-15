using System.Diagnostics;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace HighlandMilkBar.Tests;

/// <summary>
/// Step 15 performance baseline. Generates a representative dataset in an isolated temporary
/// database, then measures the cost of the most common read paths and a backup cycle. The
/// assertions are deliberately generous upper bounds so the test acts as a regression guard
/// rather than a flaky micro-benchmark; the actual timings are written to test output so
/// before/after comparisons can be recorded.
/// </summary>
public sealed class PerformanceBaselineTests : IAsyncLifetime
{
    private const int ProductCount = 2_000;
    private const int SaleCount = 5_000;
    private const int CustomerCount = 1_000;
    private static readonly decimal SaleTotal = 100m;

    private readonly TestDatabase _db = new();
    private readonly ITestOutputHelper _output;
    private AppDbContext Db => _db.Db;

    public PerformanceBaselineTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        await GenerateDatasetAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task GenerateDatasetAsync()
    {
        var category = await _db.AddCategoryAsync("Cat");

        var products = new List<Product>(ProductCount);
        for (var i = 0; i < ProductCount; i++)
        {
            products.Add(new Product
            {
                Sku = $"SKU-{i:D6}",
                Name = $"Product {i}",
                CategoryId = category.Id,
                Type = ProductType.Packaged,
                Unit = "Unit",
                CostPrice = 50m,
                SellingPrice = SaleTotal,
                CurrentStock = 100m,
                TrackInventory = true
            });
        }
        Db.Products.AddRange(products);

        var customers = new List<Customer>(CustomerCount);
        for (var i = 0; i < CustomerCount; i++)
        {
            customers.Add(new Customer
            {
                CustomerCode = $"CUS-{i:D6}",
                Name = $"Customer {i}",
                Phone = $"077{i:D7}",
                Type = CustomerType.Individual,
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-i)
            });
        }
        Db.Customers.AddRange(customers);
        await Db.SaveChangesAsync();

        var productIds = products.Select(p => p.Id).ToList();
        var sales = new List<Sale>(SaleCount);
        var saleItems = new List<SaleItem>(SaleCount);
        var payments = new List<Payment>(SaleCount);
        var start = DateTime.Now.AddDays(-60);

        for (var i = 0; i < SaleCount; i++)
        {
            var productId = productIds[i % productIds.Count];
            var sale = new Sale
            {
                ReceiptNumber = $"RCP-{i:D8}",
                Subtotal = SaleTotal,
                TotalAmount = SaleTotal,
                AmountPaid = SaleTotal,
                CostOfGoodsSold = 50m,
                Status = SaleStatus.Completed,
                SoldAt = start.AddMinutes(i)
            };
            sale.Items.Add(new SaleItem
            {
                ProductId = productId,
                ItemNameSnapshot = "Product",
                Quantity = 1m,
                UnitPrice = SaleTotal,
                UnitCostSnapshot = 50m,
                LineTotal = SaleTotal
            });
            sale.Payments.Add(new Payment { Method = PaymentMethod.Cash, Amount = SaleTotal, PaidAt = sale.SoldAt });
            sales.Add(sale);
        }

        Db.Sales.AddRange(sales);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Baseline_MeasuresKeyReadPathsAndBackup()
    {
        var products = new ProductService(Db);
        var inventory = new InventoryQueryService(Db);
        var financial = new FinancialQueryService(Db);
        var backup = new BackupService(Db);
        var folder = Path.Combine(Path.GetTempPath(), $"hmb-perf-{Guid.NewGuid():N}");

        try
        {
            // Warm up JIT / EF model.
            await financial.GetSummaryAsync(DateTime.Now.AddDays(-90), DateTime.Now.AddDays(1));

            var productMs = await TimeAsync(() => products.SearchAsync(new ProductFilter { Search = "Product 1", PageSize = 50 }));
            var overviewMs = await TimeAsync(() => inventory.GetOverviewAsync());
            var financialMs = await TimeAsync(async () =>
            {
                var summary = await financial.GetSummaryAsync(DateTime.Now.AddDays(-90), DateTime.Now.AddDays(1));
                Assert.Equal(SaleCount * SaleTotal, summary.NetSales);
            });

            var backupResult = await backup.CreateBackupAsync(BackupType.Manual, folder);
            var backupMs = await TimeAsync(() => backup.VerifyBackupAsync(backupResult.Path));
            Assert.True(backupResult.Success, backupResult.ErrorMessage);

            _output.WriteLine($"Dataset: {ProductCount} products, {CustomerCount} customers, {SaleCount} sales");
            _output.WriteLine($"Product search (paged 50): {productMs} ms");
            _output.WriteLine($"Inventory overview:         {overviewMs} ms");
            _output.WriteLine($"Financial summary (90d):    {financialMs} ms");
            _output.WriteLine($"Backup create+verify:       {backupMs} ms");

            // Generous regression ceilings for an isolated SQLite file on dev hardware.
            Assert.True(productMs < 3000, $"Product search took {productMs} ms");
            Assert.True(overviewMs < 3000, $"Inventory overview took {overviewMs} ms");
            Assert.True(financialMs < 5000, $"Financial summary took {financialMs} ms");
            Assert.True(backupMs < 10000, $"Backup verify took {backupMs} ms");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private static async Task<long> TimeAsync(Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }
}
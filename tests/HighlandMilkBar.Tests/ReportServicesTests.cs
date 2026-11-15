using System.IO.Compression;
using System.Text;
using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.DTOs.Reports;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using HighlandMilkBar.Infrastructure.Services.Reports;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class ReportServicesTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private ReportQueryService BuildQuery()
    {
        var inventory = new InventoryService(Db);
        var recipe = new RecipeService(Db);
        var analytics = new BusinessAnalyticsQueryService(
            Db, new FinancialQueryService(Db), new InventoryQueryService(Db), new CustomerQueryService(Db),
            new MilkBarService(Db, recipe), new PurchaseService(Db, inventory), new ExpenseService(Db));
        return new ReportQueryService(
            Db, analytics, new FinancialQueryService(Db), new InventoryQueryService(Db), new CustomerQueryService(Db),
            new PurchaseService(Db, inventory), new ExpenseService(Db), new MilkBarService(Db, recipe));
    }

    private void AddSale(decimal total, DateTime when, decimal cogs = 0m, SaleStatus status = SaleStatus.Completed)
    {
        var sale = new Sale
        {
            ReceiptNumber = $"RCP-{Guid.NewGuid():N}"[..12],
            Subtotal = total,
            TotalAmount = total,
            AmountPaid = total,
            CostOfGoodsSold = cogs,
            Status = status,
            SoldAt = when
        };
        sale.Items.Add(new SaleItem { ItemNameSnapshot = "Item", Quantity = 1, UnitPrice = total, UnitCostSnapshot = cogs, LineTotal = total });
        sale.Payments.Add(new Payment { Method = PaymentMethod.Cash, Amount = total, PaidAt = when });
        Db.Sales.Add(sale);
        Db.SaveChanges();
    }

    [Fact]
    public async Task FinancialPl_MatchesFinancialService()
    {
        AddSale(1000m, DateTime.Now, cogs: 400m);
        Db.Expenses.Add(new Expense { Description = "E", Amount = 150m, ExpenseDate = DateTime.Now, PaymentMethod = PaymentMethod.Cash });
        Db.SaveChanges();

        var period = ReportingPeriod.FromPreset(ReportingPreset.ThisMonth);
        var summary = await new FinancialQueryService(Db).GetSummaryAsync(period.Start, period.EndExclusive);
        var report = await BuildQuery().GenerateAsync("financial.pl", period);

        var netRow = report.Rows.Single(r => (r.Values[0] as string) == "Net sales");
        var profitRow = report.Rows.Single(r => (r.Values[0] as string) == "Operating profit");

        Assert.Equal(summary.NetSales, netRow.Values[1]);
        Assert.Equal(summary.OperatingProfit, profitRow.Values[1]);
    }

    [Fact]
    public async Task SalesSummary_ReportsTotals()
    {
        AddSale(1000m, DateTime.Now);
        AddSale(500m, DateTime.Now);

        var period = ReportingPeriod.FromPreset(ReportingPreset.ThisMonth);
        var report = await BuildQuery().GenerateAsync("sales.summary", period);

        Assert.Equal("Sales Summary", report.Title);
        Assert.Contains(report.Summary, s => s.Label == "Net sales");
        Assert.Contains(report.Rows, r => (r.Values[1] as decimal?) == 1500m && Convert.ToInt32(r.Values[2]) == 2);
    }

    [Fact]
    public async Task InventoryStock_IncludesTrackedProduct()
    {
        var category = await _db.AddCategoryAsync("Cat");
        await _db.AddProductAsync("SKU-1", "Tracked", category.Id, track: true, cost: 10m, price: 20m, stock: 5m);

        var report = await BuildQuery().GenerateAsync("inventory.stock", ReportingPeriod.FromPreset(ReportingPreset.Today));

        Assert.Equal("Current Stock", report.Title);
        Assert.NotEmpty(report.Rows);
    }

    [Fact]
    public async Task CsvExport_EscapesAndPreventsFormulaInjection()
    {
        var report = new ReportResult
        {
            Key = "t", Title = "T", Category = "Sales", PeriodLabel = "p",
            Columns = new[] { new ReportColumn { Header = "Text", Type = ReportCellType.Text } },
            Rows = new[]
            {
                new ReportRow { Values = new object?[] { "=1+1" } },
                new ReportRow { Values = new object?[] { "+A1" } },
                new ReportRow { Values = new object?[] { "@SUM" } },
                new ReportRow { Values = new object?[] { "-5" } },
                new ReportRow { Values = new object?[] { "normal" } }
            }
        };
        var path = Path.Combine(Path.GetTempPath(), $"rep-{Guid.NewGuid():N}.csv");
        try
        {
            await new ReportExportService().ExportAsync(report, ReportExportFormat.Csv, path);
            var lines = File.ReadAllLines(path);
            Assert.Equal(6, lines.Length); // header + 5 rows
            Assert.All(lines.Skip(1).Take(4), l => Assert.StartsWith("'", l));
            Assert.Equal("normal", lines[5]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExcelExport_ProducesValidWorkbook()
    {
        var report = SampleReport();
        var path = Path.Combine(Path.GetTempPath(), $"rep-{Guid.NewGuid():N}.xlsx");
        try
        {
            await new ReportExportService().ExportAsync(report, ReportExportFormat.Excel, path);
            Assert.True(File.Exists(path));
            using var zip = ZipFile.OpenRead(path);
            Assert.Contains(zip.Entries, e => e.FullName.Contains("xl/workbook.xml"));
            Assert.Contains(zip.Entries, e => e.FullName.Contains("worksheets/"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PdfExport_ProducesValidPdf()
    {
        var report = SampleReport();
        var path = Path.Combine(Path.GetTempPath(), $"rep-{Guid.NewGuid():N}.pdf");
        try
        {
            await new ReportExportService().ExportAsync(report, ReportExportFormat.Pdf, path);
            var bytes = await File.ReadAllBytesAsync(path);
            var header = Encoding.ASCII.GetString(bytes, 0, 5);
            Assert.Equal("%PDF-", header);
            var content = Encoding.ASCII.GetString(bytes);
            Assert.Contains("HIGHLAND MILK BAR", content);
            Assert.Contains("Profit & Loss", content);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ReportGeneration_IsReadOnly()
    {
        AddSale(1000m, DateTime.Now, cogs: 400m);
        var sales = await Db.Sales.CountAsync();
        var payments = await Db.Payments.CountAsync();
        var products = await Db.Products.CountAsync();

        var svc = BuildQuery();
        var period = ReportingPeriod.FromPreset(ReportingPreset.ThisMonth);
        foreach (var key in new[] { "sales.summary", "financial.pl", "inventory.stock", "customers.directory" })
            await svc.GenerateAsync(key, period);

        Assert.Equal(sales, await Db.Sales.CountAsync());
        Assert.Equal(payments, await Db.Payments.CountAsync());
        Assert.Equal(products, await Db.Products.CountAsync());
    }

    private static ReportResult SampleReport() => new()
    {
        Key = "financial.pl",
        Title = "Profit & Loss",
        Category = "Financial",
        PeriodLabel = "01 Nov 2026 – 30 Nov 2026",
        Summary = new[] { new ReportSummaryItem { Label = "Net sales", Value = "LKR 1,000.00" } },
        Columns = new[]
        {
            new ReportColumn { Header = "Line", Type = ReportCellType.Text },
            new ReportColumn { Header = "Amount", Type = ReportCellType.Currency }
        },
        Rows = new[]
        {
            new ReportRow { Values = new object?[] { "Net sales", 1000m } },
            new ReportRow { Values = new object?[] { "Gross profit", 600m } }
        }
    };
}
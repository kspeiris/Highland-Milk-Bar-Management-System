using System.Text;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class ReceiptServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private InventoryService Inventory => new(Db);
    private SalesService Sales => new(Db, Inventory, new LoyaltyService(Db));
    private ReceiptService Receipts => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<int> CompletedSaleAsync(decimal cash = 60m)
    {
        var cat = await _db.AddCategoryAsync("Cat");
        var p = await _db.AddProductAsync("R-1", "Fresh Milk", cat.Id, price: 20m, cost: 10m);
        await Db.SaveChangesAsync();

        await Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = p.Id, BatchNumber = "B1", Quantity = 10m, UnitCost = 10m, ExpiryDate = DateTime.Today.AddDays(30)
        });

        var result = await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = new List<SaleLineRequest> { new() { ProductId = p.Id, Quantity = 3m, UnitPrice = 20m } },
            Payments = new List<PaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = cash } }
        });
        return result.SaleId;
    }

    [Fact]
    public async Task Receipt_BuildsFromSale_WithTotalsAndPayments()
    {
        var saleId = await CompletedSaleAsync();

        var receipt = await Receipts.CreateForSaleAsync(saleId, ReceiptFormat.Thermal80);

        Assert.Equal(ReceiptFormat.Thermal80, receipt.Format);
        Assert.False(string.IsNullOrWhiteSpace(receipt.InvoiceNumber));
        Assert.Equal(60m, receipt.Total);
        Assert.Single(receipt.Items);
        Assert.Equal("Fresh Milk", receipt.Items[0].Name);
        Assert.Single(receipt.Payments);
        Assert.Equal("Cash", receipt.Payments[0].Method);
        Assert.Equal(0m, receipt.Change);
    }

    [Fact]
    public async Task Receipt_ByReceiptNumber_MatchesSale()
    {
        var saleId = await CompletedSaleAsync();
        var byId = await Receipts.CreateForSaleAsync(saleId, ReceiptFormat.Thermal58);

        var byNumber = await Receipts.CreateForReceiptNumberAsync(byId.InvoiceNumber, ReceiptFormat.Thermal58);
        Assert.Equal(byId.InvoiceNumber, byNumber.InvoiceNumber);
        Assert.Equal(byId.Total, byNumber.Total);
    }

    [Fact]
    public async Task Receipt_TextRender_ContainsInvoiceAndTotal()
    {
        var saleId = await CompletedSaleAsync(cash: 100m);
        var receipt = await Receipts.CreateForSaleAsync(saleId, ReceiptFormat.Thermal80);

        var text = ReceiptTextRenderer.Render(receipt);

        Assert.Contains(receipt.InvoiceNumber, text);
        Assert.Contains("TOTAL", text);
        Assert.Contains("60.00", text);
        Assert.Contains("Change", text);
        Assert.Contains("40.00", text); // change = 100 - 60
        var normalized = text.Replace("\r\n", "\n");
        Assert.All(normalized.Split('\n'), line => Assert.True(line.Length <= 48, $"Line too long: '{line}'"));
    }

    [Fact]
    public void PdfWriter_CreatesValidPdfFile()
    {
        var doc = new ReceiptDocument
        {
            Format = ReceiptFormat.Thermal80,
            OutletName = "Highland Milk Bar",
            InvoiceNumber = "R20261115-0001",
            SoldAt = DateTime.Now,
            Items = new List<ReceiptItem> { new() { Name = "Fresh Milk", Quantity = 2m, UnitPrice = 250m, LineTotal = 500m } },
            Subtotal = 500m,
            Total = 500m,
            Payments = new List<ReceiptPayment> { new() { Method = "Cash", Amount = 500m } },
            AmountPaid = 500m,
            Change = 0m,
            Footer = "Thank you!"
        };

        var path = Path.Combine(Path.GetTempPath(), $"hmb-receipt-{Guid.NewGuid():N}.pdf");
        try
        {
            ReceiptPdfWriter.Write(path, doc);

            Assert.True(File.Exists(path));
            var bytes = File.ReadAllBytes(path);
            var text = Encoding.ASCII.GetString(bytes);
            Assert.StartsWith("%PDF-1.4", text);
            Assert.Contains("%%EOF", text);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Thermal58_IsNarrowerThanThermal80()
    {
        var doc = new ReceiptDocument { Format = ReceiptFormat.Thermal58 };
        Assert.Equal(32, doc.Width);
        Assert.Equal(48, new ReceiptDocument { Format = ReceiptFormat.Thermal80 }.Width);
        Assert.Equal(80, new ReceiptDocument { Format = ReceiptFormat.A4 }.Width);
    }
}
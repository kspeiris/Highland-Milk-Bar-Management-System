using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

/// <summary>Supplier payments: full/partial, outstanding, overpayment, status (PUR-13).</summary>
public sealed class SupplierPaymentServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private PurchaseService Purchases => new(Db, new InventoryService(Db));
    private SupplierService Suppliers => new(Db);
    private SupplierPaymentService Payments => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<int> ReceivedPurchaseAsync(decimal total = 50000m, decimal unitCost = 500m, int qty = 100)
    {
        var cat = await _db.AddCategoryAsync("Cat");
        var product = await _db.AddProductAsync("FM-" + Guid.NewGuid().ToString("N")[..6], "Fresh Milk", cat.Id);
        var supplier = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Sup " + Guid.NewGuid().ToString("N")[..6] });
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = qty, UnitCost = unitCost, BatchNumber = "B-" + Guid.NewGuid().ToString("N")[..6] }]
        });
        await Purchases.ReceiveAsync(purchase.Id);
        return purchase.Id;
    }

    [Fact]
    public async Task RecordFull_SetsPaid()
    {
        var id = await ReceivedPurchaseAsync();
        var payment = await Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = id, Amount = 50000m });

        Assert.Equal(50000m, payment.Amount);
        var purchase = await Purchases.GetByIdAsync(id);
        Assert.Equal(PaymentStatus.Paid, purchase!.PaymentStatus);
        Assert.Equal(0m, purchase.Outstanding);
    }

    [Fact]
    public async Task RecordPartial_SetsPartiallyPaidAndOutstanding()
    {
        var id = await ReceivedPurchaseAsync();
        await Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = id, Amount = 20000m });

        var purchase = await Purchases.GetByIdAsync(id);
        Assert.Equal(PaymentStatus.PartiallyPaid, purchase!.PaymentStatus);
        Assert.Equal(30000m, purchase.Outstanding);
    }

    [Fact]
    public async Task MultiplePartialPayments_Accumulate()
    {
        var id = await ReceivedPurchaseAsync();
        await Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = id, Amount = 20000m });
        await Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = id, Amount = 15000m });

        var purchase = await Purchases.GetByIdAsync(id);
        Assert.Equal(35000m, purchase!.AmountPaid);
        Assert.Equal(15000m, purchase.Outstanding);
        Assert.Equal(PaymentStatus.PartiallyPaid, purchase.PaymentStatus);
        Assert.Equal(2, purchase.Payments.Count);
    }

    [Fact]
    public async Task Overpayment_Rejected()
    {
        var id = await ReceivedPurchaseAsync();
        await Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = id, Amount = 40000m });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = id, Amount = 20000m }));
    }

    [Fact]
    public async Task InvalidAmount_Rejected()
    {
        var id = await ReceivedPurchaseAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = id, Amount = 0m }));
    }

    [Fact]
    public async Task Payment_DoesNotChangeInventoryOrReceiveStatus()
    {
        var cat = await _db.AddCategoryAsync("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat.Id);
        var supplier = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Sup" });
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 50, UnitCost = 100, BatchNumber = "FM-1" }]
        });
        await Purchases.ReceiveAsync(purchase.Id);
        var stockBefore = (await Db.Products.FindAsync(product.Id))!.CurrentStock;

        await Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = purchase.Id, Amount = 2000m });

        Assert.Equal(stockBefore, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
        var reloaded = await Db.Purchases.FindAsync(purchase.Id);
        Assert.Equal(PurchaseStatus.Received, reloaded!.Status);
    }

    [Fact]
    public async Task Payment_RejectedOnDraftPurchase()
    {
        var cat = await _db.AddCategoryAsync("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat.Id);
        var supplier = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Sup" });
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 100 }]
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = purchase.Id, Amount = 100m }));
    }

    [Fact]
    public async Task Search_ReturnsPaymentHistory()
    {
        var id = await ReceivedPurchaseAsync();
        await Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = id, Amount = 10000m, Method = PaymentMethod.BankTransfer });
        var page = await Payments.SearchAsync(null, id, null, null, 1, 50);
        Assert.Single(page.Items);
        Assert.Equal(PaymentMethod.BankTransfer, page.Items[0].Method);
    }
}
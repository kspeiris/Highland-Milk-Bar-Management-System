using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

/// <summary>Purchase draft, calculation, receiving, rollback and dashboard (PUR rules).</summary>
public sealed class PurchaseServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private InventoryService Inventory => new(Db);
    private PurchaseService Purchases => new(Db, Inventory);
    private SupplierService Suppliers => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<int> Cat(string name) => (await _db.AddCategoryAsync(name)).Id;

    private async Task<SupplierDetailDto> Supplier(string name = "Highland Distributor", bool active = true)
        => await Suppliers.CreateAsync(new CreateSupplierRequest { Name = name, Code = "S-" + Guid.NewGuid().ToString("N")[..6], IsActive = active });

    [Fact]
    public async Task CreateDraft_DoesNotAffectStock()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();

        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 100, UnitCost = 180, BatchNumber = "FM-1" }]
        });

        Assert.Equal(PurchaseStatus.Draft, purchase.ReceiveStatus);
        Assert.Equal(0m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.Empty(await Db.InventoryBatches.ToListAsync());
    }

    [Fact]
    public async Task Create_ComputesSubtotalDiscountChargesGrandTotal()
    {
        var cat = await Cat("Cat");
        var milk = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var yoghurt = await _db.AddProductAsync("YG-1", "Yoghurt", cat);
        var supplier = await Supplier();

        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            DiscountAmount = 100m,
            AdditionalCharges = 250m,
            Items =
            [
                new PurchaseLineRequest { ProductId = milk.Id, Quantity = 100, UnitCost = 180 },      // 18000
                new PurchaseLineRequest { ProductId = yoghurt.Id, Quantity = 50, UnitCost = 90 }      // 4500
            ]
        });

        // subtotal 22500 − 100 + 250 = 22650
        Assert.Equal(22500m, purchase.Subtotal);
        Assert.Equal(22650m, purchase.TotalAmount);
    }

    [Fact]
    public async Task Create_RejectsInvalidQuantity()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 0, UnitCost = 10 }]
        }));
    }

    [Fact]
    public async Task Create_RejectsNegativeCost()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = -5 }]
        }));
    }

    [Fact]
    public async Task Create_RejectsInactiveSupplier()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier(active: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 10 }]
        }));
    }

    [Fact]
    public async Task Receive_CreatesBatchAndMovement()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 100, UnitCost = 180, BatchNumber = "FM-101", ExpiryDate = DateTime.Today.AddDays(10) }]
        });

        var result = await Purchases.ReceiveAsync(purchase.Id);

        Assert.Equal(1, result.BatchesCreated);
        Assert.Single(await Db.InventoryBatches.ToListAsync());
        Assert.Equal(100m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.True(await Db.StockMovements.AnyAsync(m => m.MovementType == StockMovementType.Purchase));
        var reloaded = await Purchases.GetByIdAsync(purchase.Id);
        Assert.Equal(PurchaseStatus.Received, reloaded!.ReceiveStatus);
    }

    [Fact]
    public async Task Receive_AllocatesChargesIntoBatchCost()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            AdditionalCharges = 50m,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 100, BatchNumber = "FM-1" }]
        });

        await Purchases.ReceiveAsync(purchase.Id);

        var batch = await Db.InventoryBatches.SingleAsync();
        // (10*100 + 50 allocated) / 10 = 105 landed unit cost
        Assert.Equal(105m, batch.UnitCost);
    }

    [Fact]
    public async Task Receive_RequiresBatchNumber()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 10 }]
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Purchases.ReceiveAsync(purchase.Id));
    }

    [Fact]
    public async Task Receive_RequiresExpiryWhenProductNeedsIt()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("YG-1", "Yoghurt", cat);
        product.RequiresExpiryDate = true;
        await Db.SaveChangesAsync();
        var supplier = await Supplier();
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 10, BatchNumber = "YG-1" }]
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Purchases.ReceiveAsync(purchase.Id));
    }

    [Fact]
    public async Task Receive_CannotBeDoneTwice()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 10, BatchNumber = "FM-1" }]
        });
        await Purchases.ReceiveAsync(purchase.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Purchases.ReceiveAsync(purchase.Id));
    }

    [Fact]
    public async Task Receive_FailureRollsBackEverything()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();
        // Two lines, same product, same batch number -> second batch creation fails -> rollback.
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items =
            [
                new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 10, BatchNumber = "DUP" },
                new PurchaseLineRequest { ProductId = product.Id, Quantity = 5, UnitCost = 10, BatchNumber = "DUP" }
            ]
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Purchases.ReceiveAsync(purchase.Id));

        // Read fresh from the database so the rolled-back in-memory tracking cannot mask the result.
        Db.ChangeTracker.Clear();
        Assert.Empty(await Db.InventoryBatches.ToListAsync());
        Assert.Equal(0m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.Equal(PurchaseStatus.Draft, (await Db.Purchases.FindAsync(purchase.Id))!.Status);
    }

    [Fact]
    public async Task Update_OnlyAllowedOnDraft()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 10, BatchNumber = "FM-1" }]
        });
        await Purchases.ReceiveAsync(purchase.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Purchases.UpdateAsync(new UpdatePurchaseRequest
        {
            Id = purchase.Id,
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 99, UnitCost = 10 }]
        }));
    }

    [Fact]
    public async Task Cancel_ReceivedPurchaseRejected()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 10, BatchNumber = "FM-1" }]
        });
        await Purchases.ReceiveAsync(purchase.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Purchases.CancelAsync(purchase.Id));
    }

    [Fact]
    public async Task Outstanding_ExcludesDraftPurchases()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();
        var draft = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 100, BatchNumber = "FM-1" }]
        });
        await Purchases.ReceiveAsync(draft.Id);

        var balances = await Purchases.GetOutstandingAsync(supplier.Id, onlyOverdue: false);
        Assert.Single(balances);
        Assert.Equal(1000m, balances[0].Outstanding);
    }

    [Fact]
    public async Task Dashboard_UsesRealDatabaseData()
    {
        var cat = await Cat("Cat");
        var product = await _db.AddProductAsync("FM-1", "Fresh Milk", cat);
        var supplier = await Supplier();
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 10, UnitCost = 100, BatchNumber = "FM-1" }]
        });
        await Purchases.ReceiveAsync(purchase.Id);

        var dashboard = await Purchases.GetDashboardAsync();
        Assert.Equal(1, dashboard.TotalReceivedPurchases);
        Assert.Equal(1000m, dashboard.PurchaseValue);
        Assert.Equal(1, dashboard.ActiveSuppliers);
        Assert.Single(dashboard.RecentPurchases);
    }
}
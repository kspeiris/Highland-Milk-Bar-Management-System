using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class InventoryTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private InventoryService Inventory => new(Db);
    private InventoryQueryService Queries => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<int> CategoryAsync(string name)
    {
        var c = await _db.AddCategoryAsync(name);
        return c.Id;
    }

    private async Task<Product> ProductAsync(int categoryId, string sku = "INV-1", string name = "Fresh Milk", decimal reorder = 0m, bool track = true)
    {
        var p = await _db.AddProductAsync(sku, name, categoryId, track: track, stock: 0m);
        p.ReorderLevel = reorder;
        await Db.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task Overview_ComputesPhysicalSellableExpiredAndValue()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat);
        var today = DateTime.Today;
        _db.AddBatch(p.Id, "B1", 20m, 100m, today.AddDays(-5), today.AddDays(10));
        _db.AddBatch(p.Id, "B2", 15m, 100m, today.AddDays(-2), today.AddDays(20));
        _db.AddBatch(p.Id, "B3", 10m, 100m, today.AddDays(-10), today.AddDays(-1));
        await Db.SaveChangesAsync();

        var ov = await Queries.GetOverviewAsync();
        Assert.Equal(45m, ov.TotalPhysicalStock);
        Assert.Equal(35m, ov.TotalSellableStock);
        Assert.Equal(10m, ov.ExpiredQuantity);
        Assert.Equal(4500m, ov.PhysicalStockValue);
        Assert.Equal(3500m, ov.SellableStockValue);
        Assert.Equal(1, ov.ExpiredBatches);
    }

    [Fact]
    public async Task StockOverview_SeparatesStockAndComputesStatus()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat, reorder: 40m);
        var today = DateTime.Today;
        _db.AddBatch(p.Id, "B1", 20m, 50m, today.AddDays(-5), today.AddDays(10));
        _db.AddBatch(p.Id, "B2", 15m, 50m, today.AddDays(-2), today.AddDays(20));
        _db.AddBatch(p.Id, "B3", 10m, 50m, today.AddDays(-10), today.AddDays(-1));
        await Db.SaveChangesAsync();

        var page = await Queries.GetStockAsync(new StockFilter { CategoryId = cat });
        var row = page.Items.Single();
        Assert.Equal(45m, row.PhysicalStock);
        Assert.Equal(35m, row.SellableStock);
        Assert.Equal(10m, row.ExpiredStock);
        Assert.Equal(StockStatus.LowStock, row.Status); // 35 <= 40
        Assert.Equal(5m, row.Shortfall);
    }

    [Fact]
    public async Task LowStock_UsesSellableStock()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat, reorder: 40m);
        var today = DateTime.Today;
        _db.AddBatch(p.Id, "B1", 30m, 50m, today.AddDays(-5), today.AddDays(10));
        _db.AddBatch(p.Id, "B2", 5m, 50m, today.AddDays(-2), today.AddDays(-1)); // expired
        await Db.SaveChangesAsync();

        var low = await Queries.GetLowStockAsync();
        var row = low.Single(r => r.ProductId == p.Id);
        Assert.Equal(30m, row.SellableStock); // expired excluded
        Assert.Equal(10m, row.Shortfall);
    }

    [Fact]
    public async Task NonInventoryProduct_ExcludedFromStock()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat, track: false);
        var today = DateTime.Today;
        _db.AddBatch(p.Id, "NB", 10m, 50m, today, today.AddDays(10));
        await Db.SaveChangesAsync();

        var page = await Queries.GetStockAsync(new StockFilter { CategoryId = cat });
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Batches_EnrichExpiryStatus()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat);
        var today = DateTime.Today;
        _db.AddBatch(p.Id, "OK", 10m, 50m, today.AddDays(-5), today.AddDays(60));
        _db.AddBatch(p.Id, "SOON", 5m, 50m, today.AddDays(-5), today.AddDays(3));
        _db.AddBatch(p.Id, "OLD", 2m, 50m, today.AddDays(-20), today.AddDays(-1));
        await Db.SaveChangesAsync();

        var all = await Queries.GetBatchesAsync(new BatchFilter { ProductId = p.Id });
        Assert.Equal(3, all.TotalCount);
        Assert.Contains(all.Items, b => b.BatchNumber == "SOON" && b.ExpiryStatus == "Warning");
        Assert.Contains(all.Items, b => b.BatchNumber == "OLD" && b.IsExpired);

        var near = await Queries.GetNearExpiryAsync(30);
        Assert.Contains(near, r => r.BatchNumber == "SOON");
        Assert.DoesNotContain(near, r => r.BatchNumber == "OLD");

        var expired = await Queries.GetExpiredAsync();
        Assert.Contains(expired, r => r.BatchNumber == "OLD");
        Assert.DoesNotContain(expired, r => r.BatchNumber == "SOON");
    }

    [Fact]
    public async Task Movements_ReturnLedger()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat);
        var batch = await Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = p.Id, BatchNumber = "MV-1", Quantity = 10m, UnitCost = 100m, ExpiryDate = DateTime.Today.AddDays(30)
        });

        var page = await Queries.GetMovementsAsync(new MovementFilter { ProductId = p.Id });
        Assert.Single(page.Items);
        Assert.Equal("Opening", page.Items[0].MovementType);
        Assert.Equal(batch.BatchNumber, page.Items[0].BatchNumber);
    }

    [Fact]
    public async Task Receive_RequiresExpiryForExpiryTrackedProduct()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat);
        p.RequiresExpiryDate = true;
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = p.Id, BatchNumber = "NE", Quantity = 5m, UnitCost = 10m
        }));
    }

    [Fact]
    public async Task Receive_RejectsDuplicateBatchNumber()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat);
        await Inventory.ReceiveAsync(new StockReceiptRequest { ProductId = p.Id, BatchNumber = "DUP", Quantity = 5m, UnitCost = 10m, ExpiryDate = DateTime.Today.AddDays(10) });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = p.Id, BatchNumber = "dup", Quantity = 5m, UnitCost = 10m, ExpiryDate = DateTime.Today.AddDays(10)
        }));
    }

    [Fact]
    public async Task Receive_RejectsInactiveProduct()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat);
        p.IsActive = false;
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = p.Id, BatchNumber = "X", Quantity = 5m, UnitCost = 10m
        }));
    }

    [Fact]
    public async Task Receive_RejectsNonTrackedProduct()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat, track: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = p.Id, BatchNumber = "X", Quantity = 5m, UnitCost = 10m
        }));
    }

    [Fact]
    public async Task Receive_RejectsExpiryBeforeReceived()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat);
        await Assert.ThrowsAsync<ArgumentException>(() => Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = p.Id, BatchNumber = "X", Quantity = 5m, UnitCost = 10m, ExpiryDate = DateTime.Today.AddDays(-1)
        }));
    }

    [Fact]
    public async Task Adjust_Damaged_RecordsWasteAndReducesStock()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat);
        var batch = await Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = p.Id, BatchNumber = "DMG", Quantity = 10m, UnitCost = 100m, ExpiryDate = DateTime.Today.AddDays(30)
        });

        await Inventory.AdjustAsync(new StockAdjustmentRequest
        {
            ReferenceNumber = "ADJ-DMG-1",
            Reason = "Damaged in transit",
            Lines = new List<StockAdjustmentLine>
            {
                new() { ProductId = p.Id, InventoryBatchId = batch.Id, QuantityChange = -4m, Kind = StockAdjustmentKind.Damaged }
            }
        });

        Assert.Equal(6m, (await Db.InventoryBatches.FindAsync(batch.Id))!.QuantityRemaining);
        Assert.Equal(6m, (await Db.Products.FindAsync(p.Id))!.CurrentStock);
        Assert.True(await Db.StockMovements.AnyAsync(m => m.MovementType == StockMovementType.Waste && m.ProductId == p.Id));
    }

    [Fact]
    public async Task Adjust_Expired_RecordsExpiredMovement()
    {
        var cat = await CategoryAsync("Inv Cat");
        var p = await ProductAsync(cat);
        var batch = await Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = p.Id, BatchNumber = "EXP", Quantity = 10m, UnitCost = 100m, ExpiryDate = DateTime.Today.AddDays(30)
        });

        await Inventory.AdjustAsync(new StockAdjustmentRequest
        {
            ReferenceNumber = "ADJ-EXP-1",
            Reason = "Expired",
            Lines = new List<StockAdjustmentLine>
            {
                new() { ProductId = p.Id, InventoryBatchId = batch.Id, QuantityChange = -10m, Kind = StockAdjustmentKind.Expired }
            }
        });

        Assert.Equal(0m, (await Db.InventoryBatches.FindAsync(batch.Id))!.QuantityRemaining);
        Assert.True(await Db.StockMovements.AnyAsync(m => m.MovementType == StockMovementType.Expired && m.ProductId == p.Id));
    }
}
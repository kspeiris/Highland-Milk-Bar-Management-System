using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class ProductServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private ProductService Service => new(Db);
    private int _categoryId;
    private int _bottleUnitId;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        var category = await _db.AddCategoryAsync("Product Test Category");
        _categoryId = category.Id;
        _bottleUnitId = await Db.Units.Where(u => u.Name == "Bottle").Select(u => u.Id).FirstAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    private CreateProductRequest NewRequest(string sku = "T-1", string name = "Test Milk") => new()
    {
        Sku = sku,
        Name = name,
        Barcode = null,
        CategoryId = _categoryId,
        Type = ProductType.Packaged,
        Unit = "Bottle",
        BaseUnitId = _bottleUnitId,
        CostPrice = 10m,
        SellingPrice = 20m,
        OpeningStock = 5m,
        ReorderLevel = 2m,
        TrackInventory = true,
        IsActive = true
    };

    [Fact]
    public async Task Create_ValidProduct_Persists()
    {
        var dto = await Service.CreateAsync(NewRequest());
        Assert.Equal("T-1", dto.Sku);
        Assert.Equal(5m, dto.CurrentStock);
        Assert.True(await Db.Products.AnyAsync(p => p.Sku == "T-1"));
    }

    [Fact]
    public async Task Create_MissingName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(NewRequest(name: "   ")));
    }

    [Fact]
    public async Task Create_MissingCategory_Throws()
    {
        var request = NewRequest() with { CategoryId = 99999 };
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(request));
    }

    [Fact]
    public async Task Create_DuplicateSku_Throws()
    {
        await Service.CreateAsync(NewRequest(sku: "DUP-1"));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(NewRequest(sku: "DUP-1", name: "Another")));
    }

    [Fact]
    public async Task Create_DuplicateBarcode_Throws()
    {
        await Service.CreateAsync(NewRequest(sku: "B-1") with { Barcode = "8901234567890" });
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.CreateAsync(NewRequest(sku: "B-2", name: "Other") with { Barcode = "8901234567890" }));
    }

    [Fact]
    public async Task Create_NullBarcodes_AreAllowed()
    {
        await Service.CreateAsync(NewRequest(sku: "N-1"));
        await Service.CreateAsync(NewRequest(sku: "N-2", name: "No barcode"));
        Assert.Equal(2, await Db.Products.CountAsync(p => p.Sku == "N-1" || p.Sku == "N-2"));
    }

    [Fact]
    public async Task Create_NegativePrices_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(NewRequest() with { SellingPrice = -1m }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(NewRequest() with { CostPrice = -1m }));
    }

    [Fact]
    public async Task Create_NegativeReorder_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(NewRequest() with { ReorderLevel = -1m }));
    }

    [Fact]
    public async Task Create_TrackInventoryWithoutUnit_Throws()
    {
        var request = NewRequest() with { BaseUnitId = null, Unit = "" };
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(request));
    }

    [Fact]
    public async Task Create_InactiveCategory_Throws()
    {
        var category = await _db.AddCategoryAsync("Inactive Category");
        category.IsActive = false;
        await Db.SaveChangesAsync();

        var request = NewRequest() with { CategoryId = category.Id };
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(request));
    }

    [Fact]
    public async Task Update_ChangesPrices_PreservesStock()
    {
        var created = await Service.CreateAsync(NewRequest(sku: "U-1"));
        var detail = await Service.GetByIdAsync(created.Id);
        Assert.NotNull(detail);

        await Service.UpdateAsync(new UpdateProductRequest
        {
            Id = created.Id,
            Sku = "U-1",
            Name = "Test Milk Updated",
            Barcode = null,
            CategoryId = _categoryId,
            Type = ProductType.Packaged,
            Unit = "Bottle",
            CostPrice = 12m,
            SellingPrice = 25m,
            ReorderLevel = 3m,
            TrackInventory = true,
            IsActive = true
        });

        var after = await Db.Products.FindAsync(created.Id);
        Assert.Equal("Test Milk Updated", after!.Name);
        Assert.Equal(25m, after.SellingPrice);
        Assert.Equal(5m, after.CurrentStock); // stock is untouched by product edits
    }

    [Fact]
    public async Task SetActive_DeactivatesProduct()
    {
        var created = await Service.CreateAsync(NewRequest(sku: "A-1"));
        var updated = await Service.SetActiveAsync(created.Id, false);
        Assert.False(updated.IsActive);
        Assert.False((await Db.Products.FindAsync(created.Id))!.IsActive);
    }

    [Fact]
    public async Task Delete_WhenReferenced_Throws()
    {
        var created = await Service.CreateAsync(NewRequest(sku: "R-1"));
        _db.AddBatch(created.Id, "BATCH-1", 10m, 5m, DateTime.Today, null);
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.DeleteAsync(created.Id));
        Assert.True(await Db.Products.AnyAsync(p => p.Id == created.Id));
    }

    [Fact]
    public async Task Delete_WhenUnreferenced_Succeeds()
    {
        var created = await Service.CreateAsync(NewRequest(sku: "DEL-1"));
        await Service.DeleteAsync(created.Id);
        Assert.False(await Db.Products.AnyAsync(p => p.Id == created.Id));
    }

    [Fact]
    public async Task Search_FiltersByText()
    {
        await Service.CreateAsync(NewRequest(sku: "S-1", name: "Alpha Milk"));
        await Service.CreateAsync(NewRequest(sku: "S-2", name: "Beta Curd"));

        var result = await Service.SearchAsync(new ProductFilter { Search = "Alpha" });
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Alpha Milk", result.Items.Single().Name);
    }

    [Fact]
    public async Task Search_FiltersByCategory()
    {
        var other = await _db.AddCategoryAsync("Other Category");
        await Service.CreateAsync(NewRequest(sku: "C-1", name: "In test cat"));
        await Service.CreateAsync(new CreateProductRequest
        {
            Sku = "C-2", Name = "In other cat", CategoryId = other.Id, Type = ProductType.Packaged,
            Unit = "Bottle", BaseUnitId = _bottleUnitId, CostPrice = 1m, SellingPrice = 2m, TrackInventory = true
        });

        var result = await Service.SearchAsync(new ProductFilter { CategoryId = _categoryId });
        Assert.Single(result.Items);
        Assert.Equal("In test cat", result.Items[0].Name);
    }

    [Fact]
    public async Task Search_FiltersByType()
    {
        await Service.CreateAsync(NewRequest(sku: "TY-1", name: "Ingredient X") with { Type = ProductType.Ingredient });
        await Service.CreateAsync(NewRequest(sku: "TY-2", name: "Packaged Y") with { Type = ProductType.Packaged });

        var result = await Service.SearchAsync(new ProductFilter { Type = ProductType.Ingredient });
        Assert.Single(result.Items);
        Assert.Equal("Ingredient X", result.Items[0].Name);
    }

    [Fact]
    public async Task Search_FiltersByStatus()
    {
        var active = await Service.CreateAsync(NewRequest(sku: "ST-1", name: "Active one"));
        var inactive = await Service.CreateAsync(NewRequest(sku: "ST-2", name: "Inactive one"));
        await Service.SetActiveAsync(inactive.Id, false);

        var activeResult = await Service.SearchAsync(new ProductFilter { IsActive = true });
        Assert.Contains(activeResult.Items, p => p.Sku == "ST-1");
        Assert.DoesNotContain(activeResult.Items, p => p.Sku == "ST-2");

        var inactiveResult = await Service.SearchAsync(new ProductFilter { IsActive = false });
        Assert.Contains(inactiveResult.Items, p => p.Sku == "ST-2");
        Assert.DoesNotContain(inactiveResult.Items, p => p.Sku == "ST-1");
    }

    [Fact]
    public async Task Search_Pages()
    {
        await Service.CreateAsync(NewRequest(sku: "P-1", name: "Page item 1"));
        await Service.CreateAsync(NewRequest(sku: "P-2", name: "Page item 2"));
        await Service.CreateAsync(NewRequest(sku: "P-3", name: "Page item 3"));

        var page1 = await Service.SearchAsync(new ProductFilter { Search = "Page item", Page = 1, PageSize = 2 });
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);

        var page2 = await Service.SearchAsync(new ProductFilter { Search = "Page item", Page = 2, PageSize = 2 });
        Assert.Single(page2.Items);
    }

    [Fact]
    public async Task GetStats_CountsProductsAndCategories()
    {
        var before = await Service.GetStatsAsync();
        await Service.CreateAsync(NewRequest(sku: "GS-1"));
        var after = await Service.GetStatsAsync();
        Assert.Equal(before.TotalProducts + 1, after.TotalProducts);
        Assert.Equal(before.ActiveProducts + 1, after.ActiveProducts);
        Assert.True(after.Categories >= 1);
    }
}
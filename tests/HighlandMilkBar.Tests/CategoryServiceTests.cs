using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class CategoryServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private CategoryService Service => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Create_ValidCategory_Persists()
    {
        var dto = await Service.CreateAsync(new CreateCategoryRequest { Name = "Test Category", Description = "desc" });
        Assert.Equal("Test Category", dto.Name);
        Assert.True(dto.IsActive);
        Assert.Equal("desc", dto.Description);
        Assert.True(await Db.Categories.AnyAsync(c => c.Name == "Test Category"));
    }

    [Fact]
    public async Task Create_EmptyName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(new CreateCategoryRequest { Name = "  " }));
    }

    [Fact]
    public async Task Create_DuplicateName_ThrowsCaseInsensitive()
    {
        await _db.AddCategoryAsync("Unique Cat");
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(new CreateCategoryRequest { Name = "unique cat" }));
    }

    [Fact]
    public async Task Update_ChangesNameAndDescription()
    {
        var created = await Service.CreateAsync(new CreateCategoryRequest { Name = "Before" });
        var updated = await Service.UpdateAsync(new UpdateCategoryRequest { Id = created.Id, Name = "After", Description = "new" });
        Assert.Equal("After", updated.Name);
        Assert.Equal("new", updated.Description);
    }

    [Fact]
    public async Task SetActive_DeactivatesWithoutTouchingProducts()
    {
        var category = await _db.AddCategoryAsync("Category With Products");
        var product = await _db.AddProductAsync("CAT-P-1", "Child product", category.Id);

        var updated = await Service.SetActiveAsync(category.Id, false);
        Assert.False(updated.IsActive);
        Assert.False((await Db.Categories.FindAsync(category.Id))!.IsActive);
        Assert.True((await Db.Products.FindAsync(product.Id))!.IsActive);
    }

    [Fact]
    public async Task Delete_WhenProductsExist_Throws()
    {
        var category = await _db.AddCategoryAsync("Occupied Category");
        await _db.AddProductAsync("OCC-1", "Occupant", category.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.DeleteAsync(category.Id));
        Assert.True(await Db.Categories.AnyAsync(c => c.Id == category.Id));
    }

    [Fact]
    public async Task Delete_WhenEmpty_Succeeds()
    {
        var category = await _db.AddCategoryAsync("Empty Category");
        await Service.DeleteAsync(category.Id);
        Assert.False(await Db.Categories.AnyAsync(c => c.Id == category.Id));
    }

    [Fact]
    public async Task GetAll_ReportsProductCount()
    {
        var category = await _db.AddCategoryAsync("Counted Category");
        await _db.AddProductAsync("CNT-1", "One", category.Id);
        await _db.AddProductAsync("CNT-2", "Two", category.Id);

        var all = await Service.GetAllAsync();
        var dto = all.Single(c => c.Id == category.Id);
        Assert.Equal(2, dto.ProductCount);
    }
}
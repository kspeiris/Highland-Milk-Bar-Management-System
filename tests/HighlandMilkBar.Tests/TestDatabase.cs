using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

/// <summary>
/// Provides an isolated temporary SQLite database for a single test. The schema is
/// created by applying the real EF Core migrations, so tests exercise relational
/// behaviour (foreign keys, unique constraints, transactions) rather than the InMemory provider.
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"hmb-tests-{Guid.NewGuid():N}.db");

    public AppDbContext Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        Db = new AppDbContext(options);
        await DbInitializer.InitializeAsync(Db);
    }

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public async Task<Category> AddCategoryAsync(string name)
    {
        var category = new Category { Name = name, SortOrder = 1 };
        Db.Categories.Add(category);
        await Db.SaveChangesAsync();
        return category;
    }

    public async Task<Product> AddProductAsync(
        string sku,
        string name,
        int categoryId,
        ProductType type = ProductType.Packaged,
        bool track = true,
        decimal cost = 10m,
        decimal price = 20m,
        decimal stock = 0m)
    {
        var product = new Product
        {
            Sku = sku,
            Name = name,
            CategoryId = categoryId,
            Type = type,
            Unit = "Unit",
            CostPrice = cost,
            SellingPrice = price,
            CurrentStock = stock,
            TrackInventory = track
        };
        Db.Products.Add(product);
        await Db.SaveChangesAsync();
        return product;
    }

    public InventoryBatch AddBatch(
        int productId,
        string batchNumber,
        decimal quantity,
        decimal unitCost,
        DateTime received,
        DateTime? expiry)
    {
        var batch = new InventoryBatch
        {
            ProductId = productId,
            BatchNumber = batchNumber,
            QuantityReceived = quantity,
            QuantityRemaining = quantity,
            UnitCost = unitCost,
            ReceivedAt = received,
            ExpiryDate = expiry
        };
        Db.InventoryBatches.Add(batch);
        return batch;
    }
}
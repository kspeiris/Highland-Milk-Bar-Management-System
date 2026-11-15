using HighlandMilkBar.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class MigrationCompatibilityTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"hmb-mig-{Guid.NewGuid():N}.db");
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;
        _db = new AppDbContext(options);

        // Build the schema as it existed before Step 09 (up to the Step 08 migration).
        await _db.Database.MigrateAsync("20261115120000_AddPurchaseSupplierManagement");
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        SqliteConnection.ClearAllPools();
        foreach (var p in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(p)) File.Delete(p);
    }

    [Fact]
    public async Task ApplyStep09Migration_PreservesExistingCustomerAndSale()
    {
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Customers (Name, Phone, Email, Address, IsActive, CreatedAt) " +
            "VALUES ('Legacy Customer', '0770000001', NULL, NULL, 1, '2026-01-01 00:00:00');");
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Sales (ReceiptNumber, SoldAt, CustomerId, Subtotal, DiscountAmount, TaxAmount, TotalAmount, " +
            "CostOfGoodsSold, AmountPaid, Status, CreatedAt) " +
            "VALUES ('RCP-1', '2026-01-01 00:00:00', 1, 100, 0, 0, 100, 0, 100, 0, '2026-01-01 00:00:00');");

        await _db.Database.MigrateAsync();

        var customer = await _db.Customers.SingleAsync();
        Assert.Equal("Legacy Customer", customer.Name);
        Assert.Equal("0770000001", customer.Phone);
        Assert.False(string.IsNullOrWhiteSpace(customer.CustomerCode));
        Assert.StartsWith("CUS-", customer.CustomerCode);

        var sale = await _db.Sales.SingleAsync();
        Assert.Equal("RCP-1", sale.ReceiptNumber);
        Assert.Equal(1, sale.CustomerId);

        // Loyalty ledger exists and is empty for legacy data.
        Assert.Empty(await _db.LoyaltyTransactions.ToListAsync());
    }
}
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;

namespace HighlandMilkBar.Tests;

/// <summary>Supplier CRUD, validation, deactivation and ledger (SUP rules, PUR-17).</summary>
public sealed class SupplierServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private SupplierService Suppliers => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Create_PersistsSupplier()
    {
        var s = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Ceylon Dairy", Code = "SUP-01", Phone = "+94 11 234 5678", Email = "sales@ceylon.lk" });
        Assert.True(s.Id > 0);
        Assert.Equal("Ceylon Dairy", s.Name);
        Assert.Equal("SUP-01", s.Code);
    }

    [Fact]
    public async Task Create_RequiresName()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Suppliers.CreateAsync(new CreateSupplierRequest { Name = "  " }));
    }

    [Fact]
    public async Task Create_RejectsInvalidEmail()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Suppliers.CreateAsync(new CreateSupplierRequest { Name = "X", Email = "not-an-email" }));
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "A", Code = "DUP" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Suppliers.CreateAsync(new CreateSupplierRequest { Name = "B", Code = "dup" }));
    }

    [Fact]
    public async Task Update_ChangesDetails()
    {
        var s = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Old", Code = "C1" });
        var updated = await Suppliers.UpdateAsync(new UpdateSupplierRequest { Id = s.Id, Name = "New", Code = "C1", Phone = "0112223344" });
        Assert.Equal("New", updated.Name);
        Assert.Equal("0112223344", updated.Phone);
    }

    [Fact]
    public async Task Search_FiltersByTextAndStatus()
    {
        await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Alpha Foods", IsActive = true });
        await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Beta Traders", IsActive = false });

        var all = await Suppliers.SearchAsync(new SupplierFilter());
        Assert.Equal(2, all.TotalCount);

        var active = await Suppliers.SearchAsync(new SupplierFilter { IsActive = true });
        Assert.Single(active.Items);

        var text = await Suppliers.SearchAsync(new SupplierFilter { Search = "alpha" });
        Assert.Single(text.Items);
    }

    [Fact]
    public async Task SetActive_DeactivatesWithoutDeleting()
    {
        var s = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Temp" });
        await Suppliers.SetActiveAsync(s.Id, false);
        var reloaded = await Suppliers.GetByIdAsync(s.Id);
        Assert.False(reloaded!.IsActive);
    }

    [Fact]
    public async Task Delete_RejectedWhenHistoryExists()
    {
        var supplier = await _db.AddSupplierAsync("With History");
        var cat = await _db.AddCategoryAsync("Cat");
        var product = await _db.AddProductAsync("SKU-1", "Prod", cat.Id);
        Db.Purchases.Add(new Domain.Entities.Purchase
        {
            PurchaseNumber = "PUR-00000001",
            SupplierId = supplier.Id,
            TotalAmount = 100m,
            Status = PurchaseStatus.Received
        });
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Suppliers.DeleteAsync(supplier.Id));
    }

    [Fact]
    public async Task Statement_ExcludesDraftAndCancelled_Purchases()
    {
        var supplier = await _db.AddSupplierAsync("Ledger Supplier");
        var cat = await _db.AddCategoryAsync("Cat");

        var received = new Domain.Entities.Purchase
        {
            PurchaseNumber = "PUR-00000001",
            SupplierId = supplier.Id,
            PurchaseDate = DateTime.Today.AddDays(-5),
            ReceivedAt = DateTime.Today.AddDays(-5),
            TotalAmount = 10000m,
            Status = PurchaseStatus.Received
        };
        var draft = new Domain.Entities.Purchase
        {
            PurchaseNumber = "PUR-00000002",
            SupplierId = supplier.Id,
            TotalAmount = 5000m,
            Status = PurchaseStatus.Draft
        };
        var cancelled = new Domain.Entities.Purchase
        {
            PurchaseNumber = "PUR-00000003",
            SupplierId = supplier.Id,
            TotalAmount = 7000m,
            Status = PurchaseStatus.Cancelled
        };
        Db.Purchases.AddRange(received, draft, cancelled);
        await Db.SaveChangesAsync();

        Db.SupplierPayments.Add(new Domain.Entities.SupplierPayment { PurchaseId = received.Id, Amount = 4000m, PaidAt = DateTime.Today.AddDays(-2) });
        await Db.SaveChangesAsync();

        var statement = await Suppliers.GetStatementAsync(supplier.Id, DateTime.Today.AddDays(-30), DateTime.Today);

        Assert.Equal(2, statement.Lines.Count); // one purchase debit + one payment credit
        Assert.Equal(10000m, statement.TotalDebit);
        Assert.Equal(4000m, statement.TotalCredit);
        Assert.Equal(6000m, statement.ClosingBalance);
    }

    [Fact]
    public async Task Statement_RunningBalance_IsCorrect()
    {
        var supplier = await _db.AddSupplierAsync("Runner");
        var p1 = new Domain.Entities.Purchase { PurchaseNumber = "PUR-1", SupplierId = supplier.Id, PurchaseDate = DateTime.Today.AddDays(-10), ReceivedAt = DateTime.Today.AddDays(-10), TotalAmount = 1000m, Status = PurchaseStatus.Received };
        var p2 = new Domain.Entities.Purchase { PurchaseNumber = "PUR-2", SupplierId = supplier.Id, PurchaseDate = DateTime.Today.AddDays(-3), ReceivedAt = DateTime.Today.AddDays(-3), TotalAmount = 2000m, Status = PurchaseStatus.Received };
        Db.Purchases.AddRange(p1, p2);
        await Db.SaveChangesAsync();
        Db.SupplierPayments.Add(new Domain.Entities.SupplierPayment { PurchaseId = p1.Id, Amount = 600m, PaidAt = DateTime.Today.AddDays(-8) });
        Db.SupplierPayments.Add(new Domain.Entities.SupplierPayment { PurchaseId = p2.Id, Amount = 1500m, PaidAt = DateTime.Today.AddDays(-2) });
        await Db.SaveChangesAsync();

        var statement = await Suppliers.GetStatementAsync(supplier.Id, DateTime.Today.AddDays(-30), DateTime.Today);

        Assert.Equal(1000m, statement.Lines[0].RunningBalance); // purchase
        Assert.Equal(400m, statement.Lines[1].RunningBalance);  // payment
        Assert.Equal(2400m, statement.Lines[2].RunningBalance); // second purchase
        Assert.Equal(900m, statement.Lines[3].RunningBalance);  // second payment
    }
}
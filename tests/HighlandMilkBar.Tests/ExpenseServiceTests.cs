using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class ExpenseServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private ExpenseService Service => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private async Task<int> CategoryAsync(string name = "Utilities")
    {
        var existing = await Db.ExpenseCategories.FirstOrDefaultAsync(c => c.Name == name);
        if (existing is not null) return existing.Id;
        var category = await Service.CreateCategoryAsync(new ExpenseCategoryRequest { Name = name });
        return category.Id;
    }

    [Fact]
    public async Task Create_ValidExpense_Persists()
    {
        var categoryId = await CategoryAsync();
        var created = await Service.CreateAsync(new ExpenseCreateRequest
        {
            CategoryId = categoryId,
            Description = "Electricity bill",
            Amount = 1500m,
            ExpenseDate = new DateTime(2026, 1, 15),
            PaymentMethod = PaymentMethod.BankTransfer,
            ReferenceNumber = "INV-1"
        });

        Assert.Equal("Electricity bill", created.Description);
        Assert.Equal(1500m, created.Amount);
        Assert.Equal("INV-1", created.ReferenceNumber);
        Assert.Equal("Bank transfer", created.PaymentMethodText);
        Assert.Single(Db.Expenses);
    }

    [Fact]
    public async Task Create_MissingDescription_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.CreateAsync(new ExpenseCreateRequest { Description = "  ", Amount = 100m }));
    }

    [Fact]
    public async Task Create_NonPositiveAmount_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.CreateAsync(new ExpenseCreateRequest { Description = "Zero", Amount = 0m }));
    }

    [Fact]
    public async Task Create_InvalidCategory_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service.CreateAsync(new ExpenseCreateRequest { Description = "Bad", Amount = 10m, CategoryId = 999 }));
    }

    [Fact]
    public async Task Create_WithOpenCashSession_RecordsCashMovement()
    {
        var session = _db.AddCashSession(open: true);
        var created = await Service.CreateAsync(new ExpenseCreateRequest
        {
            Description = "Petty cash",
            Amount = 200m,
            CashSessionId = session.Id
        });

        var movement = await Db.CashMovements.SingleAsync(m => m.MovementType == CashMovementType.Expense && m.ReferenceId == created.Id);
        Assert.Equal(CashDirection.Out, movement.Direction);
        Assert.Equal(200m, movement.Amount);
        Assert.Equal(session.Id, movement.CashSessionId);
    }

    [Fact]
    public async Task Create_NonCashWithOpenSession_DoesNotRecordCashMovement()
    {
        var session = _db.AddCashSession(open: true);
        await Service.CreateAsync(new ExpenseCreateRequest
        {
            Description = "Card fuel",
            Amount = 200m,
            PaymentMethod = PaymentMethod.Card,
            CashSessionId = session.Id
        });

        Assert.Empty(Db.CashMovements);
    }

    [Fact]
    public async Task Update_Amount_SyncsCashMovement()
    {
        var session = _db.AddCashSession(open: true);
        var created = await Service.CreateAsync(new ExpenseCreateRequest
        {
            Description = "Cash snack",
            Amount = 100m,
            PaymentMethod = PaymentMethod.Cash,
            CashSessionId = session.Id
        });

        await Service.UpdateAsync(new ExpenseUpdateRequest
        {
            Id = created.Id,
            Description = "Cash snack",
            Amount = 150m,
            ExpenseDate = new DateTime(2026, 1, 1),
            PaymentMethod = PaymentMethod.Cash
        });

        var movement = await Db.CashMovements.SingleAsync(m => m.MovementType == CashMovementType.Expense && m.ReferenceId == created.Id);
        Assert.Equal(150m, movement.Amount);
    }

    [Fact]
    public async Task Stats_MonthExcludesFutureDatedExpenses()
    {
        var cat = await CategoryAsync();
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        await Service.CreateAsync(new ExpenseCreateRequest { Description = "Now", Amount = 300m, CategoryId = cat, ExpenseDate = DateTime.Now });
        await Service.CreateAsync(new ExpenseCreateRequest { Description = "Future", Amount = 999m, CategoryId = cat, ExpenseDate = monthStart.AddMonths(1).AddDays(1) });

        var stats = await Service.GetStatsAsync();

        Assert.Equal(300m, stats.MonthExpenses);
    }

    [Fact]
    public async Task Create_WithClosedCashSession_Throws()
    {
        var session = _db.AddCashSession(open: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service.CreateAsync(new ExpenseCreateRequest { Description = "Closed", Amount = 50m, CashSessionId = session.Id }));
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        var created = await Service.CreateAsync(new ExpenseCreateRequest { Description = "Old", Amount = 100m });
        var updated = await Service.UpdateAsync(new ExpenseUpdateRequest
        {
            Id = created.Id,
            Description = "New",
            Amount = 250m,
            ExpenseDate = new DateTime(2026, 2, 1),
            PaymentMethod = PaymentMethod.Card
        });

        Assert.Equal("New", updated.Description);
        Assert.Equal(250m, updated.Amount);
        Assert.Equal("Card", updated.PaymentMethodText);
    }

    [Fact]
    public async Task Delete_RemovesExpenseAndCashMovement()
    {
        var session = _db.AddCashSession(open: true);
        var created = await Service.CreateAsync(new ExpenseCreateRequest { Description = "Del", Amount = 10m, CashSessionId = session.Id });

        await Service.DeleteAsync(created.Id);

        Assert.Empty(Db.Expenses);
        Assert.Empty(Db.CashMovements);
    }

    [Fact]
    public async Task Categories_DuplicateName_Rejected()
    {
        await Service.CreateCategoryAsync(new ExpenseCategoryRequest { Name = "Internet" });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service.CreateCategoryAsync(new ExpenseCategoryRequest { Name = "internet" }));
    }

    [Fact]
    public async Task Categories_SetActive_Toggles()
    {
        var category = await Service.CreateCategoryAsync(new ExpenseCategoryRequest { Name = "Temp" });
        await Service.SetCategoryActiveAsync(category.Id, false);
        Assert.False((await Service.GetCategoriesAsync()).Single(c => c.Id == category.Id).IsActive);
    }

    [Fact]
    public async Task Search_FiltersByMethodCategoryAndDate()
    {
        var cat = await CategoryAsync("Rent");
        await Service.CreateAsync(new ExpenseCreateRequest { Description = "January rent", Amount = 1000m, CategoryId = cat, PaymentMethod = PaymentMethod.BankTransfer, ExpenseDate = new DateTime(2026, 1, 10) });
        await Service.CreateAsync(new ExpenseCreateRequest { Description = "February rent", Amount = 1000m, CategoryId = cat, PaymentMethod = PaymentMethod.BankTransfer, ExpenseDate = new DateTime(2026, 2, 10) });
        await Service.CreateAsync(new ExpenseCreateRequest { Description = "Cash snack", Amount = 50m, PaymentMethod = PaymentMethod.Cash, ExpenseDate = new DateTime(2026, 1, 11) });

        var byMethod = await Service.SearchAsync(new ExpenseSearchFilter { PaymentMethod = PaymentMethod.Cash });
        Assert.Single(byMethod.Items);

        var byCategory = await Service.SearchAsync(new ExpenseSearchFilter { CategoryId = cat });
        Assert.Equal(2, byCategory.TotalCount);

        var byRange = await Service.SearchAsync(new ExpenseSearchFilter { From = new DateTime(2026, 2, 1), To = new DateTime(2026, 2, 28) });
        Assert.Single(byRange.Items);
        Assert.Equal("February rent", byRange.Items[0].Description);

        var bySearch = await Service.SearchAsync(new ExpenseSearchFilter { Search = "snack" });
        Assert.Single(bySearch.Items);
    }

    [Fact]
    public async Task Search_SortByAmount_AndPagination()
    {
        for (var i = 0; i < 20; i++)
            await Service.CreateAsync(new ExpenseCreateRequest { Description = $"E{i:D2}", Amount = 100m + i });

        var desc = await Service.SearchAsync(new ExpenseSearchFilter { Sort = ExpenseSort.Amount, Page = 1, PageSize = 15 });
        Assert.Equal(15, desc.Items.Count);
        Assert.Equal(20, desc.TotalCount);
        Assert.True(desc.Items[0].Amount >= desc.Items[^1].Amount);

        var page2 = await Service.SearchAsync(new ExpenseSearchFilter { Sort = ExpenseSort.Amount, Page = 2, PageSize = 15 });
        Assert.Equal(5, page2.Items.Count);
    }

    [Fact]
    public async Task Stats_AndBreakdown_AndReport()
    {
        var cat = await CategoryAsync("Utilities");
        await Service.CreateAsync(new ExpenseCreateRequest { Description = "Now", Amount = 300m, CategoryId = cat, ExpenseDate = DateTime.Now });
        await Service.CreateAsync(new ExpenseCreateRequest { Description = "Old", Amount = 700m, CategoryId = cat, ExpenseDate = new DateTime(2020, 1, 1) });

        var stats = await Service.GetStatsAsync();
        Assert.Equal(1000m, stats.TotalExpenses);
        Assert.Equal(300m, stats.TodayExpenses);

        var breakdown = await Service.GetCategoryBreakdownAsync(null, null);
        var utilities = breakdown.Single(b => b.CategoryName == "Utilities");
        Assert.Equal(1000m, utilities.TotalAmount);

        var report = await Service.GetReportAsync(DateTime.Today, DateTime.Today);
        Assert.Equal(300m, report.TotalAmount);
        Assert.Single(report.ByCategory);
    }

    [Fact]
    public async Task Expense_DoesNotTouchSalesOrInventory()
    {
        var categoryId = (await _db.AddCategoryAsync("Cat")).Id;
        await _db.AddProductAsync("P-1", "Milk", categoryId, track: true, price: 100m);
        var customer = await _db.AddCustomerAsync("C", "0770000001");
        _db.AddSale(customer.Id, 500m);
        var salesBefore = await Db.Sales.CountAsync();
        var productsBefore = await Db.Products.CountAsync();

        await Service.CreateAsync(new ExpenseCreateRequest { Description = "Rent", Amount = 9000m });

        Assert.Equal(salesBefore, await Db.Sales.CountAsync());
        Assert.Equal(productsBefore, await Db.Products.CountAsync());
        Assert.Equal(500m, (await Db.Sales.SingleAsync()).TotalAmount);
    }
}
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class CustomerServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private CustomerService Service => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Create_ValidCustomer_GeneratesCodeAndPersists()
    {
        var created = await Service.CreateAsync(new CustomerCreateRequest
        {
            Name = "Kavindu Perera",
            Phone = "0771234567",
            Email = "kavindu@example.com",
            Type = CustomerType.Individual
        });

        Assert.Equal("CUS-00000001", created.CustomerCode);
        Assert.True(created.IsActive);
        var stored = await Db.Customers.SingleAsync(c => c.Id == created.Id);
        Assert.Equal("0771234567", stored.Phone);
        Assert.Equal("kavindu@example.com", stored.Email);
    }

    [Fact]
    public async Task Create_GeneratesUniqueSequentialCodes()
    {
        var a = await Service.CreateAsync(new CustomerCreateRequest { Name = "A", Phone = "0770000001" });
        var b = await Service.CreateAsync(new CustomerCreateRequest { Name = "B", Phone = "0770000002" });
        Assert.NotEqual(a.CustomerCode, b.CustomerCode);
        Assert.Equal("CUS-00000002", b.CustomerCode);
    }

    [Fact]
    public async Task Create_MissingName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.CreateAsync(new CustomerCreateRequest { Name = "   ", Phone = "0770000001" }));
    }

    [Fact]
    public async Task Create_MissingPhone_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.CreateAsync(new CustomerCreateRequest { Name = "No Phone" }));
    }

    [Fact]
    public async Task Create_InvalidEmail_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.CreateAsync(new CustomerCreateRequest { Name = "Bad Email", Phone = "0770000001", Email = "not-an-email" }));
    }

    [Fact]
    public async Task Create_NormalizesPhone()
    {
        var created = await Service.CreateAsync(new CustomerCreateRequest { Name = "Spaced", Phone = "077 123 4567" });
        Assert.Equal("0771234567", created.Phone);
    }

    [Fact]
    public async Task PhoneExists_ReturnsTrueForExisting_AndFalseOtherwise()
    {
        await Service.CreateAsync(new CustomerCreateRequest { Name = "Existing", Phone = "0771112222" });
        Assert.True(await Service.PhoneExistsAsync("0771112222", null));
        Assert.True(await Service.PhoneExistsAsync("0771112222", null));
        Assert.False(await Service.PhoneExistsAsync("0719998888", null));
    }

    [Fact]
    public async Task Update_ChangesFields_PreservesCode()
    {
        var created = await Service.CreateAsync(new CustomerCreateRequest { Name = "Old Name", Phone = "0770000001" });
        var updated = await Service.UpdateAsync(new CustomerUpdateRequest
        {
            Id = created.Id,
            Name = "New Name",
            Phone = "0770000002",
            Type = CustomerType.Business,
            IsActive = true
        });

        Assert.Equal(created.CustomerCode, updated.CustomerCode);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal("0770000002", updated.Phone);
        var stored = await Db.Customers.SingleAsync(c => c.Id == created.Id);
        Assert.Equal(CustomerType.Business, stored.Type);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task SetActive_DeactivatesAndReactivates()
    {
        var created = await Service.CreateAsync(new CustomerCreateRequest { Name = "Toggle", Phone = "0770000001" });
        await Service.SetActiveAsync(created.Id, false);
        Assert.False((await Db.Customers.SingleAsync(c => c.Id == created.Id)).IsActive);
        await Service.SetActiveAsync(created.Id, true);
        Assert.True((await Db.Customers.SingleAsync(c => c.Id == created.Id)).IsActive);
    }

    [Fact]
    public async Task Delete_WithoutHistory_Succeeds()
    {
        var created = await Service.CreateAsync(new CustomerCreateRequest { Name = "Disposable", Phone = "0770000001" });
        await Service.DeleteAsync(created.Id);
        Assert.Empty(Db.Customers);
    }

    [Fact]
    public async Task Delete_WithSales_Throws()
    {
        var customer = await _db.AddCustomerAsync("Historic", "0770000001");
        _db.AddSale(customer.Id, 1000m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.DeleteAsync(customer.Id));
        Assert.Single(Db.Customers);
    }
}
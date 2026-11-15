using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Command-side customer management: registration, editing, activation and safe deletion.
/// Customers with sales, returns or loyalty history are deactivated rather than deleted.
/// </summary>
public sealed class CustomerService(AppDbContext db) : ICustomerService
{
    public async Task<CustomerDto> CreateAsync(CustomerCreateRequest request, CancellationToken cancellationToken = default)
    {
        var name = RequireName(request.Name);
        var phone = NormalizePhone(request.Phone);
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("A primary phone number is required.", nameof(request));
        if (request.Email is { } email && !IsLikelyEmail(email))
            throw new ArgumentException($"'{email}' is not a valid email address.", nameof(request));

        var code = await NextCustomerCodeAsync(cancellationToken);

        var customer = new Customer
        {
            CustomerCode = code,
            Name = name,
            Type = request.Type,
            Phone = phone,
            AlternativePhone = NormalizePhone(request.AlternativePhone),
            Email = NullIfBlank(request.Email),
            Address = NullIfBlank(request.Address),
            City = NullIfBlank(request.City),
            DateOfBirth = request.DateOfBirth,
            Notes = NullIfBlank(request.Notes),
            IsActive = request.IsActive,
            CreatedAt = DateTime.Now
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(customer);
    }

    public async Task<CustomerDto> UpdateAsync(CustomerUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Customer {request.Id} was not found.");

        var name = RequireName(request.Name);
        if (request.Email is { } email && !IsLikelyEmail(email))
            throw new ArgumentException($"'{email}' is not a valid email address.", nameof(request));

        customer.Name = name;
        customer.Type = request.Type;
        customer.Phone = NormalizePhone(request.Phone);
        customer.AlternativePhone = NormalizePhone(request.AlternativePhone);
        customer.Email = NullIfBlank(request.Email);
        customer.Address = NullIfBlank(request.Address);
        customer.City = NullIfBlank(request.City);
        customer.DateOfBirth = request.DateOfBirth;
        customer.Notes = NullIfBlank(request.Notes);
        customer.IsActive = request.IsActive;
        customer.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(customer);
    }

    public async Task SetActiveAsync(int customerId, bool isActive, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer {customerId} was not found.");

        customer.IsActive = isActive;
        customer.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer {customerId} was not found.");

        var hasSales = await db.Sales.AnyAsync(s => s.CustomerId == customerId, cancellationToken);
        var hasReturns = await db.SalesReturns.AnyAsync(r => r.CustomerId == customerId, cancellationToken);
        var hasLoyalty = await db.LoyaltyTransactions.AnyAsync(l => l.CustomerId == customerId, cancellationToken);

        if (hasSales || hasReturns || hasLoyalty)
            throw new InvalidOperationException(
                "This customer has transaction history and cannot be deleted. Deactivate the customer instead.");

        db.Customers.Remove(customer);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> PhoneExistsAsync(string phone, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePhone(phone);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        var query = db.Customers.AsNoTracking().Where(c => c.Phone == normalized);
        if (excludeId is { } id) query = query.Where(c => c.Id != id);
        return await query.AnyAsync(cancellationToken);
    }

    private async Task<string> NextCustomerCodeAsync(CancellationToken cancellationToken)
    {
        const string prefix = "CUS-";
        var codes = await db.Customers
            .Where(c => c.CustomerCode.StartsWith(prefix))
            .Select(c => c.CustomerCode)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var code in codes)
            if (int.TryParse(code.AsSpan(prefix.Length), out var value) && value > max)
                max = value;
        return $"{prefix}{max + 1:D8}";
    }

    private static string RequireName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("A customer name is required.", nameof(name));
        return trimmed;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Strips spacing/separators so the same number is stored consistently.</summary>
    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var trimmed = phone.Trim();
        var kept = new string(trimmed.Where(ch => char.IsDigit(ch) || ch == '+').ToArray());
        return kept.Length == 0 ? null : kept;
    }

    private static bool IsLikelyEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return true;
        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@');
        if (at <= 0 || at != trimmed.LastIndexOf('@')) return false;
        var domain = trimmed[(at + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
    }

    private static CustomerDto ToDto(Customer c) => new()
    {
        Id = c.Id,
        CustomerCode = c.CustomerCode,
        Name = c.Name,
        Type = c.Type,
        Phone = c.Phone,
        Email = c.Email,
        IsActive = c.IsActive
    };
}
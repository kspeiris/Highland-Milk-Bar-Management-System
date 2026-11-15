using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>Command-side operations for managing customers.</summary>
public interface ICustomerService
{
    Task<CustomerDto> CreateAsync(CustomerCreateRequest request, CancellationToken cancellationToken = default);

    Task<CustomerDto> UpdateAsync(CustomerUpdateRequest request, CancellationToken cancellationToken = default);

    Task SetActiveAsync(int customerId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Deletes a customer only when it has no sales, returns or loyalty references.</summary>
    Task DeleteAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>True when another customer (other than <paramref name="excludeId"/>) already uses the phone.</summary>
    Task<bool> PhoneExistsAsync(string phone, int? excludeId, CancellationToken cancellationToken = default);
}
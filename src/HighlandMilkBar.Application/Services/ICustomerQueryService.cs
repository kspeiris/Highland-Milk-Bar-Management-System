using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>Read-only queries for the customer listing, profile, history and analytics.</summary>
public interface ICustomerQueryService
{
    Task<PagedResult<CustomerListRowDto>> SearchAsync(CustomerSearchFilter filter, CancellationToken cancellationToken = default);

    Task<CustomerStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<CustomerProfileDto?> GetProfileAsync(int customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerPurchaseHistoryRowDto>> GetPurchaseHistoryAsync(int customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerPurchasedProductDto>> GetPurchasedProductsAsync(int customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerPaymentDto>> GetPaymentsAsync(int customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerReturnDto>> GetReturnsAsync(int customerId, CancellationToken cancellationToken = default);

    Task<CustomerAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default);
}
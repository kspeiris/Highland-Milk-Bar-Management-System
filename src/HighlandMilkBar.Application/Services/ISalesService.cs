using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

public interface ISalesService
{
    /// <summary>
    /// Completes a sale atomically: validates stock, allocates batches, deducts inventory,
    /// consumes recipe ingredients, records payments and stock movements. Rolls back fully
    /// on any failure.
    /// </summary>
    Task<SaleResult> CheckoutAsync(SaleCheckoutRequest request, CancellationToken cancellationToken = default);

    Task<SalesReturnResult> ProcessReturnAsync(SalesReturnRequest request, CancellationToken cancellationToken = default);
}
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Read-only queries for the POS sales history, sale details, receipt/reprint and the
/// optional customer picker. Kept separate from <see cref="ISalesService"/> (commands).
/// </summary>
public interface ISalesQueryService
{
    Task<PagedResult<SaleListRowDto>> SearchAsync(SaleSearchFilter filter, CancellationToken cancellationToken = default);

    Task<SaleDetailDto?> GetByIdAsync(int saleId, CancellationToken cancellationToken = default);

    Task<SaleDetailDto?> GetByReceiptNumberAsync(string receiptNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerOptionDto>> SearchCustomersAsync(string? search, CancellationToken cancellationToken = default);
}
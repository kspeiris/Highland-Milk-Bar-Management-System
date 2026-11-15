using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Builds a receipt document from a completed sale and outlet settings. Receipt generation is
/// intentionally separate from checkout: a printing failure must never roll back a sale.
/// </summary>
public interface IReceiptService
{
    Task<ReceiptDocument> CreateForSaleAsync(int saleId, ReceiptFormat format, CancellationToken cancellationToken = default);

    Task<ReceiptDocument> CreateForReceiptNumberAsync(string receiptNumber, ReceiptFormat format, CancellationToken cancellationToken = default);
}
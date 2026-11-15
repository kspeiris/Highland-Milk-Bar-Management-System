using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Records payments made to suppliers against received purchases. Recording a payment never
/// changes inventory or receiving status — only the settlement state and amount paid
/// (SUP payment rules). Overpayment beyond the outstanding amount is rejected (PUR-13).
/// </summary>
public interface ISupplierPaymentService
{
    Task<SupplierPaymentDto> RecordAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<SupplierPaymentDto>> SearchAsync(
        int? supplierId, int? purchaseId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Recomputes and persists a purchase's amount paid and payment status.</summary>
    Task RecalculatePaymentStatusAsync(int purchaseId, CancellationToken cancellationToken = default);
}
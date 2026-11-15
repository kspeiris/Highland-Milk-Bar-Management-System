using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Supplier directory management: CRUD, activate/deactivate, and the financial rollups
/// that feed the details screen. Suppliers with transaction history are deactivated, not
/// deleted, so purchase and payment records stay intact (PUR-17).
/// </summary>
public interface ISupplierService
{
    Task<PagedResult<SupplierRowDto>> SearchAsync(SupplierFilter filter, CancellationToken cancellationToken = default);

    Task<SupplierDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<SupplierDetailDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default);

    Task<SupplierDetailDto> UpdateAsync(UpdateSupplierRequest request, CancellationToken cancellationToken = default);

    /// <summary>Activates or deactivates a supplier without deleting history.</summary>
    Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to delete a supplier. Rejected when purchases or payments reference it;
    /// callers should deactivate instead.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Builds a dated supplier ledger/statement with a running balance.</summary>
    Task<SupplierStatementDto> GetStatementAsync(
        int supplierId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}
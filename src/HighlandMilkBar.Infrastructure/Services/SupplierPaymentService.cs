using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Records supplier payments against received purchases. Payment recording is transactional
/// and never touches inventory or receiving status; it only moves money and the settlement
/// state (PUR-13, supplier payment rules).
/// </summary>
public sealed class SupplierPaymentService(AppDbContext db) : ISupplierPaymentService
{
    public async Task<SupplierPaymentDto> RecordAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(request));

        var purchase = await db.Purchases
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseId, cancellationToken)
            ?? throw new InvalidOperationException($"Purchase {request.PurchaseId} was not found.");

        if (purchase.Status != PurchaseStatus.Received)
            throw new InvalidOperationException("Payments can only be recorded against a received purchase.");

        var paid = await db.SupplierPayments
            .Where(p => p.PurchaseId == purchase.Id)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var outstanding = PurchaseFinancials.Outstanding(purchase.TotalAmount, paid);
        if (request.Amount > outstanding + 0.005m)
            throw new InvalidOperationException(
                $"Payment {request.Amount:0.##} exceeds the outstanding balance {outstanding:0.##}.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var payment = new SupplierPayment
        {
            PurchaseId = purchase.Id,
            Amount = request.Amount,
            Method = request.Method,
            PaidAt = request.PaidAt,
            Reference = request.Reference,
            Notes = request.Notes
        };
        db.SupplierPayments.Add(payment);

        purchase.AmountPaid = paid + request.Amount;
        purchase.PaymentStatus = PurchaseFinancials.ResolvePaymentStatus(purchase.TotalAmount, purchase.AmountPaid);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SupplierPaymentDto
        {
            Id = payment.Id,
            PurchaseId = purchase.Id,
            PurchaseNumber = purchase.PurchaseNumber,
            SupplierId = purchase.SupplierId,
            SupplierName = purchase.Supplier?.Name ?? string.Empty,
            Amount = payment.Amount,
            Method = payment.Method,
            PaidAt = payment.PaidAt,
            Reference = payment.Reference,
            Notes = payment.Notes
        };
    }

    public async Task<PagedResult<SupplierPaymentDto>> SearchAsync(
        int? supplierId, int? purchaseId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 50 : pageSize;

        var query = db.SupplierPayments
            .AsNoTracking()
            .Include(p => p.Purchase!).ThenInclude(pu => pu.Supplier)
            .AsQueryable();

        if (supplierId.HasValue) query = query.Where(p => p.Purchase!.SupplierId == supplierId.Value);
        if (purchaseId.HasValue) query = query.Where(p => p.PurchaseId == purchaseId.Value);
        if (from.HasValue) query = query.Where(p => p.PaidAt >= from.Value);
        if (to.HasValue) query = query.Where(p => p.PaidAt < to.Value.AddDays(1));

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.PaidAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new SupplierPaymentDto
            {
                Id = p.Id,
                PurchaseId = p.PurchaseId,
                PurchaseNumber = p.Purchase!.PurchaseNumber,
                SupplierId = p.Purchase.SupplierId,
                SupplierName = p.Purchase.Supplier != null ? p.Purchase.Supplier.Name : string.Empty,
                Amount = p.Amount,
                Method = p.Method,
                PaidAt = p.PaidAt,
                Reference = p.Reference,
                Notes = p.Notes
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierPaymentDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task RecalculatePaymentStatusAsync(int purchaseId, CancellationToken cancellationToken = default)
    {
        var purchase = await db.Purchases.FirstOrDefaultAsync(p => p.Id == purchaseId, cancellationToken)
            ?? throw new InvalidOperationException($"Purchase {purchaseId} was not found.");

        var paid = await db.SupplierPayments
            .Where(p => p.PurchaseId == purchaseId)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        purchase.AmountPaid = paid;
        purchase.PaymentStatus = PurchaseFinancials.ResolvePaymentStatus(purchase.TotalAmount, paid);
        await db.SaveChangesAsync(cancellationToken);
    }
}
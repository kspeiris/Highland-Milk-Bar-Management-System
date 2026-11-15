using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Supplier directory service. Suppliers with transaction history are never hard-deleted;
/// callers deactivate instead. Financial rollups count only received purchases and allocated
/// payments so draft/cancelled purchases never affect balances (PUR-17).
/// </summary>
public sealed class SupplierService(AppDbContext db) : ISupplierService
{
    public async Task<PagedResult<SupplierRowDto>> SearchAsync(SupplierFilter filter, CancellationToken cancellationToken = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 200 : filter.PageSize;

        var query = db.Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(s =>
                EF.Functions.Like(s.Name, $"%{term}%") ||
                (s.Code != null && EF.Functions.Like(s.Code, $"%{term}%")) ||
                (s.ContactPerson != null && EF.Functions.Like(s.ContactPerson, $"%{term}%")) ||
                (s.Phone != null && EF.Functions.Like(s.Phone, $"%{term}%")));
        }
        if (filter.IsActive.HasValue) query = query.Where(s => s.IsActive == filter.IsActive.Value);

        var total = await query.CountAsync(cancellationToken);

        var pageRows = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new { s.Id, s.Code, s.Name, s.ContactPerson, s.Phone, s.Email, s.IsActive })
            .ToListAsync(cancellationToken);

        var ids = pageRows.Select(r => r.Id).ToList();
        var purchased = await PurchasedBySuppliersAsync(ids, cancellationToken);
        var paid = await PaidBySuppliersAsync(ids, cancellationToken);

        var items = pageRows.Select(r =>
        {
            var p = purchased.GetValueOrDefault(r.Id);
            var paidAmount = paid.GetValueOrDefault(r.Id);
            return new SupplierRowDto
            {
                Id = r.Id,
                Code = r.Code ?? string.Empty,
                Name = r.Name,
                ContactPerson = r.ContactPerson,
                Phone = r.Phone,
                Email = r.Email,
                IsActive = r.IsActive,
                TotalPurchased = p,
                TotalPaid = paidAmount,
                Outstanding = PurchaseFinancials.Outstanding(p, paidAmount)
            };
        }).ToList();

        return new PagedResult<SupplierRowDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<SupplierDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var s = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (s is null) return null;

        var purchased = await PurchasedBySuppliersAsync([id], cancellationToken);
        var paid = await PaidBySuppliersAsync([id], cancellationToken);

        var purchaseEntities = await db.Purchases.AsNoTracking()
            .Where(p => p.SupplierId == id && p.Status != PurchaseStatus.Cancelled)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync(cancellationToken);
        var purchases = purchaseEntities.Select(p => new SupplierPurchaseDto
        {
            PurchaseId = p.Id,
            PurchaseNumber = p.PurchaseNumber,
            InvoiceNumber = p.InvoiceNumber,
            PurchaseDate = p.PurchaseDate,
            TotalAmount = p.Status == PurchaseStatus.Received ? p.TotalAmount : 0m,
            AmountPaid = p.AmountPaid,
            Outstanding = p.Status == PurchaseStatus.Received
                ? PurchaseFinancials.Outstanding(p.TotalAmount, p.AmountPaid)
                : 0m,
            ReceiveStatus = p.Status,
            PaymentStatus = p.PaymentStatus
        }).ToList();

        var paymentEntities = await db.SupplierPayments.AsNoTracking()
            .Include(pay => pay.Purchase)
            .Where(pay => pay.Purchase != null && pay.Purchase.SupplierId == id)
            .OrderByDescending(pay => pay.PaidAt)
            .ToListAsync(cancellationToken);
        var payments = paymentEntities.Select(pay => new SupplierPaymentDto
        {
            Id = pay.Id,
            PurchaseId = pay.PurchaseId,
            PurchaseNumber = pay.Purchase != null ? pay.Purchase.PurchaseNumber : string.Empty,
            SupplierId = id,
            SupplierName = s.Name,
            Amount = pay.Amount,
            Method = pay.Method,
            PaidAt = pay.PaidAt,
            Reference = pay.Reference,
            Notes = pay.Notes
        }).ToList();

        var totalPurchased = purchased.GetValueOrDefault(id);
        var totalPaid = paid.GetValueOrDefault(id);

        return new SupplierDetailDto
        {
            Id = s.Id,
            Code = s.Code ?? string.Empty,
            Name = s.Name,
            ContactPerson = s.ContactPerson,
            Phone = s.Phone,
            Email = s.Email,
            Address = s.Address,
            BusinessRegNo = s.BusinessRegNo,
            TaxRegNo = s.TaxRegNo,
            Notes = s.Notes,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt,
            TotalPurchased = totalPurchased,
            TotalPaid = totalPaid,
            Outstanding = PurchaseFinancials.Outstanding(totalPurchased, totalPaid),
            LatestPurchaseDate = purchases.Count > 0 ? purchases[0].PurchaseDate : null,
            LatestPaymentDate = payments.Count > 0 ? payments[0].PaidAt : null,
            Purchases = purchases,
            Payments = payments
        };
    }

    public async Task<SupplierDetailDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.Name, request.Email, request.Phone, request.Code);
        var code = Normalize(request.Code);
        await EnsureCodeUniqueAsync(code, null, cancellationToken);

        var supplier = new Supplier
        {
            Name = request.Name.Trim(),
            Code = code,
            ContactPerson = Clean(request.ContactPerson),
            Phone = Clean(request.Phone),
            Email = Clean(request.Email),
            Address = Clean(request.Address),
            BusinessRegNo = Clean(request.BusinessRegNo),
            TaxRegNo = Clean(request.TaxRegNo),
            Notes = Clean(request.Notes),
            IsActive = request.IsActive,
            CreatedAt = DateTime.Now
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(supplier.Id, cancellationToken))!;
    }

    public async Task<SupplierDetailDto> UpdateAsync(UpdateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.Name, request.Email, request.Phone, request.Code);
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Supplier {request.Id} was not found.");
        var code = Normalize(request.Code);
        await EnsureCodeUniqueAsync(code, request.Id, cancellationToken);

        supplier.Name = request.Name.Trim();
        supplier.Code = code;
        supplier.ContactPerson = Clean(request.ContactPerson);
        supplier.Phone = Clean(request.Phone);
        supplier.Email = Clean(request.Email);
        supplier.Address = Clean(request.Address);
        supplier.BusinessRegNo = Clean(request.BusinessRegNo);
        supplier.TaxRegNo = Clean(request.TaxRegNo);
        supplier.Notes = Clean(request.Notes);
        supplier.IsActive = request.IsActive;
        supplier.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(supplier.Id, cancellationToken))!;
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Supplier {id} was not found.");
        supplier.IsActive = isActive;
        supplier.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Supplier {id} was not found.");

        var hasPurchases = await db.Purchases.AnyAsync(p => p.SupplierId == id, cancellationToken);
        var hasPayments = await db.SupplierPayments.AnyAsync(p => p.Purchase != null && p.Purchase.SupplierId == id, cancellationToken);
        if (hasPurchases || hasPayments)
            throw new InvalidOperationException(
                "This supplier has purchase or payment history and cannot be deleted. Deactivate it instead.");

        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SupplierStatementDto> GetStatementAsync(
        int supplierId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken)
            ?? throw new InvalidOperationException($"Supplier {supplierId} was not found.");

        var fromDate = (from ?? DateTime.Today.AddYears(-100)).Date;
        var toDate = (to ?? DateTime.Today).Date;

        var receivedPurchases = await db.Purchases.AsNoTracking()
            .Where(p => p.SupplierId == supplierId && p.Status == PurchaseStatus.Received)
            .Select(p => new { p.Id, p.PurchaseNumber, p.InvoiceNumber, p.PurchaseDate, p.ReceivedAt, p.TotalAmount })
            .ToListAsync(cancellationToken);

        var paymentEntities = await db.SupplierPayments.AsNoTracking()
            .Include(pay => pay.Purchase)
            .Where(pay => pay.Purchase != null && pay.Purchase.SupplierId == supplierId)
            .ToListAsync(cancellationToken);
        var payments = paymentEntities
            .Select(pay => new { pay.Id, pay.PaidAt, pay.Amount, pay.Reference, pay.Method, PurchaseNumber = pay.Purchase != null ? pay.Purchase.PurchaseNumber : string.Empty })
            .ToList();

        // Opening balance = received purchases − payments strictly before the period start.
        var opening = 0m;
        foreach (var p in receivedPurchases)
        {
            var d = (p.ReceivedAt ?? p.PurchaseDate).Date;
            if (d < fromDate) opening += p.TotalAmount;
        }
        foreach (var pay in payments)
            if (pay.PaidAt.Date < fromDate) opening -= pay.Amount;

        var entries = new List<(DateTime Date, int Sort, StatementLineDto Line)>();

        foreach (var p in receivedPurchases)
        {
            var d = (p.ReceivedAt ?? p.PurchaseDate).Date;
            if (d < fromDate || d > toDate) continue;
            entries.Add((d, 0, new StatementLineDto
            {
                Date = d,
                Reference = p.PurchaseNumber,
                Type = "Purchase",
                Description = string.IsNullOrWhiteSpace(p.InvoiceNumber)
                    ? "Goods received"
                    : $"Goods received (invoice {p.InvoiceNumber})",
                Debit = p.TotalAmount
            }));
        }

        foreach (var pay in payments)
        {
            if (pay.PaidAt.Date < fromDate || pay.PaidAt.Date > toDate) continue;
            entries.Add((pay.PaidAt.Date, 1, new StatementLineDto
            {
                Date = pay.PaidAt.Date,
                Reference = string.IsNullOrWhiteSpace(pay.Reference) ? pay.PurchaseNumber : pay.Reference,
                Type = "Payment",
                Description = $"{pay.Method} payment for {pay.PurchaseNumber}",
                Credit = pay.Amount
            }));
        }

        var running = opening;
        var totalDebit = 0m;
        var totalCredit = 0m;
        var lines = entries
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Sort)
            .Select(e =>
            {
                running += e.Line.Debit - e.Line.Credit;
                totalDebit += e.Line.Debit;
                totalCredit += e.Line.Credit;
                return new StatementLineDto
                {
                    Date = e.Line.Date,
                    Reference = e.Line.Reference,
                    Type = e.Line.Type,
                    Description = e.Line.Description,
                    Debit = e.Line.Debit,
                    Credit = e.Line.Credit,
                    RunningBalance = running
                };
            })
            .ToList();

        return new SupplierStatementDto
        {
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            From = fromDate,
            To = toDate,
            OpeningBalance = opening,
            Lines = lines,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            ClosingBalance = running
        };
    }

    // ---------- helpers ----------

    private async Task<Dictionary<int, decimal>> PurchasedBySuppliersAsync(
        IReadOnlyCollection<int> supplierIds, CancellationToken cancellationToken)
    {
        if (supplierIds.Count == 0) return new();
        var ids = supplierIds.ToList();
        var rows = await db.Purchases.AsNoTracking()
            .Where(p => p.SupplierId != null && ids.Contains(p.SupplierId.Value) && p.Status == PurchaseStatus.Received)
            .Select(p => new { SupplierId = p.SupplierId, p.TotalAmount })
            .ToListAsync(cancellationToken);
        return rows
            .Where(r => r.SupplierId != null)
            .GroupBy(r => r.SupplierId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount));
    }

    private async Task<Dictionary<int, decimal>> PaidBySuppliersAsync(
        IReadOnlyCollection<int> supplierIds, CancellationToken cancellationToken)
    {
        if (supplierIds.Count == 0) return new();
        var ids = supplierIds.ToList();
        var rows = await db.SupplierPayments.AsNoTracking()
            .Include(pay => pay.Purchase)
            .Where(pay => pay.Purchase != null && pay.Purchase.SupplierId != null && ids.Contains(pay.Purchase.SupplierId.Value))
            .Select(pay => new { SupplierId = pay.Purchase != null ? pay.Purchase.SupplierId : null, pay.Amount })
            .ToListAsync(cancellationToken);
        return rows
            .Where(r => r.SupplierId != null)
            .GroupBy(r => r.SupplierId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
    }

    private async Task EnsureCodeUniqueAsync(string? code, int? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(code)) return;
        var clash = await db.Suppliers.AnyAsync(
            s => s.Code != null && s.Code.ToLower() == code && (excludeId == null || s.Id != excludeId.Value),
            cancellationToken);
        if (clash) throw new InvalidOperationException($"Supplier code '{code}' is already in use.");
    }

    private static void Validate(string name, string? email, string? phone, string? code)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Supplier name is required.", nameof(name));
        if (name.Trim().Length > 200)
            throw new ArgumentException("Supplier name must be 200 characters or fewer.", nameof(name));
        if (!string.IsNullOrWhiteSpace(code) && code.Trim().Length > 64)
            throw new ArgumentException("Supplier code must be 64 characters or fewer.", nameof(code));
        if (!string.IsNullOrWhiteSpace(email) && !IsLikelyEmail(email.Trim()))
            throw new ArgumentException("A valid email address is required.", nameof(email));
        // Telephone validation is intentionally permissive so legitimate local formats pass.
        if (!string.IsNullOrWhiteSpace(phone) && phone.Any(c => !char.IsDigit(c) && c is not ('+' or '-' or ' ' or '(' or ')' or '/')))
            throw new ArgumentException("Phone number contains invalid characters.", nameof(phone));
    }

    private static bool IsLikelyEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0 || at >= email.Length - 1) return false;
        var dot = email.IndexOf('.', at);
        return dot > at + 1 && dot < email.Length - 1 && !email.Contains(' ');
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Normalize(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim();
}
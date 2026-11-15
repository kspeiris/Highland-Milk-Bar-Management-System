using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Read-only customer queries: listing with DB-side filter/sort/paging, profile statistics,
/// purchase history, purchased products, payments/returns and analytics/segmentation.
/// Completed sales are those not <see cref="SaleStatus.Voided"/>; returns are tracked
/// separately rather than netted into spending.
/// </summary>
public sealed class CustomerQueryService(AppDbContext db) : ICustomerQueryService
{
    private sealed class CustomerAggRow
    {
        public int Id { get; init; }
        public string CustomerCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public CustomerType Type { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public bool IsActive { get; init; }
        public int Purchases { get; init; }
        public decimal Spending { get; init; }
        public DateTime? LastPurchase { get; init; }
        public DateTime? FirstPurchase { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    public async Task<PagedResult<CustomerListRowDto>> SearchAsync(CustomerSearchFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new CustomerSearchFilter();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 200 ? 15 : filter.PageSize;

        var customers = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var like = $"%{EscapeLike(filter.Search)}%";
            customers = customers.Where(c => EF.Functions.Like(c.CustomerCode, like)
                || EF.Functions.Like(c.Name, like)
                || (c.Phone != null && EF.Functions.Like(c.Phone, like))
                || (c.Email != null && EF.Functions.Like(c.Email, like)));
        }
        if (filter.Type is { } type) customers = customers.Where(c => c.Type == type);
        if (filter.IsActive is { } active) customers = customers.Where(c => c.IsActive == active);

        var sales = db.Sales.AsNoTracking()
            .Where(s => s.CustomerId != null && s.Status != SaleStatus.Voided);

        var projected = customers.Select(c => new CustomerAggRow
        {
            Id = c.Id,
            CustomerCode = c.CustomerCode,
            Name = c.Name,
            Type = c.Type,
            Phone = c.Phone,
            Email = c.Email,
            IsActive = c.IsActive,
            Purchases = sales.Count(s => s.CustomerId == c.Id),
            Spending = sales.Where(s => s.CustomerId == c.Id).Sum(s => s.TotalAmount),
            LastPurchase = sales.Where(s => s.CustomerId == c.Id).Max(s => (DateTime?)s.SoldAt),
            FirstPurchase = sales.Where(s => s.CustomerId == c.Id).Min(s => (DateTime?)s.SoldAt),
            CreatedAt = c.CreatedAt
        });

        var total = await customers.CountAsync(cancellationToken);

        var ordered = filter.Sort switch
        {
            CustomerSort.Spending => projected.OrderByDescending(r => r.Spending).ThenBy(r => r.Name),
            CustomerSort.LastPurchase => projected.OrderByDescending(r => r.LastPurchase).ThenBy(r => r.Name),
            _ => projected.OrderBy(r => r.Name)
        };

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new CustomerListRowDto
        {
            Id = r.Id,
            CustomerCode = r.CustomerCode,
            Name = r.Name,
            Type = r.Type,
            TypeText = FormatType(r.Type),
            Phone = r.Phone,
            Email = r.Email,
            TotalPurchases = r.Purchases,
            TotalSpending = r.Spending,
            LastPurchase = r.LastPurchase,
            IsActive = r.IsActive,
            StatusText = r.IsActive ? "Active" : "Inactive"
        }).ToList();

        return new PagedResult<CustomerListRowDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomerStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        var total = await db.Customers.CountAsync(cancellationToken);
        var active = await db.Customers.CountAsync(c => c.IsActive, cancellationToken);
        var newThisMonth = await db.Customers.CountAsync(c => c.CreatedAt >= monthStart, cancellationToken);

        var completedSales = db.Sales.AsNoTracking()
            .Where(s => s.CustomerId != null && s.Status != SaleStatus.Voided);

        var repeat = await completedSales
            .GroupBy(s => s.CustomerId!.Value)
            .CountAsync(g => g.Count() >= 2, cancellationToken);

        var totalSales = await completedSales.SumAsync(s => s.TotalAmount, cancellationToken);

        return new CustomerStatsDto
        {
            TotalCustomers = total,
            ActiveCustomers = active,
            NewThisMonth = newThisMonth,
            RepeatCustomers = repeat,
            TotalCustomerSales = totalSales
        };
    }

    public async Task<CustomerProfileDto?> GetProfileAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null) return null;

        var agg = await LoadSingleAggAsync(customerId, cancellationToken);

        return new CustomerProfileDto
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            Name = customer.Name,
            Type = customer.Type,
            TypeText = FormatType(customer.Type),
            Phone = customer.Phone,
            AlternativePhone = customer.AlternativePhone,
            Email = customer.Email,
            Address = customer.Address,
            City = customer.City,
            DateOfBirth = customer.DateOfBirth,
            Notes = customer.Notes,
            IsActive = customer.IsActive,
            StatusText = customer.IsActive ? "Active" : "Inactive",
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt,
            TotalPurchases = agg.Purchases,
            TotalSpending = agg.Spending,
            AveragePurchaseValue = agg.Purchases == 0 ? 0m : agg.Spending / agg.Purchases,
            LastPurchase = agg.LastPurchase,
            TotalReturns = await SumReturnsAsync(customerId, cancellationToken),
            PointsBalance = await SumLoyaltyAsync(customerId, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<CustomerPurchaseHistoryRowDto>> GetPurchaseHistoryAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.CustomerId == customerId && s.Status != SaleStatus.Voided)
            .OrderByDescending(s => s.SoldAt)
            .ThenByDescending(s => s.Id)
            .Select(s => new { s.Id, s.ReceiptNumber, s.SoldAt, s.TotalAmount, s.Status })
            .ToListAsync(cancellationToken);

        var ids = sales.Select(s => s.Id).ToList();
        var methods = await db.Payments.AsNoTracking()
            .Where(p => ids.Contains(p.SaleId))
            .Select(p => new { p.SaleId, p.Method })
            .ToListAsync(cancellationToken);

        return sales.Select(s => new CustomerPurchaseHistoryRowDto
        {
            SaleId = s.Id,
            ReceiptNumber = s.ReceiptNumber,
            SoldAt = s.SoldAt,
            TotalAmount = s.TotalAmount,
            PaymentMethodText = string.Join(", ",
                methods.Where(m => m.SaleId == s.Id).Select(m => FormatPaymentMethod(m.Method)).Distinct()),
            Status = s.Status,
            StatusText = FormatStatus(s.Status)
        }).ToList();
    }

    public async Task<IReadOnlyList<CustomerPurchasedProductDto>> GetPurchasedProductsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var rows = await db.SaleItems.AsNoTracking()
            .Where(i => i.Sale!.CustomerId == customerId && i.Sale.Status != SaleStatus.Voided)
            .GroupBy(i => new { i.ProductId, i.ItemNameSnapshot })
            .Select(g => new
            {
                ProductId = g.Key.ProductId,
                Name = g.Key.ItemNameSnapshot,
                Quantity = g.Sum(x => x.Quantity),
                Spent = g.Sum(x => x.LineTotal),
                Last = g.Max(x => x.Sale!.SoldAt)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CustomerPurchasedProductDto
            {
                ProductId = r.ProductId,
                ProductName = r.Name,
                TotalQuantity = r.Quantity,
                TotalSpent = r.Spent,
                LastPurchased = r.Last
            })
            .OrderByDescending(r => r.LastPurchased)
            .ToList();
    }

    public async Task<IReadOnlyList<CustomerPaymentDto>> GetPaymentsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        return await db.Payments.AsNoTracking()
            .Where(p => p.Sale!.CustomerId == customerId && p.Sale.Status != SaleStatus.Voided)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new CustomerPaymentDto
            {
                ReceiptNumber = p.Sale!.ReceiptNumber,
                PaidAt = p.PaidAt,
                MethodText = FormatPaymentMethod(p.Method),
                Amount = p.Amount,
                Reference = p.Reference
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerReturnDto>> GetReturnsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        return await db.SalesReturns.AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.Status != SalesReturnStatus.Voided)
            .OrderByDescending(r => r.ReturnedAt)
            .Select(r => new CustomerReturnDto
            {
                ReturnNumber = r.ReturnNumber,
                ReceiptNumber = r.Sale!.ReceiptNumber,
                ReturnedAt = r.ReturnedAt,
                TotalAmount = r.TotalAmount,
                StatusText = FormatStatus(r.Status)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var newCutoff = now.AddDays(-30);
        var inactiveCutoff = now.AddDays(-90);

        var rows = await LoadAllAggsAsync(cancellationToken);

        var totalCustomers = rows.Count;
        var activeCustomers = rows.Count(r => r.IsActive);
        var newThisMonth = rows.Count(r => r.CreatedAt >= monthStart);
        var repeatCustomers = rows.Count(r => r.Purchases >= 2);
        var totalCustomerSales = await db.Sales.AsNoTracking()
            .Where(s => s.CustomerId != null && s.Status != SaleStatus.Voided)
            .SumAsync(s => s.TotalAmount, cancellationToken);

        var avgSpending = totalCustomers == 0 ? 0m : rows.Sum(r => r.Spending) / totalCustomers;

        var topBySpending = rows
            .Where(r => r.Purchases > 0)
            .OrderByDescending(r => r.Spending)
            .ThenByDescending(r => r.Purchases)
            .Take(10)
            .Select(ToRank)
            .ToList();

        var topByFrequency = rows
            .Where(r => r.Purchases > 0)
            .OrderByDescending(r => r.Purchases)
            .ThenByDescending(r => r.Spending)
            .Take(10)
            .Select(ToRank)
            .ToList();

        var inactive = rows
            .Where(r => r.Purchases > 0 && (r.LastPurchase ?? r.FirstPurchase ?? now) < inactiveCutoff)
            .OrderByDescending(r => r.LastPurchase)
            .Take(10)
            .Select(ToRank)
            .ToList();

        // Configurable thresholds (AppSettings overrides, else defaults).
        var thresholds = await LoadThresholdsAsync(cancellationToken);

        var segmentCounts = new List<CustomerSegmentCountDto>();
        foreach (var segment in new[]
                 {
                     CustomerSegment.New, CustomerSegment.Regular, CustomerSegment.HighValue,
                     CustomerSegment.Inactive, CustomerSegment.OneTime
                 })
        {
            var count = rows.Count(r => GetSegments(r, now, newCutoff, inactiveCutoff, thresholds.HighValueThreshold).Contains(segment));
            segmentCounts.Add(new CustomerSegmentCountDto
            {
                Segment = segment,
                SegmentText = FormatSegment(segment),
                Count = count
            });
        }

        return new CustomerAnalyticsDto
        {
            TotalCustomers = totalCustomers,
            ActiveCustomers = activeCustomers,
            NewThisMonth = newThisMonth,
            RepeatCustomers = repeatCustomers,
            TotalCustomerSales = totalCustomerSales,
            AverageCustomerSpending = avgSpending,
            TopCustomersBySpending = topBySpending,
            TopCustomersByFrequency = topByFrequency,
            InactiveCustomers = inactive,
            SegmentCounts = segmentCounts,
            NewCustomerDays = 30,
            RegularMinPurchases = thresholds.RegularMinPurchases,
            InactiveDays = 90,
            HighValueThreshold = thresholds.HighValueThreshold
        };
    }

    private async Task<CustomerAggRow> LoadSingleAggAsync(int customerId, CancellationToken cancellationToken)
    {
        return await db.Customers.AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new CustomerAggRow
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                Name = c.Name,
                Type = c.Type,
                Phone = c.Phone,
                Email = c.Email,
                IsActive = c.IsActive,
                Purchases = db.Sales.Count(s => s.CustomerId == c.Id && s.Status != SaleStatus.Voided),
                Spending = db.Sales.Where(s => s.CustomerId == c.Id && s.Status != SaleStatus.Voided).Sum(s => s.TotalAmount),
                LastPurchase = db.Sales.Where(s => s.CustomerId == c.Id && s.Status != SaleStatus.Voided).Max(s => (DateTime?)s.SoldAt),
                FirstPurchase = db.Sales.Where(s => s.CustomerId == c.Id && s.Status != SaleStatus.Voided).Min(s => (DateTime?)s.SoldAt),
                CreatedAt = c.CreatedAt
            })
            .FirstAsync(cancellationToken);
    }

    private async Task<List<CustomerAggRow>> LoadAllAggsAsync(CancellationToken cancellationToken)
    {
        // Analytics inherently spans all customers; each row carries only its aggregates
        // (never the underlying sales), so this does not load every sale into memory.
        return await db.Customers.AsNoTracking()
            .Select(c => new CustomerAggRow
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                Name = c.Name,
                Type = c.Type,
                Phone = c.Phone,
                Email = c.Email,
                IsActive = c.IsActive,
                Purchases = db.Sales.Count(s => s.CustomerId == c.Id && s.Status != SaleStatus.Voided),
                Spending = db.Sales.Where(s => s.CustomerId == c.Id && s.Status != SaleStatus.Voided).Sum(s => s.TotalAmount),
                LastPurchase = db.Sales.Where(s => s.CustomerId == c.Id && s.Status != SaleStatus.Voided).Max(s => (DateTime?)s.SoldAt),
                FirstPurchase = db.Sales.Where(s => s.CustomerId == c.Id && s.Status != SaleStatus.Voided).Min(s => (DateTime?)s.SoldAt),
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<decimal> SumReturnsAsync(int customerId, CancellationToken cancellationToken)
    {
        return await db.SalesReturns.AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.Status != SalesReturnStatus.Voided)
            .SumAsync(r => r.TotalAmount, cancellationToken);
    }

    private async Task<int> SumLoyaltyAsync(int customerId, CancellationToken cancellationToken)
    {
        return await db.LoyaltyTransactions.AsNoTracking()
            .Where(l => l.CustomerId == customerId)
            .SumAsync(l => l.Points, cancellationToken);
    }

    private async Task<(decimal HighValueThreshold, int RegularMinPurchases)> LoadThresholdsAsync(CancellationToken cancellationToken)
    {
        var settings = await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == "customer.high_value_threshold" || s.Key == "customer.regular_min_purchases")
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        var highValue = decimal.TryParse(
            settings.GetValueOrDefault("customer.high_value_threshold"),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var hv) ? hv : 50000m;
        var regular = int.TryParse(settings.GetValueOrDefault("customer.regular_min_purchases"), out var rp) ? rp : 5;

        return (highValue, regular);
    }

    private static IReadOnlyList<CustomerSegment> GetSegments(
        CustomerAggRow r, DateTime now, DateTime newCutoff, DateTime inactiveCutoff, decimal highValueThreshold)
    {
        if (r.Purchases <= 0) return Array.Empty<CustomerSegment>();

        var segments = new List<CustomerSegment>();

        if (r.FirstPurchase is { } first && first >= newCutoff) segments.Add(CustomerSegment.New);
        if (r.Purchases >= 5) segments.Add(CustomerSegment.Regular);
        if (r.Spending > highValueThreshold) segments.Add(CustomerSegment.HighValue);
        var last = r.LastPurchase ?? r.FirstPurchase;
        if (last is { } lastDate && lastDate < inactiveCutoff) segments.Add(CustomerSegment.Inactive);
        if (r.Purchases == 1) segments.Add(CustomerSegment.OneTime);

        return segments;
    }

    private static CustomerRankDto ToRank(CustomerAggRow r) => new()
    {
        Id = r.Id,
        CustomerCode = r.CustomerCode,
        Name = r.Name,
        TotalPurchases = r.Purchases,
        TotalSpending = r.Spending,
        LastPurchase = r.LastPurchase
    };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string FormatType(CustomerType type) => type switch
    {
        CustomerType.Individual => "Individual",
        CustomerType.Business => "Business",
        _ => type.ToString()
    };

    private static string FormatStatus(SaleStatus status) => status switch
    {
        SaleStatus.Completed => "Completed",
        SaleStatus.Voided => "Voided",
        SaleStatus.PartiallyReturned => "Partially Returned",
        SaleStatus.Returned => "Returned",
        _ => status.ToString()
    };

    private static string FormatStatus(SalesReturnStatus status) => status switch
    {
        SalesReturnStatus.Completed => "Completed",
        SalesReturnStatus.Voided => "Voided",
        _ => status.ToString()
    };

    private static string FormatPaymentMethod(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Card => "Card",
        PaymentMethod.Cheque => "Cheque",
        _ => method.ToString()
    };

    private static string FormatSegment(CustomerSegment segment) => segment switch
    {
        CustomerSegment.New => "New Customer",
        CustomerSegment.Regular => "Regular Customer",
        CustomerSegment.HighValue => "High-Value Customer",
        CustomerSegment.Inactive => "Inactive Customer",
        CustomerSegment.OneTime => "One-Time Customer",
        _ => segment.ToString()
    };
}
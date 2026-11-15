using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

/// <summary>Filter for the supplier directory list.</summary>
public sealed class SupplierFilter
{
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 200;
}

/// <summary>One supplier row in the directory listing.</summary>
public sealed class SupplierRowDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ContactPerson { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsActive { get; init; }

    /// <summary>Value of received purchases for this supplier.</summary>
    public decimal TotalPurchased { get; init; }

    /// <summary>Total allocated payments to this supplier.</summary>
    public decimal TotalPaid { get; init; }

    /// <summary>Received purchases − allocated payments.</summary>
    public decimal Outstanding { get; init; }
}

/// <summary>Request to create a supplier.</summary>
public sealed class CreateSupplierRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? ContactPerson { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? BusinessRegNo { get; init; }
    public string? TaxRegNo { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>Request to update an existing supplier.</summary>
public sealed class UpdateSupplierRequest
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? ContactPerson { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? BusinessRegNo { get; init; }
    public string? TaxRegNo { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>Full supplier record for the details screen.</summary>
public sealed class SupplierDetailDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ContactPerson { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? BusinessRegNo { get; init; }
    public string? TaxRegNo { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }

    public decimal TotalPurchased { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal Outstanding { get; init; }
    public DateTime? LatestPurchaseDate { get; init; }
    public DateTime? LatestPaymentDate { get; init; }

    public IReadOnlyList<SupplierPurchaseDto> Purchases { get; init; } = Array.Empty<SupplierPurchaseDto>();
    public IReadOnlyList<SupplierPaymentDto> Payments { get; init; } = Array.Empty<SupplierPaymentDto>();
}

/// <summary>A purchase line shown in a supplier's history.</summary>
public sealed class SupplierPurchaseDto
{
    public int PurchaseId { get; init; }
    public string PurchaseNumber { get; init; } = string.Empty;
    public string? InvoiceNumber { get; init; }
    public DateTime PurchaseDate { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal Outstanding { get; init; }
    public PurchaseStatus ReceiveStatus { get; init; }
    public PaymentStatus PaymentStatus { get; init; }
}

/// <summary>One line of a supplier statement.</summary>
public sealed class StatementLineDto
{
    public DateTime Date { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>Amount the supplier is owed increases (a received purchase).</summary>
    public decimal Debit { get; init; }

    /// <summary>Amount paid to the supplier (reduces the balance).</summary>
    public decimal Credit { get; init; }

    /// <summary>Running balance after this line.</summary>
    public decimal RunningBalance { get; init; }
}

/// <summary>A supplier ledger/statement for a date range.</summary>
public sealed class SupplierStatementDto
{
    public int SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public DateTime From { get; init; }
    public DateTime To { get; init; }

    /// <summary>Opening balance carried into the period (received purchases − payments before From).</summary>
    public decimal OpeningBalance { get; init; }

    public IReadOnlyList<StatementLineDto> Lines { get; init; } = Array.Empty<StatementLineDto>();

    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    public decimal ClosingBalance { get; init; }
}
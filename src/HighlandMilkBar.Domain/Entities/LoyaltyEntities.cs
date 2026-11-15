using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Domain.Entities;

/// <summary>
/// Append-only loyalty ledger. Every points change is recorded here; the balance is
/// never overwritten in place. A positive value awards points, a negative value
/// reverses (or redeems) them.
/// </summary>
public class LoyaltyTransaction
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>The sale that earned these points, when applicable.</summary>
    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }

    /// <summary>The return that reversed these points, when applicable.</summary>
    public int? SalesReturnId { get; set; }
    public SalesReturn? SalesReturn { get; set; }

    public LoyaltyTransactionType Type { get; set; }

    /// <summary>Signed points delta (positive = earned/adjusted up, negative = reversed/redeemed).</summary>
    public int Points { get; set; }

    /// <summary>Customer balance after this entry was applied.</summary>
    public int BalanceAfter { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.Now;
    public string? Description { get; set; }

    /// <summary>Human reference such as the receipt number, for traceability.</summary>
    public string? Reference { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.Common;

/// <summary>
/// Pure, side-effect-free purchase and supplier-payment calculations. Keeping these here
/// makes the money rules testable in isolation and guarantees the WPF layer, services and
/// tests all agree on the same arithmetic (PUR-04/05/13, supplier balances).
/// </summary>
public static class PurchaseFinancials
{
    /// <summary>Line subtotal = quantity × unit cost.</summary>
    public static decimal LineSubtotal(decimal quantity, decimal unitCost) => quantity * unitCost;

    /// <summary>Line total = quantity × unit cost − line discount (never negative).</summary>
    public static decimal LineTotal(decimal quantity, decimal unitCost, decimal lineDiscount)
    {
        var gross = quantity * unitCost;
        var net = gross - lineDiscount;
        return net < 0 ? 0 : net;
    }

    /// <summary>Subtotal = sum of line totals.</summary>
    public static decimal Subtotal(IEnumerable<decimal> lineTotals) => lineTotals.Sum();

    /// <summary>
    /// Grand total = subtotal − purchase discount + additional charges (never negative).
    /// </summary>
    public static decimal GrandTotal(decimal subtotal, decimal purchaseDiscount, decimal additionalCharges)
    {
        var total = subtotal - purchaseDiscount + additionalCharges;
        return total < 0 ? 0 : total;
    }

    /// <summary>Outstanding = total − amount paid (never negative).</summary>
    public static decimal Outstanding(decimal total, decimal amountPaid)
    {
        var outstanding = total - amountPaid;
        return outstanding < 0 ? 0 : outstanding;
    }

    /// <summary>
    /// Derives the settlement state from the total and amount paid. A purchase with a
    /// zero total is treated as Paid (nothing to settle).
    /// </summary>
    public static PaymentStatus ResolvePaymentStatus(decimal total, decimal amountPaid)
    {
        if (amountPaid <= 0) return PaymentStatus.Unpaid;
        if (total <= 0 || amountPaid >= total) return PaymentStatus.Paid;
        return PaymentStatus.PartiallyPaid;
    }

    /// <summary>
    /// Proportional allocation of a purchase-level surcharge (e.g. additional charges)
    /// across a line, so batch unit costs stay accurate without double counting. The last
    /// line absorbs rounding remainder via <see cref="AllocateProportionally"/>.
    /// </summary>
    public static decimal AllocateProportionally(decimal lineValue, decimal totalValue, decimal pool)
    {
        if (totalValue <= 0 || pool == 0) return 0;
        return Math.Round(lineValue / totalValue * pool, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Allocates a pool proportionally to weights and returns one amount per weight. The
    /// final element absorbs any rounding difference so the parts sum exactly to the pool.
    /// </summary>
    public static IReadOnlyList<decimal> AllocateProportionally(IReadOnlyList<decimal> weights, decimal pool)
    {
        var result = new decimal[weights.Count];
        var total = weights.Sum();
        if (weights.Count == 0) return result;
        if (total <= 0 || pool == 0) return result;

        var allocated = 0m;
        for (var i = 0; i < weights.Count; i++)
        {
            var amount = i == weights.Count - 1
                ? pool - allocated
                : AllocateProportionally(weights[i], total, pool);
            result[i] = amount;
            allocated += amount;
        }
        return result;
    }

    /// <summary>
    /// Unit cost a batch should be recorded at, after spreading purchase-level charges and
    /// discounts across its line. Returns (adjustedUnitCost, allocatedToThisLine).
    /// </summary>
    public static decimal AdjustedUnitCost(decimal lineNetValue, decimal allocatedCharges, decimal quantity)
        => quantity <= 0 ? 0 : Math.Round((lineNetValue + allocatedCharges) / quantity, 4, MidpointRounding.AwayFromZero);
}
using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Tests;

/// <summary>Pure purchase and supplier-payment arithmetic (PUR cost/settlement rules).</summary>
public sealed class PurchaseFinancialsTests
{
    public static IEnumerable<object?[]> LineTotalCases()
    {
        yield return [10m, 180m, 0m, 1800m];
        yield return [3m, 90.5m, 20m, 251.5m];
    }

    [Theory]
    [MemberData(nameof(LineTotalCases))]
    public void LineTotal_SubtractsDiscount(decimal qty, decimal cost, decimal discount, decimal expected)
    {
        Assert.Equal(expected, PurchaseFinancials.LineTotal(qty, cost, discount));
    }

    [Fact]
    public void LineTotal_NeverNegative()
    {
        Assert.Equal(0m, PurchaseFinancials.LineTotal(1, 10, 50));
    }

    [Fact]
    public void GrandTotal_SubtractsDiscountAddsCharges()
    {
        // subtotal 1000 − discount 50 + charges 75 = 1025
        Assert.Equal(1025m, PurchaseFinancials.GrandTotal(1000m, 50m, 75m));
    }

    [Fact]
    public void GrandTotal_NeverNegative()
    {
        Assert.Equal(0m, PurchaseFinancials.GrandTotal(100m, 200m, 0m));
    }

    [Fact]
    public void Outstanding_FloorsAtZero()
    {
        Assert.Equal(0m, PurchaseFinancials.Outstanding(100m, 150m));
        Assert.Equal(30m, PurchaseFinancials.Outstanding(100m, 70m));
    }

    public static IEnumerable<object?[]> PaymentStatusCases()
    {
        yield return [1000m, 0m, PaymentStatus.Unpaid];
        yield return [1000m, 400m, PaymentStatus.PartiallyPaid];
        yield return [1000m, 1000m, PaymentStatus.Paid];
        yield return [1000m, 1200m, PaymentStatus.Paid];
    }

    [Theory]
    [MemberData(nameof(PaymentStatusCases))]
    public void ResolvePaymentStatus_TracksSettlement(decimal total, decimal paid, PaymentStatus expected)
    {
        Assert.Equal(expected, PurchaseFinancials.ResolvePaymentStatus(total, paid));
    }

    [Fact]
    public void AllocateProportionally_SumsExactlyToPool()
    {
        var weights = new List<decimal> { 100m, 300m, 600m };
        var parts = PurchaseFinancials.AllocateProportionally(weights, 100m);
        Assert.Equal(100m, parts.Sum());
        Assert.Equal(10m, parts[0]);
        Assert.Equal(30m, parts[1]);
        Assert.Equal(60m, parts[2]);
    }

    [Fact]
    public void AdjustedUnitCost_SpreadsChargesAcrossQuantity()
    {
        // line net 1800 + 200 allocated charges over 100 units = 20.0000
        Assert.Equal(20m, PurchaseFinancials.AdjustedUnitCost(1800m, 200m, 100m));
    }
}
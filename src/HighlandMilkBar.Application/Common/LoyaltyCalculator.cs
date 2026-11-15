namespace HighlandMilkBar.Application.Common;

/// <summary>
/// Pure, decimal-safe loyalty points calculations. Points are always rounded
/// <em>down</em> (floor) to whole points, both when earning and when reversing, so a
/// customer can never accumulate fractional points. All money is treated as LKR.
/// </summary>
public static class LoyaltyCalculator
{
    /// <summary>
    /// Points earned for a qualifying sale total. Returns 0 when the sale is below the
    /// minimum, loyalty is disabled, or the rate is non-positive.
    /// </summary>
    public static int PointsForSale(decimal saleTotal, decimal pointsPerHundred, decimal minimumQualifyingAmount, bool enabled)
    {
        if (!enabled || pointsPerHundred <= 0m) return 0;
        if (saleTotal < minimumQualifyingAmount) return 0;
        if (saleTotal <= 0m) return 0;

        // points = floor(saleTotal / 100 * pointsPerHundred)
        var scaled = saleTotal / 100m * pointsPerHundred;
        return (int)decimal.Floor(scaled);
    }

    /// <summary>
    /// Points to reverse for a return of <paramref name="returnedAmount"/> against a sale of
    /// <paramref name="saleTotal"/> that originally earned <paramref name="earnedPoints"/>.
    /// The reversal is proportional to the returned value and is capped so the total reversed
    /// across all returns never exceeds the points originally earned. Always floors.
    /// </summary>
    public static int PointsToReverse(int earnedPoints, decimal saleTotal, decimal returnedAmount)
    {
        if (earnedPoints <= 0 || saleTotal <= 0m || returnedAmount <= 0m) return 0;

        var proportion = returnedAmount / saleTotal;
        if (proportion > 1m) proportion = 1m;

        var raw = earnedPoints * proportion;
        return (int)decimal.Floor(raw);
    }
}
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

/// <summary>Configurable loyalty rules (stored in the AppSettings key/value store).</summary>
public sealed class LoyaltySettingsDto
{
    /// <summary>Points awarded per LKR 100 of qualifying spend.</summary>
    public decimal PointsPerHundred { get; init; } = 1m;

    /// <summary>Minimum sale total (LKR) required for a sale to earn points.</summary>
    public decimal MinimumQualifyingAmount { get; init; } = 500m;

    /// <summary>Master switch; when false no points are awarded.</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>Payload for updating the loyalty configuration.</summary>
public sealed class LoyaltyUpdateSettingsRequest
{
    public decimal PointsPerHundred { get; init; } = 1m;
    public decimal MinimumQualifyingAmount { get; init; } = 500m;
    public bool Enabled { get; init; } = true;
}

/// <summary>One entry in a customer's loyalty ledger.</summary>
public sealed class LoyaltyTransactionDto
{
    public int Id { get; init; }
    public DateTime TransactionDate { get; init; }
    public LoyaltyTransactionType Type { get; init; }
    public string TypeText { get; init; } = string.Empty;
    public int Points { get; init; }
    public int BalanceAfter { get; init; }
    public string? Description { get; init; }
    public string? Reference { get; init; }
}

/// <summary>A customer's loyalty summary.</summary>
public sealed class LoyaltyBalanceDto
{
    public int CustomerId { get; init; }
    public int Balance { get; init; }
    public int TotalEarned { get; init; }
    public int TotalRedeemed { get; init; }
    public int TotalReversed { get; init; }
}
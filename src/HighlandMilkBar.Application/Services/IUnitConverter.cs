namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Centralised unit conversion used by recipe costing and availability. Implemented over the
/// pure <see cref="Common.UnitConversion"/> rules so it can be reused and tested independently.
/// </summary>
public interface IUnitConverter
{
    /// <summary>Converts a quantity between two unit symbols. Returns false when incompatible.</summary>
    bool TryConvert(string? fromSymbol, string? toSymbol, decimal quantity, out decimal result, out string? error);

    /// <summary>True when the two symbols can be converted into one another.</summary>
    bool IsCompatible(string? fromSymbol, string? toSymbol);
}
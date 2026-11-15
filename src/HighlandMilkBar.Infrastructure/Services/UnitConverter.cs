using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Default <see cref="IUnitConverter"/> backed by the pure <see cref="UnitConversion"/> rules.
/// Registered in DI so ViewModels and services share one conversion policy.
/// </summary>
public sealed class UnitConverter : IUnitConverter
{
    public bool TryConvert(string? fromSymbol, string? toSymbol, decimal quantity, out decimal result, out string? error)
        => UnitConversion.TryConvert(fromSymbol, toSymbol, quantity, out result, out error);

    public bool IsCompatible(string? fromSymbol, string? toSymbol)
        => UnitConversion.IsCompatible(fromSymbol, toSymbol);
}
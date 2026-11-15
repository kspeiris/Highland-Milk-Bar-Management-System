using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.Common;

/// <summary>
/// Centralised, pure unit-conversion rules shared by recipe costing, availability and tests.
///
/// Policy:
/// * Standard units are convertible within their own kind (mass: mg/g/kg; volume: ml/L).
/// * Count-style units (piece, bottle, packet, scoop, cup, glass, portion, pack, pot) are opaque
///   and only convert 1:1 when they resolve to the same normalised symbol. A "scoop" is never
///   assumed to equal a fixed mass or volume across ingredients (REC-05 / SPEC §9).
/// * Mass and volume are never converted into each other without an explicit ingredient factor.
/// </summary>
public static class UnitConversion
{
    public const string Gram = "g";
    public const string Kilogram = "kg";
    public const string Milligram = "mg";
    public const string Millilitre = "ml";
    public const string Litre = "l";

    /// <summary>Resolves common synonyms to a canonical symbol for comparison.</summary>
    public static string Normalize(string? symbol)
    {
        var s = (symbol ?? string.Empty).Trim().ToLowerInvariant();
        return s switch
        {
            "" or "unit" or "units" or "pcs" or "pc" or "piece" or "pieces" or "count" or "nos" => "pcs",
            "g" or "gram" or "grams" or "gm" or "gms" => Gram,
            "kg" or "kilogram" or "kilograms" or "kilo" or "kilos" => Kilogram,
            "mg" or "milligram" or "milligrams" => Milligram,
            "ml" or "millilitre" or "millilitres" or "milliliter" or "milliliters" => Millilitre,
            "l" or "ltr" or "litre" or "litres" or "liter" or "liters" => Litre,
            "scoop" or "scoops" => "scoop",
            "bottle" or "bottles" or "btl" => "bottle",
            "cup" or "cups" => "cup",
            "glass" or "glasses" => "glass",
            "pack" or "packs" or "packet" or "packets" => "pack",
            "pot" or "pots" or "tub" or "tubs" => "pot",
            "portion" or "portions" or "serving" or "servings" => "portion",
            _ => s
        };
    }

    /// <summary>True when the symbol is one of the standard convertible units.</summary>
    public static bool IsStandard(string? symbol)
    {
        var n = Normalize(symbol);
        return n is Gram or Kilogram or Milligram or Millilitre or Litre;
    }

    /// <summary>The physical kind of a standard symbol (only meaningful for standard symbols).</summary>
    public static UnitKind KindOf(string? symbol)
    {
        var n = Normalize(symbol);
        if (n is Gram or Kilogram or Milligram) return UnitKind.Weight;
        if (n is Millilitre or Litre) return UnitKind.Volume;
        return UnitKind.Count;
    }

    /// <summary>Converts a quantity to the canonical base unit for its kind (grams or millilitres).</summary>
    public static decimal ToBase(string symbol, decimal quantity)
    {
        return Normalize(symbol) switch
        {
            Gram => quantity,
            Kilogram => quantity * 1000m,
            Milligram => quantity / 1000m,
            Millilitre => quantity,
            Litre => quantity * 1000m,
            _ => quantity
        };
    }

    /// <summary>True when two symbols can be converted into one another.</summary>
    public static bool IsCompatible(string? from, string? to)
    {
        var f = Normalize(from);
        var t = Normalize(to);
        if (f == t) return true;
        if (IsStandard(f) && IsStandard(t))
            return KindOf(f) == KindOf(t);
        return false;
    }

    /// <summary>
    /// Converts <paramref name="quantity"/> from <paramref name="fromSymbol"/> to <paramref name="toSymbol"/>.
    /// Returns false with a human-readable <paramref name="error"/> when the units are incompatible.
    /// </summary>
    public static bool TryConvert(string? fromSymbol, string? toSymbol, decimal quantity, out decimal result, out string? error)
    {
        result = quantity;
        error = null;

        var from = Normalize(fromSymbol);
        var to = Normalize(toSymbol);

        if (from == to)
        {
            result = quantity;
            return true;
        }

        if (IsStandard(from) && IsStandard(to) && KindOf(from) == KindOf(to))
        {
            var baseQty = ToBase(from, quantity);
            var perTarget = ToBase(to, 1m);
            result = perTarget == 0 ? quantity : baseQty / perTarget;
            return true;
        }

        error = $"Cannot convert '{fromSymbol}' to '{toSymbol}'.";
        return false;
    }
}
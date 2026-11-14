namespace HighlandMilkBar.Domain.Enums;

/// <summary>
/// The physical nature of a measurement unit. Used to validate that a product's
/// base unit and its recipe/allocation quantities are dimensionally compatible.
/// </summary>
public enum UnitKind
{
    Count,
    Weight,
    Volume
}
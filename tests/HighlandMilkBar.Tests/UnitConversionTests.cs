using HighlandMilkBar.Application.Common;

namespace HighlandMilkBar.Tests;

public sealed class UnitConversionTests
{
    private static void Convert(string from, string to, decimal qty, decimal expected)
    {
        Assert.True(UnitConversion.TryConvert(from, to, qty, out var result, out var error), error);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ml", "l", 2000, 2)]
    [InlineData("l", "ml", 2, 2000)]
    [InlineData("g", "kg", 500, 0.5)]
    [InlineData("kg", "g", 2, 2000)]
    [InlineData("millilitre", "ml", 1500, 1500)]
    public void Converts_StandardUnits(string from, string to, decimal qty, decimal expected)
        => Convert(from, to, qty, expected);

    [Fact]
    public void SameSymbol_IsIdentity()
    {
        Assert.True(UnitConversion.TryConvert("scoop", "Scoop", 2, out var result, out _));
        Assert.Equal(2m, result);
    }

    [Fact]
    public void Rejects_MassToVolume()
    {
        Assert.False(UnitConversion.TryConvert("g", "ml", 100, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Rejects_DifferentCountUnits()
    {
        // A scoop is never assumed equal to a cup across ingredients.
        Assert.False(UnitConversion.TryConvert("scoop", "cup", 1, out _, out _));
    }

    [Fact]
    public void Rejects_StandardToOpaque()
    {
        Assert.False(UnitConversion.TryConvert("ml", "scoop", 100, out _, out _));
    }

    [Theory]
    [InlineData("ml", "l")]
    [InlineData("g", "kg")]
    [InlineData("scoop", "scoop")]
    public void CompatiblePairs(string a, string b) => Assert.True(UnitConversion.IsCompatible(a, b));

    [Theory]
    [InlineData("g", "ml")]
    [InlineData("scoop", "cup")]
    public void IncompatiblePairs(string a, string b) => Assert.False(UnitConversion.IsCompatible(a, b));
}
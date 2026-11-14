using System.Globalization;
using System.Windows.Data;

namespace HighlandMilkBar.WPF.Converters;

/// <summary>Converts a boolean to its logical inverse.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}
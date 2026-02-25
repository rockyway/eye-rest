using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace EyeRest.UI.Converters;

/// <summary>
/// Converts a 0–100 percentage value to a 0.0–1.0 scale factor for ScaleTransform.
/// </summary>
public class PercentToScaleConverter : IValueConverter
{
    public static readonly PercentToScaleConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percent)
            return Math.Clamp(percent / 100.0, 0.0, 1.0);
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

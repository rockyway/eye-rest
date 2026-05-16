using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace EyeRest.UI.Converters;

/// <summary>
/// Two-way enum ↔ bool converter for binding a RadioButton's IsChecked to
/// an enum property. Convert returns true iff <c>value.ToString() == parameter</c>;
/// ConvertBack returns the enum value parsed from <c>parameter</c> when the
/// IsChecked is true (otherwise BindingOperations.DoNothing to leave the
/// underlying property alone — RadioButton uncheck events otherwise reset it).
/// Used by the BL-002 Popup Audio card for per-channel Source selection.
/// </summary>
public class EnumToBoolConverter : IValueConverter
{
    public static readonly EnumToBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && parameter is not null
           && string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null
            && Enum.TryParse(targetType, parameter.ToString(), out var parsed))
        {
            return parsed!;
        }
        return BindingOperations.DoNothing;
    }
}

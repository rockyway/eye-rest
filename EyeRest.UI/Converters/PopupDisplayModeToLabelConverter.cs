using System;
using System.Globalization;
using Avalonia.Data.Converters;
using EyeRest.Models;

namespace EyeRest.UI.Converters
{
    public sealed class PopupDisplayModeToLabelConverter : IValueConverter
    {
        public static readonly PopupDisplayModeToLabelConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is PopupDisplayMode m
                ? m switch
                {
                    PopupDisplayMode.Standard => "Standard",
                    PopupDisplayMode.Compact  => "Compact",
                    PopupDisplayMode.Minimal  => "Minimal (notification)",
                    _ => m.ToString(),
                }
                : null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}

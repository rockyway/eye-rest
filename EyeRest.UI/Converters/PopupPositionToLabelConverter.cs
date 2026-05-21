using System;
using System.Globalization;
using Avalonia.Data.Converters;
using EyeRest.Models;

namespace EyeRest.UI.Converters
{
    public sealed class PopupPositionToLabelConverter : IValueConverter
    {
        public static readonly PopupPositionToLabelConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is PopupPosition p
                ? p switch
                {
                    PopupPosition.Center       => "Center",
                    PopupPosition.TopLeft      => "Top Left",
                    PopupPosition.TopCenter    => "Top Middle",
                    PopupPosition.TopRight     => "Top Right",
                    PopupPosition.LeftCenter   => "Left Middle",
                    PopupPosition.RightCenter  => "Right Middle",
                    PopupPosition.BottomLeft   => "Bottom Left",
                    PopupPosition.BottomCenter => "Bottom Middle",
                    PopupPosition.BottomRight  => "Bottom Right",
                    _                          => p.ToString(),
                }
                : value?.ToString();

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value;
    }
}

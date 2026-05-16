using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace EyeRest.UI.Converters;

/// <summary>
/// Converts an integer count to a bool: true when count is 0, false otherwise.
/// Used by the BL-002 recent-items flyouts to toggle an empty-state message.
/// Binding source should be a collection's <c>.Count</c> property (Avalonia
/// observes <c>Count</c> change notifications from <c>ObservableCollection</c>).
/// </summary>
public class CountToIsEmptyConverter : IValueConverter
{
    public static readonly CountToIsEmptyConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i ? i == 0 : true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

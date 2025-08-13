using System;
using System.Globalization;
using System.Windows.Data;

namespace EyeRest.Converters
{
    /// <summary>
    /// Converter for scaling chart X values to canvas width
    /// </summary>
    public class ScaleConverter : IMultiValueConverter
    {
        public static readonly ScaleConverter Instance = new ScaleConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 || 
                !(values[0] is double xValue) || 
                !(values[1] is double canvasWidth))
            {
                return 0.0;
            }

            // Scale X value to canvas width (assuming max X is around 30 for monthly data)
            var maxX = 30.0;
            var scaledX = (xValue / maxX) * (canvasWidth - 20) + 10; // 10px padding
            return Math.Max(10, Math.Min(canvasWidth - 10, scaledX));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for scaling chart Y values to canvas height (inverted for charts)
    /// </summary>
    public class InverseScaleConverter : IMultiValueConverter
    {
        public static readonly InverseScaleConverter Instance = new InverseScaleConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 || 
                !(values[0] is double yValue) || 
                !(values[1] is double canvasHeight))
            {
                return 0.0; // Return default value when inputs are invalid
            }

            // Scale Y value to canvas height (inverted - 0 at bottom, 100 at top)
            var maxY = 100.0; // Assuming percentage values
            var scaledY = canvasHeight - ((yValue / maxY) * (canvasHeight - 20)) - 10; // 10px padding
            return Math.Max(10, Math.Min(canvasHeight - 10, scaledY));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for determining chart point colors based on values
    /// </summary>
    public class ChartColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double doubleValue))
            {
                return "#2196F3"; // Default blue
            }

            // Color based on compliance rate
            if (doubleValue >= 90) return "#4CAF50"; // Green
            if (doubleValue >= 80) return "#8BC34A"; // Light Green
            if (doubleValue >= 70) return "#FFC107"; // Amber
            if (doubleValue >= 60) return "#FF9800"; // Orange
            return "#F44336"; // Red
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for formatting time spans as readable text
    /// </summary>
    public class TimeSpanToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is TimeSpan timeSpan))
            {
                return "0min";
            }

            if (timeSpan.TotalHours >= 1)
            {
                return $"{timeSpan.TotalHours:F1}h";
            }
            else
            {
                return $"{timeSpan.TotalMinutes:F0}min";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for formatting percentages
    /// </summary>
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double doubleValue))
            {
                return "0%";
            }

            return $"{doubleValue:P0}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for health status indicators
    /// </summary>
    public class HealthStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double complianceRate))
            {
                return "Unknown";
            }

            if (complianceRate >= 0.9) return "Excellent 🌟";
            if (complianceRate >= 0.8) return "Good 👍";
            if (complianceRate >= 0.7) return "Fair 👌";
            if (complianceRate >= 0.6) return "Needs Improvement ⚠️";
            return "Poor ❌";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
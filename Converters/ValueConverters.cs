using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ImageBatchConverter.Models;
using Wpf.Ui.Controls;

namespace ImageBatchConverter.Converters;

public class FileSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
        return "0 B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PercentageConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return $"{d:F1}%";
        }
        return "0.0%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ConversionStatus status)
        {
            return status switch
            {
                ConversionStatus.Success => new SolidColorBrush(Color.FromRgb(16, 124, 65)), // Green
                ConversionStatus.Skipped => new SolidColorBrush(Color.FromRgb(216, 131, 0)), // Amber/Orange
                ConversionStatus.Failed => new SolidColorBrush(Color.FromRgb(219, 43, 57)),  // Red
                ConversionStatus.Converting => new SolidColorBrush(Color.FromRgb(0, 120, 212)), // Accent Blue
                ConversionStatus.Pending => new SolidColorBrush(Color.FromRgb(140, 140, 140)), // Neutral Gray
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ConversionStatus status)
        {
            return status switch
            {
                ConversionStatus.Success => SymbolRegular.CheckmarkCircle24,
                ConversionStatus.Skipped => SymbolRegular.ArrowForward24,
                ConversionStatus.Failed => SymbolRegular.DismissCircle24,
                ConversionStatus.Converting => SymbolRegular.ArrowSync24,
                ConversionStatus.Pending => SymbolRegular.Clock24,
                _ => SymbolRegular.Circle24
            };
        }
        return SymbolRegular.Circle24;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class EqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null && parameter == null) return Visibility.Visible;
        if (value == null || parameter == null) return Visibility.Collapsed;

        string valStr = value.ToString() ?? "";
        string paramStr = parameter.ToString() ?? "";

        var allowed = paramStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        bool match = allowed.Any(a => a.Equals(valStr, StringComparison.OrdinalIgnoreCase));

        return match ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InvertedBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        return b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return Visibility.Collapsed;
        if (value is string s)
        {
            return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class SpaceSavingsColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long savedBytes)
        {
            if (savedBytes > 0)
                return new SolidColorBrush(Color.FromRgb(16, 124, 65)); // Positive savings = Green
            if (savedBytes < 0)
                return new SolidColorBrush(Color.FromRgb(216, 59, 1));  // Negative savings (larger) = Warning Red/Orange
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

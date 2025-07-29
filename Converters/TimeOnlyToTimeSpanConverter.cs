using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AHON_TRACK.Converters;

public class TimeOnlyToTimeSpanConverter : IValueConverter
{
    public static readonly TimeOnlyToTimeSpanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is TimeOnly timeOnly ? timeOnly.ToTimeSpan() : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is TimeSpan timeSpan ? TimeOnly.FromTimeSpan(timeSpan) : null;
    }
}
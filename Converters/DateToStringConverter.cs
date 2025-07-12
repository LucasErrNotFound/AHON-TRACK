using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AHON_TRACK.Converters;

public class DateToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime date)
        {
            return date.ToString("MMMM d, yyyy");
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string dateString && !string.IsNullOrWhiteSpace(dateString))
        {
            if (DateTime.TryParseExact(dateString, "MMMM d, yyyy", culture, DateTimeStyles.None, out DateTime exactResult))
            {
                return exactResult;
            }
            if (DateTime.TryParse(dateString, culture, DateTimeStyles.None, out DateTime generalResult))
            {
                return generalResult;
            }
        }
        return DateTime.Today;
    }
}

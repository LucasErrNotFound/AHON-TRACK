using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AHON_TRACK.Converters;

public class NullableDateTimeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            return dateTime.ToString("h:mm tt");
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string stringValue) return null;
        
        if (string.IsNullOrWhiteSpace(stringValue))
            return null;
        
        if (DateTime.TryParseExact(stringValue, "h:mm tt", culture, DateTimeStyles.None, out DateTime result))
        {
            return result;
        }
        
        string[] timeFormats = { "h:mm tt", "hh:mm tt", "H:mm", "HH:mm" };
        foreach (string format in timeFormats)
        {
            if (DateTime.TryParseExact(stringValue, format, culture, DateTimeStyles.None, out DateTime parsedResult))
            {
                return parsedResult;
            }
        }
        return null;
    }
}
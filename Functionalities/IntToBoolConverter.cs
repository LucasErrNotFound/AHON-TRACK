using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AHON_TRACK.Functionalities;

public class IntToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue >= 2;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
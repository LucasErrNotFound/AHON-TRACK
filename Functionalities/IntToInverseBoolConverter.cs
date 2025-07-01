using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AHON_TRACK.Functionalities;

public class IntToInverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue < 2;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

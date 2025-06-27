using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Functionalities;

public class StringToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string uriString && !string.IsNullOrEmpty(uriString))
        {
            try
            {
                // Handle avares:// URIs
                if (uriString.StartsWith("avares://"))
                {
                    var uri = new Uri(uriString);
                    var assets = AssetLoader.Open(uri);
                    return new Bitmap(assets);
                }

                // Handle regular file paths or URLs
                return new Bitmap(uriString);
            }
            catch
            {
                // Return null if image cannot be loaded
                return null;
            }
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

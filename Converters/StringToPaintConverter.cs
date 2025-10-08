using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using LiveChartsCore.SkiaSharpView.Painting;
using ShimSkiaSharp;
using SKColor = SkiaSharp.SKColor;

namespace AHON_TRACK.Converters;

public class StringToPaintConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string colorString)
        {
            var avaloniaColor = Color.Parse(colorString);
            var skColor = new SKColor(avaloniaColor.R, avaloniaColor.G, avaloniaColor.B, avaloniaColor.A);
            return new SolidColorPaint(skColor);
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SolidColorPaint paint)
        {
            var c = paint.Color;
            return $"#{c.Alpha:X2}{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
        }

        return null;
    }
}
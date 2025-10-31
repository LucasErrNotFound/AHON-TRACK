using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AHON_TRACK.Converters;

// Expects two values:
// values[0] = row.Status (string)
// values[1] = viewModel.CanDeleteSelectedMembers (bool)
// Returns true only when viewModel.CanDeleteSelectedMembers == true
// AND row.Status equals "Expired" (case-insensitive).
public class StatusAndCanDeleteSelectedConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count < 2) return false;

        var statusObj = values[0];
        var canDeleteObj = values[1];

        var status = statusObj as string ?? string.Empty;
        var canDelete = canDeleteObj is bool b && b;

        return canDelete && (
            status.Equals("Expired", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Suspended", StringComparison.OrdinalIgnoreCase)
        );
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
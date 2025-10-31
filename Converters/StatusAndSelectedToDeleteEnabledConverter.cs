using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AHON_TRACK.Converters;

// values[0] = menu-row Status (string)
// values[1] = SelectedMember?.Status (string or null)
// Returns true only when:
//  - menu-row status == "Expired" (case-insensitive)
//  - AND (SelectedMember is null OR SelectedMember.Status == "Expired")
public class StatusAndSelectedToDeleteEnabledConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count < 2) return false;

        var rowStatus = values[0] as string ?? string.Empty;
        var selectedStatus = values[1] as string; // may be null

        if (!rowStatus.Equals("Expired", StringComparison.OrdinalIgnoreCase) && 
            !rowStatus.Equals("Suspended", StringComparison.OrdinalIgnoreCase) &&
            !rowStatus.Equals("Broken", StringComparison.OrdinalIgnoreCase) &&
            !rowStatus.Equals("Out of Stock", StringComparison.OrdinalIgnoreCase))
            return false;

        // If there's no selected member/supplier -> allow (single-delete)
        if (string.IsNullOrEmpty(selectedStatus))
            return true;

        // Only allow if the currently selected member/supplier/product has matching status
        return selectedStatus.Equals("Expired", StringComparison.OrdinalIgnoreCase) ||
               selectedStatus.Equals("Suspended", StringComparison.OrdinalIgnoreCase) ||
               selectedStatus.Equals("Broken", StringComparison.OrdinalIgnoreCase) ||
               selectedStatus.Equals("Out of Stock", StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
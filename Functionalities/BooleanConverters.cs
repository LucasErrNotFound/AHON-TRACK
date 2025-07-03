using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace AHON_TRACK.Functionalities;

public static class BooleanConverters
{
    public static readonly IValueConverter NullOrString =
        new FuncValueConverter<bool, string?, string?>((value, param) => value ? null : param);

    public static readonly IValueConverter Opaque =
        new FuncValueConverter<bool, int>(value => value ? 1 : 0);

    public static readonly IValueConverter SidebarPadding =
        new FuncValueConverter<bool, Thickness>(value => value ? new Thickness(16, 8) : new Thickness(8));

    public static readonly IValueConverter SidebarTogglerHorizontalAlignment =
        new FuncValueConverter<bool, HorizontalAlignment>(value =>
            value ? HorizontalAlignment.Right : HorizontalAlignment.Center);
}
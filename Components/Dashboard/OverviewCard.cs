using Avalonia;
using Avalonia.Controls.Primitives;

namespace AHON_TRACK.Components.Dashboard;

public class OverviewCard : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<OverviewCard, string>(nameof(Title));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<OverviewCard, string>(nameof(Value));

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly StyledProperty<string> HintProperty =
        AvaloniaProperty.Register<OverviewCard, string>(nameof(Hint));

    public string Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public static readonly StyledProperty<object> IconProperty =
        AvaloniaProperty.Register<OverviewCard, object>(nameof(Icon));

    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}

using Avalonia.Interactivity;
using Window = ShadUI.Window;

namespace AHON_TRACK;

public partial class InstanceDialog : Window
{
    public InstanceDialog()
    {
        InitializeComponent();
    }
    
    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
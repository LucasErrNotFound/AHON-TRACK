using AHON_TRACK.Components.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AHON_TRACK.Components.SettingsDialog;

public partial class SettingsDialogCard : UserControl
{
    public SettingsDialogCard()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
    }
    
    private async void OnLoaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SettingsDialogCardViewModel vm) return;
        
        vm.DownloadTextBoxControl = this.FindControl<TextBox>("DownloadTextBox");
        vm.DataRecoveryTextBoxControl = this.FindControl<TextBox>("RecoverDataTextBox");
        
        // Reload settings after controls are assigned
        await vm.InitializeAsync().ConfigureAwait(false);
    }
}
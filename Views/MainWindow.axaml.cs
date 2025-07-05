using AHON_TRACK.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Window = ShadUI.Window;

namespace AHON_TRACK.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Closing += OnClosing;
        //FullscreenButton.Click += OnFullScreen;
        //ExitFullscreenButton.Click += OnExitFullScreen;
    }

    private void OnFullScreen(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.FullScreen;
    }

    private void OnExitFullScreen(object? sender, RoutedEventArgs e)
    {
        ExitFullScreen();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        e.Cancel = true;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.TryCloseCommand.Execute(null);
        }
    }
}
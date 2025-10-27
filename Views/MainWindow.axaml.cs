using AHON_TRACK.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using Window = ShadUI.Window;

namespace AHON_TRACK.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow != this) return;
        }

        e.Cancel = true;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.TryCloseCommand.Execute(null);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // Dispose the ViewModel when window is closed
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
        DataContext = null;
    }
}
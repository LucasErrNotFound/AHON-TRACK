﻿using AHON_TRACK.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Window = ShadUI.Window;

namespace AHON_TRACK.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
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
}

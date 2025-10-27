using System;
using Window = ShadUI.Window;

namespace AHON_TRACK.Views;

public partial class LoginView : Window
{
    public LoginView()
    {
        InitializeComponent();
        Closed += OnClosed;
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
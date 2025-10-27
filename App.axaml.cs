using AHON_TRACK.ViewModels;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AHON_TRACK;

public class App : Application, IAsyncDisposable
{
    private static Mutex? _appMutex;
    private ServiceProvider? _serviceProvider;
    private CancellationTokenSource? _shutdownCts;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) 
            return;

        // Single instance check
        _appMutex = new Mutex(true, "AHON_TRACK_MUTEX_v1", out var createdNew);
        
        if (!createdNew)
        {
            var instanceDialog = new InstanceDialog();
            instanceDialog.Show();
            return;
        }

        _shutdownCts = new CancellationTokenSource();
        
        DisableAvaloniaDataAnnotationValidation();

        _serviceProvider = new ServiceProvider();
        var viewModel = _serviceProvider.GetService<LoginViewModel>();
        viewModel.Initialize();

        var loginWindow = new Views.LoginView { DataContext = viewModel };
        desktop.MainWindow = loginWindow;
        
        // Handle shutdown gracefully
        desktop.ShutdownRequested += OnShutdownRequested;
        
        base.OnFrameworkInitializationCompleted();
    }

    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Cancel shutdown to do cleanup
        e.Cancel = true;

        try
        {
            _shutdownCts?.Cancel();
            
            // Dispose ViewModels and services
            if (_serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }

            // Flush logs with timeout
            /*
            ValueTask flushTask = Log.CloseAndFlushAsync();
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
            await Task.WhenAny(flushTask, timeoutTask).ConfigureAwait(false);
            */
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during shutdown: {ex.Message}");
        }
        finally
        {
            _appMutex?.Dispose();
            _shutdownCts?.Dispose();
            
            // Now allow shutdown
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown(0);
            }
        }
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdownCts?.Cancel();
        _shutdownCts?.Dispose();
        _shutdownCts = null;

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        
        _appMutex?.Dispose();
        _appMutex = null;

        GC.SuppressFinalize(this);
    }
}
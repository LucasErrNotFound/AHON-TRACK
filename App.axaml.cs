using AHON_TRACK.Services;
using AHON_TRACK.ViewModels;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AHON_TRACK;

public class App : Application
{
    private static Mutex? _appMutex;
    private BackupSchedulerService? _backupScheduler;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        _appMutex = new Mutex(true, "AHON_TRACK", out var createdNew);

        if (!createdNew)
        {
            var instanceDialog = new InstanceDialog();
            instanceDialog.Show();
            return;
        }
        DisableAvaloniaDataAnnotationValidation();

        var provider = new ServiceProvider();

        // Initialize the backup scheduler but DON'T await it here
        _backupScheduler = provider.GetService<BackupSchedulerService>();

        // Start scheduler in background - don't block UI initialization
        _ = Task.Run(async () =>
        {
            try
            {
                await _backupScheduler.StartSchedulerAsync();
            }
            catch (System.Exception ex)
            {
                // Log error but don't crash the app
                System.Diagnostics.Debug.WriteLine($"Backup scheduler failed to start: {ex.Message}");
            }
        });

        // Set up cleanup on app exit
        desktop.ShutdownRequested += (s, e) =>
        {
            _backupScheduler?.Dispose();
        };

        var viewModel = provider.GetService<LoginViewModel>();
        viewModel.Initialize();

        var loginWindow = new Views.LoginView { DataContext = viewModel };
        desktop.MainWindow = loginWindow;
        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove) BindingPlugins.DataValidators.Remove(plugin);
    }
}
using AHON_TRACK.ViewModels;
using AHON_TRACK.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System.Linq;

namespace AHON_TRACK
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();

                // Initialize the service provider
                _serviceProvider = new ServiceProvider();
                
                // Get LoginViewModel from DI container
                var loginViewModel = _serviceProvider.GetService<LoginViewModel>();

                // Show LoginView first
                desktop.MainWindow = new LoginView
                {
                    DataContext = loginViewModel,
                };
            }
            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }

        // Make ServiceProvider accessible to other parts of the application
        public static ServiceProvider? GetServiceProvider() => (Current as App)?._serviceProvider;
    }
}
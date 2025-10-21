using Avalonia;
using Serilog;
using System;
using QuestPDF.Infrastructure;

namespace AHON_TRACK
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // 🔥 TEMPORARY CODE - Run once to get password hash, then DELETE! 🔥
            try
            {
                string password = "Calubayan123";
                string hash = BCrypt.Net.BCrypt.HashPassword(password);

                Console.WriteLine("================================");
                Console.WriteLine("PASSWORD HASH GENERATOR");
                Console.WriteLine("================================");
                Console.WriteLine($"Plain Password: {password}");
                Console.WriteLine($"\nHashed Password:");
                Console.WriteLine(hash);
                Console.WriteLine("\n================================");
                Console.WriteLine("Copy the hash above and use it in your SQL INSERT!");
                Console.WriteLine("================================\n");

                // Also write to debug output (visible in Visual Studio Output window)
                System.Diagnostics.Debug.WriteLine("================================");
                System.Diagnostics.Debug.WriteLine($"Hashed Password: {hash}");
                System.Diagnostics.Debug.WriteLine("================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error hashing password: {ex.Message}");
            }
            // 🔥 END TEMPORARY CODE 🔥

            try
            {
                QuestPDF.Settings.License = LicenseType.Community;
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception e)
            {
                var serviceProvider = new ServiceProvider();

                var logger = serviceProvider.GetService<ILogger>();
                logger.Fatal(e, "An unhandled exception occurred during bootstrapping the application.");
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace().With(new SkiaOptions()
                {
                    MaxGpuResourceSizeBytes = 2000000000, // 2 GB of GPU memory
                });
    }
}
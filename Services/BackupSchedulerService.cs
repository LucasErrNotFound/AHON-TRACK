using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.Models;
using ShadUI;

namespace AHON_TRACK.Services
{
    public class BackupSchedulerService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly BackupDatabaseService _backupDatabaseService;
        private readonly ToastManager _toastManager;
        private Timer? _schedulerTimer;
        private bool _isDisposed;
        private bool _isBackupInProgress;
        private const int MAX_BACKUP_RETRIES = 3;

        public BackupSchedulerService(
            SettingsService settingsService,
            BackupDatabaseService backupDatabaseService,
            ToastManager toastManager)
        {
            _settingsService = settingsService;
            _backupDatabaseService = backupDatabaseService;
            _toastManager = toastManager;
        }

        public async Task StartSchedulerAsync()
        {
            // Stop any existing timer
            StopScheduler();

            // Load settings
            var settings = await _settingsService.LoadSettingsAsync();

            if (string.IsNullOrWhiteSpace(settings.RecoveryFilePath))
            {
                // No backup path/recovery file path configured, don't start scheduler
                return;
            }

            // Get backup frequency in days
            int frequencyDays = ParseBackupFrequency(settings.BackupFrequency);

            // Check if backup is needed
            if (await ShouldPerformBackup(settings, frequencyDays))
            {
                // Perform backup immediately
                _ = Task.Run(() => PerformScheduledBackupAsync(settings));
            }

            // Set up timer to check daily at midnight
            var now = DateTime.Now;
            var nextMidnight = now.Date.AddDays(1);
            var timeUntilMidnight = nextMidnight - now;

            // Create timer that fires at midnight, then every 24 hours
            _schedulerTimer = new Timer(
                async void (_) => await OnTimerElapsed(),
                null,
                timeUntilMidnight,
                TimeSpan.FromHours(24));
        }

        public void StopScheduler()
        {
            _schedulerTimer?.Dispose();
            _schedulerTimer = null;
        }

        private async Task OnTimerElapsed()
        {
            if (_isBackupInProgress) return;

            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                int frequencyDays = ParseBackupFrequency(settings.BackupFrequency);

                if (await ShouldPerformBackup(settings, frequencyDays))
                {
                    await PerformScheduledBackupAsync(settings);
                }
            }
            catch (Exception ex)
            {
                // Log error silently - don't interrupt the timer
                Console.WriteLine($"Backup scheduler error: {ex.Message}");
            }
        }

        private async Task<bool> ShouldPerformBackup(AppSettings settings, int frequencyDays)
        {
            // Check if we have a last backup date
            if (!settings.LastBackupDate.HasValue)
            {
                // Never backed up before
                return true;
            }

            // Check if enough days have passed
            var daysSinceLastBackup = (DateTime.Now - settings.LastBackupDate.Value).TotalDays;
            return daysSinceLastBackup >= frequencyDays;
        }

        private async Task PerformScheduledBackupAsync(AppSettings settings)
        {
            if (_isBackupInProgress) return;

            _isBackupInProgress = true;
            int retryCount = 0;
            bool backupSuccessful = false;

            while (retryCount < MAX_BACKUP_RETRIES && !backupSuccessful)
            {
                try
                {
                    // Create backup folder if it doesn't exist
                    var backupFolder = settings.DownloadPath;
                    if (string.IsNullOrWhiteSpace(backupFolder))
                    {
                        // If no path configured, skip backup
                        return;
                    }

                    if (!Directory.Exists(backupFolder))
                    {
                        Directory.CreateDirectory(backupFolder);
                    }

                    // Generate backup filename with timestamp
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var backupFileName = $"AHON_TRACK_AutoBackup_{timestamp}.bak";
                    var backupFilePath = Path.Combine(backupFolder, backupFileName);

                    // Perform backup
                    await _backupDatabaseService.BackupDatabaseAsync(backupFilePath);

                    // Update last backup date
                    settings.LastBackupDate = DateTime.Now;
                    await _settingsService.SaveSettingsAsync(settings);

                    // Clean up old backups (keep last 10)
                    await CleanupOldBackups(backupFolder, 10);

                    // Show success notification
                    _toastManager.CreateToast("Automatic backup completed")
                        .WithContent($"Database backed up to: {backupFileName}")
                        .DismissOnClick()
                        .ShowSuccess();

                    backupSuccessful = true;
                }
                catch (Exception ex)
                {
                    retryCount++;

                    if (retryCount >= MAX_BACKUP_RETRIES)
                    {
                        // Final failure after retries
                        _toastManager.CreateToast("Automatic backup failed")
                            .WithContent($"Failed after {MAX_BACKUP_RETRIES} attempts: {ex.Message}")
                            .DismissOnClick()
                            .ShowError();
                    }
                    else
                    {
                        // Wait before retry (exponential backoff)
                        await Task.Delay(1000 * retryCount);
                    }
                }
            }

            _isBackupInProgress = false;
        }

        private async Task CleanupOldBackups(string backupFolder, int keepCount)
        {
            try
            {
                var backupFiles = Directory.GetFiles(backupFolder, "AHON_TRACK_AutoBackup_*.bak");

                if (backupFiles.Length > keepCount)
                {
                    // Sort by creation time (oldest first)
                    Array.Sort(backupFiles, (a, b) =>
                        File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));

                    // Delete oldest files
                    int filesToDelete = backupFiles.Length - keepCount;
                    for (int i = 0; i < filesToDelete; i++)
                    {
                        try
                        {
                            File.Delete(backupFiles[i]);
                        }
                        catch
                        {
                            // Ignore errors deleting old backups
                        }
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            await Task.CompletedTask;
        }

        private int ParseBackupFrequency(string frequency)
        {
            if (frequency == "Everyday")
                return 1;

            // Parse "X Days" format
            var parts = frequency.Split(' ');
            if (parts.Length >= 1 && int.TryParse(parts[0], out int days))
            {
                return days;
            }

            // Default to 1 day if parsing fails
            return 1;
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            StopScheduler();
            _isDisposed = true;
        }
    }
}
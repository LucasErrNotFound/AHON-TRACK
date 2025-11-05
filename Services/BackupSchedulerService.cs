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
        private DateTime? _lastBackupAttempt; // Track when we last attempted a backup
        private const int MAX_BACKUP_RETRIES = 3;
        private const int MIN_BACKUP_INTERVAL_MINUTES = 5; // Prevent backups within 5 minutes of each other

        public BackupSchedulerService(
            SettingsService settingsService,
            BackupDatabaseService backupDatabaseService,
            ToastManager toastManager)
        {
            _settingsService = settingsService;
            _backupDatabaseService = backupDatabaseService;
            _toastManager = toastManager;
        }

        public async Task StartSchedulerAsync(bool performImmediateCheckOnStart = true)
        {
            // Stop any existing timer
            StopScheduler();

            // Load settings
            var settings = await _settingsService.LoadSettingsAsync();

            if (string.IsNullOrWhiteSpace(settings.DownloadPath))
            {
                System.Diagnostics.Debug.WriteLine("No backup path configured, scheduler not started");
                // No backup path configured, don't start scheduler
                return;
            }

            // Get backup frequency in days
            int frequencyDays = ParseBackupFrequency(settings.BackupFrequency);

            // Only check for immediate backup if explicitly requested (on app start, not on settings change)
            if (performImmediateCheckOnStart && await ShouldPerformBackup(settings, frequencyDays))
            {
                // Only perform backup immediately if we haven't attempted one recently
                if (!_lastBackupAttempt.HasValue ||
                    (DateTime.Now - _lastBackupAttempt.Value).TotalMinutes >= MIN_BACKUP_INTERVAL_MINUTES)
                {
                    System.Diagnostics.Debug.WriteLine("Backup is due on startup, scheduling immediate backup");
                    // Perform backup in background without awaiting
                    _ = Task.Run(() => PerformScheduledBackupAsync(settings));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Backup was recently attempted at {_lastBackupAttempt}, skipping immediate backup");
                }
            }

            // Set up timer to check daily at midnight
            var now = DateTime.Now;
            var nextMidnight = now.Date.AddDays(1);
            var timeUntilMidnight = nextMidnight - now;

            System.Diagnostics.Debug.WriteLine($"Scheduler started. Next check at {nextMidnight}. Frequency: {frequencyDays} days");

            // Create timer that fires at midnight, then every 24 hours
            _schedulerTimer = new Timer(
                TimerCallback,
                null,
                timeUntilMidnight,
                TimeSpan.FromHours(24));
        }

        public async Task RestartSchedulerAsync()
        {
            // This method is for when settings change - it just restarts the timer
            // without performing an immediate backup check
            System.Diagnostics.Debug.WriteLine("Restarting scheduler due to settings change (no immediate backup)");
            await StartSchedulerAsync(performImmediateCheckOnStart: false);
        }

        public void StopScheduler()
        {
            _schedulerTimer?.Dispose();
            _schedulerTimer = null;
        }

        private void TimerCallback(object? state)
        {
            // Don't await - fire and forget with error handling
            _ = Task.Run(async () =>
            {
                try
                {
                    await OnTimerElapsed();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Timer callback error: {ex.Message}");
                }
            });
        }

        private async Task OnTimerElapsed()
        {
            if (_isBackupInProgress)
            {
                System.Diagnostics.Debug.WriteLine("Backup already in progress, skipping timer event");
                return;
            }

            // Prevent multiple rapid backup attempts
            if (_lastBackupAttempt.HasValue &&
                (DateTime.Now - _lastBackupAttempt.Value).TotalMinutes < MIN_BACKUP_INTERVAL_MINUTES)
            {
                System.Diagnostics.Debug.WriteLine("Recent backup attempt detected, skipping");
                return;
            }

            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                int frequencyDays = ParseBackupFrequency(settings.BackupFrequency);

                if (await ShouldPerformBackup(settings, frequencyDays))
                {
                    System.Diagnostics.Debug.WriteLine("Timer: Backup is due");
                    await PerformScheduledBackupAsync(settings);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Timer: No backup needed yet");
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
                System.Diagnostics.Debug.WriteLine("No LastBackupDate found, backup is due");
                return true;
            }

            // Check if enough days have passed
            var daysSinceLastBackup = (DateTime.Now - settings.LastBackupDate.Value).TotalDays;
            var isDue = daysSinceLastBackup >= frequencyDays;

            System.Diagnostics.Debug.WriteLine($"Days since last backup: {daysSinceLastBackup:F2}, Frequency: {frequencyDays}, Is due: {isDue}");

            return isDue;
        }

        private async Task PerformScheduledBackupAsync(AppSettings settings)
        {
            if (_isBackupInProgress)
            {
                System.Diagnostics.Debug.WriteLine("Backup already in progress, skipping duplicate backup");
                return;
            }

            // Check if we attempted a backup very recently
            if (_lastBackupAttempt.HasValue &&
                (DateTime.Now - _lastBackupAttempt.Value).TotalMinutes < MIN_BACKUP_INTERVAL_MINUTES)
            {
                System.Diagnostics.Debug.WriteLine($"Backup was attempted {(DateTime.Now - _lastBackupAttempt.Value).TotalMinutes:F1} minutes ago, skipping");
                return;
            }

            _isBackupInProgress = true;
            _lastBackupAttempt = DateTime.Now;
            int retryCount = 0;
            bool backupSuccessful = false;

            System.Diagnostics.Debug.WriteLine("===== STARTING AUTOMATIC BACKUP =====");

            while (retryCount < MAX_BACKUP_RETRIES && !backupSuccessful)
            {
                try
                {
                    // Create backup folder if it doesn't exist
                    var backupFolder = settings.DownloadPath;
                    if (string.IsNullOrWhiteSpace(backupFolder))
                    {
                        System.Diagnostics.Debug.WriteLine("Backup folder is empty, skipping backup");
                        _isBackupInProgress = false;
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

                    System.Diagnostics.Debug.WriteLine($"Creating backup: {backupFileName}");

                    // Perform backup
                    await _backupDatabaseService.BackupDatabaseAsync(backupFilePath);

                    // Update last backup date
                    settings.LastBackupDate = DateTime.Now;
                    await _settingsService.SaveSettingsAsync(settings);

                    System.Diagnostics.Debug.WriteLine($"Backup completed successfully, LastBackupDate updated to: {settings.LastBackupDate}");

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

                    System.Diagnostics.Debug.WriteLine($"Backup attempt {retryCount} failed: {ex.Message}");

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
            System.Diagnostics.Debug.WriteLine("===== AUTOMATIC BACKUP FINISHED =====");
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
                            System.Diagnostics.Debug.WriteLine($"Deleted old backup: {Path.GetFileName(backupFiles[i])}");
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
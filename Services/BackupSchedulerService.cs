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
        private bool _isMaintenanceInProgress;
        private DateTime? _lastBackupAttempt;
        private DateTime? _lastMaintenanceAttempt;
        private const int MAX_BACKUP_RETRIES = 3;
        private const int MIN_BACKUP_INTERVAL_MINUTES = 5;
        private const int MIN_MAINTENANCE_INTERVAL_MINUTES = 30;

        public BackupSchedulerService(
            SettingsService settingsService,
            BackupDatabaseService backupDatabaseService,
            ToastManager toastManager)
        {
            _settingsService = settingsService;
            _backupDatabaseService = backupDatabaseService;
            _toastManager = toastManager;
        }

        #region Scheduler Lifecycle

        public async Task StartSchedulerAsync(bool performImmediateCheckOnStart = true)
        {
            System.Diagnostics.Debug.WriteLine("===== BACKUP SCHEDULER: StartSchedulerAsync CALLED =====");
            System.Diagnostics.Debug.WriteLine($"performImmediateCheckOnStart: {performImmediateCheckOnStart}");

            StopScheduler();

            var settings = await _settingsService.LoadSettingsAsync();

            System.Diagnostics.Debug.WriteLine($"📁 DownloadPath: '{settings.DownloadPath}'");
            System.Diagnostics.Debug.WriteLine($"📅 LastBackupDate: {settings.LastBackupDate}");
            System.Diagnostics.Debug.WriteLine($"🔧 IndexMaintenanceFrequencyDays: {settings.IndexMaintenanceFrequencyDays}");
            System.Diagnostics.Debug.WriteLine($"🔧 LastIndexMaintenanceDate: {settings.LastIndexMaintenanceDate}");

            if (string.IsNullOrWhiteSpace(settings.DownloadPath))
            {
                System.Diagnostics.Debug.WriteLine("❌ No backup path configured, scheduler not started");
                return;
            }

            int backupFrequencyDays = ParseBackupFrequency(settings.BackupFrequency);
            int maintenanceFrequencyDays = settings.IndexMaintenanceFrequencyDays;

            System.Diagnostics.Debug.WriteLine($"📊 Parsed backup frequency: {backupFrequencyDays} days");
            System.Diagnostics.Debug.WriteLine($"📊 Maintenance frequency: {maintenanceFrequencyDays} days");

            if (performImmediateCheckOnStart)
            {
                System.Diagnostics.Debug.WriteLine("⏰ Checking if immediate operations are needed...");

                // Check for backup
                if (await ShouldPerformBackup(settings, backupFrequencyDays))
                {
                    System.Diagnostics.Debug.WriteLine("💾 Backup is due on startup");
                    if (!_lastBackupAttempt.HasValue ||
                        (DateTime.Now - _lastBackupAttempt.Value).TotalMinutes >= MIN_BACKUP_INTERVAL_MINUTES)
                    {
                        System.Diagnostics.Debug.WriteLine("💾 Scheduling immediate backup");
                        _ = Task.Run(() => PerformScheduledBackupAsync(settings));
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("✅ Backup not due yet");
                }

                // Check for index maintenance (independent of backup)
                if (await ShouldPerformIndexMaintenance(settings, maintenanceFrequencyDays))
                {
                    System.Diagnostics.Debug.WriteLine("🔧 Index maintenance is due on startup");
                    if (!_lastMaintenanceAttempt.HasValue ||
                        (DateTime.Now - _lastMaintenanceAttempt.Value).TotalMinutes >= MIN_MAINTENANCE_INTERVAL_MINUTES)
                    {
                        System.Diagnostics.Debug.WriteLine("🔧 Scheduling immediate maintenance");
                        _ = Task.Run(() => PerformScheduledMaintenanceAsync(settings));
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("✅ Index maintenance not due yet");
                }
            }

            var now = DateTime.Now;
            var nextMidnight = now.Date.AddDays(1);
            var timeUntilMidnight = nextMidnight - now;

            System.Diagnostics.Debug.WriteLine($"⏰ Scheduler started. Next check at {nextMidnight}");
            System.Diagnostics.Debug.WriteLine($"  - Backup frequency: {backupFrequencyDays} days");
            System.Diagnostics.Debug.WriteLine($"  - Index maintenance frequency: {maintenanceFrequencyDays} days");

            _schedulerTimer = new Timer(
                TimerCallback,
                null,
                timeUntilMidnight,
                TimeSpan.FromHours(24));

            System.Diagnostics.Debug.WriteLine("✅ Timer created and started");
        }

        public async Task RestartSchedulerAsync()
        {
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
            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                int backupFrequencyDays = ParseBackupFrequency(settings.BackupFrequency);
                int maintenanceFrequencyDays = settings.IndexMaintenanceFrequencyDays;

                // Check and perform backup if needed
                if (!_isBackupInProgress &&
                    (!_lastBackupAttempt.HasValue ||
                     (DateTime.Now - _lastBackupAttempt.Value).TotalMinutes >= MIN_BACKUP_INTERVAL_MINUTES))
                {
                    if (await ShouldPerformBackup(settings, backupFrequencyDays))
                    {
                        System.Diagnostics.Debug.WriteLine("Timer: Backup is due");
                        await PerformScheduledBackupAsync(settings);
                    }
                }

                // Check and perform index maintenance if needed (independent of backup)
                if (!_isMaintenanceInProgress &&
                    (!_lastMaintenanceAttempt.HasValue ||
                     (DateTime.Now - _lastMaintenanceAttempt.Value).TotalMinutes >= MIN_MAINTENANCE_INTERVAL_MINUTES))
                {
                    if (await ShouldPerformIndexMaintenance(settings, maintenanceFrequencyDays))
                    {
                        System.Diagnostics.Debug.WriteLine("Timer: Index maintenance is due");
                        await PerformScheduledMaintenanceAsync(settings);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Scheduler error: {ex.Message}");
            }
        }

        #endregion

        #region Backup Operations

        private async Task<bool> ShouldPerformBackup(AppSettings settings, int frequencyDays)
        {
            if (!settings.LastBackupDate.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("No LastBackupDate found, backup is due");
                return true;
            }

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

                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var backupFileName = $"AHON_TRACK_AutoBackup_{timestamp}.bak";
                    var backupFilePath = Path.Combine(backupFolder, backupFileName);

                    System.Diagnostics.Debug.WriteLine($"Creating backup: {backupFileName}");

                    await _backupDatabaseService.BackupDatabaseAsync(backupFilePath);

                    settings.LastBackupDate = DateTime.Now;
                    await _settingsService.SaveSettingsAsync(settings);

                    System.Diagnostics.Debug.WriteLine($"✅ Backup completed successfully, LastBackupDate updated to: {settings.LastBackupDate}");

                    await CleanupOldBackups(backupFolder, 10);

                    // Try to show toast, but don't fail if it doesn't work (background thread issue)
                    try
                    {
                        _toastManager.CreateToast("Automatic backup completed")
                            .WithContent($"Database backed up to: {backupFileName}")
                            .DismissOnClick()
                            .ShowSuccess();
                    }
                    catch (Exception toastEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not show success toast: {toastEx.Message}");
                    }

                    backupSuccessful = true;
                }
                catch (Exception ex)
                {
                    retryCount++;

                    System.Diagnostics.Debug.WriteLine($"❌ Backup attempt {retryCount} failed: {ex.Message}");

                    if (retryCount >= MAX_BACKUP_RETRIES)
                    {
                        // Try to show error toast, but don't fail if it doesn't work
                        try
                        {
                            _toastManager.CreateToast("Automatic backup failed")
                                .WithContent($"Failed after {MAX_BACKUP_RETRIES} attempts: {ex.Message}")
                                .DismissOnClick()
                                .ShowError();
                        }
                        catch (Exception toastEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Could not show error toast: {toastEx.Message}");
                        }
                    }
                    else
                    {
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
                    Array.Sort(backupFiles, (a, b) =>
                        File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));

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

        #endregion

        #region Index Maintenance Operations

        private async Task<bool> ShouldPerformIndexMaintenance(AppSettings settings, int frequencyDays)
        {
            if (!settings.LastIndexMaintenanceDate.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("No LastIndexMaintenanceDate found, maintenance is due");
                return true;
            }

            var daysSinceLastMaintenance = (DateTime.Now - settings.LastIndexMaintenanceDate.Value).TotalDays;
            var isDue = daysSinceLastMaintenance >= frequencyDays;

            System.Diagnostics.Debug.WriteLine($"Days since last maintenance: {daysSinceLastMaintenance:F2}, Frequency: {frequencyDays}, Is due: {isDue}");

            return isDue;
        }

        private async Task PerformScheduledMaintenanceAsync(AppSettings settings)
        {
            if (_isMaintenanceInProgress)
            {
                System.Diagnostics.Debug.WriteLine("Index maintenance already in progress, skipping");
                return;
            }

            if (_lastMaintenanceAttempt.HasValue &&
                (DateTime.Now - _lastMaintenanceAttempt.Value).TotalMinutes < MIN_MAINTENANCE_INTERVAL_MINUTES)
            {
                System.Diagnostics.Debug.WriteLine($"Maintenance was attempted {(DateTime.Now - _lastMaintenanceAttempt.Value).TotalMinutes:F1} minutes ago, skipping");
                return;
            }

            _isMaintenanceInProgress = true;
            _lastMaintenanceAttempt = DateTime.Now;

            System.Diagnostics.Debug.WriteLine("===== STARTING AUTOMATIC INDEX MAINTENANCE =====");

            try
            {
                // Don't show toast from background thread - just log
                System.Diagnostics.Debug.WriteLine("Starting index maintenance...");

                await _backupDatabaseService.PerformIndexMaintenanceAsync();

                settings.LastIndexMaintenanceDate = DateTime.Now;
                await _settingsService.SaveSettingsAsync(settings);

                System.Diagnostics.Debug.WriteLine($"✅ Index maintenance completed, LastIndexMaintenanceDate updated to: {settings.LastIndexMaintenanceDate}");

                // Try to show toast, but don't fail if it doesn't work
                try
                {
                    _toastManager.CreateToast("Index maintenance completed")
                        .WithContent("Database indexes optimized successfully")
                        .DismissOnClick()
                        .ShowSuccess();
                }
                catch (Exception toastEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not show toast notification: {toastEx.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Index maintenance failed: {ex.Message}");

                // Try to show error toast, but don't fail if it doesn't work
                try
                {
                    _toastManager.CreateToast("Index maintenance failed")
                        .WithContent($"Failed to optimize indexes: {ex.Message}")
                        .DismissOnClick()
                        .ShowError();
                }
                catch (Exception toastEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not show error toast: {toastEx.Message}");
                }
            }
            finally
            {
                _isMaintenanceInProgress = false;
                System.Diagnostics.Debug.WriteLine("===== AUTOMATIC INDEX MAINTENANCE FINISHED =====");
            }
        }

        #endregion

        #region Helper Methods

        private int ParseBackupFrequency(string frequency)
        {
            if (frequency == "Everyday")
                return 1;

            var parts = frequency.Split(' ');
            if (parts.Length >= 1 && int.TryParse(parts[0], out int days))
            {
                return days;
            }

            return 1;
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            if (_isDisposed) return;

            StopScheduler();
            _isDisposed = true;
        }

        #endregion
    }
}
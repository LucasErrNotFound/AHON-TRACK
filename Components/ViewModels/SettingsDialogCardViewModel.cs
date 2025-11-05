using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;

namespace AHON_TRACK.Components.ViewModels;

public partial class SettingsDialogCardViewModel : ViewModelBase, INavigable
{
    #region Observable Properties

    [ObservableProperty]
    private TextBox? _downloadTextBoxControl;

    [ObservableProperty]
    private TextBox? _dataRecoveryTextBoxControl;

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private string _downloadPath = string.Empty;

    [ObservableProperty]
    private string _recoveryFilePath = string.Empty;

    public ObservableCollection<string> BackupFrequencyOptions { get; }

    [ObservableProperty]
    private string _selectedBackupFrequency;

    [ObservableProperty]
    private bool _isBackingUp;

    [ObservableProperty]
    private bool _isRestoring;

    [ObservableProperty]
    private bool _restoreConfirmationPending;

    [ObservableProperty]
    private bool _isPerformingMaintenance;

    [ObservableProperty]
    private int _indexMaintenanceFrequencyDays;

    [ObservableProperty]
    private string _lastMaintenanceDate = "Never";

    #endregion

    #region Private Fields

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly SettingsService _settingsService;
    private readonly BackupDatabaseService _backupDatabaseService;
    private readonly BackupSchedulerService? _backupScheduler;
    private AppSettings? _currentSettings;
    private DateTime? _restoreConfirmationExpiry;

    #endregion

    #region Constructors

    public SettingsDialogCardViewModel(
        DialogManager dialogManager,
        ToastManager toastManager,
        PageManager pageManager,
        SettingsService settingsService,
        BackupDatabaseService backupDatabaseService,
        BackupSchedulerService backupScheduler)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _settingsService = settingsService;
        _backupDatabaseService = backupDatabaseService;
        _backupScheduler = backupScheduler;

        BackupFrequencyOptions = ["Everyday"];
        for (int i = 2; i <= 30; i++)
            BackupFrequencyOptions.Add($"{i} Days");

        _selectedBackupFrequency = BackupFrequencyOptions[0];
        _indexMaintenanceFrequencyDays = 25;

        _ = LoadSettingsAsync();
    }

    public SettingsDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _settingsService = new SettingsService();
        _backupDatabaseService = new BackupDatabaseService("Data Source=LAPTOP-SSMJIDM6\\SQLEXPRESS08;Initial Catalog=AHON_TRACK;Integrated Security=True;Encrypt=True;Trust Server Certificate=True");
        _backupScheduler = null;

        BackupFrequencyOptions = ["Everyday"];
        for (int i = 2; i <= 30; i++)
            BackupFrequencyOptions.Add($"{i} Days");

        _selectedBackupFrequency = BackupFrequencyOptions[0];
        _indexMaintenanceFrequencyDays = 25;
    }

    #endregion

    #region Initialization

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _currentSettings = await _settingsService.LoadSettingsAsync();

        DownloadPath = _currentSettings.DownloadPath;
        IsDarkMode = _currentSettings.IsDarkMode;
        SelectedBackupFrequency = _currentSettings.BackupFrequency;
        RecoveryFilePath = _currentSettings.RecoveryFilePath;
        IndexMaintenanceFrequencyDays = _currentSettings.IndexMaintenanceFrequencyDays;

        if (_currentSettings.LastIndexMaintenanceDate.HasValue)
        {
            LastMaintenanceDate = _currentSettings.LastIndexMaintenanceDate.Value.ToString("yyyy-MM-dd HH:mm");
        }

        UpdateTextBoxes();
    }

    #endregion

    #region Settings Commands

    [RelayCommand]
    private async Task Apply()
    {
        if (_currentSettings == null)
            _currentSettings = new AppSettings();

        _currentSettings.DownloadPath = DownloadPath;
        _currentSettings.IsDarkMode = IsDarkMode;
        _currentSettings.BackupFrequency = SelectedBackupFrequency;
        _currentSettings.RecoveryFilePath = RecoveryFilePath;
        _currentSettings.IndexMaintenanceFrequencyDays = IndexMaintenanceFrequencyDays;

        System.Diagnostics.Debug.WriteLine($"Saving DownloadPath: '{_currentSettings.DownloadPath}'");
        System.Diagnostics.Debug.WriteLine($"Saving BackupFrequency: '{_currentSettings.BackupFrequency}'");
        System.Diagnostics.Debug.WriteLine($"Saving IndexMaintenanceFrequencyDays: {_currentSettings.IndexMaintenanceFrequencyDays}");

        await _settingsService.SaveSettingsAsync(_currentSettings);

        if (_backupScheduler != null)
        {
            await _backupScheduler.RestartSchedulerAsync();

            _toastManager.CreateToast("Settings saved")
                .WithContent($"Backup: {SelectedBackupFrequency} | Index maintenance: Every {IndexMaintenanceFrequencyDays} days")
                .DismissOnClick()
                .ShowSuccess();
        }
        else
        {
            _toastManager.CreateToast("Settings saved")
                .WithContent("Settings have been updated")
                .DismissOnClick()
                .ShowSuccess();
        }

        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }

    #endregion

    #region Backup Path Commands

    [RelayCommand]
    private async Task SetDownloadPath()
    {
        System.Diagnostics.Debug.WriteLine("===== SetDownloadPath CALLED =====");

        var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (toplevel == null)
        {
            System.Diagnostics.Debug.WriteLine("ERROR: toplevel is null!");
            return;
        }

        var folder = await toplevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select backup folder"
        });

        System.Diagnostics.Debug.WriteLine($"Folder picker returned {folder.Count} folders");

        if (folder.Count > 0)
        {
            var selectedFolder = folder[0];
            DownloadPath = selectedFolder.Path.LocalPath;

            System.Diagnostics.Debug.WriteLine($"===== SET DownloadPath to: '{DownloadPath}' =====");

            _toastManager.CreateToast("Folder path selected")
                .WithContent($"Set path to: {selectedFolder.Name}")
                .DismissOnClick()
                .ShowInfo();

            if (DownloadTextBoxControl != null)
            {
                DownloadTextBoxControl.Text = DownloadPath;
                System.Diagnostics.Debug.WriteLine($"TextBox updated to: '{DownloadTextBoxControl.Text}'");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WARNING: DownloadTextBoxControl is null!");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("No folder selected (user cancelled)");
        }
    }

    [RelayCommand]
    private async Task SelectRecoveryFile()
    {
        System.Diagnostics.Debug.WriteLine("===== RECOVERY FILE BROWSE CLICKED =====");
        var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (toplevel == null) return;

        var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a backup file to restore",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("SQL Server Backup File")
                {
                    Patterns = ["*.bak"]
                }
            ]
        });

        if (files.Count > 0)
        {
            var selectedFile = files[0];
            RecoveryFilePath = selectedFile.Path.LocalPath;

            _toastManager.CreateToast("Backup file selected")
                .WithContent($"{selectedFile.Name}")
                .DismissOnClick()
                .ShowInfo();

            if (DataRecoveryTextBoxControl != null)
            {
                DataRecoveryTextBoxControl.Text = RecoveryFilePath;
            }

            RestoreConfirmationPending = false;
            _restoreConfirmationExpiry = null;
        }
    }

    #endregion

    #region Backup Operations

    [RelayCommand]
    private async Task BackupNow()
    {
        if (IsBackingUp) return;

        var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (toplevel == null) return;

        IStorageFolder? startLocation = null;
        if (!string.IsNullOrWhiteSpace(DownloadPath))
        {
            try
            {
                startLocation = await toplevel.StorageProvider.TryGetFolderFromPathAsync(DownloadPath);
            }
            catch
            {
                // If path is invalid, startLocation will remain null and use default
            }
        }

        var backupFileType = new FilePickerFileType("SQL Server Backup File")
        {
            Patterns = ["*.bak"],
            MimeTypes = ["application/octet-stream"]
        };

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var file = await toplevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save database backup",
            SuggestedStartLocation = startLocation,
            FileTypeChoices = [backupFileType],
            SuggestedFileName = $"AHON_TRACK_Backup_{timestamp}.bak",
            ShowOverwritePrompt = true
        });

        if (file != null)
        {
            IsBackingUp = true;

            try
            {
                _toastManager.CreateToast("Creating backup...")
                    .WithContent("Please wait while the database is being backed up")
                    .ShowInfo();

                await _backupDatabaseService.BackupDatabaseAsync(file.Path.LocalPath);

                _toastManager.CreateToast("Backup created successfully")
                    .WithContent($"Database backup saved to {file.Name}")
                    .DismissOnClick()
                    .ShowSuccess();
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Backup failed")
                    .WithContent($"Failed to create backup: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
            }
            finally
            {
                IsBackingUp = false;
            }
        }
    }

    [RelayCommand]
    private async Task RestoreFromBackup()
    {
        if (IsRestoring) return;

        if (string.IsNullOrWhiteSpace(RecoveryFilePath))
        {
            _toastManager.CreateToast("No backup file selected")
                .WithContent("Please select a backup file first")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        if (!System.IO.File.Exists(RecoveryFilePath))
        {
            _toastManager.CreateToast("File not found")
                .WithContent("The selected backup file does not exist")
                .DismissOnClick()
                .ShowError();
            return;
        }

        if (_restoreConfirmationExpiry.HasValue && _restoreConfirmationExpiry.Value < DateTime.Now)
        {
            RestoreConfirmationPending = false;
            _restoreConfirmationExpiry = null;
        }

        if (!RestoreConfirmationPending)
        {
            RestoreConfirmationPending = true;
            _restoreConfirmationExpiry = DateTime.Now.AddSeconds(10);

            _toastManager.CreateToast("?? WARNING: Restore Database Confirmation")
                .WithContent("This will REPLACE ALL current data! Click 'Restore' button again within 10 seconds to confirm.")
                .DismissOnClick()
                .ShowWarning();

            _ = Task.Run(async () =>
            {
                await Task.Delay(10000);
                if (RestoreConfirmationPending && _restoreConfirmationExpiry.HasValue && _restoreConfirmationExpiry.Value < DateTime.Now)
                {
                    RestoreConfirmationPending = false;
                    _restoreConfirmationExpiry = null;
                }
            });

            return;
        }

        RestoreConfirmationPending = false;
        _restoreConfirmationExpiry = null;
        IsRestoring = true;

        try
        {
            _toastManager.CreateToast("Restoring database...")
                .WithContent("Please wait, this may take a few minutes")
                .ShowInfo();

            await _backupDatabaseService.RestoreDatabaseAsync(RecoveryFilePath);

            _toastManager.CreateToast("Database restored successfully")
                .WithContent("The database has been restored from the backup file")
                .DismissOnClick()
                .ShowSuccess();

            _toastManager.CreateToast("?? Restart Required")
                .WithContent("Please restart the application to ensure all data is properly loaded")
                .DismissOnClick()
                .ShowWarning();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Restore failed")
                .WithContent($"Failed to restore database: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsRestoring = false;
        }
    }

    #endregion

    #region Index Maintenance Operations

    [RelayCommand]
    private async Task PerformIndexMaintenance()
    {
        if (IsPerformingMaintenance) return;

        IsPerformingMaintenance = true;

        try
        {
            _toastManager.CreateToast("Starting index maintenance...")
                .WithContent("This may take several minutes depending on database size")
                .ShowInfo();

            await _backupDatabaseService.PerformIndexMaintenanceAsync();

            if (_currentSettings != null)
            {
                _currentSettings.LastIndexMaintenanceDate = DateTime.Now;
                await _settingsService.SaveSettingsAsync(_currentSettings);
                LastMaintenanceDate = _currentSettings.LastIndexMaintenanceDate.Value.ToString("yyyy-MM-dd HH:mm");
            }

            _toastManager.CreateToast("Index maintenance completed")
                .WithContent("Database indexes have been optimized successfully")
                .DismissOnClick()
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Index maintenance failed")
                .WithContent($"Failed to optimize indexes: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsPerformingMaintenance = false;
        }
    }

    #endregion

    #region Helper Methods

    private void UpdateTextBoxes()
    {
        if (DownloadTextBoxControl != null)
            DownloadTextBoxControl.Text = DownloadPath;

        if (DataRecoveryTextBoxControl != null)
            DataRecoveryTextBoxControl.Text = RecoveryFilePath;
    }

    partial void OnDownloadTextBoxControlChanged(TextBox? value)
    {
        if (value != null)
            value.Text = DownloadPath;
    }

    partial void OnDataRecoveryTextBoxControlChanged(TextBox? value)
    {
        if (value != null)
            value.Text = RecoveryFilePath;
    }

    #endregion

    #region Disposal

    protected override void DisposeManagedResources()
    {
        if (DownloadTextBoxControl != null)
            DownloadTextBoxControl.Text = string.Empty;
        if (DataRecoveryTextBoxControl != null)
            DataRecoveryTextBoxControl.Text = string.Empty;

        DownloadTextBoxControl = null;
        DataRecoveryTextBoxControl = null;

        IsDarkMode = false;
        DownloadPath = string.Empty;
        RecoveryFilePath = string.Empty;
        SelectedBackupFrequency = string.Empty;
        IsBackingUp = false;
        IsRestoring = false;
        RestoreConfirmationPending = false;
        IsPerformingMaintenance = false;
        _restoreConfirmationExpiry = null;

        BackupFrequencyOptions?.Clear();
        _currentSettings = null;

        base.DisposeManagedResources();
    }

    #endregion
}
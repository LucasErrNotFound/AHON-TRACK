using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using Microsoft.Extensions.Logging;
using ShadUI;
using System;

namespace AHON_TRACK.Components.ViewModels;

public partial class SettingsDialogCardViewModel : ViewModelBase, INavigable
{
    public ObservableCollection<string> BackupFrequencyOptions { get; }
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly SettingsService _settingsService;
    private readonly ILogger _logger;
    private AppSettings? _currentSettings;

    [ObservableProperty] private TextBox? _downloadTextBoxControl;
    [ObservableProperty] private TextBox? _dataRecoveryTextBoxControl;
    [ObservableProperty] private string _downloadPath = string.Empty;
    [ObservableProperty] private string _recoveryFilePath = string.Empty;
    [ObservableProperty] private string _selectedBackupFrequency;
    [ObservableProperty] private bool _isInitialized;
    [ObservableProperty] private bool _isDarkMode;

    public SettingsDialogCardViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager, 
        SettingsService settingsService,
        ILogger logger)
    {
        _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        BackupFrequencyOptions = ["Everyday"];
        for (int i = 2; i <= 30; i++)
            BackupFrequencyOptions.Add($"{i} Days");
        
        _selectedBackupFrequency = BackupFrequencyOptions[0];
    }
    
    public SettingsDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _settingsService = new SettingsService();
        _logger = null!;
        
        BackupFrequencyOptions = ["Everyday"];
        for (int i = 2; i <= 30; i++)
            BackupFrequencyOptions.Add($"{i} Days");
        
        _selectedBackupFrequency = BackupFrequencyOptions[0];
    }

    #region INavigable Implementation

    [AvaloniaHotReload]
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (IsInitialized)
        {
            _logger.LogDebug("SettingsDialogCardViewModel already initialized");
            return;
        }

        _logger.LogInformation("Initializing SettingsDialogCardViewModel");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            await LoadSettingsAsync(linkedCts.Token).ConfigureAwait(false);

            IsInitialized = true;
            _logger.LogInformation("SettingsDialogCardViewModel initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SettingsDialogCardViewModel initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing SettingsDialogCardViewModel");
            _toastManager.CreateToast("Settings Error")
                .WithContent("Failed to load settings")
                .DismissOnClick()
                .ShowError();
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Navigating away from Settings");
        return ValueTask.CompletedTask;
    }

    #endregion

    /*
    #region HotAvalonia Support

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        await InitializeAsync(LifecycleToken).ConfigureAwait(false);
    }

    #endregion
    */

    private async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _currentSettings = await _settingsService.LoadSettingsAsync()
                .ConfigureAwait(false);

            DownloadPath = _currentSettings.DownloadPath;
            IsDarkMode = _currentSettings.IsDarkMode;
            SelectedBackupFrequency = _currentSettings.BackupFrequency;
            RecoveryFilePath = _currentSettings.RecoveryFilePath;

            UpdateTextBoxes();

            _logger.LogDebug("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings");
            throw;
        }
    }

    [RelayCommand]
    private async Task Apply()
    {
        ThrowIfDisposed();

        try
        {
            if (_currentSettings == null)
                _currentSettings = new AppSettings();

            _currentSettings.DownloadPath = DownloadPath;
            _currentSettings.IsDarkMode = IsDarkMode;
            _currentSettings.BackupFrequency = SelectedBackupFrequency;
            _currentSettings.RecoveryFilePath = RecoveryFilePath;

            await _settingsService.SaveSettingsAsync(_currentSettings)
                .ConfigureAwait(false);

            _logger.LogInformation("Settings saved successfully");
            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings");
            _toastManager.CreateToast("Save Failed")
                .WithContent("Failed to save settings")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        ThrowIfDisposed();
        _logger.LogDebug("Settings dialog cancelled");
        _dialogManager.Close(this);
    }

    [RelayCommand]
    private async Task SetDownloadPath()
    {
        ThrowIfDisposed();

        try
        {
            var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (toplevel == null) return;

            var folder = await toplevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select a folder"
            });

            if (folder.Count > 0)
            {
                var selectedFolder = folder[0];
                DownloadPath = selectedFolder.Path.LocalPath;

                _logger.LogDebug("Download path set to: {Path}", DownloadPath);

                _toastManager.CreateToast("Folder path selected")
                    .WithContent($"Set path to: {selectedFolder.Name} folder")
                    .DismissOnClick()
                    .ShowInfo();

                if (DownloadTextBoxControl != null)
                {
                    DownloadTextBoxControl.Text = DownloadPath;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting download path");
        }
    }

    [RelayCommand]
    private async Task SelectRecoveryFile()
    {
        ThrowIfDisposed();

        try
        {
            var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (toplevel == null) return;

            var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a recovery file",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Recovery File")
                    {
                        Patterns = ["*.json"]
                    }
                ]
            });

            if (files.Count > 0)
            {
                var selectedFile = files[0];
                RecoveryFilePath = selectedFile.Path.LocalPath;

                _logger.LogDebug("Recovery file selected: {Path}", RecoveryFilePath);

                _toastManager.CreateToast("Recovery file selected")
                    .WithContent($"{selectedFile.Name}")
                    .DismissOnClick()
                    .ShowInfo();

                if (DataRecoveryTextBoxControl != null)
                {
                    DataRecoveryTextBoxControl.Text = RecoveryFilePath;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting recovery file");
        }
    }

    [RelayCommand]
    private async Task BackupNow()
    {
        ThrowIfDisposed();

        try
        {
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

            var jsonFileType = new FilePickerFileType("JSON File")
            {
                Patterns = ["*.json"],
                MimeTypes = ["application/json"]
            };

            var file = await toplevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Download backup JSON file",
                SuggestedStartLocation = startLocation,
                FileTypeChoices = [jsonFileType],
                SuggestedFileName = "Backup.json",
                ShowOverwritePrompt = true
            });

            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new System.IO.StreamWriter(stream);

                // TODO: Replace with actual backup data
                const string jsonContent = "{}";
                await writer.WriteAsync(jsonContent);

                _logger.LogInformation("Backup created at: {Path}", file.Path.LocalPath);

                _toastManager.CreateToast("Backup created")
                    .WithContent("Your backup has been successfully created.")
                    .DismissOnClick()
                    .ShowSuccess();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup");
            _toastManager.CreateToast("Backup Failed")
                .WithContent("Failed to create backup file")
                .DismissOnClick()
                .ShowError();
        }
    }

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

    #region Disposal

    protected override async ValueTask DisposeAsyncCore()
    {
        _logger.LogInformation("Disposing SettingsDialogCardViewModel");

        // Clear any cached settings
        _currentSettings = null;

        // Clear collections
        BackupFrequencyOptions.Clear();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    #endregion
}
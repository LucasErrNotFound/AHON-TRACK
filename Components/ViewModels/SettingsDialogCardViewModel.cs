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

namespace AHON_TRACK.Components.ViewModels;

public partial class SettingsDialogCardViewModel : ViewModelBase, INavigable
{
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
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly SettingsService _settingsService;
    private AppSettings? _currentSettings;

    public SettingsDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, SettingsService settingsService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _settingsService = settingsService;
        
        BackupFrequencyOptions = ["Everyday"];
        for (int i = 2; i <= 30; i++)
            BackupFrequencyOptions.Add($"{i} Days");
        
        _selectedBackupFrequency = BackupFrequencyOptions[0];
        
        _ = LoadSettingsAsync();
    }
    
    public SettingsDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _settingsService = new SettingsService();
        
        BackupFrequencyOptions = ["Everyday"];
        for (int i = 2; i <= 30; i++)
            BackupFrequencyOptions.Add($"{i} Days");
        
        _selectedBackupFrequency = BackupFrequencyOptions[0];
    }

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
    
        UpdateTextBoxes();
    }
    
    [RelayCommand]
    private async Task Apply()
    {
        if (_currentSettings == null)
            _currentSettings = new AppSettings();
            
        _currentSettings.DownloadPath = DownloadPath;
        _currentSettings.IsDarkMode = IsDarkMode;
        _currentSettings.BackupFrequency = SelectedBackupFrequency;
        _currentSettings.RecoveryFilePath = RecoveryFilePath;
        
        await _settingsService.SaveSettingsAsync(_currentSettings);
            
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }
    
    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }

    [RelayCommand]
    private async Task SetDownloadPath()
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
    
    [RelayCommand]
    private async Task SelectRecoveryFile()
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

    [RelayCommand]
    private async Task BackupNow()
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
                // If path is invalid, startLocation will remain null and use default (which is Documents folder for some weird reason SMH)
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
            
            _toastManager.CreateToast("Backup created")
                .WithContent("Your backup has been successfully created.")
                .DismissOnClick()
                .ShowSuccess();
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
    
    protected override void DisposeManagedResources()
    {
        // Clear UI controls' contents and release references
        if (DownloadTextBoxControl != null)
            DownloadTextBoxControl.Text = string.Empty;
        if (DataRecoveryTextBoxControl != null)
            DataRecoveryTextBoxControl.Text = string.Empty;

        DownloadTextBoxControl = null;
        DataRecoveryTextBoxControl = null;

        // Reset simple properties
        IsDarkMode = false;
        DownloadPath = string.Empty;
        RecoveryFilePath = string.Empty;
        SelectedBackupFrequency = string.Empty;

        // Aggressively clear collections
        BackupFrequencyOptions?.Clear();

        // Drop loaded settings and any cached objects
        _currentSettings = null;

        // Note: injected readonly services cannot be reassigned here; we clear what we own.

        base.DisposeManagedResources();
    }
}
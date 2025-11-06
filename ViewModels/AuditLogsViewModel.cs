using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using QuestPDF.Companion;
using QuestPDF.Fluent;
using ShadUI;
using NotificationType = Avalonia.Controls.Notifications.NotificationType;
using AHON_TRACK.Services.Events;

namespace AHON_TRACK.ViewModels;

[Page("audit-logs")]
public partial class AuditLogsViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _sortFilterItems = ["By ID", "Names by A-Z", "Names by Z-A", "By newest to oldest", "By oldest to newest", "Reset Data"];

    [ObservableProperty]
    private string _selectedSortFilterItem = "By newest to oldest";

    [ObservableProperty]
    private string[] _sortPositionItems = ["All", "Administrator", "Gym Staff"];

    [ObservableProperty]
    private string _selectedSortPositionItem = "All";

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private ObservableCollection<AuditLogItems> _auditLogs = [];

    [ObservableProperty]
    private List<AuditLogItems> _originalAuditLogData = [];

    [ObservableProperty]
    private List<AuditLogItems> _currentFilteredAuditLogData = [];

    [ObservableProperty]
    private string _searchStringResult = string.Empty;

    [ObservableProperty]
    private bool _isSearchingAuditLogs;

    [ObservableProperty]
    private bool _selectAll;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private AuditLogItems? _selectedAuditLogItem;

    [ObservableProperty]
    private double _value = 0;

    [ObservableProperty]
    private bool _isLoading;

    private bool _isLoadingDataFlag = false;

    private const string DefaultAvatarSource = "avares://AHON_TRACK/Assets/MainWindowView/user.png";

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IDashboardService _dashboardService;
    private readonly SettingsService _settingsService;
    private AppSettings? _currentSettings;

    public AuditLogsViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,
        SettingsService settingsService, IDashboardService dashboardService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _dashboardService = dashboardService;
        _settingsService = settingsService;

        _ = LoadDataFromDatabaseAsync();
        SubscribeToEvents();
        UpdateCounts();
    }

    public AuditLogsViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _settingsService = new SettingsService();
        _dashboardService = null!;

    }

    [AvaloniaHotReload]
    public async void Initialize()
    {
        if (IsInitialized) return;

        if (_dashboardService != null)
        {
            SubscribeToEvents();
            await LoadDataFromDatabaseAsync();
        }
        else
        {
            LoadSampleData();
        }

        UpdateCounts();
        IsInitialized = true;
    }

    private void SubscribeToEvents()
    {
        var eventService = DashboardEventService.Instance;
        eventService.RecentLogsUpdated += OnAuditDataChanged;
    }

    private async void OnAuditDataChanged(object? sender, EventArgs e)
    {
        if (_isLoadingDataFlag) return;
        try
        {
            _isLoadingDataFlag = true;
            await LoadDataFromDatabaseAsync();
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast($"Failed to load: {ex.Message}");
        }
        finally
        {
            _isLoadingDataFlag = false;
        }
    }

    private async Task LoadDataFromDatabaseAsync()
    {
        if (_isLoadingDataFlag) return; // ? Prevent duplicate loads

        _isLoadingDataFlag = true;
        IsLoading = true;

        try
        {
            // Get all audit logs from database - already returns AuditLogItems
            var auditLogItems = (await _dashboardService.GetAuditLogsAsync()).ToList();

            OriginalAuditLogData = auditLogItems;

            // ? FilterDataByDate already handles unsubscribe via RefreshAuditLogItems
            FilterDataByDate();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadDataFromDatabaseAsync] Error: {ex.Message}");

            if (_toastManager != null)
            {
                _toastManager.CreateToast($"Error. Failed to load audit logs from database{NotificationType.Error}");
            }

            // Fallback to sample data
            LoadSampleData();
        }
        finally
        {
            IsLoading = false;
            _isLoadingDataFlag = false; // ? Reset flag
        }
    }

    private void LoadSampleData()
    {
        var sampleAuditLogs = GetSampleAuditLogData();
        OriginalAuditLogData = sampleAuditLogs;
        FilterDataByDate();
    }

    private List<AuditLogItems> GetSampleAuditLogData()
    {
        var today = DateTime.Today;
        return
        [
            new AuditLogItems
            {
                ID = "1001",
                AvatarSource = ImageHelper.GetDefaultAvatar(),
                Name = "Joel Abalos",
                Username = "Admin1",
                Position = "Admin",
                DateAndTime = today.AddHours(14),
                Action = "Deleted Employee Data: Jedd Calubayan's data"
            },
            new AuditLogItems
            {
                ID = "1002",
                AvatarSource = ImageHelper.GetDefaultAvatar(),
                Name = "Sianrey Flora",
                Username = "Reylifts",
                Position = "Staff",
                DateAndTime = today.AddHours(13),
                Action = "Deleted Member Data: Marc Torres's data"
            },
            new AuditLogItems
            {
                ID = "1003",
                AvatarSource = ImageHelper.GetDefaultAvatar(),
                Name = "Mardie Dela Cruz",
                Username = "Figora",
                Position = "Staff",
                DateAndTime = today.AddHours(12),
                Action = "Deleted Member Data: Lim's data"
            },
            new AuditLogItems
            {
                ID = "1004",
                AvatarSource = ImageHelper.GetDefaultAvatar(),
                Name = "Robert Lucas",
                Username = "Musashi",
                Position = "Admin",
                DateAndTime = today.AddDays(1).AddHours(16),
                Action = "Added gym package: Karate"
            },
            new AuditLogItems
            {
                ID = "1005",
                AvatarSource =  ImageHelper.GetDefaultAvatar(),
                Name = "JL Taberdo",
                Username = "JeyEL",
                Position = "Admin",
                DateAndTime = today.AddDays(1).AddHours(17),
                Action = "Modified gym package: Boxing"
            },
            new AuditLogItems
            {
                ID = "1006",
                AvatarSource = ImageHelper.GetDefaultAvatar(),
                Name = "Dave Dapitillo",
                Username = "Dabai",
                Position = "Staff",
                DateAndTime = today.AddDays(-1).AddHours(18),
                Action = "Added gym package: Crossfit"
            },
            new AuditLogItems
            {
                ID = "1007",
                AvatarSource = ImageHelper.GetDefaultAvatar(),
                Name = "JC Casidor",
                Username = "Yhuitrick",
                Position = "Admin",
                DateAndTime = today.AddDays(-1).AddHours(20),
                Action = "Modified gym package: Zumba"
            }
        ];
    }

    [RelayCommand]
    private async Task SearchAuditLogs()
    {
        if (string.IsNullOrWhiteSpace(SearchStringResult))
        {
            // ? Unsubscribe before clearing
            foreach (var auditLog in AuditLogs)
            {
                auditLog.PropertyChanged -= OnAuditLogPropertyChanged;
            }

            // Reset to current filtered data
            AuditLogs.Clear();
            foreach (var auditLog in CurrentFilteredAuditLogData)
            {
                auditLog.PropertyChanged += OnAuditLogPropertyChanged;
                AuditLogs.Add(auditLog);
            }
            UpdateCounts();
            UpdateGaugeValue();
            return;
        }

        IsSearchingAuditLogs = true;

        try
        {
            await Task.Delay(500);

            // Search within the current filtered data
            var filteredAuditLogs = CurrentFilteredAuditLogData.Where(log =>
                log is { ID: not null, Name: not null, Username: not null, Position: not null, Action: not null } &&
                (log.ID.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 log.Name.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 log.Username.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 log.Position.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 log.Action.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 log.DateAndTime.ToString("MMMM d, yyyy").Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 log.DateAndTime.ToString("h:mm:ss tt").Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            // ? Unsubscribe before clearing
            foreach (var auditLog in AuditLogs)
            {
                auditLog.PropertyChanged -= OnAuditLogPropertyChanged;
            }

            AuditLogs.Clear();
            foreach (var auditLog in filteredAuditLogs)
            {
                auditLog.PropertyChanged += OnAuditLogPropertyChanged;
                AuditLogs.Add(auditLog);
            }
            UpdateCounts();
            UpdateGaugeValue();
        }
        finally
        {
            IsSearchingAuditLogs = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAuditLogs()
    {
        try
        {
            // Check if there are any audit logs to export
            if (AuditLogs.Count == 0)
            {
                _toastManager.CreateToast("No audit logs to export")
                    .WithContent("There are no audit logs available for the selected date.")
                    .DismissOnClick()
                    .ShowWarning();
                return;
            }

            var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (toplevel == null) return;

            IStorageFolder? startLocation = null;
            if (!string.IsNullOrWhiteSpace(_currentSettings?.DownloadPath))
            {
                try
                {
                    startLocation = await toplevel.StorageProvider.TryGetFolderFromPathAsync(_currentSettings.DownloadPath);
                }
                catch
                {
                    // If path is invalid, startLocation will remain null
                }
            }

            var fileName = $"Audit_Logs_{SelectedDate:yyyy-MM-dd}.pdf";
            var pdfFile = await toplevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Download Audit Logs",
                SuggestedStartLocation = startLocation,
                FileTypeChoices = [FilePickerFileTypes.Pdf],
                SuggestedFileName = fileName,
                ShowOverwritePrompt = true
            });

            if (pdfFile == null) return;

            var auditLogModel = new AuditLogDocumentModel
            {
                GeneratedDate = DateTime.Today,
                GymName = "AHON Victory Fitness Gym",
                GymAddress = "2nd Flr. Event Hub, Victory Central Mall, Brgy. Balibago, Sta. Rosa City, Laguna",
                GymPhone = "+63 123 456 7890",
                GymEmail = "info@ahonfitness.com",
                Items = AuditLogs.Select(auditLog => new AuditLogItem
                {
                    ID = Convert.ToInt32(auditLog.ID),
                    Name = auditLog.Name,
                    Position = auditLog.Position,
                    LogCount = auditLog.LogCount ?? 0,
                    DateAndTime = auditLog.DateAndTime,
                    Action = auditLog.Action
                }).ToList()
            };

            var document = new AuditLogDocument(auditLogModel);

            await using var stream = await pdfFile.OpenWriteAsync();

            // Both cannot be enabled at the same time. Disable one of them 
            document.GeneratePdf(stream); // Generate the PDF
                                          // await document.ShowInCompanionAsync(); // For Hot-Reload Debugging

            _toastManager.CreateToast("Audit Logs exported successfully")
                .WithContent($"Audit Logs has been saved to {pdfFile.Name}")
                .DismissOnClick()
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Export failed")
                .WithContent($"Failed to export audit logs: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private async Task LoadSettingsAsync() => _currentSettings = await _settingsService.LoadSettingsAsync();

    [RelayCommand]
    private void SortReset()
    {
        SelectedSortFilterItem = "By ID";
        SelectedSortPositionItem = "All";
    }

    [RelayCommand]
    private void SortById()
    {
        SelectedSortFilterItem = "By ID";
        ApplyAuditLogSort();
    }

    [RelayCommand]
    private void SortNamesByAlphabetical()
    {
        SelectedSortFilterItem = "Names by A-Z";
        ApplyAuditLogSort();
    }

    [RelayCommand]
    private void SortNamesByReverseAlphabetical()
    {
        SelectedSortFilterItem = "Names by Z-A";
        ApplyAuditLogSort();
    }

    [RelayCommand]
    private void SortDateByNewestToOldest()
    {
        SelectedSortFilterItem = "By newest to oldest";
        ApplyAuditLogSort();
    }

    [RelayCommand]
    private void SortDateByOldestToNewest()
    {
        SelectedSortFilterItem = "By oldest to newest";
        ApplyAuditLogSort();
    }

    [RelayCommand]
    private void ToggleSelection(bool? isChecked)
    {
        var shouldSelect = isChecked ?? false;

        foreach (var logs in AuditLogs)
        {
            logs.IsSelected = shouldSelect;
        }
        UpdateCounts();
    }

    private void ApplyAuditLogSort()
    {
        if (OriginalAuditLogData.Count == 0) return;

        if (SelectedSortFilterItem == "Reset Data")
        {
            SelectedSortPositionItem = "All";
            SelectedSortFilterItem = "By ID";
            FilterDataByDate();
            return;
        }

        FilterDataByDate();
    }

    private void ApplyAuditLogPositionFilter() => FilterDataByDate();

    private void FilterDataByDate()
    {
        if (OriginalAuditLogData.Count == 0) return;

        // Filter by selected date
        var dateFilteredData = OriginalAuditLogData
            .Where(log => log.DateAndTime.Date == SelectedDate.Date)
            .ToList();

        // Apply position filter to date-filtered data
        List<AuditLogItems> positionFilteredData;
        if (SelectedSortPositionItem == "All")
        {
            positionFilteredData = dateFilteredData.ToList();
        }
        else
        {
            positionFilteredData = dateFilteredData
                .Where(auditLog => auditLog.Position == SelectedSortPositionItem)
                .ToList();
        }

        // Apply current sorting to the filtered data
        List<AuditLogItems> sortedList = SelectedSortFilterItem switch
        {
            "By ID" => positionFilteredData.OrderBy(log => int.Parse(log.ID ?? "0")).ToList(),
            "Names by A-Z" => positionFilteredData.OrderBy(log => log.Name).ToList(),
            "Names by Z-A" => positionFilteredData.OrderByDescending(log => log.Name).ToList(),
            "By newest to oldest" => positionFilteredData.OrderByDescending(log => log.DateAndTime).ToList(),
            "By oldest to newest" => positionFilteredData.OrderBy(log => log.DateAndTime).ToList(),
            "Reset Data" => positionFilteredData.ToList(),
            _ => positionFilteredData.ToList()
        };

        CurrentFilteredAuditLogData = sortedList;
        RefreshAuditLogItems(sortedList);
    }

    private void RefreshAuditLogItems(List<AuditLogItems> items)
    {
        // 1?? Unsubscribe before replacing collection
        foreach (var log in AuditLogs)
            log.PropertyChanged -= OnAuditLogPropertyChanged;

        // 2?? Replace collection reference
        AuditLogs = new ObservableCollection<AuditLogItems>(items);

        // 3?? Subscribe to new items
        foreach (var log in AuditLogs)
            log.PropertyChanged += OnAuditLogPropertyChanged;

        // 4?? Update state
        UpdateCounts();
        UpdateGaugeValue();
    }


    private void UpdateGaugeValue()
    {
        Value = AuditLogs.Count;
    }

    private void UpdateCounts()
    {
        SelectedCount = AuditLogs.Count(x => x.IsSelected);
        TotalCount = AuditLogs.Count;
        SelectAll = AuditLogs.Count > 0 && AuditLogs.All(x => x.IsSelected);
    }

    private void OnAuditLogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AuditLogItems.IsSelected))
        {
            UpdateCounts();
        }
    }

    partial void OnSelectedSortFilterItemChanged(string value)
    {
        ApplyAuditLogSort();
    }

    partial void OnSelectedSortPositionItemChanged(string value)
    {
        ApplyAuditLogPositionFilter();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        FilterDataByDate();
    }

    partial void OnSearchStringResultChanged(string value)
    {
        SearchAuditLogsCommand.Execute(null);
    }

    // Add to AuditLogsViewModel class

    protected override void DisposeManagedResources()
    {
        // Unsubscribe from events
        var eventService = DashboardEventService.Instance;
        eventService.RecentLogsUpdated -= OnAuditDataChanged;

        // Unsubscribe from property change handlers
        foreach (var log in AuditLogs)
        {
            log.PropertyChanged -= OnAuditLogPropertyChanged;
        }

        // Clear collections
        AuditLogs.Clear();
        OriginalAuditLogData.Clear();
        CurrentFilteredAuditLogData.Clear();

        base.DisposeManagedResources();
        ForceGarbageCollection();
    }
}

public partial class AuditLogItems : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string? _iD;

    [ObservableProperty]
    private object? _avatarSource; // Can be string path or Bitmap

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _position;

    [ObservableProperty]
    private DateTime _dateAndTime;

    [ObservableProperty]
    private string? _action;

    [ObservableProperty]
    private decimal? _logCount;

    public string FormattedDateTime => DateAndTime.ToString("MMMM dd, yyyy h:mm:ss tt");
}
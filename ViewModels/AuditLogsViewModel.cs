using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

[Page("audit-logs")]
public partial class AuditLogsViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty] 
    private string[] _sortFilterItems = ["By ID", "Names by A-Z", "Names by Z-A", "By newest to oldest", "By oldest to newest", "Reset Data"];

    [ObservableProperty] 
    private string _selectedSortFilterItem = "By newest to oldest";
    
    [ObservableProperty] 
    private string[] _sortPositionItems = ["All", "Admin", "Staff"];

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
    
    private const string DefaultAvatarSource = "avares://AHON_TRACK/Assets/MainWindowView/user.png";
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public AuditLogsViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        
        LoadSampleData();
        UpdateCounts();
    }

    public AuditLogsViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        LoadSampleData();
        UpdateCounts();
        IsInitialized = true;
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
                AvatarSource = DefaultAvatarSource,
                Name = "Joel Abalos",
                Username = "Admin1",
                Position = "Admin",
                DateAndTime = today.AddHours(14),
                Action = "Deleted Employee Data: Jedd Calubayan's data"
            },
            new AuditLogItems
            {
	            ID = "1002",
	            AvatarSource = DefaultAvatarSource,
	            Name = "Sianrey Flora",
	            Username = "Reylifts",
	            Position = "Staff",
	            DateAndTime = today.AddHours(13),
	            Action = "Deleted Member Data: Marc Torres's data"
            },
            new AuditLogItems
            {
	            ID = "1003",
	            AvatarSource = DefaultAvatarSource,
	            Name = "Mardie Dela Cruz",
	            Username = "Figora",
	            Position = "Staff",
	            DateAndTime = today.AddHours(12),
	            Action = "Deleted Member Data: Lim's data"
            },
            new AuditLogItems
            {
	            ID = "1004",
	            AvatarSource = DefaultAvatarSource,
	            Name = "Robert Lucas",
	            Username = "Musashi",
	            Position = "Admin",
	            DateAndTime = today.AddDays(1).AddHours(16),
	            Action = "Added gym package: Karate"
            },
            new AuditLogItems
            {
	            ID = "1005",
	            AvatarSource = DefaultAvatarSource,
	            Name = "JL Taberdo",
	            Username = "JeyEL",
	            Position = "Admin",
	            DateAndTime = today.AddDays(1).AddHours(17),
	            Action = "Modified gym package: Boxing"
            },
            new AuditLogItems
            {
	            ID = "1006",
	            AvatarSource = DefaultAvatarSource,
	            Name = "Dave Dapitillo",
	            Username = "Dabai",
	            Position = "Staff",
	            DateAndTime = today.AddDays(-1).AddHours(18),
	            Action = "Added gym package: Crossfit"
            },
            new AuditLogItems
            {
	            ID = "1007",
	            AvatarSource = DefaultAvatarSource,
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
			// Reset to current filtered data instead of original data
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

			// Search within the current filtered data instead of original data
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
			"By ID" => positionFilteredData.OrderBy(log => log.ID).ToList(),
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
	
	private void RefreshAuditLogItems(List<AuditLogItems> auditLogItems)
	{
		AuditLogs.Clear();
		foreach (var logs in auditLogItems)
		{
			logs.PropertyChanged += OnAuditLogPropertyChanged;
			AuditLogs.Add(logs);
		}
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
}

public partial class AuditLogItems : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;
    
    [ObservableProperty] 
    private string? _iD;
    
    [ObservableProperty]
    private string? _avatarSource;
    
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

    public string FormattedDateTime => DateAndTime.ToString("MMMM dd, yyyy h:mm:ss tt");
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Components.ViewModels;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

[Page("manage-membership")]
public sealed partial class ManageMembershipViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
	[ObservableProperty]
	private List<ManageMembersItem> _originalMemberData = [];

	[ObservableProperty]
	private ObservableCollection<ManageMembersItem> _memberItems = [];

	[ObservableProperty]
	private List<ManageMembersItem> _currentFilteredData = [];

	[ObservableProperty]
	private string _searchStringResult = string.Empty;

	[ObservableProperty]
	private bool _isSearchingMember;

	[ObservableProperty]
	private bool _selectAll;

	[ObservableProperty]
	private int _selectedCount;

	[ObservableProperty]
	private int _totalCount;

	[ObservableProperty]
	private bool _showIdColumn = true;

	[ObservableProperty]
	private bool _showPictureColumn = true;

	[ObservableProperty]
	private bool _showNameColumn = true;

	[ObservableProperty]
	private bool _showContactNumberColumn = true;
	
	[ObservableProperty]
	private bool _showAvailedPackagesColumn = true;

	[ObservableProperty]
	private bool _showStatusColumn = true;

	[ObservableProperty]
	private bool _showValidity = true;

	[ObservableProperty]
	private int _selectedSortIndex = -1;

	[ObservableProperty]
	private int _selectedFilterIndex = -1;

	[ObservableProperty]
	private bool _isInitialized;

	[ObservableProperty]
	private ManageMembersItem? _selectedMember;

	private const string DefaultAvatarSource = "avares://AHON_TRACK/Assets/MainWindowView/user.png";
	
	private readonly DialogManager _dialogManager;
	private readonly ToastManager _toastManager;
	private readonly PageManager _pageManager;
	private readonly MemberDialogCardViewModel  _memberDialogCardViewModel;
	private readonly AddNewMemberViewModel _addNewMemberViewModel;

	public bool IsActiveVisible => SelectedMember?.Status.Equals("active", StringComparison.OrdinalIgnoreCase) ?? false;
	public bool IsInactiveVisible => SelectedMember?.Status.Equals("inactive", StringComparison.OrdinalIgnoreCase) ?? false;
	public bool IsTerminatedVisible => SelectedMember?.Status.Equals("terminated", StringComparison.OrdinalIgnoreCase) ?? false;
	public bool HasSelectedMember => SelectedMember is not null;

	public ManageMembershipViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,  MemberDialogCardViewModel memberDialogCardViewModel, AddNewMemberViewModel addNewMemberViewModel)
	{
		_dialogManager = dialogManager;
		_toastManager = toastManager;
		_pageManager = pageManager;
		_memberDialogCardViewModel = memberDialogCardViewModel;
		_addNewMemberViewModel = addNewMemberViewModel;
		
		LoadSampleData();
		UpdateCounts();
	}

	public ManageMembershipViewModel()
	{ 
		_dialogManager = new DialogManager();
		_toastManager = new ToastManager();
		_pageManager = new PageManager(new ServiceProvider());
		_memberDialogCardViewModel = new MemberDialogCardViewModel();
		_addNewMemberViewModel = new AddNewMemberViewModel();
	}

	partial void OnSelectedMemberChanged(ManageMembersItem? value)
	{
		OnPropertyChanged(nameof(IsActiveVisible));
		OnPropertyChanged(nameof(IsInactiveVisible));
		OnPropertyChanged(nameof(IsTerminatedVisible));
		OnPropertyChanged(nameof(HasSelectedMember));
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
		var sampleMembers = GetSampleMembersData();

		OriginalMemberData = sampleMembers;
		CurrentFilteredData = [.. sampleMembers]; // Initialize current state

		MemberItems.Clear();
		foreach (var member in sampleMembers)
		{
			member.PropertyChanged += OnMemberPropertyChanged;
			MemberItems.Add(member);
		}

		TotalCount = MemberItems.Count;
		
		if (MemberItems.Count > 0)
		{
			SelectedMember = MemberItems[0];
		}
	}

	private List<ManageMembersItem> GetSampleMembersData()
	{
		return
		[
			new ManageMembersItem
			{
				ID = "1001",
				AvatarSource = DefaultAvatarSource,
				Name = "Jedd Calubayan",
				ContactNumber = "0975 994 3010",
				AvailedPackages = "Boxing",
				Status = "Active",
				Validity = new DateTime(2025, 6, 16)
			},
			
			new ManageMembersItem
			{
				ID = "1002",
				AvatarSource = DefaultAvatarSource,
				Name = "Marc Torres",
				ContactNumber = "0975 994 3010",
				AvailedPackages = "None",
				Status = "Inactive",
				Validity = new DateTime(2025, 7, 16)
			},
			
			new ManageMembersItem
			{
				ID = "1003",
				AvatarSource = DefaultAvatarSource,
				Name = "Mardie Dela Cruz",
				ContactNumber = "0975 994 3010",
				AvailedPackages = "None",
				Status = "Inactive",
				Validity = new DateTime(2025, 7, 18)
			},
			
			new ManageMembersItem
			{
				ID = "1004",
				AvatarSource = DefaultAvatarSource,
				Name = "Mark Dela Cruz",
				ContactNumber = "0975 994 3010",
				AvailedPackages = "Muay thai, Boxing, Crossfit, Gym",
				Status = "Active",
				Validity = new DateTime(2025, 7, 18)
			},
			
			new ManageMembersItem
			{
				ID = "1005",
				AvatarSource = DefaultAvatarSource,
				Name = "JL Taberdo",
				ContactNumber = "0975 994 3010",
				AvailedPackages = "Gym",
				Status = "Terminated",
				Validity = new DateTime(2025, 4, 18)
			},
		];
	}
	
    [RelayCommand]
    private void SortReset()
    {
        MemberItems.Clear();
        foreach (var member in OriginalMemberData)
        {
            member.PropertyChanged += OnMemberPropertyChanged;
            MemberItems.Add(member);
        }

        CurrentFilteredData = [.. OriginalMemberData];
        UpdateCounts();

        SelectedSortIndex = -1;
        SelectedFilterIndex = -1;
    }

    [RelayCommand]
    private void SortById()
    {
        var sortedById = MemberItems.OrderBy(member => member.ID).ToList();
        MemberItems.Clear();

        foreach (var members in sortedById)
        {
            MemberItems.Add(members);
        }
        // Update current filtered data to match sorted state
        CurrentFilteredData = [.. sortedById];
    }

    [RelayCommand]
    private void SortNamesByAlphabetical()
    {
        var sortedNamesInAlphabetical = MemberItems.OrderBy(member => member.Name).ToList();
        MemberItems.Clear();

        foreach (var members in sortedNamesInAlphabetical)
        {
            MemberItems.Add(members);
        }
        CurrentFilteredData = [.. sortedNamesInAlphabetical];
    }

    [RelayCommand]
    private void SortNamesByReverseAlphabetical()
    {
        var sortedReverseNamesInAlphabetical = MemberItems.OrderByDescending(member => member.Name).ToList();
        MemberItems.Clear();

        foreach (var members in sortedReverseNamesInAlphabetical)
        {
            MemberItems.Add(members);
        }
        CurrentFilteredData = [.. sortedReverseNamesInAlphabetical];
    }

    [RelayCommand]
    private void SortDateByNewestToOldest()
    {
        var sortedDates = MemberItems.OrderByDescending(log => log.Validity).ToList();
        MemberItems.Clear();

        foreach (var logs in sortedDates)
        {
            MemberItems.Add(logs);
        }
        CurrentFilteredData = [.. sortedDates];
    }

    [RelayCommand]
    private void SortDateByOldestToNewest()
    {
        var sortedDates = MemberItems.OrderBy(log => log.Validity).ToList();
        MemberItems.Clear();

        foreach (var logs in sortedDates)
        {
            MemberItems.Add(logs);
        }
        CurrentFilteredData = [.. sortedDates];
    }

    [RelayCommand]
    private void FilterActiveStatus()
    {
        var filterActiveStatus = OriginalMemberData.Where(member => member.Status.Equals("active", StringComparison.OrdinalIgnoreCase)).ToList();
        MemberItems.Clear();

        foreach (var member in filterActiveStatus)
        {
            member.PropertyChanged += OnMemberPropertyChanged;
            MemberItems.Add(member);
        }
        CurrentFilteredData = [.. filterActiveStatus];
        UpdateCounts();
    }

    [RelayCommand]
    private void FilterInactiveStatus()
    {
        var filterInactiveStatus = OriginalMemberData.Where(member => member.Status.Equals("inactive", StringComparison.OrdinalIgnoreCase)).ToList();
        MemberItems.Clear();

        foreach (var member in filterInactiveStatus)
        {
            member.PropertyChanged += OnMemberPropertyChanged;
            MemberItems.Add(member);
        }
        CurrentFilteredData = [.. filterInactiveStatus];
        UpdateCounts();
    }

    [RelayCommand]
    private void FilterTerminatedStatus()
    {
        var filterTerminatedStatus = OriginalMemberData.Where(member => member.Status.Equals("terminated", StringComparison.OrdinalIgnoreCase)).ToList();
        MemberItems.Clear();

        foreach (var member in filterTerminatedStatus)
        {
            member.PropertyChanged += OnMemberPropertyChanged;
            MemberItems.Add(member);
        }
        CurrentFilteredData = [.. filterTerminatedStatus];
        UpdateCounts();
    }

    [RelayCommand]
    private void ToggleSelection(bool? isChecked)
    {
        var shouldSelect = isChecked ?? false;

        foreach (var item in MemberItems)
        {
            item.IsSelected = shouldSelect;
        }
        UpdateCounts();
    }

	private void OnMemberPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ManageMembersItem.IsSelected))
		{
			UpdateCounts();
		}
	}
	
	private void UpdateCounts()
	{
		SelectedCount = MemberItems.Count(x => x.IsSelected);
		TotalCount = MemberItems.Count;

		SelectAll = MemberItems.Count > 0 && MemberItems.All(x => x.IsSelected);
	}
	
	[RelayCommand]
	private async Task SearchMembers()
	{
		if (string.IsNullOrWhiteSpace(SearchStringResult))
		{
			// Reset to current filtered data instead of original data
			MemberItems.Clear();
			foreach (var member in CurrentFilteredData)
			{
				member.PropertyChanged += OnMemberPropertyChanged;
				MemberItems.Add(member);
			}
			UpdateCounts();
			return;
		}
		IsSearchingMember = true;

		try
		{
			await Task.Delay(500);

			// Search within the current filtered data instead of original data
			var filteredMembers = CurrentFilteredData.Where(emp =>
				emp.ID.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
				emp.Name.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
				emp.ContactNumber.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
				emp.AvailedPackages.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
				emp.Status.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
				emp.Validity.ToString("MMMM d, yyyy").Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase)
			).ToList();

			MemberItems.Clear();
			foreach (var members in filteredMembers)
			{
				members.PropertyChanged += OnMemberPropertyChanged;
				MemberItems.Add(members);
			}
			UpdateCounts();
		}
		finally
		{
			IsSearchingMember = false;
		}
	}
	
    [RelayCommand]
    private async Task ShowCopySingleMemberName(ManageMembersItem? member)
    {
        if (member == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(member.Name);
        }

        _toastManager.CreateToast("Copy Member Name")
            .WithContent($"Copied {member.Name}'s name successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private async Task ShowCopyMultipleMemberName(ManageMembersItem? member)
    {
        var selectedMembers = MemberItems.Where(item => item.IsSelected).ToList();
        if (member == null) return;
        if (selectedMembers.Count == 0) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            var memberNames = string.Join(", ", selectedMembers.Select(emp => emp.Name));
            await clipboard.SetTextAsync(memberNames);

            _toastManager.CreateToast("Copy Member Names")
                .WithContent($"Copied multiple member names successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowInfo();
        }
    }

    [RelayCommand]
    private async Task ShowCopySingleMemberId(ManageMembersItem? member)
    {
        if (member == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(member.ID);
        }

        _toastManager.CreateToast("Copy Member ID")
            .WithContent($"Copied {member.Name}'s ID successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private async Task ShowCopyMultipleMemberId(ManageMembersItem? member)
    {
        var selectedMembers = MemberItems.Where(item => item.IsSelected).ToList();
        if (member == null) return;
        if (selectedMembers.Count == 0) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            var memberIDs = string.Join(", ", selectedMembers.Select(emp => emp.ID));
            await clipboard.SetTextAsync(memberIDs);

            _toastManager.CreateToast("Copy Member ID")
                .WithContent($"Copied multiple ID successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowInfo();
        }
    }

    [RelayCommand]
    private async Task ShowCopySingleMemberContactNumber(ManageMembersItem? member)
    {
        if (member == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(member.ContactNumber);
        }

        _toastManager.CreateToast("Copy Member Contact No.")
            .WithContent($"Copied {member.Name}'s contact number successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private async Task ShowCopyMultipleMemberContactNumber(ManageMembersItem? member)
    {
        var selectedMembers = MemberItems.Where(item => item.IsSelected).ToList();
        if (member == null) return;
        if (selectedMembers.Count == 0) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            var memberContactNumbers = string.Join(", ", selectedMembers.Select(emp => emp.ContactNumber));
            await clipboard.SetTextAsync(memberContactNumbers);

            _toastManager.CreateToast("Copy Member Contact No.")
                .WithContent($"Copied multiple member contact numbers successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowInfo();
        }
    }

    [RelayCommand]
    private async Task ShowCopySingleMemberStatus(ManageMembersItem? member)
    {
        if (member == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(member.Status);
        }

        _toastManager.CreateToast("Copy Member Status")
            .WithContent($"Copied {member.Name}'s status successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private async Task ShowCopySingleMemberValidity(ManageMembersItem? member)
    {
        if (member == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(member.Validity.ToString(CultureInfo.InvariantCulture));
        }

        _toastManager.CreateToast("Copy Member Date Joined")
            .WithContent($"Copied {member.Name}'s date joined successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private void ShowSingleItemDeletionDialog(ManageMembersItem? member)
    {
        if (member == null) return;

        try
        {
            Debug.WriteLine($"Showing deletion dialog for member: {member.Name}");
            _dialogManager.CreateDialog("" +
                "Are you absolutely sure?",
                $"This action cannot be undone. This will permanently delete {member.Name} and remove the data from your database.")
                .WithPrimaryButton("Continue", () => OnSubmitDeleteSingleItem(member), DialogButtonStyle.Destructive)
                .WithCancelButton("Cancel")
                .WithMaxWidth(512)
                .Dismissible()
                .Show();
            Debug.WriteLine("Deletion dialog shown successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing deletion dialog: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ShowMultipleItemDeletionDialog(ManageMembersItem? member)
    {
        if (member == null) return;

        _dialogManager.CreateDialog("" +
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete multiple accounts and remove their data from your database.")
            .WithPrimaryButton("Continue", () => OnSubmitDeleteMultipleItems(member), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    private async Task OnSubmitDeleteSingleItem(ManageMembersItem member)
    {
        await DeleteMemberFromDatabase(member);
        member.PropertyChanged -= OnMemberPropertyChanged;
        MemberItems.Remove(member);
        UpdateCounts();

        _toastManager.CreateToast($"Delete {member.Name} Account")
            .WithContent($"{member.Name}'s Account deleted successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowSuccess();
    }
    private async Task OnSubmitDeleteMultipleItems(ManageMembersItem member)
    {
        var selectedMembers = MemberItems.Where(item => item.IsSelected).ToList();
        if (!selectedMembers.Any()) return;

        foreach (var members in selectedMembers)
        {
            await DeleteMemberFromDatabase(member);
            members.PropertyChanged -= OnMemberPropertyChanged;
            MemberItems.Remove(members);
        }
        UpdateCounts();

        _toastManager.CreateToast($"Delete Selected Accounts")
            .WithContent($"Multiple accounts deleted successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowSuccess();
    }

    // Helper method to delete from database
    private async Task DeleteMemberFromDatabase(ManageMembersItem member)
    {
        // using var connection = new SqlConnection(connectionString);
        // await connection.ExecuteAsync("DELETE FROM Members WHERE ID = @ID", new { IDI = member.ID });

        await Task.Delay(100); // Just an animation/simulation of async operation
    }

    private void ExecuteSortCommand(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                SortByIdCommand.Execute(null);
                break;
            case 1:
                SortNamesByAlphabeticalCommand.Execute(null);
                break;
            case 2:
                SortNamesByReverseAlphabeticalCommand.Execute(null);
                break;
            case 3:
                SortDateByNewestToOldestCommand.Execute(null);
                break;
            case 4:
                SortDateByOldestToNewestCommand.Execute(null);
                break;
            case 5:
                SortResetCommand.Execute(null);
                break;
        }
    }

    private void ExecuteFilterCommand(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0: FilterActiveStatusCommand.Execute(null); break;
            case 1: FilterInactiveStatusCommand.Execute(null); break;
            case 2: FilterTerminatedStatusCommand.Execute(null); break;
        }
    }

    partial void OnSelectedSortIndexChanged(int value)
    {
        if (value >= 0)
        {
            ExecuteSortCommand(value);
        }
    }

    partial void OnSelectedFilterIndexChanged(int value)
    {
        if (value >= 0)
        {
            ExecuteFilterCommand(value);
        }
    }

    partial void OnSearchStringResultChanged(string value)
    {
        SearchMembersCommand.Execute(null);
    }

	[RelayCommand]
	private void OpenAddNewMemberView()
	{
		_pageManager.Navigate<AddNewMemberViewModel>();
	} 
	
	[RelayCommand]
	private void OpenUpgradeMemberView()
	{
		_pageManager.Navigate<AddNewMemberViewModel>();
	} 
	
	[RelayCommand]
	private void OpenRenewMemberView()
	{
		_pageManager.Navigate<AddNewMemberViewModel>();
	} 
	
	[RelayCommand]
	private void ShowDeleteMember()
	{
		_dialogManager
			.CreateDialog(
				"Are you absolutely sure?",
				"This action cannot be undone. This will permanently delete and remove this member's data from your server.")
			.WithPrimaryButton("Continue",
				() => _toastManager.CreateToast("Delete data")
					.WithContent("Data deleted successfully!")
					.DismissOnClick()
					.ShowSuccess()
				, DialogButtonStyle.Destructive)
			.WithCancelButton("Cancel")
			.WithMaxWidth(512)
			.Dismissible()
			.Show();
	} 
	
	[RelayCommand]
	private void ShowModifyMemberDialog(ManageMembersItem member)
	{
		_memberDialogCardViewModel.Initialize();
		_dialogManager.CreateDialog(_memberDialogCardViewModel)
			.WithSuccessCallback(_ =>
				_toastManager.CreateToast("Modified an existing gym member information")
					.WithContent($"You just modified {member.Name} information!")
					.DismissOnClick()
					.ShowSuccess())
			.WithCancelCallback(() =>
				_toastManager.CreateToast("Cancellation of modification")
					.WithContent($"You just cancelled modifying {member.Name} information!")
					.DismissOnClick()
					.ShowWarning()).WithMaxWidth(950)
			.Dismissible()
			.Show();
	}
}
public partial class ManageMembersItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _iD = string.Empty;

    [ObservableProperty]
    private string _avatarSource = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _contactNumber = string.Empty;
    
    [ObservableProperty]
    private string _availedPackages = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private DateTime _validity;

    public IBrush StatusForeground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),     // Green-500
        "inactive" => new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Gray-500
        "terminated" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red-500
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))           // Default Gray-500
    };

    public IBrush StatusBackground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),     // Green-500 with alpha
        "inactive" => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139)), // Gray-500 with alpha
        "terminated" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)), // Red-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))           // Default Gray-500 with alpha
    };

    public string StatusDisplayText => Status.ToLowerInvariant() switch
    {
        "active" => "● Active",
        "inactive" => "● Inactive",
        "terminated" => "● Terminated",
        _ => Status
    };

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusDisplayText));
    }
}
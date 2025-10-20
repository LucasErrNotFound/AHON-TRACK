using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
	private string[] _sortFilterItems = [
		"By ID", "Names by A-Z", "Names by Z-A", "By newest to oldest", "By oldest to newest", "Reset Data"
	];

	[ObservableProperty] 
	private string _selectedSortFilterItem = "By newest to oldest";
	
	[ObservableProperty] 
	private string[] _statusFilterItems = ["All", "Active", "Expired"];

	[ObservableProperty] 
	private string _selectedStatusFilterItem = "All";
	
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
	private bool _isInitialized;

	[ObservableProperty]
	private ManageMembersItem? _selectedMember;

	public bool CanDeleteSelectedMembers
	{
		get
		{
			// If there is no checked items, can't delete
			var selectedMembers = MemberItems.Where(item => item.IsSelected).ToList();
			if (selectedMembers.Count == 0) return false;

			// If the currently selected row is present and its status is not expired,
			// then "Delete Selected" should be disabled when opening the menu for that row.
			if (SelectedMember is not null && 
			    !SelectedMember.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			// Only allow deletion if ALL selected members are Expired
			return selectedMembers.All(member => member.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase));
		}
	}
	
	public bool IsDeleteButtonEnabled =>
		!new[] { "Expired" }
			.Any(status => SelectedMember is not null && SelectedMember.Status.
				Equals(status, StringComparison.OrdinalIgnoreCase));

	public bool IsUpgradeButtonEnabled =>
		!new[] { "Expired" }
			.Any(status => SelectedMember is not null && SelectedMember.Status.
				Equals(status, StringComparison.OrdinalIgnoreCase));
	
	public bool IsRenewButtonEnabled =>
		!new[] { "Active" }
			.Any(status => SelectedMember is not null && SelectedMember.Status
				.Equals(status, StringComparison.OrdinalIgnoreCase));
	
	public bool IsActiveVisible => SelectedMember?.Status.Equals("active", StringComparison.OrdinalIgnoreCase) ?? false;
	public bool IsExpiredVisible => SelectedMember?.Status.Equals("expired", StringComparison.OrdinalIgnoreCase) ?? false;
	public bool HasSelectedMember => SelectedMember is not null;
	
	private const string DefaultAvatarSource = "avares://AHON_TRACK/Assets/MainWindowView/user.png";
	
	private readonly DialogManager _dialogManager;
	private readonly ToastManager _toastManager;
	private readonly PageManager _pageManager;
	private readonly MemberDialogCardViewModel  _memberDialogCardViewModel;
	private readonly AddNewMemberViewModel _addNewMemberViewModel;

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
		OnPropertyChanged(nameof(IsExpiredVisible));
		OnPropertyChanged(nameof(HasSelectedMember));
		OnPropertyChanged(nameof(IsDeleteButtonEnabled));
		OnPropertyChanged(nameof(IsUpgradeButtonEnabled));
		OnPropertyChanged(nameof(IsRenewButtonEnabled));
		OnPropertyChanged(nameof(CanDeleteSelectedMembers));
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
		
		ApplyMemberStatusFilter();
		ApplyMemberSort();
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
				Gender = "Male",
				BirthDate = new DateTime(2000, 1, 1),
				Validity = new DateTime(2025, 6, 16)
			},
			
			new ManageMembersItem
			{
				ID = "1002",
				AvatarSource = DefaultAvatarSource,
				Name = "Marc Torres",
				ContactNumber = "0975 994 3010",
				AvailedPackages = "None",
				Status = "Expired",
				Gender = "Male",
				BirthDate = new DateTime(2004, 5, 1),
				Validity = new DateTime(2025, 7, 16)
			},
			
			new ManageMembersItem
			{
				ID = "1003",
				AvatarSource = DefaultAvatarSource,
				Name = "Mardie Dela Cruz",
				ContactNumber = "0975 994 3010",
				AvailedPackages = "None",
				Status = "Expired",
				Gender = "Male",
				BirthDate = new DateTime(2002, 11, 12),
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
				Gender = "Female",
				BirthDate = new DateTime(2004, 5, 9),
				Validity = new DateTime(2025, 7, 18)
			},
			
			new ManageMembersItem
			{
				ID = "1005",
				AvatarSource = DefaultAvatarSource,
				Name = "JL Taberdo",
				ContactNumber = "0975 994 3010",
				AvailedPackages = "Gym",
				Status = "Expired",
				Gender = "Male",
				BirthDate = new DateTime(2002, 5, 9),
				Validity = new DateTime(2025, 4, 18)
			},
			
			new ManageMembersItem
			{
				ID = "1006",
				AvatarSource = DefaultAvatarSource,
				Name = "Robert Xyz B. Lucas",
				ContactNumber = "0975 994 3010",
				AvailedPackages = "Gym",
				Status = "Active",
				Gender = "Male",
				BirthDate = new DateTime(2004, 5, 21),
				Validity = new DateTime(2025, 4, 18)
			},
			
			new ManageMembersItem
			{
				ID = "1007",
				AvatarSource = DefaultAvatarSource,
				Name = "Sianrey V. Flora",
				ContactNumber = "0975 994 3010",
				AvailedPackages = "Gym",
				Status = "Active",
				Gender = "Male",
				BirthDate = new DateTime(2004, 9, 9),
				Validity = new DateTime(2025, 4, 18)
			},
			
			new ManageMembersItem
			{
				ID = "1008",
				AvatarSource = DefaultAvatarSource,
				Name = "Marion James Dela Roca",
				ContactNumber = "0975 994 3010",
				AvailedPackages = "Gym",
				Status = "Active",
				Gender = "Male",
				BirthDate = new DateTime(2001, 5, 9),
				Validity = new DateTime(2025, 4, 18)
			}
		];
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
    private void SortReset()
    {
	    SelectedSortFilterItem = "By ID";  // Changed from "Reset Data" to "By ID"
	    SelectedStatusFilterItem = "All";  // Reset status filter to "All"
    }
    
    [RelayCommand]
    private void SortById()
    {
	    SelectedSortFilterItem = "By ID";
	    ApplyMemberSort();
    }

    [RelayCommand]
    private void SortNamesByAlphabetical()
    {
	    SelectedSortFilterItem = "Names by A-Z";
	    ApplyMemberSort();
    }

    [RelayCommand]
    private void SortNamesByReverseAlphabetical()
    {
	    SelectedSortFilterItem = "Names by Z-A";
	    ApplyMemberSort();
    }

    [RelayCommand]
    private void SortDateByNewestToOldest()
    {
	    SelectedSortFilterItem = "By newest to oldest";
	    ApplyMemberSort();
    }

    [RelayCommand]
    private void SortDateByOldestToNewest()
    {
	    SelectedSortFilterItem = "By oldest to newest";
	    ApplyMemberSort();
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
    
	    ShowDeleteConfirmationDialog(member);
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
    
    [RelayCommand]
    private void OpenAddNewMemberView()
    {
	    _pageManager.Navigate<AddNewMemberViewModel>(new Dictionary<string, object>
	    {
		    ["Context"] = MemberViewContext.AddNew
	    });
    } 
    
	[RelayCommand]
	private void OpenUpgradeMemberView()
	{
		if (SelectedMember == null) return;
		_pageManager.Navigate<AddNewMemberViewModel>(new Dictionary<string, object>
		{
			["Context"] = MemberViewContext.Upgrade,
			["SelectedMember"] = SelectedMember
		});
	} 
	
	[RelayCommand]
	private void OpenRenewMemberView()
	{
		if (SelectedMember == null) return;
		_pageManager.Navigate<AddNewMemberViewModel>(new Dictionary<string, object>
		{
			["Context"] = MemberViewContext.Renew,
			["SelectedMember"] = SelectedMember
		});
	} 
	
	[RelayCommand]
	private void ShowDeleteMemberDialog()
	{
		if (SelectedMember is null)
		{
			_toastManager.CreateToast("No Member Selected")
				.WithContent("Please select a member to delete")
				.DismissOnClick()
				.ShowError();
			return;
		}
    
		ShowDeleteConfirmationDialog(SelectedMember);
	}
	
	[RelayCommand]
	private void ShowModifyMemberDialog(ManageMembersItem? member)
	{
		if (member is null)
		{
			_toastManager.CreateToast("No Member Selected")
				.WithContent("Please select a member to modify")
				.DismissOnClick()
				.ShowError();
			return;
		}
		
		_memberDialogCardViewModel.InitializeForEditMode(member);
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
    
	private void ApplyMemberSort()
	{
		if (OriginalMemberData.Count == 0) return;

		if (SelectedSortFilterItem == "Reset Data")
		{
			SelectedStatusFilterItem = "All";
			SelectedSortFilterItem = "By ID";
			CurrentFilteredData = OriginalMemberData.OrderBy(m => m.ID).ToList();
			RefreshMemberItems(CurrentFilteredData);
			return;
		}

		// First apply status filter to get the base filtered data
		List<ManageMembersItem> baseFilteredData;
		if (SelectedStatusFilterItem == "All")
		{
			baseFilteredData = OriginalMemberData.ToList();
		}
		else
		{
			baseFilteredData = OriginalMemberData
				.Where(member => member.Status == SelectedStatusFilterItem)
				.ToList();
		}

		// Then apply sorting to the filtered data
		List<ManageMembersItem> sortedList = SelectedSortFilterItem switch
		{
			"By ID" => baseFilteredData.OrderBy(m => m.ID).ToList(),
			"Names by A-Z" => baseFilteredData.OrderBy(m => m.Name).ToList(),
			"Names by Z-A" => baseFilteredData.OrderByDescending(m => m.Name).ToList(),
			"By newest to oldest" => baseFilteredData.OrderByDescending(m => m.Validity).ToList(),
			"By oldest to newest" => baseFilteredData.OrderBy(m => m.Validity).ToList(),
			_ => baseFilteredData.ToList()
		};
		CurrentFilteredData = sortedList;
		RefreshMemberItems(sortedList);
	}
	
	private void ApplyMemberStatusFilter()
	{
		if (OriginalMemberData.Count == 0) return;
	
		List<ManageMembersItem> filteredList;
		if (SelectedStatusFilterItem == "All")
		{
			filteredList = OriginalMemberData.ToList();
		}
		else
		{
			filteredList = OriginalMemberData
				.Where(member => member.Status == SelectedStatusFilterItem)
				.ToList();
		}

		// Apply current sorting to the filtered data
		List<ManageMembersItem> sortedList = SelectedSortFilterItem switch
		{
			"By ID" => filteredList.OrderBy(m => m.ID).ToList(),
			"Names by A-Z" => filteredList.OrderBy(m => m.Name).ToList(),
			"Names by Z-A" => filteredList.OrderByDescending(m => m.Name).ToList(),
			"By newest to oldest" => filteredList.OrderByDescending(m => m.Validity).ToList(),
			"By oldest to newest" => filteredList.OrderBy(m => m.Validity).ToList(),
			"Reset Data" => filteredList.ToList(),
			_ => filteredList.ToList()
		};

		CurrentFilteredData = sortedList;
		RefreshMemberItems(sortedList);
	}
	
	private void RefreshMemberItems(List<ManageMembersItem> items)
	{
		MemberItems.Clear();
		foreach (var item in items)
		{
			item.PropertyChanged += OnMemberPropertyChanged;
			MemberItems.Add(item);
		}
		UpdateCounts();
	}
    
    private void UpdateCounts()
    {
	    SelectedCount = MemberItems.Count(x => x.IsSelected);
	    TotalCount = MemberItems.Count;

	    SelectAll = MemberItems.Count > 0 && MemberItems.All(x => x.IsSelected);

	    OnPropertyChanged(nameof(CanDeleteSelectedMembers));
    }
    
    private void ShowDeleteConfirmationDialog(ManageMembersItem member)
    {
	    _dialogManager
		    .CreateDialog(
			    "Are you absolutely sure?",
			    "Deleting this member will permanently remove all of their data, records, and related information from the system. This action cannot be undone.")
		    .WithPrimaryButton("Delete Member", () => OnSubmitDeleteSingleItem(member),
			    DialogButtonStyle.Destructive)
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
	    OriginalMemberData.Remove(member);
	    CurrentFilteredData.Remove(member);
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
		    OriginalMemberData.Remove(members);
		    CurrentFilteredData.Remove(members);
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
    
    partial void OnSearchStringResultChanged(string value)
    {
	    SearchMembersCommand.Execute(null);
    }
    
    partial void OnSelectedSortFilterItemChanged(string value)
    {
	    ApplyMemberSort();
    }
    
    partial void OnSelectedStatusFilterItemChanged(string value)
    {
	    ApplyMemberStatusFilter();
    }
    
    private void OnMemberPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
	    if (e.PropertyName == nameof(ManageMembersItem.IsSelected))
	    {
		    UpdateCounts();
	    }
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
    private string _gender = string.Empty;
    
    [ObservableProperty]
    private int _age;
    
    [ObservableProperty]
    private string _availedPackages = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private DateTime _validity;
    
    [ObservableProperty]
    private DateTime _birthDate;

    public IBrush StatusForeground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),     // Green-500
        "expired" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red-500
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))           // Default Gray-500
    };

    public IBrush StatusBackground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),     // Green-500 with alpha
        "expired" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)), // Red-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))           // Default Gray-500 with alpha
    };

    public string StatusDisplayText => Status.ToLowerInvariant() switch
    {
        "active" => "● Active",
        "expired" => "● Expired",
        _ => Status
    };

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusDisplayText));
    }
}
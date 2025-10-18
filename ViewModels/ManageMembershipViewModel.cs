using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

[Page("manage-membership")]
public sealed partial class ManageMembershipViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _sortFilterItems = [
        "By ID", "Names by A-Z", "Names by Z-A", "By newest to oldest", "By oldest to newest", "Reset Data"
    ];

    [ObservableProperty]
    private string _selectedSortFilterItem = "By ID";

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

    private readonly IMemberService? _memberService;
    private const string DefaultAvatarSource = "avares://AHON_TRACK/Assets/MainWindowView/user.png";

    private static List<ManageMembersItem>? _cachedMembers;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly MemberDialogCardViewModel _memberDialogCardViewModel;
    private readonly AddNewMemberViewModel _addNewMemberViewModel;

    public bool IsActiveVisible => SelectedMember?.Status.Equals("active", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsExpiredVisible => SelectedMember?.Status.Equals("expired", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool HasSelectedMember => SelectedMember is not null;

    public bool IsUpgradeButtonEnabled =>
        !new[] { "Expired" }
            .Any(status => SelectedMember is not null && SelectedMember.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

    // Constructor with dependency injection (for production)
    public ManageMembershipViewModel(IMemberService memberService, DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, MemberDialogCardViewModel memberDialogCardViewModel, AddNewMemberViewModel addNewMemberViewModel)
    {
        _memberService = memberService;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _memberDialogCardViewModel = memberDialogCardViewModel;
        _addNewMemberViewModel = addNewMemberViewModel;

        _ = LoadMemberDataAsync();
        UpdateCounts();
        DashboardEventService.Instance.MemberAdded += OnMemberChanged;
        DashboardEventService.Instance.MemberUpdated += OnMemberChanged;
        DashboardEventService.Instance.MemberDeleted += OnMemberChanged;
    }

    // Default constructor (for design-time/testing)
    public ManageMembershipViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _memberDialogCardViewModel = new MemberDialogCardViewModel();
        _addNewMemberViewModel = new AddNewMemberViewModel();

        _ = LoadMemberDataAsync();
        DashboardEventService.Instance.MemberAdded += OnMemberChanged;
        DashboardEventService.Instance.MemberUpdated += OnMemberChanged;
        DashboardEventService.Instance.MemberDeleted += OnMemberChanged;
    }

    partial void OnSelectedMemberChanged(ManageMembersItem? value)
    {
        OnPropertyChanged(nameof(IsActiveVisible));
        OnPropertyChanged(nameof(IsExpiredVisible));
        OnPropertyChanged(nameof(HasSelectedMember));
        OnPropertyChanged(nameof(IsUpgradeButtonEnabled));
    }


    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (_cachedMembers is not null && _cachedMembers.Count > 0)
        {
            OriginalMemberData = _cachedMembers;
            RefreshMemberItems(_cachedMembers);
            return;
        }

        await LoadMemberDataAsync();
        _cachedMembers = OriginalMemberData;
    }

    private async void OnMemberChanged(object? sender, EventArgs e)
    {
        await LoadMemberDataAsync();
    }

    public async Task LoadMemberDataAsync()
    {
        if (_memberService == null) return;

        try
        {
            // Use the new tuple-based service method
            var result = await _memberService.GetMembersAsync();

            if (result.Success && result.Members is { Count: > 0 })
            {
                // Map ManageMemberModel → ManageMemberItems
                var memberItems = result.Members.Select(m => new ManageMembersItem
                {
                    ID = m.MemberID.ToString(),
                    AvatarSource = m.AvatarBytes != null
                        ? ImageHelper.BytesToBitmap(m.AvatarBytes)
                        : ManageMemberModel.DefaultAvatarSource,
                    Name = m.Name ?? string.Empty,
                    ContactNumber = m.ContactNumber ?? string.Empty,
                    AvailedPackages = m.MembershipType ?? string.Empty,
                    Status = m.Status ?? "Active",
                    Validity = DateTime.TryParse(m.ValidUntil, out var parsedDate) ? parsedDate : DateTime.MinValue
                }).ToList();

                OriginalMemberData = memberItems;
                CurrentFilteredData = [.. memberItems];

                MemberItems.Clear();
                foreach (var member in memberItems)
                {
                    member.PropertyChanged += OnMemberPropertyChanged;
                    MemberItems.Add(member);
                }

                TotalCount = MemberItems.Count;
                if (MemberItems.Count > 0)
                    SelectedMember = MemberItems[0];

                ApplyMemberStatusFilter();
                ApplyMemberSort();
                return; // Successfully loaded from database
            }
            else if (!result.Success)
            {
                Debug.WriteLine($"[ManageMembership] Failed to load members: {result.Message}");
                _toastManager?.CreateToast("Database Error")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ManageMembership] Failed to load DB members: {ex.Message}");
            _toastManager?.CreateToast("Database Error")
                .WithContent("Could not load members from database. Showing sample data instead.")
                .DismissOnClick()
                .ShowWarning();
        }

        // Fallback to sample data if database fails
        LoadSampleData();
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
                AvatarSource = ManageMemberModel.DefaultAvatarSource,
                Name = "Jedd Calubayan",
                ContactNumber = "0975 994 3010",
                AvailedPackages = "Boxing",
                Status = "Active",
                Validity = new DateTime(2025, 6, 16)
            },

            new ManageMembersItem
            {
                ID = "1002",
                AvatarSource = ManageMemberModel.DefaultAvatarSource,
                Name = "Marc Torres",
                ContactNumber = "0975 994 3010",
                AvailedPackages = "None",
                Status = "Expired",
                Validity = new DateTime(2025, 7, 16)
            },

            new ManageMembersItem
            {
                ID = "1003",
                AvatarSource = ManageMemberModel.DefaultAvatarSource,
                Name = "Mardie Dela Cruz",
                ContactNumber = "0975 994 3010",
                AvailedPackages = "None",
                Status = "Expired",
                Validity = new DateTime(2025, 7, 18)
            },

            new ManageMembersItem
            {
                ID = "1004",
                AvatarSource = ManageMemberModel.DefaultAvatarSource,
                Name = "Mark Dela Cruz",
                ContactNumber = "0975 994 3010",
                AvailedPackages = "Muay thai, Boxing, Crossfit, Gym",
                Status = "Active",
                Validity = new DateTime(2025, 7, 18)
            },

            new ManageMembersItem
            {
                ID = "1005",
                AvatarSource = ManageMemberModel.DefaultAvatarSource,
                Name = "JL Taberdo",
                ContactNumber = "0975 994 3010",
                AvailedPackages = "Gym",
                Status = "Expired",
                Validity = new DateTime(2025, 4, 18)
            },

            new ManageMembersItem
            {
                ID = "1006",
                AvatarSource = ManageMemberModel.DefaultAvatarSource,
                Name = "Robert Xyz B. Lucas",
                ContactNumber = "0975 994 3010",
                AvailedPackages = "Gym",
                Status = "Active",
                Validity = new DateTime(2025, 4, 18)
            },

            new ManageMembersItem
            {
                ID = "1007",
                AvatarSource = ManageMemberModel.DefaultAvatarSource,
                Name = "Sianrey V. Flora",
                ContactNumber = "0975 994 3010",
                AvailedPackages = "Gym",
                Status = "Active",
                Validity = new DateTime(2025, 4, 18)
            },

            new ManageMembersItem
            {
                ID = "1008",
                AvatarSource = ManageMemberModel.DefaultAvatarSource,
                Name = "Marion James Dela Roca",
                ContactNumber = "0975 994 3010",
                AvailedPackages = "Gym",
                Status = "Active",
                Validity = new DateTime(2025, 4, 18)
            },
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

    [RelayCommand]
    private void SortReset()
    {
        SelectedSortFilterItem = "By ID";
        SelectedStatusFilterItem = "All";
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
        try
        {
            // Delete from database first
            await DeleteMemberFromDatabase(member);

            // Remove from all data collections
            member.PropertyChanged -= OnMemberPropertyChanged;
            MemberItems.Remove(member);

            // Update the original data and current filtered data
            OriginalMemberData = OriginalMemberData.Where(m => m.ID != member.ID).ToList();
            CurrentFilteredData = CurrentFilteredData.Where(m => m.ID != member.ID).ToList();

            UpdateCounts();

            // Don't show toast here - service already handles it
        }
        catch (Exception ex)
        {
            // Only show error toast if service didn't already handle it
            Debug.WriteLine($"Error in OnSubmitDeleteSingleItem: {ex.Message}");
        }
    }

    private async Task OnSubmitDeleteMultipleItems(ManageMembersItem member)
    {
        var selectedMembers = MemberItems.Where(item => item.IsSelected).ToList();
        if (!selectedMembers.Any()) return;

        try
        {
            // Delete from database first
            await DeleteMultipleMembersFromDatabase(selectedMembers);

            // Remove from UI and data collections
            var idsToRemove = selectedMembers.Select(m => m.ID).ToHashSet();

            foreach (var memberToDelete in selectedMembers)
            {
                memberToDelete.PropertyChanged -= OnMemberPropertyChanged;
                MemberItems.Remove(memberToDelete);
            }

            // Update the original data and current filtered data
            OriginalMemberData = OriginalMemberData.Where(m => !idsToRemove.Contains(m.ID)).ToList();
            CurrentFilteredData = CurrentFilteredData.Where(m => !idsToRemove.Contains(m.ID)).ToList();

            UpdateCounts();

            // Don't show toast here - service already handles it
        }
        catch (Exception ex)
        {
            // Only show error toast if service didn't already handle it  
            Debug.WriteLine($"Error in OnSubmitDeleteMultipleItems: {ex.Message}");
        }
    }

    // Helper method to delete from database
    private async Task DeleteMemberFromDatabase(ManageMembersItem member)
    {
        if (_memberService == null)
        {
            // Fallback for when service is not available (design-time/testing)
            await Task.Delay(100);
            return;
        }

        // Convert string ID to int
        if (int.TryParse(member.ID, out int memberId))
        {
            // Call the service - it returns a tuple now
            var result = await _memberService.DeleteMemberAsync(memberId);

            if (!result.Success)
            {
                // Service already showed error toast, just log it
                Debug.WriteLine($"Failed to delete member: {result.Message}");
            }
            else
            {
                DashboardEventService.Instance.NotifyMemberDeleted();

                _toastManager?.CreateToast("Member Deleted")
                    .WithContent($"{member.Name} has been deleted successfully.")
                    .DismissOnClick()
                    .ShowSuccess();
            }
        }
        else
        {
            Debug.WriteLine($"Invalid member ID: {member.ID}");
            _toastManager?.CreateToast("Invalid ID")
                .WithContent("Member ID is not valid.")
                .DismissOnClick()
                .ShowError();
        }
    }

    private async Task DeleteMultipleMembersFromDatabase(List<ManageMembersItem> members)
    {
        if (_memberService == null)
        {
            // Fallback for when service is not available
            await Task.Delay(100);
            return;
        }

        // Convert string IDs to int list
        var memberIds = new List<int>();
        foreach (var member in members)
        {
            if (int.TryParse(member.ID, out int memberId))
            {
                memberIds.Add(memberId);
            }
        }

        if (memberIds.Count > 0)
        {
            // Call the service - it returns a tuple now
            var result = await _memberService.DeleteMultipleMembersAsync(memberIds);

            if (!result.Success)
            {
                // Service already showed error toast, just log it
                Debug.WriteLine($"Failed to delete members: {result.Message}");
            }
            else
            {
                // ✅ Notify other ViewModels
                DashboardEventService.Instance.NotifyMemberDeleted();

                _toastManager?.CreateToast("Members Deleted")
                    .WithContent($"{memberIds.Count} members were deleted successfully.")
                    .DismissOnClick()
                    .ShowSuccess();
            }
        }
        else
        {
            Debug.WriteLine("No valid member IDs to delete");
            _toastManager?.CreateToast("Invalid IDs")
                .WithContent("No valid member IDs found.")
                .DismissOnClick()
                .ShowError();
        }
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
    private async Task ShowModifyMemberDialog(ManageMembersItem member)
    {
        _memberDialogCardViewModel.Initialize();

        // Populate the dialog with member data from database
        await _memberDialogCardViewModel.PopulateWithMemberDataAsync(member.ID);

        _dialogManager.CreateDialog(_memberDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                // Reload data after successful modification
                await LoadMemberDataAsync();

                _toastManager.CreateToast("Modified an existing gym member information")
                    .WithContent($"You just modified {member.Name} information!")
                    .DismissOnClick()
                    .ShowSuccess();
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Cancellation of modification")
                    .WithContent($"You just cancelled modifying {member.Name} information!")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(950)
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
    private Bitmap _avatarSource = ManageMemberModel.DefaultAvatarSource;

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
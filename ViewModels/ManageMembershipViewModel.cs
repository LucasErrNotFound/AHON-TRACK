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
    private string _selectedSortFilterItem = "By newest to oldest";

    [ObservableProperty]
    private string[] _statusFilterItems = ["All", "Active", "Near Expiry", "Expired"];

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

    private bool _isLoadingData = false;

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
        !new[] { "Active", "Near Expiry" }
            .Any(status => SelectedMember is not null && SelectedMember.Status
                .Equals(status, StringComparison.OrdinalIgnoreCase));

    public bool IsActiveVisible => SelectedMember?.Status.Equals("active", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsNearExpiryVisible => SelectedMember?.Status.Equals("near expiry", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsExpiredVisible => SelectedMember?.Status.Equals("expired", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool HasSelectedMember => SelectedMember is not null;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly MemberDialogCardViewModel _memberDialogCardViewModel;
    private readonly AddNewMemberViewModel _addNewMemberViewModel;
    private readonly NotifyDialogCardViewmodel _notifyDialogCardViewmodel;
    private readonly IMemberService? _memberService;

    private const string DefaultAvatarSource = "avares://AHON_TRACK/Assets/MainWindowView/user.png";

    public ManageMembershipViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,
        MemberDialogCardViewModel memberDialogCardViewModel, AddNewMemberViewModel addNewMemberViewModel,
        NotifyDialogCardViewmodel notifyDialogCardViewmodel, IMemberService memberService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _memberDialogCardViewModel = memberDialogCardViewModel;
        _addNewMemberViewModel = addNewMemberViewModel;
        _notifyDialogCardViewmodel = notifyDialogCardViewmodel;
        _memberService = memberService;

        _ = LoadMemberDataAsync();
        SubscribeToEvents();
        UpdateCounts();

    }

    // Default constructor (for design-time/testing)
    public ManageMembershipViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _memberDialogCardViewModel = new MemberDialogCardViewModel();
        _addNewMemberViewModel = new AddNewMemberViewModel();
        _notifyDialogCardViewmodel = new NotifyDialogCardViewmodel();

        _ = LoadMemberDataAsync();
        SubscribeToEvents();
        UpdateCounts();
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (IsInitialized) return;

        SubscribeToEvents();
        await LoadMemberDataAsync();

        IsInitialized = true;
    }

    private void SubscribeToEvents()
    {
        var eventService = DashboardEventService.Instance;

        eventService.MemberAdded += OnMemberChanged;
        eventService.MemberUpdated += OnMemberChanged;
        eventService.MemberDeleted += OnMemberChanged;
        eventService.CheckinAdded += OnMemberChanged;
        eventService.CheckoutAdded += OnMemberChanged;
        eventService.ProductPurchased += OnMemberChanged;
    }

    public bool IsInformButtonEnabled =>
    SelectedMember is not null &&
    (SelectedMember.Status.Equals("Near Expiry", StringComparison.OrdinalIgnoreCase) ||
     SelectedMember.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase));

    partial void OnSelectedMemberChanged(ManageMembersItem? value)
    {
        OnPropertyChanged(nameof(IsActiveVisible));
        OnPropertyChanged(nameof(IsExpiredVisible));
        OnPropertyChanged(nameof(IsNearExpiryVisible));
        OnPropertyChanged(nameof(HasSelectedMember));
        OnPropertyChanged(nameof(IsDeleteButtonEnabled));
        OnPropertyChanged(nameof(IsUpgradeButtonEnabled));
        OnPropertyChanged(nameof(IsRenewButtonEnabled));
        OnPropertyChanged(nameof(CanDeleteSelectedMembers));
        OnPropertyChanged(nameof(IsInformButtonEnabled));
    }

    private async void OnMemberChanged(object? sender, EventArgs e)
    {
        if (_isLoadingData) return;
        try
        {
            _isLoadingData = true;
            await LoadMemberDataAsync();
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast($"Failed to load: {ex.Message}");
        }
        finally
        {
            _isLoadingData = false;
        }
    }

    public async Task LoadMemberDataAsync()
    {
        if (_memberService == null) return;

        try
        {
            var result = await _memberService.GetMembersAsync();

            if (result.Success && result.Members is { Count: > 0 })
            {
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
                    Validity = DateTime.TryParse(m.ValidUntil, out var parsedDate) ? parsedDate : DateTime.MinValue,
                    DateJoined = m.DateJoined,
                    LastCheckIn = m.LastCheckIn,
                    LastCheckOut = m.LastCheckOut,
                    RecentPurchaseItem = m.RecentPurchaseItem,
                    RecentPurchaseDate = m.RecentPurchaseDate,
                    RecentPurchaseQuantity = m.RecentPurchaseQuantity,
                    ConsentLetter = m.ConsentLetter,

                    // ✅ ADD THESE THREE LINES
                    LastNotificationDate = m.LastNotificationDate,
                    NotificationCount = m.NotificationCount,
                    IsNotified = m.IsNotified
                }).ToList();

                OriginalMemberData = memberItems;
                CurrentFilteredData = [.. memberItems];

                // ✅ CRITICAL: Unsubscribe from old members BEFORE clearing
                foreach (var member in MemberItems)
                {
                    member.PropertyChanged -= OnMemberPropertyChanged;
                }

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
                await _memberService.ShowMemberExpirationAlertsAsync();
                return;
            }

            if (!result.Success)
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

        LoadSampleData();
    }

    private void LoadSampleData()
    {
        var sampleMembers = GetSampleMembersData();

        OriginalMemberData = sampleMembers;
        CurrentFilteredData = [.. sampleMembers];

        // ✅ CRITICAL: Unsubscribe from old members BEFORE clearing
        foreach (var member in MemberItems)
        {
            member.PropertyChanged -= OnMemberPropertyChanged;
        }

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
            // ✅ Unsubscribe before clearing
            foreach (var member in MemberItems)
            {
                member.PropertyChanged -= OnMemberPropertyChanged;
            }

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

            var filteredMembers = CurrentFilteredData.Where(emp =>
                emp.ID.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                emp.Name.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                emp.ContactNumber.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                emp.AvailedPackages.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                emp.Status.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                emp.Validity.ToString("MMMM d, yyyy").Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            // ✅ Unsubscribe before clearing
            foreach (var member in MemberItems)
            {
                member.PropertyChanged -= OnMemberPropertyChanged;
            }

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
            CurrentFilteredData = OriginalMemberData.OrderBy(m => int.TryParse(m.ID, out var id) ? id : 0).ToList();
            RefreshMemberItems(CurrentFilteredData);
            return;
        }

        List<ManageMembersItem> baseFilteredData;
        if (SelectedStatusFilterItem == "All")
        {
            baseFilteredData = OriginalMemberData.ToList();
        }
        else
        {
            baseFilteredData = OriginalMemberData
                .Where(member => member.Status.Equals(SelectedStatusFilterItem, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        List<ManageMembersItem> sortedList = SelectedSortFilterItem switch
        {
            "By ID" => baseFilteredData.OrderBy(m => int.TryParse(m.ID, out var id) ? id : 0).ToList(),
            "Names by A-Z" => baseFilteredData.OrderBy(m => m.Name).ToList(),
            "Names by Z-A" => baseFilteredData.OrderByDescending(m => m.Name).ToList(),
            "By newest to oldest" => baseFilteredData.OrderByDescending(m => m.DateJoined ?? DateTime.MinValue).ToList(),
            "By oldest to newest" => baseFilteredData.OrderBy(m => m.DateJoined ?? DateTime.MinValue).ToList(),
            _ => baseFilteredData.OrderByDescending(m => m.DateJoined ?? DateTime.MinValue).ToList()
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
                .Where(member => member.Status.Equals(SelectedStatusFilterItem, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        List<ManageMembersItem> sortedList = SelectedSortFilterItem switch
        {
            "By ID" => filteredList.OrderBy(m => int.TryParse(m.ID, out var id) ? id : 0).ToList(),
            "Names by A-Z" => filteredList.OrderBy(m => m.Name).ToList(),
            "Names by Z-A" => filteredList.OrderByDescending(m => m.Name).ToList(),
            "By newest to oldest" => filteredList.OrderByDescending(m => m.DateJoined ?? DateTime.MinValue).ToList(),
            "By oldest to newest" => filteredList.OrderBy(m => m.DateJoined ?? DateTime.MinValue).ToList(),
            "Reset Data" => filteredList.ToList(),
            _ => filteredList.OrderByDescending(m => m.DateJoined ?? DateTime.MinValue).ToList() // Default to newest to oldest
        };

        CurrentFilteredData = sortedList;
        RefreshMemberItems(sortedList);
    }

    private void RefreshMemberItems(List<ManageMembersItem> items)
    {
        // ✅ CRITICAL: Unsubscribe from old members BEFORE clearing
        foreach (var member in MemberItems)
        {
            member.PropertyChanged -= OnMemberPropertyChanged;
        }

        MemberItems.Clear();

        foreach (var item in items)
        {
            item.PropertyChanged += OnMemberPropertyChanged;
            MemberItems.Add(item);
        }
        UpdateCounts();
    }

    [RelayCommand]
    private async Task ShowNotifyDialog()
    {
        if (SelectedMember == null)
        {
            _toastManager.CreateToast("No Member Selected")
                .WithContent("Please select a member to notify")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        // Check if member status is Near Expiry or Expired
        if (!SelectedMember.Status.Equals("Near Expiry", StringComparison.OrdinalIgnoreCase) &&
            !SelectedMember.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase))
        {
            _toastManager.CreateToast("Invalid Status")
                .WithContent("You can only notify members with 'Near Expiry' or 'Expired' status.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        // Check if member has a contact number
        if (string.IsNullOrWhiteSpace(SelectedMember.ContactNumber))
        {
            _toastManager.CreateToast("No Contact Number")
                .WithContent($"{SelectedMember.Name} does not have a contact number on file. Please update their profile first.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        _notifyDialogCardViewmodel.Initialize();
        _notifyDialogCardViewmodel.SetMemberInfo(SelectedMember);

        _dialogManager.CreateDialog(_notifyDialogCardViewmodel)
            .WithSuccessCallback(async _ =>
            {
                // ✅ Record the notification in database (this will trigger dashboard notification)
                if (_memberService != null && int.TryParse(SelectedMember.ID, out int memberId))
                {
                    var result = await _memberService.RecordMemberNotificationAsync(
                        memberId,
                        _notifyDialogCardViewmodel.TextDescription ?? "Member notified about membership status");

                    if (result.Success)
                    {
                        // Show success toast
                        _toastManager.CreateToast("Member Notified")
                            .WithContent($"Successfully notified {SelectedMember.Name} via SMS")
                            .DismissOnClick()
                            .WithDelay(5)
                            .ShowSuccess();

                        // Reload data to reflect notification status
                        await LoadMemberDataAsync();
                    }
                    else
                    {
                        _toastManager.CreateToast("Notification Failed")
                            .WithContent($"Failed to record notification: {result.Message}")
                            .DismissOnClick()
                            .ShowError();
                    }
                }
            })
            .WithCancelCallback(() =>
            {
                _toastManager.CreateToast("Notification Cancelled")
                    .WithContent("Member notification was cancelled")
                    .DismissOnClick()
                    .ShowWarning();
            })
            .WithMaxWidth(950)
            .Dismissible()
            .Show();
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
    private async Task OpenUpgradeMemberView()
    {
        if (SelectedMember == null) return;

        // ✅ FIX: Fetch complete member data from database before navigating
        if (_memberService != null && int.TryParse(SelectedMember.ID, out int memberId))
        {
            var result = await _memberService.GetMemberByIdAsync(memberId);

            if (result.Success && result.Member != null)
            {
                // Create a fully populated ManageMembersItem with ALL data
                var fullMemberData = new ManageMembersItem
                {
                    ID = result.Member.MemberID.ToString(),
                    AvatarSource = result.Member.AvatarSource ?? ManageMemberModel.DefaultAvatarSource,
                    Name = result.Member.Name ?? string.Empty,
                    ContactNumber = result.Member.ContactNumber ?? string.Empty,
                    AvailedPackages = result.Member.MembershipType ?? string.Empty,
                    Status = result.Member.Status ?? "Active",
                    Validity = DateTime.TryParse(result.Member.ValidUntil, out var parsedDate) ? parsedDate : DateTime.MinValue,

                    Gender = result.Member.Gender ?? string.Empty,
                    BirthDate = result.Member.DateOfBirth ?? DateTime.MinValue,
                    Age = result.Member.Age ?? 0,
                    DateJoined = result.Member.DateJoined,
                    LastCheckIn = result.Member.LastCheckIn,
                    LastCheckOut = result.Member.LastCheckOut,
                    RecentPurchaseItem = result.Member.RecentPurchaseItem,
                    RecentPurchaseDate = result.Member.RecentPurchaseDate,
                    RecentPurchaseQuantity = result.Member.RecentPurchaseQuantity,
                    ConsentLetter = result.Member.ConsentLetter,

                    // ✅ ADD THESE THREE LINES
                    LastNotificationDate = result.Member.LastNotificationDate,
                    NotificationCount = result.Member.NotificationCount,
                    IsNotified = result.Member.IsNotified
                };

                _pageManager.Navigate<AddNewMemberViewModel>(new Dictionary<string, object>
                {
                    ["Context"] = MemberViewContext.Upgrade,
                    ["SelectedMember"] = fullMemberData
                });
            }
            else
            {
                _toastManager?.CreateToast("Load Error")
                    .WithContent("Failed to load member data for upgrade.")
                    .DismissOnClick()
                    .ShowError();
            }
        }
        else
        {
            // Fallback: use existing data if service is unavailable
            _pageManager.Navigate<AddNewMemberViewModel>(new Dictionary<string, object>
            {
                ["Context"] = MemberViewContext.Upgrade,
                ["SelectedMember"] = SelectedMember
            });
        }
    }

    [RelayCommand]
    private async Task OpenRenewMemberView()
    {
        if (SelectedMember == null) return;

        // ✅ FIX: Fetch complete member data from database before navigating
        if (_memberService != null && int.TryParse(SelectedMember.ID, out int memberId))
        {
            var result = await _memberService.GetMemberByIdAsync(memberId);

            if (result.Success && result.Member != null)
            {
                // Create a fully populated ManageMembersItem with ALL data
                var fullMemberData = new ManageMembersItem
                {
                    ID = result.Member.MemberID.ToString(),
                    AvatarSource = result.Member.AvatarSource ?? ManageMemberModel.DefaultAvatarSource,
                    Name = result.Member.Name ?? string.Empty,
                    ContactNumber = result.Member.ContactNumber ?? string.Empty,
                    AvailedPackages = result.Member.MembershipType ?? string.Empty,
                    Status = result.Member.Status ?? "Active",
                    Validity = DateTime.TryParse(result.Member.ValidUntil, out var parsedDate) ? parsedDate : DateTime.MinValue,

                    Gender = result.Member.Gender ?? string.Empty,
                    BirthDate = result.Member.DateOfBirth ?? DateTime.MinValue,
                    Age = result.Member.Age ?? 0,
                    DateJoined = result.Member.DateJoined,
                    LastCheckIn = result.Member.LastCheckIn,
                    LastCheckOut = result.Member.LastCheckOut,
                    RecentPurchaseItem = result.Member.RecentPurchaseItem,
                    RecentPurchaseDate = result.Member.RecentPurchaseDate,
                    RecentPurchaseQuantity = result.Member.RecentPurchaseQuantity,
                    ConsentLetter = result.Member.ConsentLetter,

                    // ✅ ADD THESE THREE LINES
                    LastNotificationDate = result.Member.LastNotificationDate,
                    NotificationCount = result.Member.NotificationCount,
                    IsNotified = result.Member.IsNotified
                };

                _pageManager.Navigate<AddNewMemberViewModel>(new Dictionary<string, object>
                {
                    ["Context"] = MemberViewContext.Renew,
                    ["SelectedMember"] = fullMemberData
                });
            }
            else
            {
                _toastManager?.CreateToast("Load Error")
                    .WithContent("Failed to load member data for renewal.")
                    .DismissOnClick()
                    .ShowError();
            }
        }
        else
        {
            // Fallback: use existing data if service is unavailable
            _pageManager.Navigate<AddNewMemberViewModel>(new Dictionary<string, object>
            {
                ["Context"] = MemberViewContext.Renew,
                ["SelectedMember"] = SelectedMember
            });
        }
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

    protected override void DisposeManagedResources()
    {
        // Unsubscribe from events
        var eventService = DashboardEventService.Instance;
        eventService.MemberAdded -= OnMemberChanged;
        eventService.MemberUpdated -= OnMemberChanged;
        eventService.MemberDeleted -= OnMemberChanged;  // ✅ ADD THIS
        eventService.CheckinAdded -= OnMemberChanged;
        eventService.CheckoutAdded -= OnMemberChanged;
        eventService.ProductPurchased -= OnMemberChanged;

        // ✅ Unsubscribe from property changed events
        foreach (var member in MemberItems)
        {
            member.PropertyChanged -= OnMemberPropertyChanged;

            // ✅ Dispose bitmaps if they exist
            if (member.AvatarSource != null &&
                member.AvatarSource != ManageMemberModel.DefaultAvatarSource)
            {
                member.AvatarSource?.Dispose();
            }
        }

        // ✅ Also dispose from other collections
        foreach (var member in OriginalMemberData)
        {
            if (member.AvatarSource != null &&
                member.AvatarSource != ManageMemberModel.DefaultAvatarSource)
            {
                member.AvatarSource?.Dispose();
            }
        }

        foreach (var member in CurrentFilteredData)
        {
            if (member.AvatarSource != null &&
                member.AvatarSource != ManageMemberModel.DefaultAvatarSource)
            {
                member.AvatarSource?.Dispose();
            }
        }

        // Clear collections
        MemberItems.Clear();
        OriginalMemberData.Clear();
        CurrentFilteredData.Clear();

        SelectedMember = null;
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
    private string _gender = string.Empty;

    [ObservableProperty]
    private int _age;

    [ObservableProperty]
    private string _availedPackages = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string? _consentLetter;

    [ObservableProperty]
    private DateTime _validity;

    [ObservableProperty]
    private DateTime _birthDate;

    [ObservableProperty]
    private DateTime? _dateJoined;

    [ObservableProperty]
    private DateTime? _lastCheckIn;

    [ObservableProperty]
    private DateTime? _lastCheckOut;

    // ✅ ADD: Recent purchase
    [ObservableProperty]
    private string? _recentPurchaseItem;

    [ObservableProperty]
    private DateTime? _recentPurchaseDate;

    [ObservableProperty]
    private int? _recentPurchaseQuantity;

    [ObservableProperty]
    private string? _remarks = string.Empty;

    [ObservableProperty]
    private DateTime? _lastNotificationDate;

    [ObservableProperty]
    private int _notificationCount;

    [ObservableProperty]
    private bool _isNotified;

    public string MembershipStartDisplay => DateJoined?.ToString("MMMM d, yyyy") ?? "Not Set";
    public string LastCheckInDisplay => LastCheckIn?.ToString("MMMM d, yyyy") ?? "No check-in";
    public string LastCheckInTimeDisplay => LastCheckIn?.ToString("h:mm tt") ?? "N/A";

    public string LastCheckOutDisplay => LastCheckOut?.ToString("MMMM d, yyyy") ?? "No check-out";
    public string LastCheckOutTimeDisplay => LastCheckOut?.ToString("h:mm tt") ?? "N/A";

    public string RecentPurchaseDisplay => !string.IsNullOrEmpty(RecentPurchaseItem)
        ? $"x{RecentPurchaseQuantity} {RecentPurchaseItem}"
        : "No purchases";

    public string RecentPurchaseTimeDisplay => RecentPurchaseDate?.ToString("h:mm tt") ?? "N/A";

    partial void OnLastCheckInChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(LastCheckInDisplay));
        OnPropertyChanged(nameof(LastCheckInTimeDisplay));
    }

    partial void OnLastCheckOutChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(LastCheckOutDisplay));
        OnPropertyChanged(nameof(LastCheckOutTimeDisplay));
    }

    partial void OnRecentPurchaseItemChanged(string? value)
    {
        OnPropertyChanged(nameof(RecentPurchaseDisplay));
    }

    partial void OnRecentPurchaseDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(RecentPurchaseTimeDisplay));
    }

    partial void OnDateJoinedChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(MembershipStartDisplay));
    }

    public IBrush StatusForeground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),     // Green-500
        "near expiry" => new SolidColorBrush(Color.FromRgb(249, 115, 22)),     // Orange-500
        "expired" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red-500
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))           // Default Gray-500
    };

    public IBrush StatusBackground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),     // Green-500 with alpha
        "near expiry" => new SolidColorBrush(Color.FromArgb(25, 249, 115, 22)),     // Orange-500 with alpha
        "expired" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)), // Red-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))           // Default Gray-500 with alpha
    };

    public string StatusDisplayText => Status.ToLowerInvariant() switch
    {
        "active" => "● Active",
        "near expiry" => "● Near Expiry",
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
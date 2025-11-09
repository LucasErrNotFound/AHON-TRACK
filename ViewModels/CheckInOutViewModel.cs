using System;
using System.ComponentModel;
using ShadUI;
using HotAvalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using Avalonia.Media.Imaging;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Validators;

namespace AHON_TRACK.ViewModels;

[Page("checkInOut")]
public partial class CheckInOutViewModel : ViewModelBase, INotifyPropertyChanged, INavigable
{
    [ObservableProperty]
    private List<WalkInPerson> _originalWalkInData = [];

    [ObservableProperty]
    private List<MemberPerson> _originalMemberData = [];

    [ObservableProperty]
    private List<WalkInPerson> _currentWalkInFilteredData = [];

    [ObservableProperty]
    private List<MemberPerson> _currentMemberFilteredData = [];

    [ObservableProperty]
    private ObservableCollection<WalkInPerson> _walkInPersons = [];

    [ObservableProperty]
    private ObservableCollection<MemberPerson> _memberPersons = [];

    [ObservableProperty]
    private bool _selectAll;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private bool _isLoadingData;

    private bool _isLoadingDataFlag = false;

    [ObservableProperty]
    private bool _canCheckIn = true;

    private readonly PageManager _pageManager;
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly LogGymMemberDialogCardViewModel _logGymMemberDialogCardViewModel;
    private readonly LogWalkInPurchaseViewModel _logWalkInPurchaseViewModel;
    private readonly ICheckInOutService _checkInOutService;

    public CheckInOutViewModel(PageManager pageManager, DialogManager dialogManager, ToastManager toastManager, LogGymMemberDialogCardViewModel logGymMemberDialogCardViewModel, LogWalkInPurchaseViewModel logWalkInPurchaseViewModel, ICheckInOutService checkInOutService)
    {
        _pageManager = pageManager;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _logGymMemberDialogCardViewModel = logGymMemberDialogCardViewModel;
        _logWalkInPurchaseViewModel = logWalkInPurchaseViewModel;
        _checkInOutService = checkInOutService;

        // Initialize collections
        WalkInPersons = new ObservableCollection<WalkInPerson>();
        MemberPersons = new ObservableCollection<MemberPerson>();
        OriginalWalkInData = new List<WalkInPerson>();
        OriginalMemberData = new List<MemberPerson>();
        CurrentWalkInFilteredData = new List<WalkInPerson>();
        CurrentMemberFilteredData = new List<MemberPerson>();

        SubscribeToEvents();

        // Load data asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                await LoadCheckInDataFromService(SelectedDate);
                UpdateWalkInCounts();
                UpdateMemberCounts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Constructor] Error loading initial data: {ex.Message}");
            }
        });
    }

    public CheckInOutViewModel()
    {
        _pageManager = new PageManager(new ServiceProvider());
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _logGymMemberDialogCardViewModel = new LogGymMemberDialogCardViewModel();
        _logWalkInPurchaseViewModel = new LogWalkInPurchaseViewModel();
        _checkInOutService = null!;

        // Initialize collections
        WalkInPersons = new ObservableCollection<WalkInPerson>();
        MemberPersons = new ObservableCollection<MemberPerson>();
        OriginalWalkInData = new List<WalkInPerson>();
        OriginalMemberData = new List<MemberPerson>();
        CurrentWalkInFilteredData = new List<WalkInPerson>();
        CurrentMemberFilteredData = new List<MemberPerson>();

        SubscribeToEvents();
        _ = LoadCheckInDataFromService(SelectedDate);
        UpdateWalkInCounts();
        UpdateMemberCounts();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;

        SubscribeToEvents();
        _ = LoadCheckInDataFromService(SelectedDate);
        IsInitialized = true;
    }

    #region INavigable Implementation

    public async Task OnNavigatedToAsync()
    {
        Debug.WriteLine("[CheckInOutViewModel] OnNavigatedToAsync called");

        if (_isLoadingDataFlag)
        {
            Debug.WriteLine("[CheckInOutViewModel] Already loading, skipping");
            return;
        }

        try
        {
            _isLoadingDataFlag = true;
            IsLoadingData = true;

            // Reload data for the selected date
            await LoadCheckInDataFromService(SelectedDate);

            Debug.WriteLine($"[CheckInOutViewModel] Data reloaded: {WalkInPersons.Count} walk-ins, {MemberPersons.Count} members");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CheckInOutViewModel] Error loading data on navigation: {ex.Message}");
            _toastManager?.CreateToast("Load Error")
                .WithContent($"Failed to load check-in data: {ex.Message}")
                .ShowError();
        }
        finally
        {
            _isLoadingDataFlag = false;
            IsLoadingData = false;
        }
    }

    public Task OnNavigatedFromAsync()
    {
        Debug.WriteLine("[CheckInOutViewModel] OnNavigatedFromAsync called");
        return Task.CompletedTask;
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToEvents()
    {
        var eventService = DashboardEventService.Instance;

        eventService.CheckinAdded += OnCheckInOutDataChanged;
        eventService.CheckoutAdded += OnCheckInOutDataChanged;
        eventService.CheckInOutDeleted += OnCheckInOutDataChanged;
    }

    private async void OnCheckInOutDataChanged(object? sender, EventArgs e)
    {
        Debug.WriteLine("[OnCheckInOutDataChanged] Event triggered");

        // Small delay to ensure DB operations complete
        await Task.Delay(100);

        if (_isLoadingDataFlag)
        {
            Debug.WriteLine("[OnCheckInOutDataChanged] Already loading, skipping");
            return;
        }

        try
        {
            _isLoadingDataFlag = true;
            await LoadCheckInDataFromService(SelectedDate);
            Debug.WriteLine($"[OnCheckInOutDataChanged] Data refreshed: {WalkInPersons.Count} walk-ins, {MemberPersons.Count} members");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OnCheckInOutDataChanged] Error: {ex.Message}");
            _toastManager?.CreateToast($"Failed to refresh: {ex.Message}");
        }
        finally
        {
            _isLoadingDataFlag = false;
        }
    }

    #endregion

    #region Data Loading

    private async Task LoadCheckInDataFromService(DateTime date)
    {
        if (_checkInOutService == null)
        {
            Debug.WriteLine("[LoadCheckInDataFromService] Service is null, loading sample data");
            LoadSampleData();
            return;
        }

        try
        {
            Debug.WriteLine($"[LoadCheckInDataFromService] Loading data for {date:yyyy-MM-dd}");

            // Load member check-ins from service
            var memberCheckIns = await _checkInOutService.GetMemberCheckInsAsync(date);
            OriginalMemberData = memberCheckIns ?? new List<MemberPerson>();
            Debug.WriteLine($"[LoadCheckInDataFromService] Loaded {OriginalMemberData.Count} member check-ins from service");

            // Load walk-in check-ins from service  
            var walkInCheckIns = await _checkInOutService.GetWalkInCheckInsAsync(date);
            OriginalWalkInData = walkInCheckIns ?? new List<WalkInPerson>();
            Debug.WriteLine($"[LoadCheckInDataFromService] Loaded {OriginalWalkInData.Count} walk-in check-ins from service");

            // Apply date filter to populate the observable collections
            FilterDataByDate(date);

            Debug.WriteLine($"[LoadCheckInDataFromService] After filter: {WalkInPersons.Count} walk-ins, {MemberPersons.Count} members visible");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LoadCheckInDataFromService] Error: {ex.Message}");
            _toastManager?.CreateToast("Service Error")
                .WithContent($"Failed to load check-in data: {ex.Message}")
                .ShowError();
            throw;
        }
    }

    private void LoadSampleData()
    {
        Debug.WriteLine("[LoadSampleData] Loading sample data");

        var walkInPeople = GetSampleWalkInPeople();
        var memberPeople = GetSampleMemberPeople();

        OriginalWalkInData = walkInPeople;
        OriginalMemberData = memberPeople;

        FilterDataByDate(SelectedDate);
    }

    #endregion

    #region Sample Data

    private List<WalkInPerson> GetSampleWalkInPeople()
    {
        var today = DateTime.Today;
        return
        [
            // Today's data
            new WalkInPerson
            {
                ID = 1018, FirstName = "Rome", LastName = "Calubayan", Age = 21, ContactNumber = "09283374574",
                PackageType = "Gym", DateAttendance = today, CheckInTime = today.AddHours(8), CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1017, FirstName = "Dave", LastName = "Dapitillo", Age = 22, ContactNumber = "09123456789",
                PackageType = "Boxing", DateAttendance = today, CheckInTime = today.AddHours(9), CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1016, FirstName = "Sianrey", LastName = "Flora", Age = 20, ContactNumber = "09123456789",
                PackageType = "Boxing", DateAttendance = today, CheckInTime = today.AddHours(10), CheckOutTime = null
            },
        
            // Yesterday's data
            new WalkInPerson
            {
                ID = 1015, FirstName = "JC", LastName = "Casidor", Age = 30, ContactNumber = "09123456789",
                PackageType = "Boxing", DateAttendance = today.AddDays(-1), CheckInTime = today.AddHours(8), CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1014, FirstName = "Mark", LastName = "Dela Cruz", Age = 21, ContactNumber = "09123456789",
                PackageType = "Muay Thai", DateAttendance = today.AddDays(-1), CheckInTime = today.AddHours(9),
                CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1013, FirstName = "Mardie", LastName = "Dela Cruz Jr.", Age = 21, ContactNumber = "09123456789",
                PackageType = "CrossFit", DateAttendance = today.AddDays(-1), CheckInTime = today.AddHours(10),
                CheckOutTime = null
            },
        
            // 2 days ago
            new WalkInPerson
            {
                ID = 1012, FirstName = "Marc", LastName = "Torres", Age = 26, ContactNumber = "09123456789",
                PackageType = "Boxing", DateAttendance = today.AddDays(-2), CheckInTime = today.AddHours(8), CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1011, FirstName = "Maverick", LastName = "Lim", Age = 21, ContactNumber = "09123456789",
                PackageType = "CrossFit", DateAttendance = today.AddDays(-2), CheckInTime = today.AddHours(9),
                CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1010, FirstName = "Adriel", LastName = "Del Rosario", Age = 21, ContactNumber = "09123456789",
                PackageType = "CrossFit", DateAttendance = today.AddDays(-2), CheckInTime = today.AddHours(10),
                CheckOutTime = null
            },
        
            // 3 days ago
            new WalkInPerson
            {
                ID = 1009, FirstName = "JL", LastName = "Taberdo", Age = 21, ContactNumber = "09123456789",
                PackageType = "CrossFit", DateAttendance = today.AddDays(-3), CheckInTime = today.AddHours(8),
                CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1008, FirstName = "Jav", LastName = "Agustin", Age = 21, ContactNumber = "09123456789",
                PackageType = "Muay Thai", DateAttendance = today.AddDays(-3), CheckInTime = today.AddHours(9),
                CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1007, FirstName = "Daniel", LastName = "Empinado", Age = 20, ContactNumber = "09123456789",
                PackageType = "Muay Thai", DateAttendance = today.AddDays(-3), CheckInTime = today.AddHours(10),
                CheckOutTime = null
            },
        
            // 4 days ago
            new WalkInPerson
            {
                ID = 1006, FirstName = "Marion", LastName = "Dela Roca", Age = 20, ContactNumber = "09123456789",
                PackageType = "Boxing", DateAttendance = today.AddDays(-4), CheckInTime = today.AddHours(8), CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1005, FirstName = "Sianrey", LastName = "Flora", Age = 20, ContactNumber = "09123456789",
                PackageType = "Boxing", DateAttendance = today.AddDays(-4), CheckInTime = today.AddHours(9), CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1004, FirstName = "JC", LastName = "Casidor", Age = 30, ContactNumber = "09123456789",
                PackageType = "Boxing", DateAttendance = today.AddDays(-4), CheckInTime = today.AddHours(10), CheckOutTime = null
            },
        
            // 5 days ago
            new WalkInPerson
            {
                ID = 1003, FirstName = "Mark", LastName = "Dela Cruz", Age = 21, ContactNumber = "09123456789",
                PackageType = "Muay Thai", DateAttendance = today.AddDays(-5), CheckInTime = today.AddHours(8),
                CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1002, FirstName = "Mardie", LastName = "Dela Cruz Jr.", Age = 21, ContactNumber = "09123456789",
                PackageType = "CrossFit", DateAttendance = today.AddDays(-5), CheckInTime = today.AddHours(9),
                CheckOutTime = null
            },
            new WalkInPerson
            {
                ID = 1001, FirstName = "Marc", LastName = "Torres", Age = 26, ContactNumber = "09123456789",
                PackageType = "Boxing", DateAttendance = today.AddDays(-5), CheckInTime = today.AddHours(10), CheckOutTime = null
            }
        ];
    }

    private List<MemberPerson> GetSampleMemberPeople()
    {
        var today = DateTime.Today;
        return
        [
            // Today's data
            new MemberPerson
            {
                ID = 2006, AvatarSource = ManageMemberModel.DefaultAvatarSource,
                FirstName = "Mardie", LastName = "Dela Cruz", ContactNumber = "09123456789",
                MembershipType = "Gym Member", Status = "Active", DateAttendance = today, CheckInTime = today.AddHours(8),
                CheckOutTime = null
            },
            new MemberPerson
            {
                ID = 2005, AvatarSource = ManageMemberModel.DefaultAvatarSource, FirstName = "Cirilo", LastName = "Pagayunan Jr.",
                ContactNumber = "09123456789", MembershipType = "Free Trial", Status = "Active",
                CheckInTime = today.AddHours(9), DateAttendance = today, CheckOutTime = null
            },
        
            // Yesterday's data
            new MemberPerson
            {
                ID = 2004, AvatarSource = ManageMemberModel.DefaultAvatarSource, FirstName = "Raymart", LastName = "Soneja",
                ContactNumber = "09123456789", MembershipType = "Gym Member", Status = "Expired",
                DateAttendance = today.AddDays(-1), CheckInTime = today.AddHours(8), CheckOutTime = null
            },
        
            // 2 days ago
            new MemberPerson
            {
                ID = 2003, AvatarSource = ManageMemberModel.DefaultAvatarSource, FirstName = "Xyrus", LastName = "Jawili",
                ContactNumber = "09123456789", MembershipType = "Gym Member", Status = "Active",
                DateAttendance = today.AddDays(-2), CheckInTime = today.AddHours(8), CheckOutTime = null
            },
        
            // 3 days ago
            new MemberPerson
            {
                ID = 2002, AvatarSource = ManageMemberModel.DefaultAvatarSource, FirstName = "Nash", LastName = "Floralde",
                ContactNumber = "09123456789", MembershipType = "Free Trial", Status = "Expired",
                DateAttendance = today.AddDays(-3), CheckInTime = today.AddHours(8), CheckOutTime = null
            },
            new MemberPerson
            {
                ID = 2001, AvatarSource = ManageMemberModel.DefaultAvatarSource, FirstName = "Ry", LastName = "Estrada", ContactNumber = "09123456789",
                MembershipType = "Free Trial", Status = "Expired", DateAttendance = today.AddDays(-3),
                CheckInTime = today.AddHours(9), CheckOutTime = null
            }
        ];
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task AddMemberPerson()
    {
        if (_checkInOutService == null)
        {
            _toastManager.CreateToast("Service Unavailable")
                .WithContent("System service is not available")
                .ShowError();
            return;
        }

        // Check if selected date is today
        if (!CanCheckIn)
        {
            _toastManager.CreateToast("Check-In Not Allowed")
                .WithContent("You can only check in members for today's date. Please select today to check in.")
                .ShowWarning();
            return;
        }

        try
        {
            IsLoadingData = true;

            // Initialize dialog
            _logGymMemberDialogCardViewModel.Initialize();

            _dialogManager.CreateDialog(_logGymMemberDialogCardViewModel)
                .WithSuccessCallback(async _ =>
                {
                    var selectedMember = _logGymMemberDialogCardViewModel.LastSelectedMember;
                    if (selectedMember != null)
                    {
                        var memberId = selectedMember.ID != 0 ? selectedMember.ID : selectedMember.ID;

                        // Pass selected date to check-in method
                        var success = await _checkInOutService.CheckInMemberAsync(memberId, SelectedDate);

                        if (success)
                        {
                            _toastManager.CreateToast($"Checked In: {selectedMember.FirstName} {selectedMember.LastName}")
                                .WithContent("Member successfully checked in!")
                                .DismissOnClick()
                                .ShowSuccess();

                            await LoadCheckInDataFromService(SelectedDate);
                        }
                        // Error messages are now handled in the service
                    }
                    else
                    {
                        _toastManager.CreateToast("No Selection")
                            .WithContent("No member was selected")
                            .DismissOnClick()
                            .ShowWarning();
                    }
                })
                .WithCancelCallback(() =>
                    _toastManager.CreateToast("Check-in Cancelled")
                        .WithContent("Member check-in was cancelled")
                        .DismissOnClick()
                        .ShowWarning())
                .WithMaxWidth(850)
                .Show();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in AddMemberPerson: {ex.Message}");
            _toastManager.CreateToast("Error")
                .WithContent($"Failed to process member check-in: {ex.Message}")
                .ShowError();
        }
        finally
        {
            IsLoadingData = false;
        }
    }

    [RelayCommand]
    private async Task StampWalkInCheckOut(WalkInPerson? walkIn)
    {
        if (walkIn is null || _checkInOutService == null) return;

        try
        {
            var success = await _checkInOutService.CheckOutWalkInAsync(walkIn.ID);

            if (success)
            {
                // Update local data
                DashboardEventService.Instance.NotifyCheckoutAdded();
                walkIn.CheckOutTime = DateTime.Now;
                _toastManager.CreateToast("Check Out Success")
                    .WithContent($"Stamped {walkIn.FirstName} {walkIn.LastName} check-out time!")
                    .DismissOnClick()
                    .ShowSuccess();
            }
            else
            {
                _toastManager.CreateToast("Check Out Failed")
                    .WithContent($"Failed to check out {walkIn.FirstName} {walkIn.LastName}")
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking out walk-in: {ex.Message}");
            _toastManager.CreateToast("Error")
                .WithContent($"Error during check-out: {ex.Message}")
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task StampMemberInCheckOut(MemberPerson? member)
    {
        if (member is null || _checkInOutService == null) return;

        try
        {
            var success = await _checkInOutService.CheckOutMemberAsync(member.ID);

            if (success)
            {
                // Update local data
                member.CheckOutTime = DateTime.Now;
                DashboardEventService.Instance.NotifyCheckoutAdded();
                _toastManager.CreateToast("Check Out Success")
                    .WithContent($"Stamped {member.FirstName} {member.LastName} check-out time!")
                    .DismissOnClick()
                    .ShowSuccess();
            }
            else
            {
                _toastManager.CreateToast("Check Out Failed")
                    .WithContent($"Failed to check out {member.FirstName} {member.LastName}")
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking out member: {ex.Message}");
            _toastManager.CreateToast("Error")
                .WithContent($"Error during check-out: {ex.Message}")
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task OpenLogWalkInPurchase()
    {
        if (!CanCheckIn)
        {
            _toastManager.CreateToast("Check-In Not Allowed")
                .WithContent("You can only register walk-ins for today's date. Please select today.")
                .ShowWarning();
            return;
        }

        // Set the shared date BEFORE navigating
        NavigationDate.SelectedCheckInDate = SelectedDate;

        _pageManager.Navigate<LogWalkInPurchaseViewModel>();
    }

    #endregion

    #region Property Changed Handlers

    private void OnWalkInPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WalkInPerson.IsSelected))
        {
            UpdateWalkInCounts();
        }
    }

    private void OnMemberPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MemberPerson.IsSelected))
        {
            UpdateMemberCounts();
        }
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        Debug.WriteLine($"[OnSelectedDateChanged] Date changed to {value:yyyy-MM-dd}");
        Debug.WriteLine($"[OnSelectedDateChanged] Original data count: Walk-ins={OriginalWalkInData?.Count ?? 0}, Members={OriginalMemberData?.Count ?? 0}");

        // Update CanCheckIn based on selected date
        CanCheckIn = value.Date == DateTime.Today;

        // Load data for selected date
        _ = LoadCheckInDataFromService(value);

        // Show warning if trying to view non-today date
        if (!CanCheckIn)
        {
            _toastManager?.CreateToast("View Only Mode")
                .WithContent("You can only check in members for today's date. Currently viewing historical data.")
                .ShowInfo();
        }
    }

    #endregion

    #region Filtering and Counts

    private void FilterDataByDate(DateTime selectedDate)
    {
        Debug.WriteLine($"[FilterDataByDate] Filtering for date: {selectedDate:yyyy-MM-dd}");
        Debug.WriteLine($"[FilterDataByDate] Before filter - Original Walk-ins: {OriginalWalkInData?.Count ?? 0}, Original Members: {OriginalMemberData?.Count ?? 0}");

        // Filter walk-in data
        var filteredWalkInData = (OriginalWalkInData ?? new List<WalkInPerson>())
            .Where(w => w.DateAttendance?.Date == selectedDate.Date)
            .ToList();

        CurrentWalkInFilteredData = filteredWalkInData;
        Debug.WriteLine($"[FilterDataByDate] Filtered walk-ins: {filteredWalkInData.Count}");

        // Unsubscribe all existing handlers first
        foreach (var walkIn in WalkInPersons.ToList())
        {
            walkIn.PropertyChanged -= OnWalkInPropertyChanged;
        }

        WalkInPersons.Clear();

        // Add new data with handlers
        foreach (var walkIn in filteredWalkInData)
        {
            walkIn.PropertyChanged += OnWalkInPropertyChanged;
            WalkInPersons.Add(walkIn);
        }

        // Filter member data
        var filteredMemberData = (OriginalMemberData ?? new List<MemberPerson>())
            .Where(m => m.DateAttendance?.Date == selectedDate.Date)
            .ToList();

        CurrentMemberFilteredData = filteredMemberData;
        Debug.WriteLine($"[FilterDataByDate] Filtered members: {filteredMemberData.Count}");

        // Unsubscribe all existing handlers first
        foreach (var member in MemberPersons.ToList())
        {
            member.PropertyChanged -= OnMemberPropertyChanged;
        }

        MemberPersons.Clear();

        // Add new data with handlers
        foreach (var member in filteredMemberData)
        {
            member.PropertyChanged += OnMemberPropertyChanged;
            MemberPersons.Add(member);
        }

        UpdateWalkInCounts();
        UpdateMemberCounts();

        Debug.WriteLine($"[FilterDataByDate] After filter - WalkInPersons: {WalkInPersons.Count}, MemberPersons: {MemberPersons.Count}");
    }

    private void UpdateWalkInCounts()
    {
        SelectedCount = WalkInPersons.Count(x => x.IsSelected);
        TotalCount = WalkInPersons.Count;

        SelectAll = WalkInPersons.Count > 0 && WalkInPersons.All(x => x.IsSelected);
    }

    private void UpdateMemberCounts()
    {
        SelectedCount = MemberPersons.Count(x => x.IsSelected);
        TotalCount = MemberPersons.Count;

        SelectAll = MemberPersons.Count > 0 && MemberPersons.All(x => x.IsSelected);
    }

    #endregion

    #region Dispose

    protected override void DisposeManagedResources()
    {
        Debug.WriteLine("[CheckInOutViewModel] Disposing resources");

        // Unsubscribe from events
        var eventService = DashboardEventService.Instance;
        eventService.CheckinAdded -= OnCheckInOutDataChanged;
        eventService.CheckoutAdded -= OnCheckInOutDataChanged;
        eventService.CheckInOutDeleted -= OnCheckInOutDataChanged;

        // Unsubscribe from property change handlers
        foreach (var walkIn in WalkInPersons)
        {
            walkIn.PropertyChanged -= OnWalkInPropertyChanged;
        }

        foreach (var member in MemberPersons)
        {
            member.PropertyChanged -= OnMemberPropertyChanged;
        }

        // Clear collections
        WalkInPersons?.Clear();
        MemberPersons?.Clear();
        OriginalWalkInData?.Clear();
        OriginalMemberData?.Clear();
        CurrentWalkInFilteredData?.Clear();
        CurrentMemberFilteredData?.Clear();

        base.DisposeManagedResources();
    }

    #endregion
}

#region Person Models

public partial class WalkInPerson : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public int ID { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string ContactNumber { get; set; } = string.Empty;
    public string PackageType { get; set; } = string.Empty;

    public DateTime? DateAttendance { get; set; }
    public DateTime? CheckInTime { get; set; }
    private DateTime? _checkOutTime;

    public DateTime? CheckOutTime
    {
        get => _checkOutTime;
        set => SetProperty(ref _checkOutTime, value);
    }

    public string DateFormatted => DateAttendance?.ToString("MMMM dd, yyyy") ?? string.Empty;
}

public partial class MemberPerson : ViewModelBase
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private Bitmap _avatarSource = ManageMemberModel.DefaultAvatarSource;

    public int ID { get; set; }
    public int MemberID { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string MembershipType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public DateTime? DateAttendance { get; set; }
    public DateTime? CheckInTime { get; set; }
    private DateTime? _checkOutTime;

    public DateTime? CheckOutTime
    {
        get => _checkOutTime;
        set => SetProperty(ref _checkOutTime, value);
    }

    public string DateFormatted => DateAttendance?.ToString("MMMM dd, yyyy") ?? string.Empty;

    public IBrush StatusForeground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),  // Green-500
        "expired" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red-500
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))        // Default Gray-500
    };

    public IBrush StatusBackground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),  // Green-500 with alpha
        "expired" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)), // Red-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))        // Default Gray-500 with alpha
    };

    public string StatusDisplayText => Status.ToLowerInvariant() switch
    {
        "active" => "● Active",
        "expired" => "● Expired",
        _ => Status ?? ""
    };

    public void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusDisplayText));
    }
}

#endregion
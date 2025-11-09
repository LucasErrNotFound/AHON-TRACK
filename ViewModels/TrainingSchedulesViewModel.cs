using AHON_TRACK.Components.ViewModels;
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

[Page("training-schedules")]
public sealed partial class TrainingSchedulesViewModel : ViewModelBase, INavigable
{
    [ObservableProperty]
    private string[] _packageFilterItems = ["All"];

    [ObservableProperty]
    private string _selectedPackageFilterItem = "All";

    [ObservableProperty]
    private bool _isLoadingPackages;

    [ObservableProperty]
    private string[] _coachFilterItems = [];

    [ObservableProperty]
    private string _selectedCoachFilterItem = "All Coaches";

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private List<ScheduledPerson> _originalScheduledPeople = [];

    [ObservableProperty]
    private List<ScheduledPerson> _currentScheduledPeople = [];

    [ObservableProperty]
    private bool _selectAll;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _currentScheduleCount;

    [ObservableProperty]
    private int _upcomingScheduleCount;

    [ObservableProperty]
    private double _currentSchedulePercentageChange;

    [ObservableProperty]
    private double _upcomingSchedulePercentageChange;

    [ObservableProperty]
    private string _currentScheduleChangeText = string.Empty;

    [ObservableProperty]
    private string _upcomingScheduleChangeText = string.Empty;

    [ObservableProperty]
    private bool _isLoadingCoaches;

    [ObservableProperty]
    private ObservableCollection<ScheduledPerson> _scheduledPeople = [];

    private bool _disposed = false;

    // ✅ Thread-safety for refresh operations
    private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
    private CancellationTokenSource? _refreshCts;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly AddTrainingScheduleDialogCardViewModel _addTrainingScheduleDialogCardViewModel;
    private readonly ChangeScheduleDialogCardViewModel _changeScheduleDialogCardViewModel;
    private readonly ITrainingService _trainingService;

    public TrainingSchedulesViewModel(PageManager pageManager, DialogManager dialogManager, ToastManager toastManager,
        AddTrainingScheduleDialogCardViewModel addTrainingScheduleDialogCardViewModel, ITrainingService trainingService, ChangeScheduleDialogCardViewModel changeScheduleDialogCardViewModel)
    {
        _dialogManager = dialogManager;
        _pageManager = pageManager;
        _toastManager = toastManager;
        _addTrainingScheduleDialogCardViewModel = addTrainingScheduleDialogCardViewModel;
        _changeScheduleDialogCardViewModel = changeScheduleDialogCardViewModel;
        _trainingService = trainingService;

        SubscribeToEvents();
        _ = LoadTrainingsAsync();
        _ = LoadCoachesAsync();
        _ = LoadPackagesAsync();
        UpdateScheduledPeopleCounts();
    }

    public TrainingSchedulesViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _addTrainingScheduleDialogCardViewModel = new AddTrainingScheduleDialogCardViewModel();
        _changeScheduleDialogCardViewModel = new ChangeScheduleDialogCardViewModel();
        _trainingService = null!;

        SubscribeToEvents();
        _ = LoadCoachesAsync();
        UpdateScheduledPeopleCounts();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        SubscribeToEvents();
        _ = LoadTrainingsAsync();
        _ = LoadCoachesAsync();
        UpdateScheduledPeopleCounts();
        IsInitialized = true;
    }

    private void SubscribeToEvents()
    {
        var eventService = DashboardEventService.Instance;
        eventService.ScheduleAdded += OnTrainingDataChanged;
        eventService.ScheduleUpdated += OnTrainingDataChanged;
        eventService.MemberUpdated += OnTrainingDataChanged;

        eventService.EmployeeAdded += OnCoachDataChanged;
        eventService.EmployeeUpdated += OnCoachDataChanged;
        eventService.EmployeeDeleted += OnCoachDataChanged;
        eventService.PackageAdded += OnPackageDataChanged;
        eventService.PackageUpdated += OnPackageDataChanged;
        eventService.PackageDeleted += OnPackageDataChanged;

    }

    private void UpdateDashboardStatistics()
    {
        var today = SelectedDate.Date;
        var yesterday = today.AddDays(-1);

        // Current Schedule (Today)
        var todaySchedules = OriginalScheduledPeople
            .Where(s => s.ScheduledDate?.Date == today)
            .ToList();
        CurrentScheduleCount = todaySchedules.Count;

        // Upcoming Schedule (Future dates, excluding today)
        var upcomingSchedules = OriginalScheduledPeople
            .Where(s => s.ScheduledDate?.Date > today)
            .ToList();
        UpcomingScheduleCount = upcomingSchedules.Count;

        // Calculate percentage changes for Current Schedule
        var yesterdaySchedules = OriginalScheduledPeople
            .Where(s => s.ScheduledDate?.Date == yesterday)
            .ToList();
        int yesterdayCount = yesterdaySchedules.Count;

        if (yesterdayCount > 0)
        {
            CurrentSchedulePercentageChange = ((double)(CurrentScheduleCount - yesterdayCount) / yesterdayCount) * 100;
            CurrentSchedulePercentageChange = Math.Max(-100, Math.Min(100, CurrentSchedulePercentageChange));
            CurrentScheduleChangeText = $"{(CurrentSchedulePercentageChange >= 0 ? "+" : "")}{CurrentSchedulePercentageChange:F1}% from yesterday";
        }
        else
        {
            CurrentSchedulePercentageChange = 0;
            CurrentScheduleChangeText = "No data from yesterday";
        }

        // For upcoming schedule comparison (compare with yesterday's upcoming count)
        var yesterdayUpcomingSchedules = OriginalScheduledPeople
            .Where(s => s.ScheduledDate?.Date > yesterday)
            .ToList();
        int yesterdayUpcomingCount = yesterdayUpcomingSchedules.Count;

        if (yesterdayUpcomingCount > 0)
        {
            UpcomingSchedulePercentageChange = ((double)(UpcomingScheduleCount - yesterdayUpcomingCount) / yesterdayUpcomingCount) * 100;
            UpcomingSchedulePercentageChange = Math.Max(-100, Math.Min(100, UpcomingSchedulePercentageChange));
            UpcomingScheduleChangeText = $"{(UpcomingSchedulePercentageChange >= 0 ? "+" : "")}{UpcomingSchedulePercentageChange:F1}% from yesterday";
        }
        else
        {
            UpcomingSchedulePercentageChange = 0;
            UpcomingScheduleChangeText = "No data from yesterday";
        }
    }

    private async Task LoadPackagesAsync()
    {
        if (_trainingService == null) return;

        IsLoadingPackages = true;

        try
        {
            var packages = await _trainingService.GetPackageNamesAsync();

            var packageList = new List<string> { "All" };
            packageList.AddRange(packages.Where(p => !string.IsNullOrEmpty(p)).OrderBy(p => p));

            PackageFilterItems = packageList.ToArray();

            if (!PackageFilterItems.Contains(SelectedPackageFilterItem))
            {
                SelectedPackageFilterItem = "All";
            }
        }
        catch (Exception ex)
        {
            PackageFilterItems = ["All"];
            SelectedPackageFilterItem = "All";

            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to load packages: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoadingPackages = false;
        }
    }

    // ✅ FIXED: Thread-safe event handler with debouncing
    private async void OnPackageDataChanged(object? sender, EventArgs e)
    {
        if (!await _refreshLock.WaitAsync(0)) return;

        try
        {
            await Task.Delay(100); // Small debounce
            await LoadPackagesAsync();
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Refresh Error")
                .WithContent($"Failed to reload packages: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // ✅ FIXED: Removed _isLoadingDataFlag check at the start
    public async Task LoadTrainingsAsync()
    {
        // ✅ Allow refresh to happen even if currently loading
        IsLoading = true;

        try
        {
            var trainings = await _trainingService.GetTrainingSchedulesAsync();
            var people = new List<ScheduledPerson>();

            foreach (var t in trainings)
            {
                people.Add(new ScheduledPerson
                {
                    TrainingID = t.trainingID,
                    ID = t.customerID,
                    FirstName = t.firstName,
                    LastName = t.lastName,
                    ContactNumber = t.contactNumber,
                    PackageType = t.packageType,
                    AssignedCoach = t.assignedCoach,
                    ScheduledDate = t.scheduledDate.Date,
                    ScheduledTimeStart = TimeOnly.FromDateTime(t.scheduledTimeStart),
                    ScheduledTimeEnd = TimeOnly.FromDateTime(t.scheduledTimeEnd),
                    Attendance = t.attendance,
                });
            }

            OriginalScheduledPeople = people;

            // ✅ Auto-mark absent for past schedules
            await AutoMarkAbsentForPastSchedulesAsync();

            FilterDataByPackageAndDate();
            UpdateDashboardStatistics();
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Load Error")
                .WithContent($"Failed to load training schedules: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadCoachesAsync()
    {
        if (_trainingService == null) return;

        IsLoadingCoaches = true;

        try
        {
            var coaches = await _trainingService.GetCoachNamesAsync();

            var coachNames = coaches
                .Where(c => !string.IsNullOrEmpty(c.FullName))
                .OrderBy(c => c.FullName)
                .Select(c => c.FullName)
                .ToList();

            coachNames.Insert(0, "All Coaches");

            CoachFilterItems = coachNames.ToArray();

            if (string.IsNullOrEmpty(SelectedCoachFilterItem))
            {
                SelectedCoachFilterItem = "All Coaches";
            }
        }
        catch (Exception ex)
        {
            CoachFilterItems = ["All Coaches"];
            SelectedCoachFilterItem = "All Coaches";

            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to load coaches: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoadingCoaches = false;
        }
    }

    [RelayCommand]
    private void OpenAddScheduleDialog()
    {
        _addTrainingScheduleDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_addTrainingScheduleDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                await LoadTrainingsAsync();

                _toastManager.CreateToast("Added new training schedule")
                    .WithContent($"You have added a new training schedule!")
                    .DismissOnClick()
                    .ShowSuccess();
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new training schedule cancelled")
                    .WithContent("Add a new training schedule to continue")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(1360)
            .Show();
    }

    [RelayCommand]
    private async Task MarkAsPresentAsync(ScheduledPerson? scheduledPerson)
    {
        if (scheduledPerson is null || scheduledPerson.TrainingID is null) return;

        // ✅ Check if schedule is in the valid time range (today only)
        if (!IsScheduleEligibleForAttendance(scheduledPerson))
        {
            _toastManager.CreateToast("Invalid Action")
                .WithContent("Attendance can only be marked for today's schedules.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        scheduledPerson.Attendance = "Present";

        var success = await _trainingService.UpdateAttendanceAsync(scheduledPerson.TrainingID.Value, "Present");

        if (success)
        {
            _toastManager.CreateToast("Marked as present")
                .WithContent($"Marked {scheduledPerson.FirstName} {scheduledPerson.LastName} as present!")
                .DismissOnClick()
                .ShowSuccess();
        }
        else
        {
            _toastManager.CreateToast("Database Error")
                .WithContent("Failed to update attendance in database.")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task MarkAsAbsentAsync(ScheduledPerson? scheduledPerson)
    {
        if (scheduledPerson is null || scheduledPerson.TrainingID is null) return;

        // ✅ Check if schedule is in the valid time range (today only)
        if (!IsScheduleEligibleForAttendance(scheduledPerson))
        {
            _toastManager.CreateToast("Invalid Action")
                .WithContent("Attendance can only be marked for today's schedules.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        scheduledPerson.Attendance = "Absent";

        var success = await _trainingService.UpdateAttendanceAsync(scheduledPerson.TrainingID.Value, "Absent");

        if (success)
        {
            _toastManager.CreateToast("Marked as absent")
                .WithContent($"Marked {scheduledPerson.FirstName} {scheduledPerson.LastName} as absent!")
                .DismissOnClick()
                .ShowSuccess();
        }
        else
        {
            _toastManager.CreateToast("Database Error")
                .WithContent("Failed to update attendance in database.")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private void ChangeTrainingSchedule(ScheduledPerson? scheduledPerson)
    {
        if (scheduledPerson is null) return;

        // ✅ Check if attendance has already been marked
        if (IsAttendanceMarked(scheduledPerson))
        {
            _toastManager.CreateToast("Schedule Locked")
                .WithContent($"Cannot change schedule for {scheduledPerson.FirstName} {scheduledPerson.LastName} because attendance has already been marked as {scheduledPerson.Attendance}.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        _changeScheduleDialogCardViewModel.Initialize(scheduledPerson);
        _dialogManager.CreateDialog(_changeScheduleDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                // ✅ Reload data after successful update
                await LoadTrainingsAsync();

                _toastManager.CreateToast("Changed training schedule")
                    .WithContent($"You have changed {scheduledPerson.FirstName} {scheduledPerson.LastName}'s training schedule!")
                    .DismissOnClick()
                    .ShowSuccess();
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Changing training schedule cancelled")
                    .WithContent("Changing training schedule cancelled")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(800)
            .Show();
    }

    /// <summary>
    /// Checks if a schedule is eligible for attendance marking (only today's schedules)
    /// </summary>
    private bool IsScheduleEligibleForAttendance(ScheduledPerson scheduledPerson)
    {
        if (scheduledPerson.ScheduledDate == null) return false;

        var today = DateTime.Today;
        var scheduleDate = scheduledPerson.ScheduledDate.Value.Date;

        // Only allow marking attendance for today's schedules
        return scheduleDate == today;
    }

    /// <summary>
    /// Checks if attendance has already been marked (Present or Absent)
    /// </summary>
    private bool IsAttendanceMarked(ScheduledPerson scheduledPerson)
    {
        if (string.IsNullOrEmpty(scheduledPerson.Attendance)) return false;

        var attendance = scheduledPerson.Attendance.Trim().ToLowerInvariant();
        return attendance == "present" || attendance == "absent";
    }

    /// <summary>
    /// Automatically marks pending schedules as absent if the schedule end time has passed + 1 hour grace period
    /// </summary>
    private async Task AutoMarkAbsentForPastSchedulesAsync()
    {
        try
        {
            var now = DateTime.Now;
            var pendingExpiredSchedules = OriginalScheduledPeople
                .Where(s => s.ScheduledDate.HasValue
                    && s.ScheduledTimeEnd.HasValue
                    && s.TrainingID.HasValue
                    && (string.IsNullOrEmpty(s.Attendance) || s.Attendance.Trim().ToLowerInvariant() == "pending"))
                .ToList();

            var schedulesToMarkAbsent = new List<ScheduledPerson>();

            foreach (var schedule in pendingExpiredSchedules)
            {
                // Combine date and end time to get the exact end datetime
                var scheduleEndDateTime = schedule.ScheduledDate.Value.Date
                    .Add(schedule.ScheduledTimeEnd!.Value.ToTimeSpan());

                // Add 1 hour grace period after schedule ends
                var graceEndDateTime = scheduleEndDateTime.AddHours(1);

                // If current time is past the grace period, mark as absent
                if (now > graceEndDateTime)
                {
                    schedulesToMarkAbsent.Add(schedule);
                }
            }

            if (!schedulesToMarkAbsent.Any()) return;

            int markedCount = 0;
            foreach (var schedule in schedulesToMarkAbsent)
            {
                var success = await _trainingService.UpdateAttendanceAsync(schedule.TrainingID!.Value, "Absent");
                if (success)
                {
                    schedule.Attendance = "Absent";
                    markedCount++;
                }
            }

            if (markedCount > 0)
            {
                _toastManager?.CreateToast("Auto-Marked Absent")
                    .WithContent($"Automatically marked {markedCount} schedule(s) as absent (1 hour after end time)")
                    .DismissOnClick()
                    .ShowInfo();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoMarkAbsentForPastSchedulesAsync] Error: {ex.Message}");
            // Don't show error toast to user as this is a background operation
        }
    }

    private void FilterDataByPackageAndDate()
    {
        var filteredScheduledData = OriginalScheduledPeople
        .Where(w => w.ScheduledDate?.Date == SelectedDate.Date)
        .ToList();

        if (SelectedPackageFilterItem is not "All")
        {
            filteredScheduledData = filteredScheduledData
                .Where(w => w.PackageType == SelectedPackageFilterItem)
                .ToList();
        }

        if (!string.IsNullOrEmpty(SelectedCoachFilterItem) && SelectedCoachFilterItem != "All Coaches")
        {
            filteredScheduledData = filteredScheduledData
                .Where(w => w.AssignedCoach == SelectedCoachFilterItem)
                .ToList();
        }

        CurrentScheduledPeople = filteredScheduledData;
        ScheduledPeople.Clear();

        foreach (var schedule in filteredScheduledData)
        {
            schedule.PropertyChanged -= OnScheduledChanged;
            schedule.PropertyChanged += OnScheduledChanged;
            ScheduledPeople.Add(schedule);
        }
        UpdateScheduledPeopleCounts();
    }

    private void OnScheduledChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MemberPerson.IsSelected))
        {
            UpdateScheduledPeopleCounts();
        }
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        FilterDataByPackageAndDate();
        UpdateDashboardStatistics();
    }

    partial void OnSelectedPackageFilterItemChanged(string value)
    {
        FilterDataByPackageAndDate();
    }

    // ✅ FIXED: Thread-safe event handler with debouncing and cancellation
    private async void OnTrainingDataChanged(object? sender, EventArgs e)
    {
        // Cancel any pending refresh operation
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        // Wait a bit to debounce multiple rapid events
        try
        {
            await Task.Delay(100, token);
        }
        catch (TaskCanceledException)
        {
            return; // Another refresh was requested, let that one handle it
        }

        // Ensure only one refresh runs at a time
        if (!await _refreshLock.WaitAsync(0))
        {
            return; // Already refreshing, skip this call
        }

        try
        {
            if (token.IsCancellationRequested) return;
            await LoadTrainingsAsync();
        }
        catch (OperationCanceledException)
        {
            // Refresh was cancelled by a newer request
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Refresh Error")
                .WithContent($"Failed to refresh schedules: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    partial void OnSelectedCoachFilterItemChanged(string value)
    {
        FilterDataByPackageAndDate();
    }

    private void UpdateScheduledPeopleCounts()
    {
        SelectedCount = ScheduledPeople.Count(x => x.IsSelected);
        TotalCount = ScheduledPeople.Count;

        SelectAll = ScheduledPeople.Count > 0 && ScheduledPeople.All(x => x.IsSelected);
    }

    // ✅ FIXED: Thread-safe event handler
    private async void OnCoachDataChanged(object? sender, EventArgs e)
    {
        if (!await _refreshLock.WaitAsync(0)) return;

        try
        {
            await Task.Delay(100); // Small debounce
            await LoadCoachesAsync();
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Refresh Error")
                .WithContent($"Failed to reload coaches: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // ✅ FIXED: Proper cleanup of thread-safety resources
    protected override void DisposeManagedResources()
    {
        // Cancel any pending refresh operations
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();

        // Dispose the semaphore
        _refreshLock?.Dispose();

        var eventService = DashboardEventService.Instance;
        eventService.TrainingSessionsUpdated -= OnTrainingDataChanged;
        eventService.ScheduleAdded -= OnTrainingDataChanged;
        eventService.ScheduleUpdated -= OnTrainingDataChanged;
        eventService.MemberUpdated -= OnTrainingDataChanged;

        eventService.EmployeeAdded -= OnCoachDataChanged;
        eventService.EmployeeUpdated -= OnCoachDataChanged;
        eventService.EmployeeDeleted -= OnCoachDataChanged;

        eventService.PackageAdded -= OnPackageDataChanged;
        eventService.PackageUpdated -= OnPackageDataChanged;
        eventService.PackageDeleted -= OnPackageDataChanged;

        foreach (var schedule in ScheduledPeople)
        {
            schedule.PropertyChanged -= OnScheduledChanged;
        }

        ScheduledPeople.Clear();
        OriginalScheduledPeople.Clear();
        CurrentScheduledPeople.Clear();
        CoachFilterItems = [];
        PackageFilterItems = [];
    }
}

public partial class ScheduledPerson : ObservableObject
{

    [ObservableProperty]
    private int? _trainingID;

    [ObservableProperty]
    private int? _iD;

    [ObservableProperty]
    private string? _picture = string.Empty;

    [ObservableProperty]
    private string? _firstName = string.Empty;

    [ObservableProperty]
    private string? _lastName = string.Empty;

    [ObservableProperty]
    private string? _contactNumber = string.Empty;

    [ObservableProperty]
    private string? _packageType = string.Empty;

    [ObservableProperty]
    private string? _assignedCoach = string.Empty;

    [ObservableProperty]
    private string? _attendance = string.Empty;

    [ObservableProperty]
    private TimeOnly? _scheduledTimeStart;

    [ObservableProperty]
    private TimeOnly? _scheduledTimeEnd;

    public DateTime? ScheduledDate { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    public string ScheduledTimeRangeFormatted => ScheduledTimeStart.HasValue && ScheduledTimeEnd.HasValue ?
        $"{ScheduledTimeStart.Value:h:mm tt} - {ScheduledTimeEnd.Value:h:mm tt}"
        : string.Empty;

    public string PicturePath => string.IsNullOrEmpty(Picture) || Picture == "null"
        ? "avares://AHON_TRACK/Assets/MainWindowView/user.png"
        : Picture;

    public IBrush AttendanceForeground => Attendance?.ToLowerInvariant() switch
    {
        "present" => new SolidColorBrush(Color.FromRgb(34, 197, 94)), // Green-500
        "absent" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),  // Red-500
        _ => new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)) // Default Gray-500
    };

    public IBrush AttendanceBackground => Attendance?.ToLowerInvariant() switch
    {
        "present" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)), // Green-500 with alpha
        "absent" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),  // Red-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(0, 255, 255, 255))        // Default Gray-500 with alpha
    };

    public string? AttendanceDisplayText => Attendance?.ToLowerInvariant() switch
    {
        "present" => "● Present",
        "absent" => "● Absent",
        _ => Attendance
    };

    partial void OnAttendanceChanged(string? value)
    {
        OnPropertyChanged(nameof(AttendanceForeground));
        OnPropertyChanged(nameof(AttendanceBackground));
        OnPropertyChanged(nameof(AttendanceDisplayText));
    }
}
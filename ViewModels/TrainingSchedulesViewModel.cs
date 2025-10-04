using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using AHON_TRACK.Components.ViewModels;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

[Page("training-schedules")]
public sealed partial class TrainingSchedulesViewModel : ViewModelBase, INavigable
{
    [ObservableProperty] 
    private string[] _packageFilterItems = ["All", "Boxing", "Muay Thai", "Crossfit"];

    [ObservableProperty] 
    private string _selectedPackageFilterItem = "All";
    
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
    private ObservableCollection<ScheduledPerson> _scheduledPeople = [];
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly AddTrainingScheduleDialogCardViewModel _addTrainingScheduleDialogCardViewModel;
    private readonly ChangeScheduleDialogCardViewModel  _changeScheduleDialogCardViewModel;

    public TrainingSchedulesViewModel(PageManager pageManager, DialogManager dialogManager, ToastManager toastManager, 
        AddTrainingScheduleDialogCardViewModel addTrainingScheduleDialogCardViewModel,  ChangeScheduleDialogCardViewModel changeScheduleDialogCardViewModel)
    {
        _dialogManager = dialogManager;
        _pageManager = pageManager;
        _toastManager = toastManager;
        _addTrainingScheduleDialogCardViewModel = addTrainingScheduleDialogCardViewModel;
        _changeScheduleDialogCardViewModel = changeScheduleDialogCardViewModel;
        
        LoadSampleData();
        UpdateScheduledPeopleCounts();
    }

    public TrainingSchedulesViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _addTrainingScheduleDialogCardViewModel = new AddTrainingScheduleDialogCardViewModel();
        _changeScheduleDialogCardViewModel = new ChangeScheduleDialogCardViewModel();
        
        LoadSampleData();
        UpdateScheduledPeopleCounts();
    }
    
    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        LoadSampleData();
        UpdateScheduledPeopleCounts();
        IsInitialized = true;
    }

    private void LoadSampleData()
    {
        var scheduledClients = CreateSampleData();
        OriginalScheduledPeople =  scheduledClients;
        FilterDataByPackageAndDate();
    }

    private List<ScheduledPerson> CreateSampleData()
    {
        var today = DateTime.Today;
        return
        [
            new ScheduledPerson
            {
                ID = 1001, Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "Rome", 
                LastName = "Calubayan", ContactNumber = "09182736273", PackageType = "Boxing", AssignedCoach = "Coach Jho", ScheduledDate = today, 
                ScheduledTimeStart = TimeOnly.FromDateTime(today.AddHours(1)), ScheduledTimeEnd = TimeOnly.FromDateTime(today.AddHours(2)), Attendance = null
            },
            new ScheduledPerson
            {
                ID = 1002, Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "Sianrey", 
                LastName = "Flora", ContactNumber = "09198656372", PackageType = "Boxing", AssignedCoach = "Coach Jho", ScheduledDate = today,
                ScheduledTimeStart = TimeOnly.FromDateTime(today.AddHours(11)), ScheduledTimeEnd = TimeOnly.FromDateTime(today.AddHours(12)), Attendance = null
            },
            new ScheduledPerson
            {
                ID = 1003, Picture = "", FirstName = "Mardie", 
                LastName = "Dela Cruz", ContactNumber = "09138545322", PackageType = "Muay Thai", AssignedCoach = "Coach Jedd", ScheduledDate = today,
                ScheduledTimeStart = TimeOnly.FromDateTime(today.AddHours(11)), ScheduledTimeEnd = TimeOnly.FromDateTime(today.AddHours(12)), Attendance = null
            },
            new ScheduledPerson
            {
                ID = 1004, Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "JL", 
                LastName = "Taberdo", ContactNumber = "09237645212", PackageType = "Crossfit", AssignedCoach = "Coach Rey", ScheduledDate = today.AddDays(1),
                ScheduledTimeStart = TimeOnly.FromDateTime(today.AddHours(14)), ScheduledTimeEnd = TimeOnly.FromDateTime(today.AddHours(16)), Attendance = null
            },
            new ScheduledPerson
            {
                ID = 1005, Picture = "", FirstName = "Jav", 
                LastName = "Agustin", ContactNumber = "09686643211", PackageType = "Muay Thai", AssignedCoach = "Coach Jedd", ScheduledDate = today.AddDays(1),
                ScheduledTimeStart = TimeOnly.FromDateTime(today.AddHours(15)), ScheduledTimeEnd = TimeOnly.FromDateTime(today.AddHours(16)), Attendance = null
            },
            new ScheduledPerson
            {
                ID = 1006, Picture = "", FirstName = "Dave", 
                LastName = "Dapitillo", ContactNumber = "09676544212", PackageType = "Muay Thai", AssignedCoach = "Coach Jedd", ScheduledDate = today.AddDays(2),
                ScheduledTimeStart = TimeOnly.FromDateTime(today.AddHours(17)), ScheduledTimeEnd = TimeOnly.FromDateTime(today.AddHours(18)), Attendance = null
            },
            new ScheduledPerson
            {
                ID = 1007, Picture = "", FirstName = "Daniel", 
                LastName = "Empinado", ContactNumber = "09666452211", PackageType = "Crossfit", AssignedCoach = "Coach Rey", ScheduledDate = today.AddDays(2),
                ScheduledTimeStart = TimeOnly.FromDateTime(today.AddHours(17)), ScheduledTimeEnd = TimeOnly.FromDateTime(today.AddHours(18)), Attendance = null
            },
            new ScheduledPerson
            {
                ID = 1008, Picture = "", FirstName = "Marc", 
                LastName = "Torres", ContactNumber = "098273647382", PackageType = "Crossfit", AssignedCoach = "Coach Rey", ScheduledDate = today.AddDays(2),
                ScheduledTimeStart = TimeOnly.FromDateTime(today.AddHours(17)), ScheduledTimeEnd = TimeOnly.FromDateTime(today.AddHours(18)), Attendance = null
            },
            new ScheduledPerson
            {
                ID = 1009, Picture = "", FirstName = "Mark", 
                LastName = "Dela Cruz", ContactNumber = "091827362837", PackageType = "Crossfit", AssignedCoach = "Coach Rey", ScheduledDate = today.AddDays(3),
                ScheduledTimeStart = TimeOnly.FromDateTime(today.AddHours(19)), ScheduledTimeEnd = TimeOnly.FromDateTime(today.AddHours(20)), Attendance = null
            },
            new ScheduledPerson
            {
                ID = 1010, Picture = "", FirstName = "Adriel", 
                LastName = "Del Rosario", ContactNumber = "09182837748", PackageType = "Boxing", AssignedCoach = "Coach Jho", ScheduledDate = today.AddDays(3),
                ScheduledTimeStart = TimeOnly.FromDateTime(today.AddHours(19)), ScheduledTimeEnd = TimeOnly.FromDateTime(today.AddHours(20)), Attendance = null
            },
            new ScheduledPerson
            {
                ID = 1011, Picture = "", FirstName = "JC", 
                LastName = "Casidor", ContactNumber = "09192818827", PackageType = "Boxing", AssignedCoach = "Coach Jho", ScheduledDate = today.AddDays(3),
                ScheduledTimeStart = TimeOnly.FromDateTime(today.AddHours(21)), ScheduledTimeEnd = TimeOnly.FromDateTime(today.AddHours(22)), Attendance = null
            }
        ];
    }

    [RelayCommand]
    private void OpenAddScheduleDialog()
    {
        _addTrainingScheduleDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_addTrainingScheduleDialogCardViewModel)
            .WithSuccessCallback(_ =>
                _toastManager.CreateToast("Added new training schedule")
                    .WithContent($"You have added a new training schedule!")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new training schedule cancelled")
                    .WithContent("Add a new training schedule to continue")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(1550)
            .Show();
    }
    
    [RelayCommand]
    private void MarkAsPresent(ScheduledPerson? scheduledPerson)
    {
        if (scheduledPerson is null) return;
        scheduledPerson.Attendance = "Present";
		
        _toastManager.CreateToast("Marked as present")
            .WithContent($"Marked {scheduledPerson.FirstName} {scheduledPerson.LastName} as present!")
            .DismissOnClick()
            .ShowSuccess();
    }
    
    [RelayCommand]
    private void MarkAsAbsent(ScheduledPerson? scheduledPerson)
    {
        if (scheduledPerson is null) return;
        scheduledPerson.Attendance = "Absent";
		
        _toastManager.CreateToast("Marked as absent")
            .WithContent($"Marked {scheduledPerson.FirstName} {scheduledPerson.LastName} as absent!")
            .DismissOnClick()
            .ShowSuccess();
    }

    [RelayCommand]
    private void ChangeTrainingSchedule(ScheduledPerson? scheduledPerson)
    {
        if (scheduledPerson is null) return;
        
        _changeScheduleDialogCardViewModel.Initialize(scheduledPerson);
        _dialogManager.CreateDialog(_changeScheduleDialogCardViewModel)
            .WithSuccessCallback(_ =>
                _toastManager.CreateToast("Changed training schedule")
                    .WithContent($"You have changed {scheduledPerson.FirstName} {scheduledPerson.LastName}'s training schedule!")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Changing training schedule cancelled")
                    .WithContent("Changing training schedule cancelled")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(800) // originally Width: 400
            .Show();
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
    }
    
    partial void OnSelectedPackageFilterItemChanged(string value)
    {
        FilterDataByPackageAndDate();
    }
    
    private void UpdateScheduledPeopleCounts()
    {
        SelectedCount = ScheduledPeople.Count(x => x.IsSelected);
        TotalCount = ScheduledPeople.Count;

        SelectAll = ScheduledPeople.Count > 0 && ScheduledPeople.All(x => x.IsSelected);
    }
}

public partial class ScheduledPerson : ObservableObject
{
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
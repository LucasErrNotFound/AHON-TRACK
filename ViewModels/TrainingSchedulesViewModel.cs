using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using AHON_TRACK.Components.ViewModels;
using Avalonia.Collections;
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
    private DataGridCollectionView _scheduledClients;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly AddTrainingScheduleDialogCardViewModel _addTrainingScheduleDialogCardViewModel;

    public TrainingSchedulesViewModel(PageManager pageManager, DialogManager dialogManager, ToastManager toastManager, 
        AddTrainingScheduleDialogCardViewModel addTrainingScheduleDialogCardViewModel)
    {
        _dialogManager = dialogManager;
        _pageManager = pageManager;
        _toastManager = toastManager;
        _addTrainingScheduleDialogCardViewModel = addTrainingScheduleDialogCardViewModel;
        
        LoadSampleData();
    }

    public TrainingSchedulesViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _addTrainingScheduleDialogCardViewModel = new AddTrainingScheduleDialogCardViewModel();
        
        LoadSampleData();
    }
    
    [AvaloniaHotReload]
    public void Initialize()
    {
    }

    private void LoadSampleData()
    {
        var scheduledClients = CreateSampleData();
        var sampleConvertedToCollection = new DataGridCollectionView(scheduledClients);
        ScheduledClients = sampleConvertedToCollection;
    }

    private List<ScheduledPerson> CreateSampleData()
    {
        return
        [
            new ScheduledPerson
            {
                ID = 1001, Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "Rome", 
                LastName = "Calubayan", ContactNumber = "09182736273", PackageType = "Boxing", AssignedCoach = "Coach Jho", ScheduledDate = DateTime.Now, 
                ScheduledTimeStart = DateTime.Today.AddHours(9), ScheduledTimeEnd = DateTime.Today.AddHours(12), Attendance = "Present"
            },
            new ScheduledPerson
            {
                ID = 1002, Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "Sianrey", 
                LastName = "Flora", ContactNumber = "09198656372", PackageType = "Boxing", AssignedCoach = "Coach Jho", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(8), ScheduledTimeEnd = DateTime.Today.AddHours(11), Attendance = "Present"
            },
            new ScheduledPerson
            {
                ID = 1003, Picture = "", FirstName = "Mardie", 
                LastName = "Dela Cruz", ContactNumber = "09138545322", PackageType = "Muay Thai", AssignedCoach = "Coach Jedd", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(14), ScheduledTimeEnd = DateTime.Today.AddHours(16),  Attendance = "Absent"
            },
            new ScheduledPerson
            {
                ID = 1004, Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "JL", 
                LastName = "Taberdo", ContactNumber = "09237645212", PackageType = "Crossfit", AssignedCoach = "Coach Rey", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(14), ScheduledTimeEnd = DateTime.Today.AddHours(16), Attendance = "Present"
            },
            new ScheduledPerson
            {
                ID = 1005, Picture = "", FirstName = "Jav", 
                LastName = "Agustin", ContactNumber = "09686643211", PackageType = "Muay Thai", AssignedCoach = "Coach Jedd", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(14), ScheduledTimeEnd = DateTime.Today.AddHours(16), Attendance = "Absent"
            },
            new ScheduledPerson
            {
                ID = 1006, Picture = "", FirstName = "Dave", 
                LastName = "Dapitillo", ContactNumber = "09676544212", PackageType = "Muay Thai", AssignedCoach = "Coach Jedd", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(17), ScheduledTimeEnd = DateTime.Today.AddHours(18), Attendance = "Absent"
            },
            new ScheduledPerson
            {
                ID = 1007, Picture = "", FirstName = "Daniel", 
                LastName = "Empinado", ContactNumber = "09666452211", PackageType = "Crossfit", AssignedCoach = "Coach Rey", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(17), ScheduledTimeEnd = DateTime.Today.AddHours(18), Attendance = "Absent"
            },
            new ScheduledPerson
            {
                ID = 1008, Picture = "", FirstName = "Marc", 
                LastName = "Torres", ContactNumber = "098273647382", PackageType = "Crossfit", AssignedCoach = "Coach Rey", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(17), ScheduledTimeEnd = DateTime.Today.AddHours(18), Attendance = "Absent"
            },
            new ScheduledPerson
            {
                ID = 1009, Picture = "", FirstName = "Mark", 
                LastName = "Dela Cruz", ContactNumber = "091827362837", PackageType = "Crossfit", AssignedCoach = "Coach Rey", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(19), ScheduledTimeEnd = DateTime.Today.AddHours(20),  Attendance = "Present"
            },
            new ScheduledPerson
            {
                ID = 1010, Picture = "", FirstName = "Adriel", 
                LastName = "Del Rosario", ContactNumber = "09182837748", PackageType = "Boxing", AssignedCoach = "Coach Jho", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(19), ScheduledTimeEnd = DateTime.Today.AddHours(20), Attendance = "Present"
            },
            new ScheduledPerson
            {
                ID = 1011, Picture = "", FirstName = "JC", 
                LastName = "Casidor", ContactNumber = "09192818827", PackageType = "Boxing", AssignedCoach = "Coach Jho", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(21), ScheduledTimeEnd = DateTime.Today.AddHours(22), Attendance = "Absent"
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
}

public partial class ScheduledPerson : ObservableObject
{
    [ObservableProperty] 
    private int _iD;
    
    [ObservableProperty]
    private string? _picture = string.Empty;
    
    [ObservableProperty]
    private string _firstName = string.Empty;
    
    [ObservableProperty]
    private string _lastName = string.Empty;
    
    [ObservableProperty]
    private string _contactNumber = string.Empty;
    
    [ObservableProperty]
    private string _packageType = string.Empty;
    
    [ObservableProperty]
    private string _assignedCoach = string.Empty;
    
    [ObservableProperty]
    private string _attendance = string.Empty;

    [ObservableProperty] 
    private DateTime? _scheduledTimeStart;

    [ObservableProperty] 
    private DateTime? _scheduledTimeEnd;

    [ObservableProperty] 
    private DateTime _scheduledDate;
    
    // public string DateFormatted => ScheduledTime?.ToString("h:mm tt") ?? string.Empty;
    
    public string ScheduledTimeRangeFormatted => ScheduledTimeStart.HasValue && ScheduledTimeEnd.HasValue ? 
        $"{ScheduledTimeStart.Value:h:mm tt} - {ScheduledTimeEnd.Value:h:mm tt}" 
        : string.Empty;
    
    public string PicturePath => string.IsNullOrEmpty(Picture) || Picture == "null"
        ? "avares://AHON_TRACK/Assets/MainWindowView/user.png"
        : Picture;
    
    public IBrush AttendanceForeground => Attendance.ToLowerInvariant() switch
    {
        "present" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),  // Green-500
        "absent" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),  // Red-500
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))        // Default Gray-500
    };

    public IBrush AttendanceBackground => Attendance.ToLowerInvariant() switch
    {
        "present" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)), // Green-500 with alpha
        "absent" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),  // Red-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))        // Default Gray-500 with alpha
    };

    public string AttendanceDisplayText => Attendance.ToLowerInvariant() switch
    {
        "present" => "● Present",
        "absent" => "● Absent",
        _ => Attendance 
    };

    partial void OnAttendanceChanged(string value)
    {
        OnPropertyChanged(nameof(AttendanceForeground));
        OnPropertyChanged(nameof(AttendanceBackground));
        OnPropertyChanged(nameof(AttendanceDisplayText));
    }
}
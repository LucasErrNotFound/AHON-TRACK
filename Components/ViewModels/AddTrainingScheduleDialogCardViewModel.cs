using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AHON_TRACK.ViewModels;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using AHON_TRACK.Validators;

namespace AHON_TRACK.Components.ViewModels;

public sealed partial class AddTrainingScheduleDialogCardViewModel : ViewModelBase, INavigable
{
    [ObservableProperty]
    private string[] _coachItems = ["Coach Jho", "Coach Rey", "Coach Jedd"];

    private TimeOnly? _startTime;
    private TimeOnly? _endTime;
    private DateTime? _selectedTrainingDate;
    private string _selectedCoachItem = string.Empty;
    
    [ObservableProperty] 
    private DataGridCollectionView _traineeList;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    
    [Required(ErrorMessage = "Select the coach")]
    public string SelectedCoachItem 
    {
        get => _selectedCoachItem;
        set => SetProperty(ref _selectedCoachItem, value, true);
    }
    
    [TodayValidation]
    [Required(ErrorMessage = "A training date is required.")]
    public DateTime? SelectedTrainingDate
    {
        get => _selectedTrainingDate;
        set => SetProperty(ref _selectedTrainingDate, value, true);
    }
    
    [Required(ErrorMessage = "Start time is required.")]
    [StartTimeValidation(nameof(EndTime), ErrorMessage = "Start time must be less than end time")]
    public TimeOnly? StartTime
    {
        get => _startTime;
        set
        {
            SetProperty(ref _startTime, value, true);
            ValidateProperty(EndTime, nameof(EndTime));
        }
    }
    
    [Required(ErrorMessage = "End time is required.")]
    [EndTimeValidation(nameof(StartTime), ErrorMessage = "End time must be greater than start time")]
    public TimeOnly? EndTime
    {
        get => _endTime;
        set
        {
            SetProperty(ref _endTime, value, true);
            ValidateProperty(StartTime, nameof(StartTime));
        }
    }
    
    public AddTrainingScheduleDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        
        LoadSampleData();
    }

    public AddTrainingScheduleDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        
        LoadSampleData();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        ClearAllFields();
    }

    private void LoadSampleData()
    {
        var trainees = CreateSampleData();
        var sampleConvertedCollection = new DataGridCollectionView(trainees);
        TraineeList = sampleConvertedCollection;
    }

    private List<Trainees> CreateSampleData()
    {
        return 
        [
            new Trainees
            {
                ID = 1001, Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "Rome", 
                LastName = "Calubayan", ContactNumber = "09182736273", PackageType = "Boxing", SessionLeft = 1
            },
            new Trainees
            {
                ID = 1002, Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "Sianrey", 
                LastName = "Flora", ContactNumber = "09198656372", PackageType = "Boxing", SessionLeft = 1
            },
            new Trainees
            {
                ID = 1003, Picture = "", FirstName = "Mardie", 
                LastName = "Dela Cruz", ContactNumber = "09138545322", PackageType = "Muay Thai", SessionLeft = 2
            },
            new Trainees
            {
                ID = 1004, Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "JL", 
                LastName = "Taberdo", ContactNumber = "09237645212", PackageType = "Crossfit", SessionLeft = 4
            },
            new Trainees
            {
                ID = 1005, Picture = "", FirstName = "Jav", 
                LastName = "Agustin", ContactNumber = "09686643211", PackageType = "Muay Thai", SessionLeft = 1
            },
            new Trainees
            {
                ID = 1006, Picture = "", FirstName = "Dave", 
                LastName = "Dapitillo", ContactNumber = "09676544212", PackageType = "Muay Thai", SessionLeft = 1
            },
            new Trainees
            {
                ID = 1007, Picture = "", FirstName = "Daniel", 
                LastName = "Empinado", ContactNumber = "09666452211", PackageType = "Crossfit", SessionLeft = 2
            },
            new Trainees
            {
                ID = 1008, Picture = "", FirstName = "Marc", 
                LastName = "Torres", ContactNumber = "098273647382", PackageType = "Crossfit", SessionLeft = 5
            },
            new Trainees
            {
                ID = 1009, Picture = "", FirstName = "Mark", 
                LastName = "Dela Cruz", ContactNumber = "091827362837", PackageType = "Crossfit", SessionLeft = 7
            },
            new Trainees
            {
                ID = 1010, Picture = "", FirstName = "Adriel", 
                LastName = "Del Rosario", ContactNumber = "09182837748", PackageType = "Boxing", SessionLeft = 1
            },
            new Trainees
            {
                ID = 1011, Picture = "", FirstName = "JC", 
                LastName = "Casidor", ContactNumber = "09192818827", PackageType = "Boxing", SessionLeft = 3
            }
        ];
    }
    
    [RelayCommand]
    private void Submit()
    {
        ClearAllErrors();
        ValidateAllProperties();
        ValidateProperty(StartTime, nameof(StartTime));
        ValidateProperty(EndTime, nameof(EndTime));
        if (HasErrors) return;
        
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void Cancel()
    {
        ClearAllErrors();
        _dialogManager.Close(this);
    }
    
    private void ClearAllFields()
    {
        SelectedCoachItem = string.Empty;
        StartTime = null;
        EndTime = null;
        SelectedTrainingDate = null;
        ClearAllErrors();
    }
        
}

class Trainees
{
    public int ID { get; set; }
    public string? Picture { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string PackageType { get; set; } = string.Empty;
    public int SessionLeft { get; set; }

    public string PicturePath => string.IsNullOrEmpty(Picture) || Picture == "null"
        ? "avares://AHON_TRACK/Assets/MainWindowView/user.png"
        : Picture;
}
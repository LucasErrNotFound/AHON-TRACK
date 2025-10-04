using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public partial class ChangeScheduleDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty] 
    private string[] _coachFilterItems = ["Coach Jho", "Coach Rey", "Coach Jedd"];
    
    private TimeOnly? _startTime;
    private TimeOnly? _endTime;
    private DateTime? _selectedTrainingDate;
    private string? _selectedCoach;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public ChangeScheduleDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public ChangeScheduleDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }
    
    [Required(ErrorMessage = "A training date is required.")]
    public DateTime? SelectedTrainingDate
    {
        get => _selectedTrainingDate;
        set => SetProperty(ref _selectedTrainingDate, value, true);
    }
    
    public string? SelectedCoach
    {
        get => _selectedCoach;
        set => SetProperty(ref _selectedCoach, value, true);
    }

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

    [AvaloniaHotReload]
    public void Initialize(ScheduledPerson? scheduledPerson)
    {
        ClearAllErrors();

        SelectedTrainingDate = scheduledPerson?.ScheduledDate;
        SelectedCoach = scheduledPerson?.AssignedCoach;
        StartTime = scheduledPerson?.ScheduledTimeStart;
        EndTime = scheduledPerson?.ScheduledTimeEnd;
    }

    [RelayCommand]
    private void Save()
    {
        ClearAllErrors();
        ValidateAllProperties();
        ValidateProperty(StartTime, nameof(StartTime));
        ValidateProperty(EndTime, nameof(EndTime));
        
        if (HasErrors) return;
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }
    
    [RelayCommand]
    private void Discard()
    {
        ClearAllErrors();
        _dialogManager.Close(this);
    }
    
    private void ClearAllFields()
    {
        StartTime = null;
        EndTime = null;
        SelectedTrainingDate = null;
        ClearAllErrors();
    }
}
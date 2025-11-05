using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.ViewModels;

public partial class ChangeScheduleDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _coachFilterItems = ["None"];

    private TimeOnly? _startTime;
    private TimeOnly? _endTime;
    private DateTime? _selectedTrainingDate;
    private string? _selectedCoach;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly ITrainingService _trainingService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingCoaches;

    [ObservableProperty]
    private int? _trainingID;

    private Dictionary<string, int> _coachNameToIdMap = new();

    public ChangeScheduleDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, ITrainingService trainingService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _trainingService = trainingService;
    }

    public ChangeScheduleDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _trainingService = null!;
    }

    [TodayValidation]
    [Required(ErrorMessage = "A training date is required.")]
    public DateTime? SelectedTrainingDate
    {
        get => _selectedTrainingDate;
        set => SetProperty(ref _selectedTrainingDate, value, true);
    }

    [Required(ErrorMessage = "Select the coach")]
    public string? SelectedCoach
    {
        get => _selectedCoach;
        set => SetProperty(ref _selectedCoach, value, true);
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

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        ClearAllFields();
        await LoadCoachesAsync();
    }

    public async Task Initialize(ScheduledPerson scheduledPerson)
    {
        TrainingID = scheduledPerson.TrainingID;
        SelectedTrainingDate = scheduledPerson.ScheduledDate;

        // Load coaches first
        await LoadCoachesAsync();

        // Then set the selected coach
        SelectedCoach = scheduledPerson.AssignedCoach;

        StartTime = scheduledPerson.ScheduledTimeStart;
        EndTime = scheduledPerson.ScheduledTimeEnd;
    }

    private async Task LoadCoachesAsync()
    {
        if (_trainingService == null) return;

        IsLoadingCoaches = true;

        try
        {
            var coaches = await _trainingService.GetCoachNamesAsync();

            _coachNameToIdMap.Clear();

            var coachNames = coaches
                .Where(c => !string.IsNullOrEmpty(c.FullName))
                .OrderBy(c => c.FullName)
                .ToList();

            foreach (var coach in coachNames)
            {
                _coachNameToIdMap[coach.FullName] = coach.CoachID;
            }

            var names = coachNames.Select(c => c.FullName).ToList();
            names.Insert(0, "None");

            CoachFilterItems = names.ToArray();

            if (string.IsNullOrEmpty(SelectedCoach))
            {
                SelectedCoach = "None";
            }
        }
        catch (Exception ex)
        {
            CoachFilterItems = ["None"];
            SelectedCoach = "None";

            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to load coaches: {ex.Message}")
                .ShowError();
        }
        finally
        {
            IsLoadingCoaches = false;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        ClearAllErrors();
        ValidateAllProperties();
        ValidateProperty(StartTime, nameof(StartTime));
        ValidateProperty(EndTime, nameof(EndTime));

        if (HasErrors || _trainingService == null || !TrainingID.HasValue) return;

        try
        {
            IsLoading = true;

            var training = await _trainingService.GetTrainingScheduleByIdAsync(TrainingID.Value);
            if (training == null)
            {
                _toastManager.CreateToast("Error")
                    .WithContent("Training schedule not found")
                    .ShowError();
                return;
            }

            training.scheduledDate = SelectedTrainingDate!.Value.Date;
            training.scheduledTimeStart = SelectedTrainingDate.Value.Date.Add(StartTime!.Value.ToTimeSpan());
            training.scheduledTimeEnd = SelectedTrainingDate.Value.Date.Add(EndTime!.Value.ToTimeSpan());
            training.assignedCoach = SelectedCoach;

            // Get the coach ID if a coach is selected
            if (!string.IsNullOrEmpty(SelectedCoach) && SelectedCoach != "None")
            {
                if (_coachNameToIdMap.TryGetValue(SelectedCoach, out int coachId))
                {
                    training.coachID = coachId;
                }
            }
            else
            {
                training.coachID = 0; // or null if your model supports it
            }

            var success = await _trainingService.UpdateTrainingScheduleAsync(training);

            if (success)
            {
                _dialogManager.Close(this, new CloseDialogOptions { Success = true });
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error")
                .WithContent($"Failed to update training schedule: {ex.Message}")
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
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
        SelectedCoach = null;
        ClearAllErrors();
    }
    
    protected override void DisposeManagedResources()
    {
        // Clear fields that may hold data
        StartTime = null;
        EndTime = null;
        SelectedTrainingDate = null;
        SelectedCoach = null;

        // Clear coach map
        _coachNameToIdMap?.Clear();

        base.DisposeManagedResources();
    }
}
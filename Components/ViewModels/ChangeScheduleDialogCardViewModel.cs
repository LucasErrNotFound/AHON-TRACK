using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.ViewModels;

public partial class ChangeScheduleDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    private TimeOnly? _startTime;
    private TimeOnly? _endTime;
    private DateTime? _selectedTrainingDate;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly ITrainingService _trainingService;

    [ObservableProperty]
    private bool _isLoading;
    [ObservableProperty]
    private int? _trainingID;

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

    [AvaloniaHotReload]
    public void Initialize()
    {
        ClearAllFields();
    }

    public void Initialize(ScheduledPerson scheduledPerson)
    {
        TrainingID = scheduledPerson.TrainingID;
        SelectedTrainingDate = scheduledPerson.ScheduledDate;

        if (scheduledPerson.ScheduledTimeStart.HasValue)
            StartTime = TimeOnly.FromDateTime(scheduledPerson.ScheduledTimeStart.Value);

        if (scheduledPerson.ScheduledTimeEnd.HasValue)
            EndTime = TimeOnly.FromDateTime(scheduledPerson.ScheduledTimeEnd.Value);
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
        ClearAllErrors();
    }
}
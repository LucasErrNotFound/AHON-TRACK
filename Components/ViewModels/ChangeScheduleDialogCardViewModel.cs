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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AHON_TRACK.Components.ViewModels;

public partial class ChangeScheduleDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _coachFilterItems = ["None"];
    
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingCoaches;
    [ObservableProperty] private int? _trainingID;
    [ObservableProperty] private bool _isInitialized;

    private Dictionary<string, int> _coachNameToIdMap = new();
    private TimeOnly? _startTime;
    private TimeOnly? _endTime;
    private DateTime? _selectedTrainingDate;
    private string? _selectedCoach;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly ITrainingService _trainingService;
    private readonly ILogger _logger;

    public ChangeScheduleDialogCardViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager, 
        ITrainingService trainingService,
        ILogger logger)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _trainingService = trainingService;
        _logger = logger;
    }

    public ChangeScheduleDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _trainingService = null!;
        _logger = null!;
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

    /*
    [AvaloniaHotReload]
    public async Task Initialize()
    {
        ClearAllFields();
        await LoadCoachesAsync();
    }
    */
    
    [AvaloniaHotReload]
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
    
        if (IsInitialized)
        {
            _logger?.LogDebug("ChangeScheduleDialogCardViewModel already initialized");
            return;
        }

        _logger?.LogInformation("Initializing ChangeScheduleDialogCardViewModel");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            ClearAllFields();
            await LoadCoachesAsync(linkedCts.Token).ConfigureAwait(false);
        
            IsInitialized = true;
            _logger?.LogInformation("ChangeScheduleDialogCardViewModel initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("ChangeScheduleDialogCardViewModel initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing ChangeScheduleDialogCardViewModel");
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Navigating away from ChangeScheduleDialog");
        return ValueTask.CompletedTask;
    }

    public async Task Initialize(ScheduledPerson scheduledPerson)
    {
        try
        {
            _logger?.LogInformation("Initializing with scheduled person for training {TrainingId}", 
                scheduledPerson.TrainingID);
        
            TrainingID = scheduledPerson.TrainingID;
            SelectedTrainingDate = scheduledPerson.ScheduledDate;

            // Load coaches first
            await LoadCoachesAsync(LifecycleToken).ConfigureAwait(false);

            // Then set the selected coach
            SelectedCoach = scheduledPerson.AssignedCoach;
            StartTime = scheduledPerson.ScheduledTimeStart;
            EndTime = scheduledPerson.ScheduledTimeEnd;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Initialize with scheduled person cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing with scheduled person");
        }
    }

    private async Task LoadCoachesAsync(CancellationToken cancellationToken = default)
    {
        if (_trainingService == null) return;

        IsLoadingCoaches = true;

        try
        {
            var coaches = await _trainingService.GetCoachNamesAsync()
                .ConfigureAwait(false);
        
            cancellationToken.ThrowIfCancellationRequested();

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

            _logger?.LogDebug("Loaded {Count} coaches", coachNames.Count);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("LoadCoachesAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            CoachFilterItems = ["None"];
            SelectedCoach = "None";

            _logger?.LogError(ex, "Failed to load coaches");
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
        try
        {
            LifecycleToken.ThrowIfCancellationRequested();
        
            ClearAllErrors();
            ValidateAllProperties();
            ValidateProperty(StartTime, nameof(StartTime));
            ValidateProperty(EndTime, nameof(EndTime));

            if (HasErrors)
            {
                _logger?.LogWarning("Schedule change validation failed");
                return;
            }

            if (_trainingService == null)
            {
                _logger?.LogWarning("Training service not available");
                _toastManager?.CreateToast("Service Error")
                    .WithContent("Training service is not available")
                    .ShowError();
                return;
            }

            if (!TrainingID.HasValue)
            {
                _logger?.LogWarning("Training ID is missing");
                _toastManager?.CreateToast("Error")
                    .WithContent("Training ID is missing")
                    .ShowError();
                return;
            }

            IsLoading = true;

            var training = await _trainingService.GetTrainingScheduleByIdAsync(TrainingID.Value)
                .ConfigureAwait(false);
        
            if (training == null)
            {
                _logger?.LogWarning("Training schedule {TrainingId} not found", TrainingID.Value);
                _toastManager?.CreateToast("Error")
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
                else
                {
                    _logger?.LogWarning("Coach '{Coach}' not found in map", SelectedCoach);
                }
            }
            else
            {
                training.coachID = 0;
            }

            var success = await _trainingService.UpdateTrainingScheduleAsync(training)
                .ConfigureAwait(false);

            if (success)
            {
                _logger?.LogInformation("Training schedule {TrainingId} updated successfully", TrainingID.Value);
                _dialogManager.Close(this, new CloseDialogOptions { Success = true });
            }
            else
            {
                _logger?.LogWarning("Failed to update training schedule {TrainingId}", TrainingID.Value);
                _toastManager?.CreateToast("Update Failed")
                    .WithContent("Failed to update training schedule")
                    .ShowError();
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Schedule change save cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating training schedule");
            _toastManager?.CreateToast("Error")
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
        try
        {
            _logger?.LogDebug("Schedule change discarded");
            ClearAllErrors();
            _dialogManager.Close(this);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during discard");
        }
    }

    private void ClearAllFields()
    {
        StartTime = null;
        EndTime = null;
        SelectedTrainingDate = null;
        SelectedCoach = null;
        ClearAllErrors();
    }
    
    protected override async ValueTask DisposeAsyncCore()
    {
        _logger?.LogInformation("Disposing ChangeScheduleDialogCardViewModel");

        // Clear coach name map
        _coachNameToIdMap.Clear();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}
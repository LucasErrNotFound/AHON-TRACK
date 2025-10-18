using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.ViewModels;

public sealed partial class AddTrainingScheduleDialogCardViewModel : ViewModelBase, INavigable
{
    [ObservableProperty]
    private string[] _coachItems = ["Coach Jho", "Coach Rey", "Coach Jedd"];

    [ObservableProperty]
    private ObservableCollection<Trainees> _allTrainees = [];

    [ObservableProperty]
    private ObservableCollection<Trainees> _filteredTrainees = [];

    [ObservableProperty]
    private ObservableCollection<string> _traineesSuggestions = [];

    [ObservableProperty]
    private string _searchTraineeText = string.Empty;

    [ObservableProperty]
    private Trainees? _selectedTrainee;

    [ObservableProperty]
    private bool _isSearchingTrainee;

    private TimeOnly? _startTime;
    private TimeOnly? _endTime;
    private DateTime? _selectedTrainingDate;
    private string _selectedCoachItem = string.Empty;

    [ObservableProperty]
    private DataGridCollectionView _traineeList;

    [ObservableProperty]
    private bool _isLoading;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly ITrainingService _trainingService;

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

    public AddTrainingScheduleDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, ITrainingService trainingService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _trainingService = trainingService;

        _ = LoadTraineesFromDatabaseAsync();
        UpdateSuggestions();
    }

    public AddTrainingScheduleDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _trainingService = null!;

        _ = LoadTraineesFromDatabaseAsync();
        UpdateSuggestions();
    }

    [AvaloniaHotReload]
    public async void Initialize()
    {
        ClearAllFields();
        ClearSearch();

        await LoadTraineesFromDatabaseAsync();
        UpdateSuggestions();
    }

    private List<Trainees> CreateSampleData()
    {
        return
        [
            new Trainees
        {
            ID = 1001,
            CustomerType = "Member",  // Added
            Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png",
            FirstName = "Rome",
            LastName = "Calubayan",
            ContactNumber = "09182736273",
            PackageID = 1,  // Added
            PackageType = "Boxing",
            SessionLeft = 1
        },
        new Trainees
        {
            ID = 1002,
            CustomerType = "Member",  // Added
            Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png",
            FirstName = "Sianrey",
            LastName = "Flora",
            ContactNumber = "09198656372",
            PackageID = 1,  // Added
            PackageType = "Boxing",
            SessionLeft = 1
        },
        new Trainees
        {
            ID = 1003,
            CustomerType = "Member",  // Added
            Picture = "",
            FirstName = "Mardie",
            LastName = "Dela Cruz",
            ContactNumber = "09138545322",
            PackageID = 2,  // Added
            PackageType = "Muay Thai",
            SessionLeft = 2
        },
        new Trainees
        {
            ID = 1004,
            CustomerType = "Member",  // Added
            Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png",
            FirstName = "JL",
            LastName = "Taberdo",
            ContactNumber = "09237645212",
            PackageID = 3,  // Added
            PackageType = "Crossfit",
            SessionLeft = 4
        },
        new Trainees
        {
            ID = 1005,
            CustomerType = "WalkIn",  // Added - example walk-in
            Picture = "",
            FirstName = "Jav",
            LastName = "Agustin",
            ContactNumber = "09686643211",
            PackageID = 2,  // Added
            PackageType = "Muay Thai",
            SessionLeft = 1
        },
        new Trainees
        {
            ID = 1006,
            CustomerType = "Member",  // Added
            Picture = "",
            FirstName = "Dave",
            LastName = "Dapitillo",
            ContactNumber = "09676544212",
            PackageID = 2,  // Added
            PackageType = "Muay Thai",
            SessionLeft = 1
        },
        new Trainees
        {
            ID = 1007,
            CustomerType = "Member",  // Added
            Picture = "",
            FirstName = "Daniel",
            LastName = "Empinado",
            ContactNumber = "09666452211",
            PackageID = 3,  // Added
            PackageType = "Crossfit",
            SessionLeft = 2
        },
        new Trainees
        {
            ID = 1008,
            CustomerType = "WalkIn",  // Added - example walk-in
            Picture = "",
            FirstName = "Marc",
            LastName = "Torres",
            ContactNumber = "098273647382",
            PackageID = 3,  // Added
            PackageType = "Crossfit",
            SessionLeft = 5
        },
        new Trainees
        {
            ID = 1009,
            CustomerType = "Member",  // Added
            Picture = "",
            FirstName = "Mark",
            LastName = "Dela Cruz",
            ContactNumber = "091827362837",
            PackageID = 3,  // Added
            PackageType = "Crossfit",
            SessionLeft = 7
        },
        new Trainees
        {
            ID = 1010,
            CustomerType = "Member",  // Added
            Picture = "",
            FirstName = "Adriel",
            LastName = "Del Rosario",
            ContactNumber = "09182837748",
            PackageID = 1,  // Added
            PackageType = "Boxing",
            SessionLeft = 1
        },
        new Trainees
        {
            ID = 1011,
            CustomerType = "Member",  // Added
            Picture = "",
            FirstName = "JC",
            LastName = "Casidor",
            ContactNumber = "09192818827",
            PackageID = 1,  // Added
            PackageType = "Boxing",
            SessionLeft = 3
        }
        ];
    }

    private async Task LoadTraineesFromDatabaseAsync()
    {
        if (_trainingService == null)
        {
            LoadTraineeData();
            return;
        }

        try
        {
            IsLoading = true;
            var trainees = await _trainingService.GetAvailableTraineesAsync();

            AllTrainees.Clear();
            FilteredTrainees.Clear();

            foreach (var trainee in trainees)
            {
                var traineeItem = new Trainees
                {
                    ID = trainee.ID,
                    CustomerType = trainee.CustomerType,  // Added
                    Picture = trainee.Picture,
                    FirstName = trainee.FirstName,
                    LastName = trainee.LastName,
                    ContactNumber = trainee.ContactNumber,
                    PackageID = trainee.PackageID,  // Added
                    PackageType = trainee.PackageType,  // This is the package name
                    SessionLeft = trainee.SessionLeft
                };

                AllTrainees.Add(traineeItem);
                FilteredTrainees.Add(traineeItem);
            }

            TraineeList = new DataGridCollectionView(FilteredTrainees);
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to load trainees: {ex.Message}")
                .ShowError();
            LoadTraineeData(); // Fallback to sample data
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadTraineeData()
    {
        var trainees = CreateSampleData();

        AllTrainees.Clear();
        FilteredTrainees.Clear();

        foreach (var trainee in trainees)
        {
            AllTrainees.Add(trainee);
            FilteredTrainees.Add(trainee);
        }

        TraineeList = new DataGridCollectionView(FilteredTrainees);
    }

    [RelayCommand]
    private async Task SearchTrainees()
    {
        if (string.IsNullOrWhiteSpace(SearchTraineeText))
        {
            // Reset to show all trainees
            FilteredTrainees.Clear();
            foreach (var trainee in AllTrainees)
            {
                FilteredTrainees.Add(trainee);
            }
            SelectedTrainee = null;
            return;
        }

        IsSearchingTrainee = true;

        try
        {
            await Task.Delay(200);
            var searchTerm = SearchTraineeText.ToLowerInvariant();
            var filteredResults = AllTrainees.Where(trainee =>
                trainee.FirstName.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                trainee.LastName.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                $"{trainee.FirstName} {trainee.LastName}".Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                trainee.ID.ToString().Contains(searchTerm) ||
                trainee.PackageType.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                trainee.ContactNumber.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase)
            ).ToList();

            FilteredTrainees.Clear();
            foreach (var trainee in filteredResults)
            {
                FilteredTrainees.Add(trainee);
            }

            var exactMatch = filteredResults.FirstOrDefault(m =>
                $"{m.FirstName} {m.LastName}".Equals(SearchTraineeText, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                SelectedTrainee = exactMatch;
            }
        }
        finally
        {
            IsSearchingTrainee = false;
        }
    }

    private void UpdateSuggestions()
    {
        var suggestions = AllTrainees
            .Select(m => $"{m.FirstName} {m.LastName}")
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        TraineesSuggestions.Clear();
        foreach (var suggestion in suggestions)
        {
            TraineesSuggestions.Add(suggestion);
        }
    }

    partial void OnSearchTraineeTextChanged(string value)
    {
        SearchTraineesCommand.Execute(null);
    }

    partial void OnSelectedTraineeChanged(Trainees? value)
    {
        if (value == null) return;
        // Update the search text to match the selected trainee
        SearchTraineeText = $"{value.FirstName} {value.LastName}";

        // Show only the selected trainee in the grid
        FilteredTrainees.Clear();
        FilteredTrainees.Add(value);
    }

    [RelayCommand]
    private void SelectTrainee(Trainees trainee)
    {
        SelectedTrainee = trainee;
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchTraineeText = string.Empty;
        SelectedTrainee = null;
        FilteredTrainees.Clear();
        foreach (var trainee in AllTrainees)
        {
            FilteredTrainees.Add(trainee);
        }
    }

    public Trainees? LastSelectedTrainee { get; private set; }

    [RelayCommand]
    private async Task Submit()
    {
        ClearAllErrors();
        ValidateAllProperties();
        ValidateProperty(StartTime, nameof(StartTime));
        ValidateProperty(EndTime, nameof(EndTime));

        if (HasErrors || _trainingService == null) return;

        try
        {
            IsLoading = true;

            var selectedTrainees = FilteredTrainees
                .Where(t => t.IsSelected)
                .ToList();

            if (!selectedTrainees.Any())
            {
                _toastManager.CreateToast("No Trainees Selected")
                    .WithContent("Please select at least one trainee")
                    .ShowWarning();
                return;
            }

            var scheduledDate = SelectedTrainingDate!.Value.Date;
            var startDateTime = scheduledDate.Add(StartTime!.Value.ToTimeSpan());
            var endDateTime = scheduledDate.Add(EndTime!.Value.ToTimeSpan());

            foreach (var trainee in selectedTrainees)
            {
                var training = new TrainingModel
                {
                    customerID = trainee.ID,
                    customerType = trainee.CustomerType,  // "Member" or "WalkIn"
                    firstName = trainee.FirstName,
                    lastName = trainee.LastName,
                    contactNumber = trainee.ContactNumber,
                    picture = trainee.Picture,
                    packageID = trainee.PackageID,
                    packageType = trainee.PackageType,  // Package name (e.g., "Boxing")
                    assignedCoach = SelectedCoachItem,
                    scheduledDate = scheduledDate,
                    scheduledTimeStart = startDateTime,
                    scheduledTimeEnd = endDateTime,
                    attendance = "Pending"
                };

                await _trainingService.AddTrainingScheduleAsync(training);
            }

            LastSelectedTrainee = selectedTrainees.LastOrDefault();
            ClearSearch();
            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error")
                .WithContent($"Failed to add training schedule: {ex.Message}")
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        ClearSearch();
        ClearAllErrors();
        _dialogManager.Close(this);
    }

    private void ClearAllFields()
    {
        SelectedCoachItem = string.Empty;
        StartTime = null;
        EndTime = null;
        SelectedTrainingDate = null;

        SearchTraineeText = string.Empty;
        SelectedTrainee = null;
        ClearAllErrors();
    }
}

public partial class Trainees : ObservableObject
{
    [ObservableProperty]
    private int _iD;

    [ObservableProperty]
    private string _customerType = string.Empty;  // Added: "Member" or "WalkIn"

    [ObservableProperty]
    private string? _picture = string.Empty;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string _contactNumber = string.Empty;

    [ObservableProperty]
    private int _packageID;  // Added: Package ID from database

    [ObservableProperty]
    private string _packageType = string.Empty;  // Package name (e.g., "Boxing", "Muay Thai", "CrossFit")

    [ObservableProperty]
    private int _sessionLeft;

    [ObservableProperty]
    private bool _isSelected;

    public string PicturePath => string.IsNullOrEmpty(Picture) || Picture == "null"
        ? "avares://AHON_TRACK/Assets/MainWindowView/user.png"
        : Picture;
}
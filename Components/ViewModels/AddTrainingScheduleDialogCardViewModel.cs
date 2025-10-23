using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
using Avalonia.Collections;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.ViewModels;

public sealed partial class AddTrainingScheduleDialogCardViewModel : ViewModelBase, INavigable
{
    [ObservableProperty]
    private string[] _coachItems = ["None"];

    private string? _selectedCoachItems = "None";

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

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingCoaches;

    private Dictionary<string, int> _coachNameToIdMap = new();
    private bool _coachesLoaded = false;


    private TimeOnly? _startTime;
    private TimeOnly? _endTime;
    private DateTime? _selectedTrainingDate;

    [ObservableProperty]
    private DataGridCollectionView _traineeList;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly ITrainingService _trainingService;

    [Required(ErrorMessage = "Select the coach")]
    public string SelectedCoachItems
    {
        get => _selectedCoachItems;
        set => SetProperty(ref _selectedCoachItems, value, true);
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

    public AddTrainingScheduleDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager,
        PageManager pageManager, ITrainingService trainingService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _trainingService = trainingService;

        SubscribeToEvents();
        _ = LoadCoachesAsync();
        LoadTraineeData();
    }

    public AddTrainingScheduleDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _trainingService = null!;

        SubscribeToEvents();
        UpdateSuggestions();
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        ClearAllFields();
        ClearSearch();
        SubscribeToEvents();
        if (AllTrainees.Count == 0) LoadTraineeData();
        await LoadCoachesAsync();
        UpdateSuggestions();
    }

    private void SubscribeToEvents()
    {
        var eventService = DashboardEventService.Instance;

        // When members are updated (sessions left changes)
        eventService.MemberUpdated += OnTraineeDataChanged;

        // When schedules are added/updated (affects sessions left)
        eventService.ScheduleAdded += OnTraineeDataChanged;
        eventService.ScheduleUpdated += OnTraineeDataChanged;
    }

    private async void OnTraineeDataChanged(object? sender, EventArgs e)
    {
        // Reload trainee data when sessions left changes
        await LoadTraineeDataFromDatabaseAsync();
        UpdateSuggestions();
    }

    private async void LoadTraineeData()
    {
        if (_trainingService != null)
        {
            await LoadTraineeDataFromDatabaseAsync();
        }
        else
        {
            LoadTraineeDataFromSample();
        }
    }

    private async Task LoadCoachesAsync()
    {
        if (_trainingService == null) return;

        IsLoadingCoaches = true;

        try
        {
            // Assuming you have a method in ITrainingService to get coaches
            // Adjust this based on your actual service method
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

            CoachItems = names.ToArray();

            if (string.IsNullOrEmpty(SelectedCoachItems))
            {
                SelectedCoachItems = "None";
            }
        }
        catch (Exception ex)
        {
            CoachItems = ["None"];
            SelectedCoachItems = "None";

            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to load coaches: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoadingCoaches = false;
            _coachesLoaded = true;
        }
    }


    private async Task LoadTraineeDataFromDatabaseAsync()
    {
        try
        {
            IsLoading = true;

            var traineeModels = await _trainingService.GetAvailableTraineesAsync();

            AllTrainees.Clear();
            FilteredTrainees.Clear();

            // Convert TraineeModel to Trainees
            foreach (var model in traineeModels)
            {
                var trainee = new Trainees
                {
                    ID = model.ID,
                    Picture = model.Picture,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    ContactNumber = model.ContactNumber,
                    PackageType = model.PackageType,
                    SessionLeft = model.SessionLeft,
                    // Store additional data for later use
                    CustomerType = model.CustomerType,
                    PackageID = model.PackageID
                };

                AllTrainees.Add(trainee);
                FilteredTrainees.Add(trainee);
            }

            TraineeList = new DataGridCollectionView(FilteredTrainees);
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to load trainees: {ex.Message}")
                .WithDelay(5)
                .ShowError();

            // Load sample data as fallback
            LoadTraineeDataFromSample();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadTraineeDataFromSample()
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

    private List<Trainees> CreateSampleData()
    {
        return
        [
            new Trainees
            {
                ID = 1001, Picture = ImageHelper.GetDefaultAvatarSafe(), FirstName = "Rome",
                LastName = "Calubayan", ContactNumber = "09182736273", PackageType = "Boxing", SessionLeft = 1
            },
            new Trainees
            {
                ID = 1002, Picture = ImageHelper.GetDefaultAvatarSafe(), FirstName = "Sianrey",
                LastName = "Flora", ContactNumber = "09198656372", PackageType = "Boxing", SessionLeft = 1
            },
            new Trainees
            {
                ID = 1003, Picture = ImageHelper.GetDefaultAvatarSafe(), FirstName = "Mardie",
                LastName = "Dela Cruz", ContactNumber = "09138545322", PackageType = "Muay Thai", SessionLeft = 2
            },
            new Trainees
            {
                ID = 1004, Picture = ImageHelper.GetDefaultAvatarSafe(), FirstName = "JL",
                LastName = "Taberdo", ContactNumber = "09237645212", PackageType = "Crossfit", SessionLeft = 4
            },
            new Trainees
            {
                ID = 1005, Picture = ImageHelper.GetDefaultAvatarSafe(), FirstName = "Jav",
                LastName = "Agustin", ContactNumber = "09686643211", PackageType = "Muay Thai", SessionLeft = 1
            },
            new Trainees
            {
                ID = 1006, Picture = ImageHelper.GetDefaultAvatarSafe(), FirstName = "Dave",
                LastName = "Dapitillo", ContactNumber = "09676544212", PackageType = "Muay Thai", SessionLeft = 1
            },
            new Trainees
            {
                ID = 1007, Picture = ImageHelper.GetDefaultAvatarSafe(), FirstName = "Daniel",
                LastName = "Empinado", ContactNumber = "09666452211", PackageType = "Crossfit", SessionLeft = 2
            },
            new Trainees
            {
                ID = 1008, Picture = ImageHelper.GetDefaultAvatarSafe(), FirstName = "Marc",
                LastName = "Torres", ContactNumber = "098273647382", PackageType = "Crossfit", SessionLeft = 5
            },
            new Trainees
            {
                ID = 1009, Picture = ImageHelper.GetDefaultAvatarSafe(), FirstName = "Mark",
                LastName = "Dela Cruz", ContactNumber = "091827362837", PackageType = "Crossfit", SessionLeft = 7
            },
            new Trainees
            {
                ID = 1010, Picture = ImageHelper.GetDefaultAvatarSafe(), FirstName = "Adriel",
                LastName = "Del Rosario", ContactNumber = "09182837748", PackageType = "Boxing", SessionLeft = 1
            },
            new Trainees
            {
                ID = 1011, Picture = ImageHelper.GetDefaultAvatarSafe(), FirstName = "JC",
                LastName = "Casidor", ContactNumber = "09192818827", PackageType = "Boxing", SessionLeft = 3
            }
        ];
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

        if (HasErrors) return;
        if (SelectedTrainee == null)
        {
            _toastManager?.CreateToast("Validation Error")
                .WithContent("Please select a trainee")
                .ShowWarning();
            return;
        }

        // If no service available (design-time), skip DB call
        if (_trainingService == null)
        {
            LastSelectedTrainee = SelectedTrainee;
            ClearSearch();
            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
            return;
        }

        try
        {
            IsLoading = true;

            // ✅ Convert Bitmap (Avalonia) to byte[]
            byte[]? imageBytes = null;
            if (SelectedTrainee.Picture != null)
            {
                using var ms = new MemoryStream();
                SelectedTrainee.Picture.Save(ms);
                imageBytes = ms.ToArray();
            }

            // ✅ CRITICAL FIX: Ensure CustomerType is exactly 'Member' or 'WalkIn'
            string customerType = SelectedTrainee.CustomerType?.Trim();

            // Normalize the customer type to match CHECK constraint
            if (string.IsNullOrWhiteSpace(customerType))
            {
                customerType = "Member"; // Default fallback
            }
            else if (customerType.Equals("walk-in", StringComparison.OrdinalIgnoreCase))
            {
                customerType = "WalkIn"; // Ensure correct casing
            }
            else if (!customerType.Equals("Member", StringComparison.Ordinal) &&
                     !customerType.Equals("Walk-in", StringComparison.Ordinal))
            {
                customerType = "Member"; // Fallback for any other value
            }
            int? coachId = null;
            if (!string.IsNullOrEmpty(SelectedCoachItems) && SelectedCoachItems != "None")
            {
                if (_coachNameToIdMap.TryGetValue(SelectedCoachItems, out int id))
                {
                    coachId = id;
                }
            }

            // ✅ Create the model to send to the DB
            var training = new TrainingModel
            {
                customerID = SelectedTrainee.ID,
                customerType = customerType, // ✅ Now properly validated
                firstName = SelectedTrainee.FirstName,
                lastName = SelectedTrainee.LastName,
                contactNumber = SelectedTrainee.ContactNumber,
                picture = imageBytes,
                packageID = SelectedTrainee.PackageID,
                packageType = SelectedTrainee.PackageType,
                assignedCoach = SelectedCoachItems,
                coachID = coachId ?? 0,
                scheduledDate = SelectedTrainingDate!.Value.Date,
                scheduledTimeStart = SelectedTrainingDate.Value.Date.Add(StartTime!.Value.ToTimeSpan()),
                scheduledTimeEnd = SelectedTrainingDate.Value.Date.Add(EndTime!.Value.ToTimeSpan()),
                attendance = "Pending"
            };

            // ✅ Save to DB using your service
            var success = await _trainingService.AddTrainingScheduleAsync(training);

            if (success)
            {
                _toastManager?.CreateToast("Success")
                    .WithContent("Training schedule added successfully.")
                    .ShowSuccess();

                LastSelectedTrainee = SelectedTrainee;
                ClearSearch();
                _dialogManager.Close(this, new CloseDialogOptions { Success = true });
            }
            else
            {
                _toastManager?.CreateToast("Error")
                    .WithContent("Failed to add training schedule.")
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to add training schedule: {ex.Message}")
                .WithDelay(5)
                .ShowError();

            Console.WriteLine($"[Submit] Error: {ex.Message}");
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
        _dialogManager.Close(this);
    }

    private void ClearAllFields()
    {
        SelectedCoachItems = string.Empty;
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
    private Bitmap? _picture;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string _contactNumber = string.Empty;

    [ObservableProperty]
    private string _packageType = string.Empty;

    [ObservableProperty]
    private int _sessionLeft;

    // Additional properties for database operations (not displayed in UI)
    public string? CustomerType { get; set; }
    public int PackageID { get; set; }

    public Bitmap? PicturePath;
}
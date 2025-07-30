using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using AHON_TRACK.Components.ViewModels;
using Avalonia.Collections;
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

    [ObservableProperty] 
    private bool? _isChecked = false;

    [ObservableProperty] 
    private ObservableCollection<CheckBoxItem> _filterItems =
    [
        new() { IsChecked = false, Text = "Boxing" },
        new() { IsChecked = false, Text = "Muay Thai" },
        new() { IsChecked = false, Text = "Crossfit" },
    ];
    
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
        
        FilterItems.CollectionChanged += (_, _) => UpdateSelectAllState();
        foreach (var item in FilterItems) item.PropertyChanged += (_, _) => UpdateSelectAllState();
        
        LoadSampleData();
    }

    public TrainingSchedulesViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _addTrainingScheduleDialogCardViewModel = new AddTrainingScheduleDialogCardViewModel();
        
        FilterItems.CollectionChanged += (_, _) => UpdateSelectAllState();
        foreach (var item in FilterItems) item.PropertyChanged += (_, _) => UpdateSelectAllState();
        
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
                LastName = "Calubayan", ContactNumber = "09182736273", PackageType = "Boxing", ScheduledDate = DateTime.Now, 
                ScheduledTimeStart = DateTime.Today.AddHours(9), ScheduledTimeEnd = DateTime.Today.AddHours(12)
            },
            new ScheduledPerson
            {
                ID = 1002, Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "Sianrey", 
                LastName = "Flora", ContactNumber = "09198656372", PackageType = "Boxing", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(8), ScheduledTimeEnd = DateTime.Today.AddHours(11)
            },
            new ScheduledPerson
            {
                ID = 1003, Picture = "", FirstName = "Mardie", 
                LastName = "Dela Cruz", ContactNumber = "09138545322", PackageType = "Muay Thai", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(14), ScheduledTimeEnd = DateTime.Today.AddHours(16)
            },
            new ScheduledPerson
            {
                ID = 1004, Picture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "JL", 
                LastName = "Taberdo", ContactNumber = "09237645212", PackageType = "Crossfit", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(14), ScheduledTimeEnd = DateTime.Today.AddHours(16)
            },
            new ScheduledPerson
            {
                ID = 1005, Picture = "", FirstName = "Jav", 
                LastName = "Agustin", ContactNumber = "09686643211", PackageType = "Muay Thai", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(14), ScheduledTimeEnd = DateTime.Today.AddHours(16)
            },
            new ScheduledPerson
            {
                ID = 1006, Picture = "", FirstName = "Dave", 
                LastName = "Dapitillo", ContactNumber = "09676544212", PackageType = "Muay Thai", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(17), ScheduledTimeEnd = DateTime.Today.AddHours(18)
            },
            new ScheduledPerson
            {
                ID = 1007, Picture = "", FirstName = "Daniel", 
                LastName = "Empinado", ContactNumber = "09666452211", PackageType = "Crossfit", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(17), ScheduledTimeEnd = DateTime.Today.AddHours(18)
            },
            new ScheduledPerson
            {
                ID = 1008, Picture = "", FirstName = "Marc", 
                LastName = "Torres", ContactNumber = "098273647382", PackageType = "Crossfit", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(17), ScheduledTimeEnd = DateTime.Today.AddHours(18)
            },
            new ScheduledPerson
            {
                ID = 1009, Picture = "", FirstName = "Mark", 
                LastName = "Dela Cruz", ContactNumber = "091827362837", PackageType = "Crossfit", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(19), ScheduledTimeEnd = DateTime.Today.AddHours(20)
            },
            new ScheduledPerson
            {
                ID = 1010, Picture = "", FirstName = "Adriel", 
                LastName = "Del Rosario", ContactNumber = "09182837748", PackageType = "Boxing", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(19), ScheduledTimeEnd = DateTime.Today.AddHours(20)
            },
            new ScheduledPerson
            {
                ID = 1011, Picture = "", FirstName = "JC", 
                LastName = "Casidor", ContactNumber = "09192818827", PackageType = "Boxing", ScheduledDate = DateTime.Now,
                ScheduledTimeStart = DateTime.Today.AddHours(21), ScheduledTimeEnd = DateTime.Today.AddHours(22)
            }
        ];
    }
    
    partial void OnIsCheckedChanged(bool? value)
    {
        if (!value.HasValue) return;

        foreach (var item in FilterItems)
        {
            item.IsChecked = value.Value;
        }
    }

    private void UpdateSelectAllState()
    {
        IsChecked = FilterItems.All(i => i.IsChecked == true) ? true :
            FilterItems.All(i => i.IsChecked == false) ? false : null;
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

public class ScheduledPerson
{
    public int ID { get; set; }
    public string? Picture { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string PackageType { get; set; } = string.Empty;
    
    public DateTime? ScheduledTimeStart { get; set; }
    public DateTime? ScheduledTimeEnd { get; set; }
    public DateTime? ScheduledDate { get; set; }
    
    // public string DateFormatted => ScheduledTime?.ToString("h:mm tt") ?? string.Empty;
    
    public string ScheduledTimeRangeFormatted => ScheduledTimeStart.HasValue && ScheduledTimeEnd.HasValue ? 
        $"{ScheduledTimeStart.Value:h:mm tt} - {ScheduledTimeEnd.Value:h:mm tt}" 
        : string.Empty;
    
    public string PicturePath => string.IsNullOrEmpty(Picture) || Picture == "null"
        ? "avares://AHON_TRACK/Assets/MainWindowView/user.png"
        : Picture;
}

public partial class CheckBoxItem : ObservableObject
{
    [ObservableProperty]
    private bool? _isChecked;
    
    [ObservableProperty]
    private string _text = string.Empty;
}
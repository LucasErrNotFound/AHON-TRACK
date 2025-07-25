﻿using AHON_TRACK.Components.ViewModels;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

[Page("manageEmployees")]
public partial class ManageEmployeesViewModel : ViewModelBase, INavigable
{
    [ObservableProperty]
    private List<ManageEmployeesItem> _originalEmployeeData = [];

    [ObservableProperty]
    private ObservableCollection<ManageEmployeesItem> _employeeItems = [];

    [ObservableProperty]
    private List<ManageEmployeesItem> _currentFilteredData = [];

    [ObservableProperty]
    private string _searchStringResult = string.Empty;

    [ObservableProperty]
    private bool _isSearchingEmployee;

    [ObservableProperty]
    private bool _selectAll;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _showIdColumn = true;

    [ObservableProperty]
    private bool _showPictureColumn = true;

    [ObservableProperty]
    private bool _showNameColumn = true;

    [ObservableProperty]
    private bool _showUsernameColumn = true;

    [ObservableProperty]
    private bool _showContactNumberColumn = true;

    [ObservableProperty]
    private bool _showPositionColumn = true;

    [ObservableProperty]
    private bool _showStatusColumn = true;

    [ObservableProperty]
    private bool _showDateJoined = true;

    [ObservableProperty]
    private int _selectedSortIndex = -1;

    [ObservableProperty]
    private int _selectedFilterIndex = -1;

    [ObservableProperty]
    private bool _isInitialized;

    private const string DefaultAvatarSource = "avares://AHON_TRACK/Assets/MainWindowView/user.png";

    private readonly PageManager _pageManager;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly AddNewEmployeeDialogCardViewModel _addNewEmployeeDialogCardViewModel;
    private readonly EmployeeProfileInformationViewModel _employeeProfileInformationViewModel;

    public ManageEmployeesViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager, 
        PageManager pageManager, 
        AddNewEmployeeDialogCardViewModel addNewEmployeeDialogCardViewModel, EmployeeProfileInformationViewModel employeeProfileInformationViewModel)
    {
        _pageManager = pageManager;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _addNewEmployeeDialogCardViewModel = addNewEmployeeDialogCardViewModel;
        _employeeProfileInformationViewModel = employeeProfileInformationViewModel;
        LoadSampleData();
        UpdateCounts();
    }

    public ManageEmployeesViewModel()
    {
        _toastManager = new ToastManager();
        _dialogManager = new DialogManager();
        _pageManager = new PageManager(new ServiceProvider());
        _addNewEmployeeDialogCardViewModel = new AddNewEmployeeDialogCardViewModel();
        _employeeProfileInformationViewModel = new EmployeeProfileInformationViewModel();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        LoadSampleData();
        UpdateCounts();
        IsInitialized = true;
    }

    private void LoadSampleData()
    {
        var sampleEmployees = GetSampleEmployeesData();

        // Store original data for filtering/sorting operations
        OriginalEmployeeData = sampleEmployees;
        CurrentFilteredData = [.. sampleEmployees]; // Initialize current state

        EmployeeItems.Clear();
        foreach (var employee in sampleEmployees)
        {
            employee.PropertyChanged += OnEmployeePropertyChanged;
            EmployeeItems.Add(employee);
        }

        TotalCount = EmployeeItems.Count;
    }

    private List<ManageEmployeesItem> GetSampleEmployeesData()
    {
        return
        [
            new ManageEmployeesItem
            {
                ID = "1001",
                AvatarSource = DefaultAvatarSource,
                Name = "Jedd Calubayan",
                Username = "Kuya Rome",
                ContactNumber = "0975 994 3010",
                Position = "Gym Staff",
                Status = "Active",
                DateJoined = new DateTime(2025, 6, 16)
            },

            new ManageEmployeesItem
            {
                ID = "1002",
                AvatarSource = DefaultAvatarSource,
                Name = "JC Casidore",
                Username = "Jaycee",
                ContactNumber = "0989 445 0949",
                Position = "Gym Staff",
                Status = "Active",
                DateJoined = new DateTime(2025, 6, 16)
            },

            new ManageEmployeesItem
            {
                ID = "1003",
                AvatarSource = DefaultAvatarSource,
                Name = "Mardie Dela Cruz",
                Username = "Figora",
                ContactNumber = "0901 990 9921",
                Position = "Gym Staff",
                Status = "Inactive",
                DateJoined = new DateTime(2025, 6, 17)
            },

            new ManageEmployeesItem
            {
                ID = "1004",
                AvatarSource = DefaultAvatarSource,
                Name = "JL Taberdo",
                Username = "JeyEL",
                ContactNumber = "0957 889 3724",
                Position = "Gym Staff",
                Status = "Terminated",
                DateJoined = new DateTime(2025, 6, 19)
            },

            new ManageEmployeesItem
            {
                ID = "1005",
                AvatarSource = DefaultAvatarSource,
                Name = "Jav Agustin",
                Username = "Mr. Javitos",
                ContactNumber = "0923 354 4866",
                Position = "Gym Staff",
                Status = "Inactive",
                DateJoined = new DateTime(2025, 6, 21)
            },

            new ManageEmployeesItem
            {
                ID = "1006",
                AvatarSource = DefaultAvatarSource,
                Name = "Marc Torres",
                Username = "Sora",
                ContactNumber = "0913 153 4456",
                Position = "Gym Admin",
                Status = "Active",
                DateJoined = new DateTime(2025, 6, 21)
            },

            new ManageEmployeesItem
            {
                ID = "1007",
                AvatarSource = DefaultAvatarSource,
                Name = "Maverick Lim",
                Username = "Kiriya",
                ContactNumber = "0983 853 0459",
                Position = "Gym Staff",
                Status = "Terminated",
                DateJoined = new DateTime(2022, 11, 9)
            },

            new ManageEmployeesItem
            {
                ID = "1008",
                AvatarSource = DefaultAvatarSource,
                Name = "Dave Dapitillo",
                Username = "Dabii69",
                ContactNumber = "0914 145 4552",
                Position = "Gym Admin",
                Status = "Inactive",
                DateJoined = new DateTime(2023, 11, 9)
            },

            new ManageEmployeesItem
            {
                ID = "1009",
                AvatarSource = DefaultAvatarSource,
                Name = "Sianrey Flora",
                Username = "Reylifts",
                ContactNumber = "0911 115 4232",
                Position = "Gym Admin",
                Status = "Active",
                DateJoined = new DateTime(2020, 11, 29)
            },

            new ManageEmployeesItem
            {
                ID = "1010",
                AvatarSource = DefaultAvatarSource,
                Name = "Mark Dela Cruz",
                Username = "MarkyWTF",
                ContactNumber = "0931 315 1672",
                Position = "Gym Staff",
                Status = "Terminated",
                DateJoined = new DateTime(2018, 10, 1)
            }
        ];
    }

    // Method to load employees from a database (for future implementation)
    public async Task<List<ManageEmployeesItem>> GetEmployeesFromDatabaseAsync()
    {
        // Chill, hardcoded for visual only || Replace this with your actual SQL database call
        // Example:
        // using var connection = new SqlConnection(connectionString);
        // var employees = await connection.QueryAsync<ManageEmployeesItem>("SELECT * FROM Employees ORDER BY Name");
        // return employees.ToList();

        await Task.Delay(100); // Simulate async operation
        return [];
    }

    // Method to generate summary text
    /*
    public string GenerateEmployeesSummary()
    {
        var activeCount = Items.Count(x => x.Status?.ToLowerInvariant() == "active");
        var inactiveCount = Items.Count(x => x.Status?.ToLowerInvariant() == "inactive");
        var terminatedCount = Items.Count(x => x.Status?.ToLowerInvariant() == "terminated");

        return $"Total: {TotalCount} employees (Active: {activeCount}, Inactive: {inactiveCount}, Terminated: {terminatedCount})";
    }
    */

    private void OnEmployeePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManageEmployeesItem.IsSelected))
        {
            UpdateCounts();
        }
    }

    private void UpdateCounts()
    {
        SelectedCount = EmployeeItems.Count(x => x.IsSelected);
        TotalCount = EmployeeItems.Count;

        SelectAll = EmployeeItems.Count > 0 && EmployeeItems.All(x => x.IsSelected);
    }

    [RelayCommand]
    private void ShowAddNewEmployeeDialog()
    {
        _addNewEmployeeDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_addNewEmployeeDialogCardViewModel)
            .WithSuccessCallback(_ =>
                _toastManager.CreateToast("Added a new employee")
                    .WithContent($"Welcome, new employee!")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new employee cancelled")
                    .WithContent("Add a new employee to continue")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(950)
            .Show();
    }

    [RelayCommand]
    private void ShowModifyEmployeeDialog(ManageEmployeesItem? employee)
    {
        if (employee is null)
        {
            _toastManager.CreateToast("No Employee Selected")
                .WithContent("Please select an employee to modify")
                .DismissOnClick()
                .ShowError();
            return;
        }
        _addNewEmployeeDialogCardViewModel.InitializeForEditMode(employee);
        _dialogManager.CreateDialog(_addNewEmployeeDialogCardViewModel)
            .WithSuccessCallback(_ =>
                _toastManager.CreateToast("Modified Employee Details")
                    .WithContent($"You have successfully modified {employee.Name}'s details")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Modifying Employee Details Cancelled")
                    .WithContent("Click the three-dots if you want to modify your employees' details")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(950)
            .Show();
    }


    [RelayCommand]
    private void OpenViewEmployeeProfile(ManageEmployeesItem? employee)
    {
        if (employee == null)
        {
            // Write a debugger message on why employee variable is null
            _toastManager.CreateToast("Error")
                .WithContent("No employee selected to view profile.")
                .DismissOnClick()
                .ShowError();
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { "IsCurrentUser", false },
            { "EmployeeData", employee }
        };
        _pageManager.Navigate<EmployeeProfileInformationViewModel>(parameters);
    }

    [RelayCommand]
    private void SortReset()
    {
        EmployeeItems.Clear();
        foreach (var employee in OriginalEmployeeData)
        {
            employee.PropertyChanged += OnEmployeePropertyChanged;
            EmployeeItems.Add(employee);
        }

        CurrentFilteredData = [.. OriginalEmployeeData];
        UpdateCounts();

        SelectedSortIndex = -1;
        SelectedFilterIndex = -1;
    }

    [RelayCommand]
    private void SortById()
    {
        var sortedById = EmployeeItems.OrderBy(employee => employee.ID).ToList();
        EmployeeItems.Clear();

        foreach (var employees in sortedById)
        {
            EmployeeItems.Add(employees);
        }
        // Update current filtered data to match sorted state
        CurrentFilteredData = [.. sortedById];
    }

    [RelayCommand]
    private void SortNamesByAlphabetical()
    {
        var sortedNamesInAlphabetical = EmployeeItems.OrderBy(employee => employee.Name).ToList();
        EmployeeItems.Clear();

        foreach (var employees in sortedNamesInAlphabetical)
        {
            EmployeeItems.Add(employees);
        }
        CurrentFilteredData = [.. sortedNamesInAlphabetical];
    }

    [RelayCommand]
    private void SortNamesByReverseAlphabetical()
    {
        var sortedReverseNamesInAlphabetical = EmployeeItems.OrderByDescending(employee => employee.Name).ToList();
        EmployeeItems.Clear();

        foreach (var employees in sortedReverseNamesInAlphabetical)
        {
            EmployeeItems.Add(employees);
        }
        CurrentFilteredData = [.. sortedReverseNamesInAlphabetical];
    }

    [RelayCommand]
    private void SortUsernamesByAlphabetical()
    {
        var sortedUsernamesInAlphabetical = EmployeeItems.OrderBy(employee => employee.Username).ToList();
        EmployeeItems.Clear();

        foreach (var employees in sortedUsernamesInAlphabetical)
        {
            EmployeeItems.Add(employees);
        }
        CurrentFilteredData = [.. sortedUsernamesInAlphabetical];
    }

    [RelayCommand]
    private void SortUsernamesByReverseAlphabetical()
    {
        var sortedUsernamesInReverseAlphabetical = EmployeeItems.OrderByDescending(employee => employee.Username).ToList();
        EmployeeItems.Clear();

        foreach (var employees in sortedUsernamesInReverseAlphabetical)
        {
            EmployeeItems.Add(employees);
        }
        CurrentFilteredData = [.. sortedUsernamesInReverseAlphabetical];
    }

    [RelayCommand]
    private void SortDateByNewestToOldest()
    {
        var sortedDates = EmployeeItems.OrderByDescending(log => log.DateJoined).ToList();
        EmployeeItems.Clear();

        foreach (var logs in sortedDates)
        {
            EmployeeItems.Add(logs);
        }
        CurrentFilteredData = [.. sortedDates];
    }

    [RelayCommand]
    private void SortDateByOldestToNewest()
    {
        var sortedDates = EmployeeItems.OrderBy(log => log.DateJoined).ToList();
        EmployeeItems.Clear();

        foreach (var logs in sortedDates)
        {
            EmployeeItems.Add(logs);
        }
        CurrentFilteredData = [.. sortedDates];
    }

    [RelayCommand]
    private void FilterActiveStatus()
    {
        var filterActiveStatus = OriginalEmployeeData.Where(employee => employee.Status.Equals("active", StringComparison.OrdinalIgnoreCase)).ToList();
        EmployeeItems.Clear();

        foreach (var employee in filterActiveStatus)
        {
            employee.PropertyChanged += OnEmployeePropertyChanged;
            EmployeeItems.Add(employee);
        }
        CurrentFilteredData = [.. filterActiveStatus];
        UpdateCounts();
    }

    [RelayCommand]
    private void FilterInactiveStatus()
    {
        var filterInactiveStatus = OriginalEmployeeData.Where(employee => employee.Status.Equals("inactive", StringComparison.OrdinalIgnoreCase)).ToList();
        EmployeeItems.Clear();

        foreach (var employee in filterInactiveStatus)
        {
            employee.PropertyChanged += OnEmployeePropertyChanged;
            EmployeeItems.Add(employee);
        }
        CurrentFilteredData = [.. filterInactiveStatus];
        UpdateCounts();
    }

    [RelayCommand]
    private void FilterTerminatedStatus()
    {
        var filterTerminatedStatus = OriginalEmployeeData.Where(employee => employee.Status.Equals("terminated", StringComparison.OrdinalIgnoreCase)).ToList();
        EmployeeItems.Clear();

        foreach (var employee in filterTerminatedStatus)
        {
            employee.PropertyChanged += OnEmployeePropertyChanged;
            EmployeeItems.Add(employee);
        }
        CurrentFilteredData = [.. filterTerminatedStatus];
        UpdateCounts();
    }

    [RelayCommand]
    private void ToggleSelection(bool? isChecked)
    {
        var shouldSelect = isChecked ?? false;

        foreach (var item in EmployeeItems)
        {
            item.IsSelected = shouldSelect;
        }
        UpdateCounts();
    }

    [RelayCommand]
    private async Task SearchEmployees()
    {
        if (string.IsNullOrWhiteSpace(SearchStringResult))
        {
            // Reset to current filtered data instead of original data
            EmployeeItems.Clear();
            foreach (var employee in CurrentFilteredData)
            {
                employee.PropertyChanged += OnEmployeePropertyChanged;
                EmployeeItems.Add(employee);
            }
            UpdateCounts();
            return;
        }
        IsSearchingEmployee = true;

        try
        {
            await Task.Delay(500);

            // Search within the current filtered data instead of original data
            var filteredEmployees = CurrentFilteredData.Where(emp =>
                emp.ID.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                emp.Name.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                emp.Username.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                emp.ContactNumber.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                emp.Position.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                emp.Status.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                emp.DateJoined.ToString("MMMM d, yyyy").Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            EmployeeItems.Clear();
            foreach (var employees in filteredEmployees)
            {
                employees.PropertyChanged += OnEmployeePropertyChanged;
                EmployeeItems.Add(employees);
            }
            UpdateCounts();
        }
        finally
        {
            IsSearchingEmployee = false;
        }
    }

    [RelayCommand]
    private async Task ShowCopySingleEmployeeName(ManageEmployeesItem? employee)
    {
        if (employee == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(employee.Name);
        }

        _toastManager.CreateToast("Copy Employee Name")
            .WithContent($"Copied {employee.Name}'s name successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private async Task ShowCopyMultipleEmployeeName(ManageEmployeesItem? employee)
    {
        var selectedEmployees = EmployeeItems.Where(item => item.IsSelected).ToList();
        if (employee == null) return;
        if (selectedEmployees.Count == 0) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            var employeeNames = string.Join(", ", selectedEmployees.Select(emp => emp.Name));
            await clipboard.SetTextAsync(employeeNames);

            _toastManager.CreateToast("Copy Employee Names")
                .WithContent($"Copied multiple employee names successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowInfo();
        }
    }

    [RelayCommand]
    private async Task ShowCopySingleEmployeeId(ManageEmployeesItem? employee)
    {
        if (employee == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(employee.ID);
        }

        _toastManager.CreateToast("Copy Employee ID")
            .WithContent($"Copied {employee.Name}'s ID successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private async Task ShowCopyMultipleEmployeeId(ManageEmployeesItem? employee)
    {
        var selectedEmployees = EmployeeItems.Where(item => item.IsSelected).ToList();
        if (employee == null) return;
        if (selectedEmployees.Count == 0) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            var employeeIDs = string.Join(", ", selectedEmployees.Select(emp => emp.ID));
            await clipboard.SetTextAsync(employeeIDs);

            _toastManager.CreateToast("Copy Employee ID")
                .WithContent($"Copied multiple ID successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowInfo();
        }
    }

    [RelayCommand]
    private async Task ShowCopySingleEmployeeUsername(ManageEmployeesItem? employee)
    {
        if (employee == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(employee.Username);
        }

        _toastManager.CreateToast("Copy Employee Username")
            .WithContent($"Copied {employee.Username}'s username successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private async Task ShowCopyMultipleEmployeeUsername(ManageEmployeesItem? employee)
    {
        var selectedEmployees = EmployeeItems.Where(item => item.IsSelected).ToList();
        if (employee == null) return;
        if (selectedEmployees.Count == 0) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            var employeeUsernames = string.Join(", ", selectedEmployees.Select(emp => emp.Username));
            await clipboard.SetTextAsync(employeeUsernames);

            _toastManager.CreateToast("Copy Employee Usernames")
                .WithContent($"Copied multiple employee usernames successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowInfo();
        }
    }

    [RelayCommand]
    private async Task ShowCopySingleEmployeeContactNumber(ManageEmployeesItem? employee)
    {
        if (employee == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(employee.ContactNumber);
        }

        _toastManager.CreateToast("Copy Employee Contact No.")
            .WithContent($"Copied {employee.Name}'s contact number successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private async Task ShowCopyMultipleEmployeeContactNumber(ManageEmployeesItem? employee)
    {
        var selectedEmployees = EmployeeItems.Where(item => item.IsSelected).ToList();
        if (employee == null) return;
        if (selectedEmployees.Count == 0) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            var employeeContactNumbers = string.Join(", ", selectedEmployees.Select(emp => emp.ContactNumber));
            await clipboard.SetTextAsync(employeeContactNumbers);

            _toastManager.CreateToast("Copy Employee Contact No.")
                .WithContent($"Copied multiple employee contact numbers successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowInfo();
        }
    }

    [RelayCommand]
    private async Task ShowCopySingleEmployeePosition(ManageEmployeesItem? employee)
    {
        if (employee == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(employee.Position);
        }

        _toastManager.CreateToast("Copy Employee Position")
            .WithContent($"Copied {employee.Name}'s position successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private async Task ShowCopySingleEmployeeStatus(ManageEmployeesItem? employee)
    {
        if (employee == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(employee.Status);
        }

        _toastManager.CreateToast("Copy Employee Status")
            .WithContent($"Copied {employee.Name}'s status successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private async Task ShowCopySingleEmployeeDateJoined(ManageEmployeesItem? employee)
    {
        if (employee == null) return;

        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(employee.DateJoined.ToString(CultureInfo.InvariantCulture));
        }

        _toastManager.CreateToast("Copy Employee Date Joined")
            .WithContent($"Copied {employee.Name}'s date joined successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowInfo();
    }

    [RelayCommand]
    private void ShowSingleItemDeletionDialog(ManageEmployeesItem? employee)
    {
        if (employee == null) return;

        try
        {
            Debug.WriteLine($"Showing deletion dialog for employee: {employee.Name}");
            _dialogManager.CreateDialog("" +
                "Are you absolutely sure?",
                $"This action cannot be undone. This will permanently delete {employee.Name} and remove the data from your database.")
                .WithPrimaryButton("Continue", () => OnSubmitDeleteSingleItem(employee), DialogButtonStyle.Destructive)
                .WithCancelButton("Cancel")
                .WithMaxWidth(512)
                .Dismissible()
                .Show();
            Debug.WriteLine("Deletion dialog shown successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing deletion dialog: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ShowMultipleItemDeletionDialog(ManageEmployeesItem? employee)
    {
        if (employee == null) return;

        _dialogManager.CreateDialog("" +
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete multiple accounts and remove their data from your database.")
            .WithPrimaryButton("Continue", () => OnSubmitDeleteMultipleItems(employee), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    private async Task OnSubmitDeleteSingleItem(ManageEmployeesItem employee)
    {
        await DeleteEmployeeFromDatabase(employee);
        employee.PropertyChanged -= OnEmployeePropertyChanged;
        EmployeeItems.Remove(employee);
        UpdateCounts();

        _toastManager.CreateToast($"Delete {employee.Position} Account")
            .WithContent($"{employee.Name}'s Account deleted successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowSuccess();
    }
    private async Task OnSubmitDeleteMultipleItems(ManageEmployeesItem employee)
    {
        var selectedEmployees = EmployeeItems.Where(item => item.IsSelected).ToList();
        if (!selectedEmployees.Any()) return;

        foreach (var employees in selectedEmployees)
        {
            await DeleteEmployeeFromDatabase(employee);
            employees.PropertyChanged -= OnEmployeePropertyChanged;
            EmployeeItems.Remove(employees);
        }
        UpdateCounts();

        _toastManager.CreateToast($"Delete Selected Accounts")
            .WithContent($"Multiple accounts deleted successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowSuccess();
    }

    // Helper method to delete from database
    private async Task DeleteEmployeeFromDatabase(ManageEmployeesItem employee)
    {
        // using var connection = new SqlConnection(connectionString);
        // await connection.ExecuteAsync("DELETE FROM Employees WHERE ID = @ID", new { IDI = employee.ID });

        await Task.Delay(100); // Just an animation/simulation of async operation
    }

    private void ExecuteSortCommand(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                SortByIdCommand.Execute(null);
                break;
            case 1:
                SortNamesByAlphabeticalCommand.Execute(null);
                break;
            case 2:
                SortNamesByReverseAlphabeticalCommand.Execute(null);
                break;
            case 3:
                SortUsernamesByAlphabeticalCommand.Execute(null);
                break;
            case 4:
                SortUsernamesByReverseAlphabeticalCommand.Execute(null);
                break;
            case 5:
                SortDateByNewestToOldestCommand.Execute(null);
                break;
            case 6:
                SortDateByOldestToNewestCommand.Execute(null);
                break;
            case 7:
                SortResetCommand.Execute(null);
                break;
        }
    }

    private void ExecuteFilterCommand(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0: FilterActiveStatusCommand.Execute(null); break;
            case 1: FilterInactiveStatusCommand.Execute(null); break;
            case 2: FilterTerminatedStatusCommand.Execute(null); break;
        }
    }

    partial void OnSelectedSortIndexChanged(int value)
    {
        if (value >= 0)
        {
            ExecuteSortCommand(value);
        }
    }

    partial void OnSelectedFilterIndexChanged(int value)
    {
        if (value >= 0)
        {
            ExecuteFilterCommand(value);
        }
    }

    partial void OnSearchStringResultChanged(string value)
    {
        SearchEmployeesCommand.Execute(null);
    }
}

public partial class ManageEmployeesItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _iD = string.Empty;

    [ObservableProperty]
    private string _avatarSource = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _contactNumber = string.Empty;

    [ObservableProperty]
    private string _position = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private DateTime _dateJoined;

    public IBrush StatusForeground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),     // Green-500
        "inactive" => new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Gray-500
        "terminated" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red-500
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))           // Default Gray-500
    };

    public IBrush StatusBackground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),     // Green-500 with alpha
        "inactive" => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139)), // Gray-500 with alpha
        "terminated" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)), // Red-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))           // Default Gray-500 with alpha
    };

    public string StatusDisplayText => Status.ToLowerInvariant() switch
    {
        "active" => "● Active",
        "inactive" => "● Inactive",
        "terminated" => "● Terminated",
        _ => Status
    };

    // Notify when status changes to update colors
    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusDisplayText));
    }
}

public class Clipboard
{
    public static IClipboard? Get()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window } ? window.Clipboard! : null;
    }
}
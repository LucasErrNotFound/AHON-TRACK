using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using Microsoft.Data.SqlClient;
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
    private readonly IEmployeeService _employeeService;
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly AddNewEmployeeDialogCardViewModel _addNewEmployeeDialogCardViewModel;
    private readonly EmployeeProfileInformationViewModel _employeeProfileInformationViewModel;

    public ObservableCollection<ManageEmployeeModel> Employees { get; }
        = new ObservableCollection<ManageEmployeeModel>();

    public ManageEmployeesViewModel(
        DialogManager dialogManager,
        ToastManager toastManager,
        PageManager pageManager,
        AddNewEmployeeDialogCardViewModel addNewEmployeeDialogCardViewModel, EmployeeProfileInformationViewModel employeeProfileInformationViewModel, IEmployeeService employeeService)
    {
        _pageManager = pageManager;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _addNewEmployeeDialogCardViewModel = addNewEmployeeDialogCardViewModel;
        _employeeProfileInformationViewModel = employeeProfileInformationViewModel;
        _employeeService = employeeService;
        //LoadEmployeesAsync();
        _ = UpdateCounts();

    }

    public ManageEmployeesViewModel()
    {
        _toastManager = new ToastManager();
        _dialogManager = new DialogManager();
        _pageManager = new PageManager(new ServiceProvider());
        _addNewEmployeeDialogCardViewModel = new AddNewEmployeeDialogCardViewModel();
        _employeeProfileInformationViewModel = new EmployeeProfileInformationViewModel();
        _employeeService = null!;
    }


    [AvaloniaHotReload]
    public void Initialize() // new
    {
        if (IsInitialized) return;
        _ = LoadEmployeesFromDatabaseAsync();
        _ = UpdateCounts();
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
                AvatarSource = ManageEmployeeModel.DefaultAvatarSource,
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
                AvatarSource = ManageEmployeeModel.DefaultAvatarSource,
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
                AvatarSource = ManageEmployeeModel.DefaultAvatarSource,
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
                AvatarSource = ManageEmployeeModel.DefaultAvatarSource,
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
                AvatarSource = ManageEmployeeModel.DefaultAvatarSource,
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
                AvatarSource = ManageEmployeeModel.DefaultAvatarSource,
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
                AvatarSource = ManageEmployeeModel.DefaultAvatarSource,
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
                AvatarSource = ManageEmployeeModel.DefaultAvatarSource,
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
                AvatarSource = ManageEmployeeModel.DefaultAvatarSource,
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
                AvatarSource = ManageEmployeeModel.DefaultAvatarSource,
                Name = "Mark Dela Cruz",
                Username = "MarkyWTF",
                ContactNumber = "0931 315 1672",
                Position = "Gym Staff",
                Status = "Terminated",
                DateJoined = new DateTime(2018, 10, 1)
            }
        ];
    }

    // Method to load employees from a database (for future implementation) new
    public const string connectionString =
    "Data Source=LAPTOP-SSMJIDM6\\SQLEXPRESS08;Initial Catalog=AHON_TRACK;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

    private async Task LoadEmployeesFromDatabaseAsync()
    {
        try
        {
            var employees = await _employeeService.GetEmployeesAsync();

            var employeeItems = employees.Select(emp => new ManageEmployeesItem
            {
                ID = emp.ID,
                // UPDATED: Use ImageHelper to get avatar or default
                AvatarSource = emp.AvatarBytes != null
        ? ImageHelper.BytesToBitmap(emp.AvatarBytes)
        : ManageEmployeeModel.DefaultAvatarSource,
                Name = emp.Name,
                Username = emp.Username,
                ContactNumber = emp.ContactNumber,
                Position = emp.Position,
                Status = emp.Status,
                DateJoined = emp.DateJoined
            }).ToList();

            OriginalEmployeeData = employeeItems;
            CurrentFilteredData = [.. employeeItems];

            EmployeeItems.Clear();
            foreach (var employee in employeeItems)
            {
                employee.PropertyChanged += OnEmployeePropertyChanged;
                EmployeeItems.Add(employee);
            }

            TotalCount = EmployeeItems.Count;
            _ = UpdateCounts();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Database Error")
                .WithContent($"Failed to load employees: {ex.Message}")
                .DismissOnClick()
                .ShowError();

            LoadSampleData(); // Fallback to sample data
        }
    }

    public List<ManageEmployeesItem> Items { get; set; } = new();
    public string GenerateEmployeesSummary() // new
    {
        int totalCount = Items?.Count ?? 0;

        int activeCount = Items?.Count(x => string.Equals(x.Status, "active", StringComparison.OrdinalIgnoreCase)) ?? 0;
        int inactiveCount = Items?.Count(x => string.Equals(x.Status, "inactive", StringComparison.OrdinalIgnoreCase)) ?? 0;
        int terminatedCount = Items?.Count(x => string.Equals(x.Status, "terminated", StringComparison.OrdinalIgnoreCase)) ?? 0;

        return $"Total: {totalCount} employees (Active: {activeCount}, Inactive: {inactiveCount}, Terminated: {terminatedCount})";
    }

    public async Task<string> GenerateEmployeesSummaryAsync()
    {
        try
        {
            int totalCount = await _employeeService.GetTotalEmployeeCountAsync();
            int activeCount = await _employeeService.GetEmployeeCountByStatusAsync("Active");
            int inactiveCount = await _employeeService.GetEmployeeCountByStatusAsync("Inactive");
            int terminatedCount = await _employeeService.GetEmployeeCountByStatusAsync("Terminated");

            return $"Total: {totalCount} employees (Active: {activeCount}, Inactive: {inactiveCount}, Terminated: {terminatedCount})";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error generating summary: {ex.Message}");

            // Fallback to current method
            return GenerateEmployeesSummary();
        }
    }


    private void OnEmployeePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManageEmployeesItem.IsSelected))
        {
            _ = UpdateCounts();
        }
    }

    private async Task UpdateEmployeeItemsFromService(List<ManageEmployeeModel> employees)
    {
        var employeeItems = employees.Select(emp => new ManageEmployeesItem
        {
            ID = emp.ID,
            AvatarSource = emp.AvatarBytes != null
                ? ImageHelper.BytesToBitmap(emp.AvatarBytes)
                : ManageEmployeeModel.DefaultAvatarSource,
            Name = emp.Name,
            Username = emp.Username,
            ContactNumber = emp.ContactNumber,
            Position = emp.Position,
            Status = emp.Status,
            DateJoined = emp.DateJoined
        }).ToList();

        // Update collections
        OriginalEmployeeData = employeeItems; // Keep original data updated
        CurrentFilteredData = [.. employeeItems];

        EmployeeItems.Clear();
        foreach (var employee in employeeItems)
        {
            employee.PropertyChanged += OnEmployeePropertyChanged;
            EmployeeItems.Add(employee);
        }

        await UpdateCounts();
    }

    private async Task UpdateCounts()
    {
        try
        {
            SelectedCount = EmployeeItems.Count(x => x.IsSelected);
            TotalCount = await _employeeService.GetTotalEmployeeCountAsync();

            SelectAll = EmployeeItems.Count > 0 && EmployeeItems.All(x => x.IsSelected);
        }
        catch (Exception ex)
        {
            // Fallback to current method if service fails
            SelectedCount = EmployeeItems.Count(x => x.IsSelected);
            TotalCount = EmployeeItems.Count;
            SelectAll = EmployeeItems.Count > 0 && EmployeeItems.All(x => x.IsSelected);

            System.Diagnostics.Debug.WriteLine($"Error updating counts: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ShowAddNewEmployeeDialog()
    {
        _addNewEmployeeDialogCardViewModel.Initialize();

        _dialogManager.CreateDialog(_addNewEmployeeDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                await LoadEmployeesFromDatabaseAsync();
                _toastManager.CreateToast("Added a new employee")
                    .WithContent("Welcome, new employee!")
                    .DismissOnClick()
                    .ShowSuccess();
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new employee cancelled")
                    .WithContent("Add a new employee to continue")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(950)
            .Show();
    }


    [RelayCommand]
    private async Task ShowModifyEmployeeDialog(ManageEmployeesItem? employee)
    {
        if (employee is null)
        {
            _toastManager.CreateToast("No Employee Selected")
                .WithContent("Please select an employee to modify")
                .DismissOnClick()
                .ShowError();
            return;
        }

        await _addNewEmployeeDialogCardViewModel.InitializeForEditMode(employee);

        _dialogManager.CreateDialog(_addNewEmployeeDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                await LoadEmployeesFromDatabaseAsync();
                _toastManager.CreateToast("Modified Employee Details")
                    .WithContent($"You have successfully modified {employee.Name}'s details")
                    .DismissOnClick()
                    .ShowSuccess();
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Modifying Employee Details Cancelled")
                    .WithContent("Click the three-dots if you want to modify your employees' details")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(950)
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
        _ = UpdateCounts();

        SelectedSortIndex = -1;
        SelectedFilterIndex = -1;
    }

    [RelayCommand]
    private async Task SortById()
    {
        try
        {
            var employees = await _employeeService.GetEmployeesSortedAsync("id", false);
            await UpdateEmployeeItemsFromService(employees);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Sort Error")
                .WithContent($"Error sorting by ID: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task SortNamesByAlphabetical()
    {
        try
        {
            var employees = await _employeeService.GetEmployeesSortedAsync("name", false);
            await UpdateEmployeeItemsFromService(employees);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Sort Error")
                .WithContent($"Error sorting names alphabetically: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task SortNamesByReverseAlphabetical()
    {
        try
        {
            var employees = await _employeeService.GetEmployeesSortedAsync("name", true);
            await UpdateEmployeeItemsFromService(employees);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Sort Error")
                .WithContent($"Error sorting names reverse alphabetically: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task SortUsernamesByAlphabetical()
    {
        try
        {
            var employees = await _employeeService.GetEmployeesSortedAsync("username", false);
            await UpdateEmployeeItemsFromService(employees);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Sort Error")
                .WithContent($"Error sorting usernames alphabetically: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task SortUsernamesByReverseAlphabetical()
    {
        try
        {
            var employees = await _employeeService.GetEmployeesSortedAsync("username", true);
            await UpdateEmployeeItemsFromService(employees);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Sort Error")
                .WithContent($"Error sorting usernames reverse alphabetically: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task SortDateByNewestToOldest()
    {
        try
        {
            var employees = await _employeeService.GetEmployeesSortedAsync("datejoined", true);
            await UpdateEmployeeItemsFromService(employees);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Sort Error")
                .WithContent($"Error sorting by newest date: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task SortDateByOldestToNewest()
    {
        try
        {
            var employees = await _employeeService.GetEmployeesSortedAsync("datejoined", false);
            await UpdateEmployeeItemsFromService(employees);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Sort Error")
                .WithContent($"Error sorting by oldest date: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task FilterActiveStatus()
    {
        try
        {
            var employees = await _employeeService.GetEmployeesByStatusAsync("Active");
            await UpdateEmployeeItemsFromService(employees);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Filter Error")
                .WithContent($"Error filtering active employees: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task FilterInactiveStatus()
    {
        try
        {
            var employees = await _employeeService.GetEmployeesByStatusAsync("Inactive");
            await UpdateEmployeeItemsFromService(employees);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Filter Error")
                .WithContent($"Error filtering inactive employees: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task FilterTerminatedStatus()
    {
        try
        {
            var employees = await _employeeService.GetEmployeesByStatusAsync("Terminated");
            await UpdateEmployeeItemsFromService(employees);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Filter Error")
                .WithContent($"Error filtering terminated employees: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private void ToggleSelection(bool? isChecked)
    {
        var shouldSelect = isChecked ?? false;

        foreach (var item in EmployeeItems)
        {
            item.IsSelected = shouldSelect;
        }
        _ = UpdateCounts();
    }

    [RelayCommand]
    private async Task SearchEmployees()
    {
        IsSearchingEmployee = true;

        try
        {
            List<ManageEmployeeModel> employees;

            if (string.IsNullOrWhiteSpace(SearchStringResult))
            {
                // If search is empty, get all employees or apply current filter
                if (SelectedFilterIndex >= 0)
                {
                    string status = SelectedFilterIndex switch
                    {
                        0 => "Active",
                        1 => "Inactive",
                        2 => "Terminated",
                        _ => "Active"
                    };
                    employees = await _employeeService.GetEmployeesByStatusAsync(status);
                }
                else
                {
                    employees = await _employeeService.GetEmployeesAsync();
                }
            }
            else
            {
                // Use service search method
                employees = await _employeeService.SearchEmployeesAsync(SearchStringResult);
            }

            // Convert to ManageEmployeesItem and update UI
            var employeeItems = employees.Select(emp => new ManageEmployeesItem
            {
                ID = emp.ID,
                AvatarSource = emp.AvatarBytes != null
                    ? ImageHelper.BytesToBitmap(emp.AvatarBytes)
                    : ManageEmployeeModel.DefaultAvatarSource,
                Name = emp.Name,
                Username = emp.Username,
                ContactNumber = emp.ContactNumber,
                Position = emp.Position,
                Status = emp.Status,
                DateJoined = emp.DateJoined
            }).ToList();

            EmployeeItems.Clear();
            foreach (var employee in employeeItems)
            {
                employee.PropertyChanged += OnEmployeePropertyChanged;
                EmployeeItems.Add(employee);
            }

            await UpdateCounts();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Search Error")
                .WithContent($"Error searching employees: {ex.Message}")
                .DismissOnClick()
                .ShowError();
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

        // ✅ Instead of just removing from UI, reload from database
        await LoadEmployeesFromDatabaseAsync();

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

        foreach (var emp in selectedEmployees)
        {
            await DeleteEmployeeFromDatabase(emp);
        }

        // ✅ Instead of removing from UI, reload from database
        await LoadEmployeesFromDatabaseAsync();

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

        if (int.TryParse(employee.ID, out int id))
        {
            await _employeeService.DeleteEmployeeAsync(id);
        }

        await Task.Delay(100); // Just an animation/simulation of async operation
    }

    private async void ExecuteSortCommand(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                await SortById();
                break;
            case 1:
                await SortNamesByAlphabetical();
                break;
            case 2:
                await SortNamesByReverseAlphabetical();
                break;
            case 3:
                await SortUsernamesByAlphabetical();
                break;
            case 4:
                await SortUsernamesByReverseAlphabetical();
                break;
            case 5:
                await SortDateByNewestToOldest();
                break;
            case 6:
                await SortDateByOldestToNewest();
                break;
            case 7:
                SortResetCommand.Execute(null);
                break;
        }
    }

    private async void ExecuteFilterCommand(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0: await FilterActiveStatus(); break;
            case 1: await FilterInactiveStatus(); break;
            case 2: await FilterTerminatedStatus(); break;
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
    private Bitmap _avatarSource = ManageEmployeeModel.DefaultAvatarSource;

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

    //public static Bitmap DefaultAvatarSource { get; internal set; }

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
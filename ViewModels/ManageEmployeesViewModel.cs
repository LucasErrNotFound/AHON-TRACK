using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using Microsoft.Extensions.Logging;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.Services;

namespace AHON_TRACK.ViewModels;

[Page("manageEmployees")]
public sealed partial class ManageEmployeesViewModel : ViewModelBase, INavigable
{
    #region Private Fields

    private readonly INavigationService _navigationService;
    private readonly IEmployeeService _employeeService;
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly AddNewEmployeeDialogCardViewModel _addNewEmployeeDialogCardViewModel;
    private readonly ILogger _logger;

    [ObservableProperty] private List<ManageEmployeesItem> _originalEmployeeData = [];
    [ObservableProperty] private ObservableCollection<ManageEmployeesItem> _employeeItems = [];
    [ObservableProperty] private List<ManageEmployeesItem> _currentFilteredData = [];
    [ObservableProperty] private string _searchStringResult = string.Empty;
    [ObservableProperty] private bool _isSearchingEmployee;
    [ObservableProperty] private bool _selectAll;
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _showIdColumn = true;
    [ObservableProperty] private bool _showPictureColumn = true;
    [ObservableProperty] private bool _showNameColumn = true;
    [ObservableProperty] private bool _showUsernameColumn = true;
    [ObservableProperty] private bool _showContactNumberColumn = true;
    [ObservableProperty] private bool _showPositionColumn = true;
    [ObservableProperty] private bool _showStatusColumn = true;
    [ObservableProperty] private bool _showDateJoined = true;
    [ObservableProperty] private int _selectedSortIndex = -1;
    [ObservableProperty] private int _selectedFilterIndex = -1;
    [ObservableProperty] private bool _isInitialized;

    #endregion

    #region Constructor

    public ManageEmployeesViewModel(
        DialogManager dialogManager,
        ToastManager toastManager,
        AddNewEmployeeDialogCardViewModel addNewEmployeeDialogCardViewModel,
        INavigationService navigationService,
        IEmployeeService employeeService,
        ILogger logger)
    {
        _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
        _addNewEmployeeDialogCardViewModel = addNewEmployeeDialogCardViewModel ?? throw new ArgumentNullException(nameof(addNewEmployeeDialogCardViewModel));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _employeeService = employeeService ?? throw new ArgumentNullException(nameof(employeeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        SubscribeToEvents();
    }

    // Design-time constructor
    public ManageEmployeesViewModel()
    {
        _toastManager = new ToastManager();
        _dialogManager = new DialogManager();
        _addNewEmployeeDialogCardViewModel = new AddNewEmployeeDialogCardViewModel();
        _navigationService = null!;
        _employeeService = null!;
        _logger = null!;

        LoadSampleData();
    }

    #endregion

    #region INavigable Implementation

    [AvaloniaHotReload]
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (IsInitialized)
        {
            _logger.LogDebug("ManageEmployeesViewModel already initialized");
            return;
        }

        _logger.LogInformation("Initializing ManageEmployeesViewModel");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            await LoadEmployeesFromDatabaseAsync(linkedCts.Token).ConfigureAwait(false);
            await UpdateCounts(linkedCts.Token).ConfigureAwait(false);

            IsInitialized = true;
            _logger.LogInformation("ManageEmployeesViewModel initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ManageEmployeesViewModel initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing ManageEmployeesViewModel");
            LoadSampleData(); // Fallback
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Navigating away from ManageEmployees");
        return ValueTask.CompletedTask;
    }

    #endregion

    #region Event Management

    private void SubscribeToEvents()
    {
        var eventService = DashboardEventService.Instance;
        eventService.EmployeeAdded += OnEmployeeChanged;
        eventService.EmployeeUpdated += OnEmployeeChanged;
        eventService.EmployeeDeleted += OnEmployeeChanged;
    }

    private void UnsubscribeFromEvents()
    {
        var eventService = DashboardEventService.Instance;
        eventService.EmployeeAdded -= OnEmployeeChanged;
        eventService.EmployeeUpdated -= OnEmployeeChanged;
        eventService.EmployeeDeleted -= OnEmployeeChanged;
    }

    private async void OnEmployeeChanged(object? sender, EventArgs e)
    {
        try
        {
            _logger.LogDebug("Detected employee data change — refreshing");
            await LoadEmployeesFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);
            await UpdateCounts(LifecycleToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing employees after change event");
        }
    }

    private void OnEmployeePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManageEmployeesItem.IsSelected))
        {
            _ = UpdateCounts(LifecycleToken);
        }
    }

    #endregion

    #region Data Loading

    private async Task LoadEmployeesFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (success, message, employees) = await _employeeService.GetEmployeesAsync()
                .ConfigureAwait(false);

            if (!success || employees == null)
            {
                _logger.LogWarning("Failed to load employees: {Message}", message);
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to load employees: {message}")
                    .DismissOnClick()
                    .ShowError();
                LoadSampleData();
                return;
            }

            // Use Span<T> for efficient transformation
            var employeeItems = new List<ManageEmployeesItem>(employees.Count);
            foreach (var emp in employees)
            {
                var item = new ManageEmployeesItem
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
                };
                item.PropertyChanged += OnEmployeePropertyChanged;
                employeeItems.Add(item);
            }

            OriginalEmployeeData = employeeItems;
            CurrentFilteredData = new List<ManageEmployeesItem>(employeeItems);

            EmployeeItems.Clear();
            foreach (var item in employeeItems)
            {
                EmployeeItems.Add(item);
            }

            TotalCount = EmployeeItems.Count;
            _logger.LogDebug("Loaded {Count} employees from database", employees.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading employees from database");
            _toastManager.CreateToast("Error")
                .WithContent($"Unexpected error: {ex.Message}")
                .DismissOnClick()
                .ShowError();
            LoadSampleData();
        }
    }

    private void LoadSampleData()
    {
        var sampleEmployees = GetSampleEmployeesData();
        OriginalEmployeeData = sampleEmployees;
        CurrentFilteredData = new List<ManageEmployeesItem>(sampleEmployees);

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
        return new List<ManageEmployeesItem>
        {
            new() { ID = 1001, AvatarSource = ManageEmployeeModel.DefaultAvatarSource, Name = "Jedd Calubayan", 
                    Username = "Kuya Rome", ContactNumber = "0975 994 3010", Position = "Gym Staff", 
                    Status = "Active", DateJoined = new DateTime(2025, 6, 16) },
            new() { ID = 1002, AvatarSource = ManageEmployeeModel.DefaultAvatarSource, Name = "JC Casidore", 
                    Username = "Jaycee", ContactNumber = "0989 445 0949", Position = "Gym Staff", 
                    Status = "Active", DateJoined = new DateTime(2025, 6, 16) },
            new() { ID = 1003, AvatarSource = ManageEmployeeModel.DefaultAvatarSource, Name = "Mardie Dela Cruz", 
                    Username = "Figora", ContactNumber = "0901 990 9921", Position = "Gym Staff", 
                    Status = "Inactive", DateJoined = new DateTime(2025, 6, 17) },
            new() { ID = 1004, AvatarSource = ManageEmployeeModel.DefaultAvatarSource, Name = "JL Taberdo", 
                    Username = "JeyEL", ContactNumber = "0957 889 3724", Position = "Gym Staff", 
                    Status = "Terminated", DateJoined = new DateTime(2025, 6, 19) },
            new() { ID = 1005, AvatarSource = ManageEmployeeModel.DefaultAvatarSource, Name = "Jav Agustin", 
                    Username = "Mr. Javitos", ContactNumber = "0923 354 4866", Position = "Gym Staff", 
                    Status = "Inactive", DateJoined = new DateTime(2025, 6, 21) }
        };
    }

    private async Task UpdateCounts(CancellationToken cancellationToken = default)
    {
        try
        {
            SelectedCount = EmployeeItems.Count(x => x.IsSelected);
            TotalCount = await _employeeService.GetTotalEmployeeCountAsync()
                .ConfigureAwait(false);
            SelectAll = EmployeeItems.Count > 0 && EmployeeItems.All(x => x.IsSelected);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating counts from service, using local count");
            SelectedCount = EmployeeItems.Count(x => x.IsSelected);
            TotalCount = EmployeeItems.Count;
            SelectAll = EmployeeItems.Count > 0 && EmployeeItems.All(x => x.IsSelected);
        }
    }

    #endregion

    #region Dialog Commands

    [RelayCommand]
    private void ShowAddNewEmployeeDialog()
    {
        _addNewEmployeeDialogCardViewModel.Initialize();

        _dialogManager.CreateDialog(_addNewEmployeeDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                await LoadEmployeesFromDatabaseAsync(LifecycleToken);
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
    private async Task ShowModifyEmployeeDialogAsync(ManageEmployeesItem? employee)
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
                await LoadEmployeesFromDatabaseAsync(LifecycleToken);
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
    private async Task OpenViewEmployeeProfileAsync(ManageEmployeesItem? employee)
    {
        if (employee == null)
        {
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
        
        await _navigationService.NavigateAsync<EmployeeProfileInformationViewModel>(
            parameters, LifecycleToken).ConfigureAwait(false);
    }

    #endregion

    #region Sort Commands

    [RelayCommand]
    private void SortReset()
    {
        EmployeeItems.Clear();
        foreach (var employee in OriginalEmployeeData)
        {
            employee.PropertyChanged += OnEmployeePropertyChanged;
            EmployeeItems.Add(employee);
        }

        CurrentFilteredData = new List<ManageEmployeesItem>(OriginalEmployeeData);
        _ = UpdateCounts(LifecycleToken);
        SelectedSortIndex = -1;
        SelectedFilterIndex = -1;
    }

    [RelayCommand]
    private void SortById()
    {
        var sorted = EmployeeItems.OrderBy(e => e.ID).ToList();
        UpdateEmployeeItems(sorted);
    }

    [RelayCommand]
    private void SortNamesByAlphabetical()
    {
        var sorted = EmployeeItems.OrderBy(e => e.Name).ToList();
        UpdateEmployeeItems(sorted);
    }

    [RelayCommand]
    private void SortNamesByReverseAlphabetical()
    {
        var sorted = EmployeeItems.OrderByDescending(e => e.Name).ToList();
        UpdateEmployeeItems(sorted);
    }

    [RelayCommand]
    private void SortUsernamesByAlphabetical()
    {
        var sorted = EmployeeItems.OrderBy(e => e.Username).ToList();
        UpdateEmployeeItems(sorted);
    }

    [RelayCommand]
    private void SortUsernamesByReverseAlphabetical()
    {
        var sorted = EmployeeItems.OrderByDescending(e => e.Username).ToList();
        UpdateEmployeeItems(sorted);
    }

    [RelayCommand]
    private void SortDateByNewestToOldest()
    {
        var sorted = EmployeeItems.OrderByDescending(e => e.DateJoined).ToList();
        UpdateEmployeeItems(sorted);
    }

    [RelayCommand]
    private void SortDateByOldestToNewest()
    {
        var sorted = EmployeeItems.OrderBy(e => e.DateJoined).ToList();
        UpdateEmployeeItems(sorted);
    }

    private void UpdateEmployeeItems(List<ManageEmployeesItem> sorted)
    {
        EmployeeItems.Clear();
        foreach (var item in sorted)
        {
            EmployeeItems.Add(item);
        }
        CurrentFilteredData = new List<ManageEmployeesItem>(sorted);
    }

    #endregion

    #region Filter Commands

    [RelayCommand]
    private void FilterActiveStatus()
    {
        FilterByStatus("active");
    }

    [RelayCommand]
    private void FilterInactiveStatus()
    {
        FilterByStatus("inactive");
    }

    [RelayCommand]
    private void FilterTerminatedStatus()
    {
        FilterByStatus("terminated");
    }

    private void FilterByStatus(string status)
    {
        var filtered = OriginalEmployeeData
            .Where(e => e.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            .ToList();

        EmployeeItems.Clear();
        foreach (var employee in filtered)
        {
            employee.PropertyChanged += OnEmployeePropertyChanged;
            EmployeeItems.Add(employee);
        }
        
        CurrentFilteredData = new List<ManageEmployeesItem>(filtered);
        _ = UpdateCounts(LifecycleToken);
    }

    #endregion

    #region Search Command

    [RelayCommand]
    private async Task SearchEmployeesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SearchStringResult))
        {
            EmployeeItems.Clear();
            foreach (var employee in CurrentFilteredData)
            {
                employee.PropertyChanged += OnEmployeePropertyChanged;
                EmployeeItems.Add(employee);
            }
            _ = UpdateCounts(cancellationToken);
            return;
        }

        IsSearchingEmployee = true;

        try
        {
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);

            var searchTerm = SearchStringResult;
            var filtered = CurrentFilteredData.Where(emp =>
                emp.ID.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                emp.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                emp.Username.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                emp.ContactNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                emp.Position.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                emp.Status.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                emp.DateJoined.ToString("MMMM d, yyyy").Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            EmployeeItems.Clear();
            foreach (var employee in filtered)
            {
                employee.PropertyChanged += OnEmployeePropertyChanged;
                EmployeeItems.Add(employee);
            }
            _ = UpdateCounts(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        finally
        {
            IsSearchingEmployee = false;
        }
    }

    #endregion

    #region Selection Commands

    [RelayCommand]
    private void ToggleSelection(bool? isChecked)
    {
        var shouldSelect = isChecked ?? false;
        foreach (var item in EmployeeItems)
        {
            item.IsSelected = shouldSelect;
        }
        _ = UpdateCounts(LifecycleToken);
    }

    #endregion

    #region Clipboard Commands

    [RelayCommand]
    private async Task CopySingleEmployeeNameAsync(ManageEmployeesItem? employee)
    {
        if (employee == null) return;
        await CopyToClipboardAsync(employee.Name, $"Copied {employee.Name}'s name successfully!");
    }

    [RelayCommand]
    private async Task CopyMultipleEmployeeNameAsync()
    {
        var selected = EmployeeItems.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) return;
        
        var names = string.Join(", ", selected.Select(e => e.Name));
        await CopyToClipboardAsync(names, "Copied multiple employee names successfully!");
    }

    [RelayCommand]
    private async Task CopySingleEmployeeIdAsync(ManageEmployeesItem? employee)
    {
        if (employee == null) return;
        await CopyToClipboardAsync(employee.ID.ToString(), $"Copied {employee.Name}'s ID successfully!");
    }

    [RelayCommand]
    private async Task CopyMultipleEmployeeIdAsync()
    {
        var selected = EmployeeItems.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) return;
        
        var ids = string.Join(", ", selected.Select(e => e.ID));
        await CopyToClipboardAsync(ids, "Copied multiple IDs successfully!");
    }

    [RelayCommand]
    private async Task CopySingleEmployeeUsernameAsync(ManageEmployeesItem? employee)
    {
        if (employee == null) return;
        await CopyToClipboardAsync(employee.Username, $"Copied {employee.Username}'s username successfully!");
    }

    [RelayCommand]
    private async Task CopyMultipleEmployeeUsernameAsync()
    {
        var selected = EmployeeItems.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) return;
        
        var usernames = string.Join(", ", selected.Select(e => e.Username));
        await CopyToClipboardAsync(usernames, "Copied multiple usernames successfully!");
    }

    [RelayCommand]
    private async Task CopySingleEmployeeContactNumberAsync(ManageEmployeesItem? employee)
    {
        if (employee == null) return;
        await CopyToClipboardAsync(employee.ContactNumber, $"Copied {employee.Name}'s contact number successfully!");
    }

    [RelayCommand]
    private async Task CopyMultipleEmployeeContactNumberAsync()
    {
        var selected = EmployeeItems.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) return;
        
        var numbers = string.Join(", ", selected.Select(e => e.ContactNumber));
        await CopyToClipboardAsync(numbers, "Copied multiple contact numbers successfully!");
    }

    [RelayCommand]
    private async Task CopySingleEmployeePositionAsync(ManageEmployeesItem? employee)
    {
        if (employee == null) return;
        await CopyToClipboardAsync(employee.Position, $"Copied {employee.Name}'s position successfully!");
    }

    [RelayCommand]
    private async Task CopySingleEmployeeStatusAsync(ManageEmployeesItem? employee)
    {
        if (employee == null) return;
        await CopyToClipboardAsync(employee.Status, $"Copied {employee.Name}'s status successfully!");
    }

    [RelayCommand]
    private async Task CopySingleEmployeeDateJoinedAsync(ManageEmployeesItem? employee)
    {
        if (employee == null) return;
        await CopyToClipboardAsync(
            employee.DateJoined.ToString(CultureInfo.InvariantCulture), 
            $"Copied {employee.Name}'s date joined successfully!");
    }

    private async Task CopyToClipboardAsync(string text, string successMessage)
    {
        var clipboard = Clipboard.Get();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(text);
            _toastManager.CreateToast("Copied!")
                .WithContent(successMessage)
                .DismissOnClick()
                .WithDelay(6)
                .ShowInfo();
        }
    }

    #endregion

    #region Delete Commands

    [RelayCommand]
    private void ShowSingleItemDeletionDialog(ManageEmployeesItem? employee)
    {
        if (employee == null) return;

        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete {employee.Name} and remove the data from your database.")
            .WithPrimaryButton("Continue", () => _ = OnSubmitDeleteSingleItemAsync(employee), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowMultipleItemDeletionDialog()
    {
        var selected = EmployeeItems.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) return;

        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            "This action cannot be undone. This will permanently delete multiple accounts and remove their data from your database.")
            .WithPrimaryButton("Continue", () => _ = OnSubmitDeleteMultipleItemsAsync(), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    private async Task OnSubmitDeleteSingleItemAsync(ManageEmployeesItem employee)
    {
        try
        {
            var (success, message) = await _employeeService.DeleteEmployeeAsync(employee.ID)
                .ConfigureAwait(false);

            if (success)
            {
                await LoadEmployeesFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);
            }
            else
            {
                _toastManager.CreateToast("Delete Failed")
                    .WithContent($"Failed to delete: {message}")
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee {ID}", employee.ID);
        }
    }

    private async Task OnSubmitDeleteMultipleItemsAsync()
    {
        var selectedEmployees = EmployeeItems.Where(item => item.IsSelected).ToList();
        if (!selectedEmployees.Any()) return;

        try
        {
            foreach (var emp in selectedEmployees)
            {
                await _employeeService.DeleteEmployeeAsync(emp.ID).ConfigureAwait(false);
            }

            await LoadEmployeesFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);
            
            _toastManager.CreateToast("Delete Selected Accounts")
                .WithContent("Multiple accounts deleted successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting multiple employees");
        }
    }

    #endregion

    #region Property Changed Handlers

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
        _ = SearchEmployeesAsync(LifecycleToken);
    }

    private void ExecuteSortCommand(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0: SortByIdCommand.Execute(null); break;
            case 1: SortNamesByAlphabeticalCommand.Execute(null); break;
            case 2: SortNamesByReverseAlphabeticalCommand.Execute(null); break;
            case 3: SortUsernamesByAlphabeticalCommand.Execute(null); break;
            case 4: SortUsernamesByReverseAlphabeticalCommand.Execute(null); break;
            case 5: SortDateByNewestToOldestCommand.Execute(null); break;
            case 6: SortDateByOldestToNewestCommand.Execute(null); break;
            case 7: SortResetCommand.Execute(null); break;
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

    #endregion

    #region HotAvalonia

    [AvaloniaHotReload]
    public void Initialize()
    {
        // Hot reload support
    }

    #endregion

    #region Disposal

    protected override async ValueTask DisposeAsyncCore()
    {
        _logger.LogInformation("Disposing ManageEmployeesViewModel");

        // Unsubscribe from events
        UnsubscribeFromEvents();

        // Unsubscribe from item property changes
        foreach (var item in EmployeeItems)
        {
            item.PropertyChanged -= OnEmployeePropertyChanged;
        }

        // Clear collections
        EmployeeItems.Clear();
        OriginalEmployeeData.Clear();
        CurrentFilteredData.Clear();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    #endregion
}

public partial class ManageEmployeesItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _iD;

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
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ShadUI;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;

namespace AHON_TRACK.Components.ViewModels;

[Page("viewEmployeeProfile")]
public sealed partial class EmployeeProfileInformationViewModel : ViewModelBase, INavigableWithParameters
{
    private readonly PageManager _pageManager;
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly AddNewEmployeeDialogCardViewModel _addNewEmployeeDialogCardViewModel;
    private readonly IEmployeeService _employeeService;

    [ObservableProperty]
    private bool _isFromCurrentUser = false;

    [ObservableProperty]
    private string _employeeFullNameHeader = string.Empty;

    [ObservableProperty]
    private int _employeeID;

    [ObservableProperty]
    private string _employeePosition = string.Empty;

    [ObservableProperty]
    private string _employeeStatus = string.Empty;

    [ObservableProperty]
    private string _employeeDateJoined = string.Empty;

    [ObservableProperty]
    private string _employeeFullName = string.Empty;

    [ObservableProperty]
    private int _employeeAge;

    [ObservableProperty]
    private string _employeeBirthDate = string.Empty;

    [ObservableProperty]
    private string _employeeGender = string.Empty;

    [ObservableProperty]
    private string _employeePhoneNumber = string.Empty;

    [ObservableProperty]
    private string _employeeLastLogin = string.Empty;

    [ObservableProperty]
    private string _employeeHouseAddress = string.Empty;

    [ObservableProperty]
    private string _employeeHouseNumber = string.Empty;

    [ObservableProperty]
    private string _employeeStreet = string.Empty;

    [ObservableProperty]
    private string _employeeBarangay = string.Empty;

    [ObservableProperty]
    private string _employeeCityProvince = string.Empty;

    [ObservableProperty]
    private string _employeeZipCode = string.Empty;

    [ObservableProperty]
    private ManageEmployeesItem? _selectedEmployeeData;

    public EmployeeProfileInformationViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, AddNewEmployeeDialogCardViewModel addNewEmployeeDialogCardViewModel, IEmployeeService employeeService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _addNewEmployeeDialogCardViewModel = addNewEmployeeDialogCardViewModel;
        _employeeService = employeeService;

    }

    public EmployeeProfileInformationViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _addNewEmployeeDialogCardViewModel = new AddNewEmployeeDialogCardViewModel();
    }

    public void SetNavigationParameters(Dictionary<string, object> parameters)
    {
        // Check if this is current user profile
        if (parameters.TryGetValue("IsCurrentUser", out var isCurrentUserValue) && isCurrentUserValue is bool isCurrentUser)
        {
            IsFromCurrentUser = isCurrentUser;
        }

        // Check if employee data is passed
        if (parameters.TryGetValue("EmployeeData", out var employeeDataValue) && employeeDataValue is ManageEmployeesItem employeeData)
        {
            _selectedEmployeeData = employeeData;
        }
    }

    public void Initialize()
    {
        if (IsFromCurrentUser)
        {
            InitializeCurrentUserProfile();
        }
        else if (_selectedEmployeeData != null)
        {
            InitializeEmployeeProfile();
        }
        else
        {
            Debug.WriteLine("No navigation parameters set, using default values.");
            SetDefaultValues();
        }
    }

    private async void InitializeEmployeeProfile()
    {
        if (_selectedEmployeeData != null)
        {
            var (success, message, fullEmployee) = await _employeeService.ViewEmployeeProfileAsync(_selectedEmployeeData.ID);

            if (!success || fullEmployee == null)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            EmployeeID = fullEmployee.ID;
            EmployeePosition = fullEmployee.Position;
            EmployeeStatus = fullEmployee.Status;
            EmployeeDateJoined = fullEmployee.DateJoined == DateTime.MinValue
                ? "N/A"
                : fullEmployee.DateJoined.ToString("MMMM d, yyyy");
            EmployeeFullName = fullEmployee.Name;
            EmployeeFullNameHeader = $"{fullEmployee.Name}'s Profile";
            EmployeePhoneNumber = fullEmployee.ContactNumber;

            EmployeeAge = fullEmployee.Age;
            EmployeeBirthDate = fullEmployee.Birthdate;
            EmployeeGender = fullEmployee.Gender;
            EmployeeLastLogin = fullEmployee.LastLogin;
            EmployeeHouseAddress = fullEmployee.HouseAddress;
            EmployeeHouseNumber = fullEmployee.HouseNumber;
            EmployeeStreet = fullEmployee.Street;
            EmployeeBarangay = fullEmployee.Barangay;
            EmployeeCityProvince = fullEmployee.CityProvince;
            EmployeeZipCode = fullEmployee.ZipCode;

            // Profile Picture
            if (fullEmployee.AvatarSource != null)
            {
                //
            }
        }
    }

    public void InitializeCurrentUserProfile()
    {
        IsFromCurrentUser = true;
        SetDefaultValues();
        EmployeeFullNameHeader = "My Profile";
    }

    private void SetDefaultValues()
    {
        EmployeeID = 13203;
        EmployeePosition = CurrentUserModel.Role;
        EmployeeStatus = "Active";
        EmployeeDateJoined = "January 15, 2023";
        EmployeeFullName = "John Doe";
        EmployeeFullNameHeader = IsFromCurrentUser ? "My Profile" : "John Doe's Profile";
        EmployeeAge = 31;
        EmployeeBirthDate = "1993-05-20";
        EmployeeGender = "Male";
        EmployeePhoneNumber = "09837756473";
        EmployeeLastLogin = "July 10, 2025 4:30PM";
        EmployeeHouseAddress = "123 Main";
        EmployeeHouseNumber = "123";
        EmployeeStreet = "Main Street";
        EmployeeBarangay = "Maungib";
        EmployeeCityProvince = "Cebu City, Cebu";
        EmployeeZipCode = "6000";
    }

    [RelayCommand]
    private void BackPage() => _pageManager.Navigate<DashboardViewModel>();

    [RelayCommand]
    private void ShowEditProfileDialog()
    {
        // Create a ManageEmployeesItem from current profile data for editing
        var currentEmployeeData = new ManageEmployeesItem
        {
            ID = EmployeeID,
            Name = EmployeeFullName,
            Position = EmployeePosition,
            Status = EmployeeStatus,
            ContactNumber = EmployeePhoneNumber,
            DateJoined = DateTime.Parse(EmployeeDateJoined),
            Username = "currentuser" // You might want to store this properly
        };

        Debug.WriteLine("Showing edit profile dialog with current employee data.");
        _addNewEmployeeDialogCardViewModel.InitializeForEditMode(currentEmployeeData);
        _dialogManager.CreateDialog(_addNewEmployeeDialogCardViewModel)
            .WithSuccessCallback(vm =>
                _toastManager.CreateToast("Modified Employee Details")
                    .WithContent($"You have successfully modified details")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Modifying Employee Details Cancelled")
                    .WithContent("Click the three-dots if you want to modify your employees' details")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(950)
            .Show();
        Debug.WriteLine("Edit profile dialog shown successfully.");
    }
}
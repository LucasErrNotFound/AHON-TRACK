﻿using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.ViewModels;

public sealed partial class AddNewEmployeeDialogCardViewModel : ViewModelBase
{
    [ObservableProperty]
    private string[] _middleInitialItems = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"]; // Will simplfy this later

    [ObservableProperty]
    private string[] _employeePositionItems = ["Gym Staff", "Gym Admin"];

    [ObservableProperty]
    private string[] _employeeStatusItems = ["Active", "Inactive", "Terminated"];

    [ObservableProperty]
    private string _dialogTitle = "Add Employee Details";

    [ObservableProperty]
    private string _dialogDescription = "Please fill out the form to create this employee's information";

    [ObservableProperty]
    private bool _isEditMode = false;

    private readonly DialogManager _dialogManager;

    // Personal Details Section
    private string _employeeFirstName = string.Empty;
    private string _selectedMiddleInitialItem = string.Empty;
    private string _employeeLastName = string.Empty;
    private string _employeeGender = string.Empty;
    private string _employeeContactNumber = string.Empty;
    private string _employeePosition = string.Empty;
    private int? _employeeAge;
    private DateTime? _employeeBirthDate;

    // Address Section
    private string _employeeHouseAddress = string.Empty;
    private string _employeeHouseNumber = string.Empty;
    private string _employeeStreet = string.Empty;
    private string _employeeBarangay = string.Empty;
    private string _employeeCityTown = string.Empty;
    private string _employeeProvince = string.Empty;

    // Account Section
    private string _employeeUsername = string.Empty;
    private string _employeePassword = string.Empty;
    private DateTime? _employeeDateJoined;
    private string _employeeStatus = string.Empty;

    [Required(ErrorMessage = "First name is required")]
    [MinLength(2, ErrorMessage = "Must be at least 2 characters long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string EmployeeFirstName
    {
        get => _employeeFirstName;
        set => SetProperty(ref _employeeFirstName, value, true);
    }

    [Required(ErrorMessage = "Select your MI")]
    public string SelectedMiddleInitialItem
    {
        get => _selectedMiddleInitialItem;
        set => SetProperty(ref _selectedMiddleInitialItem, value, true);
    }

    [Required(ErrorMessage = "Last name is required")]
    [MinLength(2, ErrorMessage = "Must be at least 2 characters long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string EmployeeLastName
    {
        get => _employeeLastName;
        set => SetProperty(ref _employeeLastName, value, true);
    }

    [Required(ErrorMessage = "Select your gender")]
    public string? EmployeeGender
    {
        get => _employeeGender;
        set
        {
            if (_employeeGender != value)
            {
                _employeeGender = value ?? string.Empty;
                OnPropertyChanged(nameof(EmployeeGender));
                OnPropertyChanged(nameof(IsMale));
                OnPropertyChanged(nameof(IsFemale));
            }
        }
    }

    public bool IsMale
    {
        get => EmployeeGender == "Male";
        set { if (value) EmployeeGender = "Male"; }
    }

    public bool IsFemale
    {
        get => EmployeeGender == "Female";
        set { if (value) EmployeeGender = "Female"; }
    }

    [Required(ErrorMessage = "Contact number is required")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Contact number must start with 09 and be 11 digits long")]
    public string EmployeeContactNumber
    {
        get => _employeeContactNumber;
        set => SetProperty(ref _employeeContactNumber, value, true);
    }

    [Required(ErrorMessage = "Position is required")]
    public string EmployeePosition
    {
        get => _employeePosition;
        set => SetProperty(ref _employeePosition, value, true);
    }

    [Required(ErrorMessage = "Age is required")]
    [Range(18, 80, ErrorMessage = "Age must be between 18 and 80")]
    public int? EmployeeAge
    {
        get => _employeeAge;
        set => SetProperty(ref _employeeAge, value, true);
    }

    [Required(ErrorMessage = "Birth date is required")]
    [System.ComponentModel.DataAnnotations.DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? EmployeeBirthDate
    {
        get => _employeeBirthDate;
        set => SetProperty(ref _employeeBirthDate, value, true);
    }

    [Required(ErrorMessage = "House address is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string EmployeeHouseAddress
    {
        get => _employeeHouseAddress;
        set => SetProperty(ref _employeeHouseAddress, value, true);
    }

    [Required(ErrorMessage = "House number is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 character long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string EmployeeHouseNumber
    {
        get => _employeeHouseNumber;
        set => SetProperty(ref _employeeHouseNumber, value, true);
    }

    [Required(ErrorMessage = "Street is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string EmployeeStreet
    {
        get => _employeeStreet;
        set => SetProperty(ref _employeeStreet, value, true);
    }

    [Required(ErrorMessage = "Barangay is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string EmployeeBarangay
    {
        get => _employeeBarangay;
        set => SetProperty(ref _employeeBarangay, value, true);
    }

    [Required(ErrorMessage = "City/Town is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string EmployeeCityTown
    {
        get => _employeeCityTown;
        set => SetProperty(ref _employeeCityTown, value, true);
    }

    [Required(ErrorMessage = "Province is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string EmployeeProvince
    {
        get => _employeeProvince;
        set => SetProperty(ref _employeeProvince, value, true);
    }

    [Required(ErrorMessage = "Username is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string EmployeeUsername
    {
        get => _employeeUsername;
        set => SetProperty(ref _employeeUsername, value, true);
    }

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Must be at least 8 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string EmployeePassword
    {
        get => _employeePassword;
        set => SetProperty(ref _employeePassword, value, true);
    }

    [Required(ErrorMessage = "Date joined is required")]
    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? EmployeeDateJoined
    {
        get => _employeeDateJoined;
        set => SetProperty(ref _employeeDateJoined, value, true);
    }

    [Required(ErrorMessage = "Status is required")]
    public string EmployeeStatus
    {
        get => _employeeStatus;
        set => SetProperty(ref _employeeStatus, value, true);
    }

    public AddNewEmployeeDialogCardViewModel(DialogManager dialogManager)
    {
        _dialogManager = dialogManager;
    }

    public AddNewEmployeeDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        IsEditMode = false;
        DialogTitle = "Add Employee Details";
        ClearAllFields();
    }

    public void InitializeForEditMode(ManageEmployeesItem? employee)
    {
        IsEditMode = true;
        DialogTitle = "Edit Employee Details";
        DialogDescription = "Please update the employee's information";

        ClearAllErrors();
        var nameParts = employee.Name?.Split(' ') ?? [];

        // Set personal details with default values
        EmployeeFirstName = nameParts.Length > 0 ? nameParts[0] : "John";
        SelectedMiddleInitialItem = nameParts.Length > 2 ? nameParts[1].TrimEnd('.') : "A";
        EmployeeLastName = nameParts.Length > 1 ? nameParts[^1] : "Doe"; // Last element

        // Set default values for fields not available in ManageEmployeesItem
        EmployeeGender = "Male";
        EmployeeContactNumber = !string.IsNullOrEmpty(employee.ContactNumber) ?
            employee.ContactNumber.Replace(" ", "") : "09123456789";
        EmployeePosition = !string.IsNullOrEmpty(employee.Position) ? employee.Position : "Gym Staff";
        EmployeeAge = 25; // Default age
        EmployeeBirthDate = DateTime.Now.AddYears(-25);

        EmployeeHouseAddress = "Sample House Address";
        EmployeeHouseNumber = "Blk 8 Lot 2";
        EmployeeStreet = "Sample Street";
        EmployeeBarangay = "Sample Barangay";
        EmployeeCityTown = "Sample City";
        EmployeeProvince = "Sample Province";

        EmployeeUsername = !string.IsNullOrEmpty(employee.Username) ? employee.Username : "defaultuser";
        EmployeePassword = "defaultpassword";
        EmployeeDateJoined = employee.DateJoined != default ? employee.DateJoined : DateTime.Now;
        EmployeeStatus = !string.IsNullOrEmpty(employee.Status) ? employee.Status : "Active";
    }


    [RelayCommand]
    private void SaveDetails()
    {
        ClearAllErrors();
        ValidateAllProperties();

        if (HasErrors) return;
        // Personal Details Debugging
        Debug.WriteLine($"First Name: {EmployeeFirstName}");
        Debug.WriteLine($"Middle Initial: {SelectedMiddleInitialItem}");
        Debug.WriteLine($"Last Name: {EmployeeLastName}");
        Debug.WriteLine($"Gender: {EmployeeGender}");
        Debug.WriteLine($"Contact No.: {EmployeeContactNumber}");
        Debug.WriteLine($"Position: {EmployeePosition}");
        Debug.WriteLine($"Age: {EmployeeAge}");
        Debug.WriteLine($"Date of Birth: {EmployeeBirthDate?.ToString("MMMM d, yyyy")}");

        // Address Details Debugging
        Debug.WriteLine($"\nHouse Adress: {EmployeeHouseAddress}");
        Debug.WriteLine($"House Number: {EmployeeHouseNumber}");
        Debug.WriteLine($"Street: {EmployeeStreet}");
        Debug.WriteLine($"Barangay: {EmployeeBarangay}");
        Debug.WriteLine($"City/Town: {EmployeeCityTown}");
        Debug.WriteLine($"Province: {EmployeeProvince}");

        // Account Details Debugging
        Debug.WriteLine($"\nUsername: {EmployeeUsername}");
        Debug.WriteLine($"Password: {EmployeePassword}");
        Debug.WriteLine($"Date Joined: {EmployeeDateJoined?.ToString("MMMM d, yyyy")}");
        Debug.WriteLine($"Status: {EmployeeStatus}");

        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }

    private void ClearAllFields()
    {
        EmployeeFirstName = string.Empty;
        SelectedMiddleInitialItem = string.Empty;
        EmployeeLastName = string.Empty;
        EmployeeGender = null;
        EmployeeContactNumber = string.Empty;
        EmployeePosition = string.Empty;
        EmployeeAge = null;
        EmployeeBirthDate = null;
        EmployeeHouseAddress = string.Empty;
        EmployeeHouseNumber = string.Empty;
        EmployeeStreet = string.Empty;
        EmployeeBarangay = string.Empty;
        EmployeeCityTown = string.Empty;
        EmployeeProvince = string.Empty;
        EmployeeUsername = string.Empty;
        EmployeePassword = string.Empty;
        EmployeeDateJoined = null;
        EmployeeStatus = string.Empty;
        ClearAllErrors();
    }
}

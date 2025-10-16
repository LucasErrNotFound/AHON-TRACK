using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace AHON_TRACK.Components.ViewModels;

public sealed partial class AddNewEmployeeDialogCardViewModel : ViewModelBase
{
    [ObservableProperty]
    private char[] _middleInitialItems = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    [ObservableProperty]
    private string[] _employeePositionItems = ["Gym Staff", "Gym Admin"];

    [ObservableProperty]
    private string[] _employeeStatusItems = ["Active", "Inactive", "Terminated"];

    [ObservableProperty]
    private string _dialogTitle = "Add Employee Details";

    [ObservableProperty]
    private string _dialogDescription = "Please fill out the form to create this employee's information";

    [ObservableProperty]
    private Image? _employeeProfileImageControl;

    [ObservableProperty]
    private bool _isEditMode = false;

    [ObservableProperty]
    private int? _editingEmployeeID;

    private readonly DialogManager _dialogManager;
    private readonly IEmployeeService _employeeService;
    private readonly ToastManager _toastManager;

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
            if (_employeeGender == value) return;
            _employeeGender = value ?? string.Empty;
            OnPropertyChanged(nameof(EmployeeGender));
            OnPropertyChanged(nameof(IsMale));
            OnPropertyChanged(nameof(IsFemale));
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
    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? EmployeeBirthDate
    {
        get => _employeeBirthDate;
        set
        {
            SetProperty(ref _employeeBirthDate, value, true);
            if (value.HasValue)
            {
                EmployeeAge = CalculateAge(value.Value);
            }
            else
            {
                _employeeAge = null;
                OnPropertyChanged(nameof(EmployeeAge));
            }
        }
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

    private byte[]? _profileImage;
    public byte[]? ProfileImage
    {
        get => _profileImage;
        set
        {
            SetProperty(ref _profileImage, value);
            Debug.WriteLine($"ProfileImage updated: {(value != null ? $"{value.Length} bytes" : "null")}");
        }
    }

    [ObservableProperty]
    private Bitmap? _profileImageSource;

    public AddNewEmployeeDialogCardViewModel(DialogManager dialogManager, IEmployeeService employeeService, ToastManager toastManager)
    {
        _dialogManager = dialogManager;
        _employeeService = employeeService;
        _toastManager = toastManager;
    }

    public AddNewEmployeeDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _employeeService = new EmployeeService("Data Source=LAPTOP-SSMJIDM6\\SQLEXPRESS08;Initial Catalog=AHON_TRACK;Integrated Security=True;Encrypt=True;Trust Server Certificate=True", _toastManager);
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        IsEditMode = false;
        DialogTitle = "Add Employee Details";
        DialogDescription = "Please fill out the form to create this employee's information";
        ClearAllFields();
    }

    public async Task InitializeForEditMode(ManageEmployeesItem? employee)
    {
        if (employee == null) return;

        IsEditMode = true;
        DialogTitle = "Edit Employee Details";
        DialogDescription = "Please update the employee's information";
        EditingEmployeeID = employee.ID;

        ClearAllErrors();

        // Load full employee data from database
        if (EditingEmployeeID.HasValue)
        {
            var (success, message, fullEmployee) = await _employeeService.ViewEmployeeProfileAsync(EditingEmployeeID.Value);

            if (success && fullEmployee != null)
            {
                // Personal Details
                EmployeeFirstName = fullEmployee.FirstName ?? string.Empty;
                SelectedMiddleInitialItem = fullEmployee.MiddleInitial?.Trim() ?? string.Empty;
                EmployeeLastName = fullEmployee.LastName ?? string.Empty;
                EmployeeGender = fullEmployee.Gender ?? "Male";
                EmployeeContactNumber = fullEmployee.ContactNumber ?? string.Empty;
                EmployeePosition = fullEmployee.Position ?? "Gym Staff";
                EmployeeAge = fullEmployee.Age;
                EmployeeBirthDate = fullEmployee.DateOfBirth;

                // Address
                EmployeeHouseAddress = fullEmployee.HouseAddress ?? string.Empty;
                EmployeeHouseNumber = fullEmployee.HouseNumber ?? string.Empty;
                EmployeeStreet = fullEmployee.Street ?? string.Empty;
                EmployeeBarangay = fullEmployee.Barangay ?? string.Empty;
                EmployeeCityTown = fullEmployee.CityTown ?? string.Empty;
                EmployeeProvince = fullEmployee.Province ?? string.Empty;

                // Account
                EmployeeUsername = fullEmployee.Username ?? string.Empty;
                EmployeePassword = fullEmployee.Password ?? "********";
                EmployeeDateJoined = fullEmployee.DateJoined;
                EmployeeStatus = fullEmployee.Status ?? "Active";

                // Profile Picture
                ProfileImage = fullEmployee.AvatarBytes;
                ProfileImageSource = fullEmployee.AvatarBytes != null
    ? ImageHelper.BytesToBitmap(fullEmployee.AvatarBytes)
    : ImageHelper.GetDefaultAvatar();

                Debug.WriteLine($"✅ Successfully loaded employee data: {fullEmployee.FirstName} {fullEmployee.LastName}");
                return;
            }
            _toastManager.CreateToast("Load Error")
                .WithContent($"Failed to load employee data: {message}")
                .DismissOnClick()
                .ShowError();

            Debug.WriteLine($"❌ Failed to load employee: {message}");
        }

        // Fallback to basic data
        var nameParts = employee.Name?.Split(' ') ?? [];
        EmployeeFirstName = nameParts.Length > 0 ? nameParts[0] : "John";
        SelectedMiddleInitialItem = nameParts.Length > 2 ? nameParts[1].TrimEnd('.') : "A";
        EmployeeLastName = nameParts.Length > 1 ? nameParts[^1] : "Doe";
        EmployeeGender = "Male";
        EmployeeContactNumber = employee.ContactNumber?.Replace(" ", "") ?? "09123456789";
        EmployeePosition = employee.Position ?? "Gym Staff";
        EmployeeAge = 25;
        EmployeeBirthDate = DateTime.Now.AddYears(-25);
        EmployeeHouseAddress = "Sample Address";
        EmployeeHouseNumber = "Block 1 Lot 1";
        EmployeeStreet = "Sample Street";
        EmployeeBarangay = "Sample Barangay";
        EmployeeCityTown = "Sample City";
        EmployeeProvince = "Sample Province";
        EmployeeUsername = employee.Username ?? "defaultuser";
        EmployeePassword = "defaultpassword";
        EmployeeDateJoined = employee.DateJoined != default ? employee.DateJoined : DateTime.Now;
        EmployeeStatus = employee.Status ?? "Active";

        Debug.WriteLine("⚠️ Using fallback data");
    }

    private int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }

    [RelayCommand]
    private async Task ChooseFile()
    {
        try
        {
            var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (toplevel == null) return;

            var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Image File",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = ["*.png", "*.jpg"]
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = ["*.*"]
                    }
                ]
            });

            if (files.Count > 0)
            {
                var selectedFile = files[0];
                _toastManager.CreateToast("Image file selected")
                    .WithContent($"{selectedFile.Name}")
                    .DismissOnClick()
                    .ShowInfo();

                var file = files[0];
                await using var stream = await file.OpenReadAsync();

                var bitmap = new Bitmap(stream);

                // Update UI control if present
                if (EmployeeProfileImageControl != null)
                {
                    EmployeeProfileImageControl.Source = bitmap;
                    EmployeeProfileImageControl.IsVisible = true;
                }

                // Convert to bytes for database storage
                stream.Position = 0;
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                ProfileImage = memoryStream.ToArray();
                ProfileImageSource = bitmap;

                Debug.WriteLine($"✅ Profile image loaded: {ProfileImage.Length} bytes");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error from uploading Picture: {ex.Message}");
            _toastManager?.CreateToast("Image Error")
                .WithContent($"Failed to load image: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task SaveDetails()
    {
        ClearAllErrors();
        ValidateAllProperties();

        if (HasErrors)
        {
            _toastManager?.CreateToast("Validation Error")
                .WithContent("Please fix all validation errors before saving.")
                .DismissOnClick()
                .ShowError();
            return;
        }

        try
        {
            var employee = new ManageEmployeeModel
            {
                EmployeeId = EditingEmployeeID ?? 0,
                FirstName = EmployeeFirstName,
                MiddleInitial = string.IsNullOrWhiteSpace(SelectedMiddleInitialItem) ? null : SelectedMiddleInitialItem,
                LastName = EmployeeLastName,
                Gender = EmployeeGender ?? string.Empty,
                ContactNumber = EmployeeContactNumber,
                Age = EmployeeAge ?? 0,
                DateOfBirth = EmployeeBirthDate,
                HouseAddress = EmployeeHouseAddress,
                HouseNumber = EmployeeHouseNumber,
                Street = EmployeeStreet,
                Barangay = EmployeeBarangay,
                CityTown = EmployeeCityTown,
                Province = EmployeeProvince,
                Username = EmployeeUsername,
                Password = EmployeePassword,
                DateJoined = EmployeeDateJoined ?? DateTime.Now,
                Status = EmployeeStatus,
                Position = EmployeePosition,
                ProfilePicture = ProfileImage ?? ImageHelper.BitmapToBytes(ImageHelper.GetDefaultAvatar())
            };

            Debug.WriteLine($"📸 Profile Picture: {(employee.ProfilePicture != null ? $"{employee.ProfilePicture.Length} bytes" : "null")}");

            if (IsEditMode && EditingEmployeeID.HasValue)
            {
                // UPDATE MODE
                var (success, message) = await _employeeService.UpdateEmployeeAsync(employee);

                if (success)
                {
                    _dialogManager.Close(this, new CloseDialogOptions { Success = true });
                }
                else
                {
                    _toastManager?.CreateToast("Update Failed")
                        .WithContent($"Failed to update employee: {message}")
                        .DismissOnClick()
                        .ShowError();
                }
            }
            else
            {
                // ADD MODE
                var (success, message, employeeId) = await _employeeService.AddEmployeeAsync(employee);

                if (success)
                {
                    Debug.WriteLine($"✅ New employee added with ID: {employeeId}");
                    _dialogManager.Close(this, new CloseDialogOptions { Success = true });
                }
                else
                {
                    _toastManager?.CreateToast("Add Failed")
                        .WithContent($"Failed to add employee: {message}")
                        .DismissOnClick()
                        .ShowError();
                }
            }
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Error")
                .WithContent($"Error: {ex.Message}")
                .DismissOnClick()
                .ShowError();

            Debug.WriteLine($"❌ Error saving employee: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
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
        ProfileImage = null;
        ProfileImageSource = ImageHelper.GetDefaultAvatar();
        EditingEmployeeID = null;
        ClearAllErrors();

        ImageResetRequested?.Invoke();
        Debug.WriteLine("🔄 All fields cleared");
    }

    public event Action? ImageResetRequested;
}
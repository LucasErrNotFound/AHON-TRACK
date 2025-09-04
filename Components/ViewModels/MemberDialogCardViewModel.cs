using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public partial class MemberDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty] 
    private char[] _middleInitialItems = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    [ObservableProperty]
    private string[] _memberPackageItems = ["Boxing", "Muay Thai", "Crossfit", "Zumba"];

    [ObservableProperty]
    private string[] _memberStatusItems = ["Active", "Inactive", "Terminated"];

    [ObservableProperty]
    private string _dialogTitle = "Edit Gym Member Details";

    [ObservableProperty]
    private string _dialogDescription = "Please fill out the form to edit this gym member's information";

    [ObservableProperty]
    private bool _isEditMode = false;
    
    // Personal Details Section
    private string _memberFirstName = string.Empty;
    private string _selectedMiddleInitialItem = string.Empty;
    private string _memberLastName = string.Empty;
    private string _memberGender = string.Empty;
    private string _memberContactNumber = string.Empty;
    private string _memberPackages = string.Empty;
    private int? _memberAge;
    private DateTime? _memberBirthDate;

    // Address Section
    private string _memberHouseAddress = string.Empty;
    private string _memberHouseNumber = string.Empty;
    private string _memberStreet = string.Empty;
    private string _memberBarangay = string.Empty;
    private string _memberCityTown = string.Empty;
    private string _memberProvince = string.Empty;

    // Account Section
    private DateTime? _memberDateJoined;
    private string _memberStatus = string.Empty;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    
    [Required(ErrorMessage = "First name is required")]
    [MinLength(2, ErrorMessage = "Must be at least 2 characters long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string MemberFirstName
    {
        get => _memberFirstName;
        set => SetProperty(ref _memberFirstName, value, true);
    }

    // [Required(ErrorMessage = "Select your MI")] -> I disabled this because apparently there are people who do not have middle name/initial
    public string SelectedMiddleInitialItem
    {
        get => _selectedMiddleInitialItem;
        set => SetProperty(ref _selectedMiddleInitialItem, value, true);
    }

    [Required(ErrorMessage = "Last name is required")]
    [MinLength(2, ErrorMessage = "Must be at least 2 characters long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string MemberLastName
    {
        get => _memberLastName;
        set => SetProperty(ref _memberLastName, value, true);
    }

    [Required(ErrorMessage = "Select your gender")]
    public string? MemberGender
    {
        get => _memberGender;
        set
        {
            if (_memberGender == value) return;
            _memberGender = value ?? string.Empty;
            OnPropertyChanged(nameof(MemberGender));
            OnPropertyChanged(nameof(IsMale));
            OnPropertyChanged(nameof(IsFemale));
        }
    }

    public bool IsMale
    {
        get => MemberGender == "Male";
        set { if (value) MemberGender = "Male"; }
    }

    public bool IsFemale
    {
        get => MemberGender == "Female";
        set { if (value) MemberGender = "Female"; }
    }

    [Required(ErrorMessage = "Contact number is required")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Contact number must start with 09 and be 11 digits long")]
    public string MemberContactNumber
    {
        get => _memberContactNumber;
        set => SetProperty(ref _memberContactNumber, value, true);
    }

    [Required(ErrorMessage = "Position is required")]
    public string MemberPackages
    {
        get => _memberPackages;
        set => SetProperty(ref _memberPackages, value, true);
    }

    [Required(ErrorMessage = "Age is required")]
    [Range(18, 80, ErrorMessage = "Age must be between 18 and 80")]
    public int? MemberAge
    {
        get => _memberAge;
        set => SetProperty(ref _memberAge, value, true);
    }

    [Required(ErrorMessage = "Birth date is required")]
    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? MemberBirthDate
    {
        get => _memberBirthDate;
        set
        {
            SetProperty(ref _memberBirthDate, value, true);
            if (value.HasValue)
            {
                MemberAge = CalculateAge(value.Value);
            }
            else
            {
                _memberAge = null;
                OnPropertyChanged(nameof(MemberAge));
            }
        }
    }

    [Required(ErrorMessage = "House address is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string MemberHouseAddress
    {
        get => _memberHouseAddress;
        set => SetProperty(ref _memberHouseAddress, value, true);
    }

    [Required(ErrorMessage = "House number is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 character long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string MemberHouseNumber
    {
        get => _memberHouseNumber;
        set => SetProperty(ref _memberHouseNumber, value, true);
    }

    [Required(ErrorMessage = "Street is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string MemberStreet
    {
        get => _memberStreet;
        set => SetProperty(ref _memberStreet, value, true);
    }

    [Required(ErrorMessage = "Barangay is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string MemberBarangay
    {
        get => _memberBarangay;
        set => SetProperty(ref _memberBarangay, value, true);
    }

    [Required(ErrorMessage = "City/Town is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string MemberCityTown
    {
        get => _memberCityTown;
        set => SetProperty(ref _memberCityTown, value, true);
    }

    [Required(ErrorMessage = "Province is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string MemberProvince
    {
        get => _memberProvince;
        set => SetProperty(ref _memberProvince, value, true);
    }
    
    [Required(ErrorMessage = "Date joined is required")]
    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? MemberDateJoined
    {
        get => _memberDateJoined;
        set => SetProperty(ref _memberDateJoined, value, true);
    }

    [Required(ErrorMessage = "Status is required")]
    public string MemberStatus
    {
        get => _memberStatus;
        set => SetProperty(ref _memberStatus, value, true);
    }

    public MemberDialogCardViewModel(DialogManager  dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public MemberDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        IsEditMode = false;
        DialogTitle = "Edit Gym Member Details";
        ClearAllFields();
    }
    
    private int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;

        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }
    
    [RelayCommand]
    private void SaveDetails()
    {
        ClearAllErrors();
        ValidateAllProperties();

        if (HasErrors) return;
        // Personal Details Debugging
        Debug.WriteLine($"First Name: {MemberFirstName}");
        Debug.WriteLine($"Middle Initial: {SelectedMiddleInitialItem}");
        Debug.WriteLine($"Last Name: {MemberLastName}");
        Debug.WriteLine($"Gender: {MemberGender}");
        Debug.WriteLine($"Contact No.: {MemberContactNumber}");
        Debug.WriteLine($"Position: {MemberPackages}");
        Debug.WriteLine($"Age: {MemberAge}");
        Debug.WriteLine($"Date of Birth: {MemberBirthDate?.ToString("MMMM d, yyyy")}");

        // Address Details Debugging
        Debug.WriteLine($"\nHouse Adress: {MemberHouseAddress}");
        Debug.WriteLine($"House Number: {MemberHouseNumber}");
        Debug.WriteLine($"Street: {MemberStreet}");
        Debug.WriteLine($"Barangay: {MemberBarangay}");
        Debug.WriteLine($"City/Town: {MemberCityTown}");
        Debug.WriteLine($"Province: {MemberProvince}");

        // Member Data Details Debugging
        Debug.WriteLine($"Date Joined: {MemberDateJoined?.ToString("MMMM d, yyyy")}");
        Debug.WriteLine($"Status: {MemberStatus}");

        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }

    private void ClearAllFields()
    {
        MemberFirstName = string.Empty;
        SelectedMiddleInitialItem = string.Empty;
        MemberLastName = string.Empty;
        MemberGender = null;
        MemberContactNumber = string.Empty;
        MemberPackages = string.Empty;
        MemberAge = null;
        MemberBirthDate = null;
        MemberHouseAddress = string.Empty;
        MemberHouseNumber = string.Empty;
        MemberStreet = string.Empty;
        MemberBarangay = string.Empty;
        MemberCityTown = string.Empty;
        MemberProvince = string.Empty;
        MemberDateJoined = null;
        MemberStatus = string.Empty;
        ClearAllErrors();
    }
}
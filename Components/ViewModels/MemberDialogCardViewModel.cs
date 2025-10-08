using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.ViewModels;
using System.Runtime.InteropServices;

namespace AHON_TRACK.Components.ViewModels;

public partial class MemberDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _middleInitialItems = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"];

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

    [ObservableProperty]
    private string _currentMemberId = string.Empty;

    // Personal Details Section
    private string _memberFirstName = string.Empty;
    private string _selectedMiddleInitialItem;
    private string _memberLastName = string.Empty;
    private string _memberGender = string.Empty;
    private string _memberContactNumber = string.Empty;
    private string _memberPackages = string.Empty;
    private int? _memberAge;
    private DateTime? _memberBirthDate;

    // Account Section
    private DateTime? _memberDateJoined;
    private string _memberStatus = string.Empty;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IMemberService _memberService;

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

    public MemberDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, IMemberService memberService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _memberService = memberService;
    }

    public MemberDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _memberService = null!;
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        IsEditMode = false;
        DialogTitle = "Edit Gym Member Details";
    }

    public async Task PopulateWithMemberDataAsync(string memberId)
    {
        CurrentMemberId = memberId;

        if (_memberService == null)
        {
            Debug.WriteLine("[PopulateWithMemberData] MemberService is null");
            return;
        }

        try
        {
            // Fetch full member data from database
            var memberData = await _memberService.GetMemberByIdAsync(memberId);

            if (memberData == null)
            {
                Debug.WriteLine($"[PopulateWithMemberData] No data found for member ID {memberId}");
                return;
            }

            // Populate all fields
            MemberFirstName = memberData.FirstName ?? string.Empty;
            SelectedMiddleInitialItem = memberData.MiddleInitial;
            MemberLastName = memberData.LastName ?? string.Empty;
            MemberGender = memberData.Gender ?? string.Empty;
            MemberContactNumber = memberData.ContactNumber?.Replace(" ", "") ?? string.Empty;
            MemberPackages = memberData.MembershipType ?? string.Empty;
            MemberAge = memberData.Age;
            MemberBirthDate = memberData.DateOfBirth;
            MemberStatus = memberData.Status ?? string.Empty;

            // Parse validity date
            if (!string.IsNullOrEmpty(memberData.Validity))
            {
                if (DateTime.TryParse(memberData.Validity, out var validityDate))
                {
                    MemberDateJoined = validityDate;
                }
            }

            IsEditMode = true;

            Debug.WriteLine($"[PopulateWithMemberData] Loaded member ID {memberId}");
            Debug.WriteLine($"  Name: {MemberFirstName} {SelectedMiddleInitialItem}. {MemberLastName}");
            Debug.WriteLine($"  Gender: {MemberGender}");
            Debug.WriteLine($"  DOB: {MemberBirthDate}");
            Debug.WriteLine($"  Contact: {MemberContactNumber}");
            Debug.WriteLine($"  Package: {MemberPackages}");
            Debug.WriteLine($"  Status: {MemberStatus}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PopulateWithMemberData] Error: {ex.Message}");
            _toastManager?.CreateToast("Load Error")
                .WithContent($"Failed to load member data: {ex.Message}")
                .ShowError();
        }
    }

    // Helper method to parse full name
    private (string FirstName, string MiddleInitial, string LastName) ParseFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return (string.Empty, string.Empty, string.Empty);

        var nameParts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (nameParts.Length == 0)
            return (string.Empty, string.Empty, string.Empty);

        if (nameParts.Length == 1)
            return (nameParts[0], string.Empty, string.Empty);

        // Look for middle initial (single character with optional dot)
        for (int i = 1; i < nameParts.Length - 1; i++)
        {
            string part = nameParts[i];
            if (part.Length == 1 || (part.Length == 2 && part.EndsWith(".")))
            {
                // Found middle initial
                string firstName = string.Join(" ", nameParts.Take(i));
                string middleInitial = part.TrimEnd('.');
                string lastName = string.Join(" ", nameParts.Skip(i + 1));
                return (firstName, middleInitial, lastName);
            }
        }

        // No middle initial found - first part is first name, rest is last name
        return (nameParts[0], string.Empty, string.Join(" ", nameParts.Skip(1)));
    }

    private int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;

        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }

    [RelayCommand]
    private async Task SaveDetails()
    {
        ClearAllErrors();
        ValidateAllProperties();

        if (HasErrors) return;

        try
        {
            // Create member model with updated data
            var memberModel = new ManageMemberModel
            {
                MemberID = int.Parse(CurrentMemberId),
                FirstName = MemberFirstName,
                MiddleInitial = SelectedMiddleInitialItem,
                LastName = MemberLastName,
                Gender = MemberGender,
                ContactNumber = MemberContactNumber,
                Age = MemberAge,
                DateOfBirth = MemberBirthDate,
                MembershipType = MemberPackages,
                Status = MemberStatus,
                Validity = MemberDateJoined?.ToString("MMM dd, yyyy")
            };

            // Update in database
            if (_memberService != null)
            {
                await _memberService.UpdateMemberAsync(CurrentMemberId, memberModel);
            }

            Debug.WriteLine($"[SaveDetails] Updated member ID: {CurrentMemberId}");
            Debug.WriteLine($"First Name: {MemberFirstName}");
            Debug.WriteLine($"Middle Initial: {SelectedMiddleInitialItem}");
            Debug.WriteLine($"Last Name: {MemberLastName}");
            Debug.WriteLine($"Gender: {MemberGender}");
            Debug.WriteLine($"Contact No.: {MemberContactNumber}");
            Debug.WriteLine($"Packages: {MemberPackages}");
            Debug.WriteLine($"Age: {MemberAge}");
            Debug.WriteLine($"Date of Birth: {MemberBirthDate?.ToString("MMMM d, yyyy")}");
            Debug.WriteLine($"Date Joined: {MemberDateJoined?.ToString("MMMM d, yyyy")}");
            Debug.WriteLine($"Status: {MemberStatus}");

            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveDetails] Error: {ex.Message}");
            _toastManager?.CreateToast("Update Failed")
                .WithContent($"Failed to update member: {ex.Message}")
                .ShowError();
        }
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
        MemberDateJoined = null;
        MemberStatus = string.Empty;
        ClearAllErrors();
    }
}
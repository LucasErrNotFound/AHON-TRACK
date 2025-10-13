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
    private int _currentMemberId = 0;  // Changed from string to int

    // Personal Details Section
    private string _memberFirstName = string.Empty;
    private string _selectedMiddleInitialItem;
    private string _memberLastName = string.Empty;
    private string _memberGender = string.Empty;
    private string _memberContactNumber = string.Empty;
    private string _memberPackages = string.Empty;
    private int? _memberAge;
    private DateTime? _memberBirthDate;
    private DateTime? _memberDateJoined;  // Unused, consider removing

    // Account Section
    private DateTime? _memberValidUntil;  // Changed from MemberDateJoined
    private string _memberStatus = string.Empty;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IMemberService _memberService;

    public DateTime? MemberDateJoined
    {
        get => _memberDateJoined;
        set => SetProperty(ref _memberDateJoined, value, true);
    }

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

    [Required(ErrorMessage = "Package is required")]
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

    [Required(ErrorMessage = "Valid until date is required")]
    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? MemberValidUntil  // Changed from MemberDateJoined
    {
        get => _memberValidUntil;
        set => SetProperty(ref _memberValidUntil, value, true);
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

    // Overload to accept string (for backward compatibility)
    public async Task PopulateWithMemberDataAsync(string memberId)
    {
        if (int.TryParse(memberId, out int id))
        {
            await PopulateWithMemberDataAsync(id);
        }
        else
        {
            Debug.WriteLine($"[PopulateWithMemberData] Invalid member ID: {memberId}");
            _toastManager?.CreateToast("Invalid ID")
                .WithContent("The member ID is not valid.")
                .DismissOnClick()
                .ShowError();
        }
    }

    // Primary method accepting int
    public async Task PopulateWithMemberDataAsync(int memberId)
    {
        CurrentMemberId = memberId;

        if (_memberService == null)
        {
            Debug.WriteLine("[PopulateWithMemberData] MemberService is null");
            return;
        }

        try
        {
            // Fetch full member data from database using new service method
            var result = await _memberService.GetMemberByIdAsync(memberId);

            if (!result.Success || result.Member == null)
            {
                Debug.WriteLine($"[PopulateWithMemberData] Failed to load member: {result.Message}");
                _toastManager?.CreateToast("Load Error")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            var memberData = result.Member;

            // Populate all fields
            MemberFirstName = memberData.FirstName ?? string.Empty;
            SelectedMiddleInitialItem = memberData.MiddleInitial ?? string.Empty;
            MemberLastName = memberData.LastName ?? string.Empty;
            MemberGender = memberData.Gender ?? string.Empty;
            MemberContactNumber = memberData.ContactNumber?.Replace(" ", "") ?? string.Empty;
            MemberPackages = memberData.MembershipType ?? string.Empty;
            MemberAge = memberData.Age;
            MemberBirthDate = memberData.DateOfBirth;
            MemberStatus = memberData.Status ?? "Active";

            // Parse ValidUntil date
            if (!string.IsNullOrEmpty(memberData.ValidUntil))
            {
                if (DateTime.TryParse(memberData.ValidUntil, out var validUntilDate))
                {
                    MemberValidUntil = validUntilDate;
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
            Debug.WriteLine($"  Valid Until: {MemberValidUntil}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PopulateWithMemberData] Error: {ex.Message}");
            _toastManager?.CreateToast("Load Error")
                .WithContent($"Failed to load member data: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
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

        if (HasErrors)
        {
            _toastManager?.CreateToast("Validation Error")
                .WithContent("Please fix all validation errors before saving.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        try
        {
            // Create member model with updated data
            var memberModel = new ManageMemberModel
            {
                MemberID = CurrentMemberId,
                FirstName = MemberFirstName,
                MiddleInitial = string.IsNullOrWhiteSpace(SelectedMiddleInitialItem) ? null : SelectedMiddleInitialItem,
                LastName = MemberLastName,
                Gender = MemberGender,
                ContactNumber = MemberContactNumber,
                Age = MemberAge,
                DateOfBirth = MemberBirthDate,
                ValidUntil = MemberValidUntil?.ToString("MMM dd, yyyy"),
                MembershipType = MemberPackages,
                Status = MemberStatus
            };

            // Update in database using new service method
            if (_memberService != null)
            {
                var result = await _memberService.UpdateMemberAsync(memberModel);

                if (!result.Success)
                {
                    _toastManager?.CreateToast("Update Failed")
                        .WithContent(result.Message)
                        .DismissOnClick()
                        .ShowError();
                    return;
                }
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
            Debug.WriteLine($"Valid Until: {MemberValidUntil?.ToString("MMMM d, yyyy")}");
            Debug.WriteLine($"Status: {MemberStatus}");

            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveDetails] Error: {ex.Message}");
            _toastManager?.CreateToast("Update Failed")
                .WithContent($"Failed to update member: {ex.Message}")
                .DismissOnClick()
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
        MemberValidUntil = null;
        MemberStatus = string.Empty;
        ClearAllErrors();
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AHON_TRACK.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public enum MemberViewContext
{
    AddNew,
    Upgrade,
    Renew
}

[Page("add-member")]
public partial class AddNewMemberViewModel : ViewModelBase, INavigable, INavigableWithParameters 
{
    [ObservableProperty] 
    private MemberViewContext _viewContext = MemberViewContext.AddNew; 
    
    [ObservableProperty] 
    private string[] _middleInitialItems = 
        ["A", "B", "C", "D", "E", "F", "G", "H",
            "I", "J", "K", "L", "M", "N", "O", "P", 
            "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"]; // Revert change because of char to string issue

    [ObservableProperty]
    private string[] _memberPackageItems = ["Boxing", "Muay Thai", "Crossfit", "Zumba"];

    [ObservableProperty]
    private string[] _memberStatusItems = ["Active", "Expired"];
    
    [ObservableProperty]
    private Image? _memberProfileImageControl;
    
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

    // Membership Plan 
    private int? _membershipDuration;
    private DateTime? _memberDateJoined;
    private string _memberStatus = string.Empty;
    
    // Payment Method
    private bool _isCashSelected;
    private bool _isGCashSelected;
    private bool _isMayaSelected;
    
    public bool IsCashVisible => IsCashSelected;
    public bool IsGCashVisible => IsGCashSelected;
    public bool IsMayaVisible => IsMayaSelected;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public string ViewTitle => ViewContext switch
    {
        MemberViewContext.AddNew => "Add New Member",
        MemberViewContext.Upgrade => "Upgrade Existing Member",
        MemberViewContext.Renew => "Renew Member",
        _ => "Add New Member"
    };

    public string ViewDescription => ViewContext switch
    {
        MemberViewContext.AddNew => "Add a new gym member by filling out the forms",
        MemberViewContext.Upgrade => "Upgrade an existing member's package and benefits",
        MemberViewContext.Renew => "Renew an existing member's subscription",
        _ => "Add a new gym member by filling out the forms"
    };

    public void SetViewContext(MemberViewContext context)
    {
        ViewContext = context;
        OnPropertyChanged(nameof(ViewTitle));
        OnPropertyChanged(nameof(ViewDescription));
    }
    
    [Required(ErrorMessage = "First name is required")]
    [MinLength(2, ErrorMessage = "Must be at least 2 characters long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string MemberFirstName
    {
        get => _memberFirstName;
        set
        {
            SetProperty(ref _memberFirstName, value, true);
            OnPropertyChanged(nameof(MemberFullName));
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    public string SelectedMiddleInitialItem
    {
        get => _selectedMiddleInitialItem;
        set
        {
            SetProperty(ref _selectedMiddleInitialItem, value, true);
            OnPropertyChanged(nameof(MemberFullName));
        }
    }

    [Required(ErrorMessage = "Last name is required")]
    [MinLength(2, ErrorMessage = "Must be at least 2 characters long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string MemberLastName
    {
        get => _memberLastName;
        set
        {
            SetProperty(ref _memberLastName, value, true);
            OnPropertyChanged(nameof(MemberFullName));
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
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
            OnPropertyChanged(nameof(IsPaymentPossible));
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
    [Range(3, 100, ErrorMessage = "Age must be between 3 and 100")]
    public int? MemberAge
    {
        get => _memberAge;
        set
        {
            SetProperty(ref _memberAge, value, true);
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
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
                MemberAge = null;
            }
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    [Required(ErrorMessage = "House address is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string MemberHouseAddress
    {
        get => _memberHouseAddress;
        set
        {
            SetProperty(ref _memberHouseAddress, value, true);
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    [Required(ErrorMessage = "House number is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 character long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string MemberHouseNumber
    {
        get => _memberHouseNumber;
        set
        {
            SetProperty(ref _memberHouseNumber, value, true);
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    [Required(ErrorMessage = "Street is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string MemberStreet
    {
        get => _memberStreet;
        set
        {
            SetProperty(ref _memberStreet, value, true);
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    [Required(ErrorMessage = "Barangay is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string MemberBarangay
    {
        get => _memberBarangay;
        set
        {
            SetProperty(ref _memberBarangay, value, true);
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    [Required(ErrorMessage = "City/Town is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string MemberCityTown
    {
        get => _memberCityTown;
        set
        {
            SetProperty(ref _memberCityTown, value, true);
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    [Required(ErrorMessage = "Province is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(20, ErrorMessage = "Must not exceed 20 characters")]
    public string MemberProvince
    {
        get => _memberProvince;
        set
        {
            SetProperty(ref _memberProvince, value, true);
            OnPropertyChanged(nameof(IsPaymentPossible));
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
    
    [Required(ErrorMessage = "Duration is required")]
    [Range(1, 12, ErrorMessage = "Duration must be between 1 and 12")]
    public int? MembershipDuration 
    {
        get => _membershipDuration;
        set
        {
            SetProperty(ref _membershipDuration, value, true);
            OnPropertyChanged(nameof(IsMembershipPlanVisible));
            OnPropertyChanged(nameof(MembershipDurationQuantity));
            OnPropertyChanged(nameof(MembershipDurationQuantityHeader));
            OnPropertyChanged(nameof(MembershipDurationQuantitySummary));
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }
    
    public bool IsCashSelected
    {
        get => _isCashSelected;
        set
        {
            if (_isCashSelected == value) return;
            _isCashSelected = value;
            
            if (value)
            {
                _isGCashSelected = false;
                _isMayaSelected = false;
            }
            
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsGCashSelected));
            OnPropertyChanged(nameof(IsMayaSelected));
            OnPropertyChanged(nameof(IsCashVisible));
            OnPropertyChanged(nameof(IsGCashVisible));
            OnPropertyChanged(nameof(IsMayaVisible));
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    public bool IsGCashSelected
    {
        get => _isGCashSelected;
        set
        {
            if (_isGCashSelected == value) return;
            _isGCashSelected = value;
            
            if (value)
            {
                _isCashSelected = false;
                _isMayaSelected = false;
            }
            
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCashSelected));
            OnPropertyChanged(nameof(IsMayaSelected));
            OnPropertyChanged(nameof(IsCashVisible));
            OnPropertyChanged(nameof(IsGCashVisible));
            OnPropertyChanged(nameof(IsMayaVisible));
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    public bool IsMayaSelected
    {
        get => _isMayaSelected;
        set
        {
            if (_isMayaSelected == value) return;
            _isMayaSelected = value;
            
            if (value)
            {
                _isCashSelected = false;
                _isGCashSelected = false;
            }
            
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCashSelected));
            OnPropertyChanged(nameof(IsGCashSelected));
            OnPropertyChanged(nameof(IsCashVisible));
            OnPropertyChanged(nameof(IsGCashVisible));
            OnPropertyChanged(nameof(IsMayaVisible));
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }
    
    public string MemberFullName 
    {
        get 
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(MemberFirstName))
                parts.Add(MemberFirstName);

            if (!string.IsNullOrWhiteSpace(SelectedMiddleInitialItem))
                parts.Add($"{SelectedMiddleInitialItem}.");

            if (!string.IsNullOrWhiteSpace(MemberLastName))
                parts.Add(MemberLastName.Trim());

            return parts.Count > 0 ? string.Join(" ", parts) : "Customer Name";
        }
    }

    public bool IsMembershipPlanVisible => MembershipDuration.HasValue && MembershipDuration > 0;
    
    public string MembershipDurationQuantity
    {
        get
        {
            if (!MembershipDuration.HasValue || MembershipDuration == 0)
                return "0 Sessions";

            int quantity = MembershipDuration.Value;
            return quantity == 1 ? $"{quantity} Month" : $"{quantity} Months";
        }
    }
    
    public string MembershipDurationQuantityHeader
    {
        get
        {
            if (!MembershipDuration.HasValue || MembershipDuration == 0)
                return "0 Sessions";

            int quantity = MembershipDuration.Value;
            return quantity == 1 ? $"{quantity} Month × ₱500.00" : $"{quantity} Months × ₱500.00";
        }
    }
    
    public string MembershipDurationQuantitySummary
    {
        get
        {
            if (!MembershipDuration.HasValue || MembershipDuration == 0)
                return "0 Sessions";

            int quantity = MembershipDuration.Value;
            return quantity == 1 ? $"Gym Membership ({quantity} Month)" : $"Gym Membership ({quantity} Months)";
        }
    }

    public AddNewMemberViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }
    
    public AddNewMemberViewModel() 
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }
    
    public void SetNavigationParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Context", out var context))
        {
            SetViewContext((MemberViewContext)context);
        }

        if (!parameters.TryGetValue("SelectedMember", out var member)) return;
        var selectedMember = (ManageMembersItem)member;
        PopulateFormWithMemberData(selectedMember);
    }
    
    private void PopulateFormWithMemberData(ManageMembersItem member)
    {
        // Remove whitespaces from contact number
        MemberContactNumber = member.ContactNumber.Replace(" ", "");

        // Parse the name using the enhanced algorithm
        var nameResult = ParseFullName(member.Name);
    
        MemberFirstName = nameResult.FirstName;
        SelectedMiddleInitialItem = nameResult.MiddleInitial;
        MemberLastName = nameResult.LastName;

        MemberPackages = member.AvailedPackages;
        MemberStatus = member.Status;
    }

    private (string FirstName, string MiddleInitial, string LastName) ParseFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return (string.Empty, string.Empty, string.Empty);

        var nameParts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (nameParts.Length == 0)
            return (string.Empty, string.Empty, string.Empty);

        if (nameParts.Length == 1)
        {
            // Only one part - treat as first name
            return (nameParts[0], string.Empty, string.Empty);
        }

        // Look for middle initial (single character with optional dot, not at first or last position)
        int middleInitialIndex = FindMiddleInitialIndex(nameParts);

        if (middleInitialIndex != -1)
        {
            // Found middle initial - everything before is first name, everything after is last name
            string firstName = string.Join(" ", nameParts.Take(middleInitialIndex));
            string middleInitial = nameParts[middleInitialIndex].TrimEnd('.');
            string lastName = string.Join(" ", nameParts.Skip(middleInitialIndex + 1));
        
            return (firstName, middleInitial, lastName);
        }

        // No middle initial found - use strategy to split first and last name
        return SplitFirstAndLastName(nameParts);
    }

    private int FindMiddleInitialIndex(string[] nameParts)
    {
        // Look for middle initial (not at first or last position)
        for (int i = 1; i < nameParts.Length - 1; i++)
        {
            string part = nameParts[i];
        
            // Check if it's a single character or single character with dot
            if (part.Length == 1 || (part.Length == 2 && part.EndsWith(".")))
            {
                return i;
            }
        }
    
        return -1; // No middle initial found
    }

    private (string FirstName, string MiddleInitial, string LastName) SplitFirstAndLastName(string[] nameParts)
    {
        // Strategy for ambiguous cases without middle initial
        // This addresses the "Juan Dela Cruz" ambiguity
    
        if (nameParts.Length == 2)
        {
            // Simple case: First Last
            return (nameParts[0], string.Empty, nameParts[1]);
        }

        // For 3+ parts without middle initial, we need a strategy
        // Option 1: Favor compound last names (common in Filipino names like "Dela Cruz", "De Leon")
        if (HasCompoundLastNamePattern(nameParts))
        {
            return SplitWithCompoundLastName(nameParts);
        }

        // Option 2: Default strategy - first part is first name, rest is last name
        return (nameParts[0], string.Empty, string.Join(" ", nameParts.Skip(1)));
    }

    private bool HasCompoundLastNamePattern(string[] nameParts)
    {
        // Common Filipino compound last name prefixes
        var compoundPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "de", "del", "dela", "delos", "delas", "van", "von", "da", "di", "du",
            "san", "santa", "santo", "mc", "mac", "o'"
        };

        // Check if any part (except the first) starts with a compound prefix
        for (int i = 1; i < nameParts.Length; i++)
        {
            if (compoundPrefixes.Contains(nameParts[i]))
            {
                return true;
            }
        }

        return false;
    }

    private (string FirstName, string MiddleInitial, string LastName) SplitWithCompoundLastName(string[] nameParts)
    {
        var compoundPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "de", "del", "dela", "delos", "delas", "van", "von", "da", "di", "du",
            "san", "santa", "santo", "mc", "mac", "o'"
        };

        // Find the first compound prefix
        for (int i = 1; i < nameParts.Length; i++)
        {
            if (compoundPrefixes.Contains(nameParts[i]))
            {
                // Everything before this index is first name
                // Everything from this index onwards is last name
                string firstName = string.Join(" ", nameParts.Take(i));
                string lastName = string.Join(" ", nameParts.Skip(i));
                return (firstName, string.Empty, lastName);
            }
        }

        // Fallback - shouldn't reach here if HasCompoundLastNamePattern returned true
        return (nameParts[0], string.Empty, string.Join(" ", nameParts.Skip(1)));
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
                
                if (MemberProfileImageControl != null)
                {
                    MemberProfileImageControl.Source = bitmap;
                    MemberProfileImageControl.IsVisible = true;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error from uploading Picture: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _toastManager.CreateToast("Add new member cancelled").ShowWarning();
        _pageManager.Navigate<ManageMembershipViewModel>();
    }
    
    [RelayCommand]
    private void Payment() 
    {
        _toastManager.CreateToast("Payment Successful!").ShowSuccess();
        _pageManager.Navigate<ManageMembershipViewModel>();
    }
    
    public bool IsPaymentPossible 
    {
        get
        {
            bool hasValidInputs = !string.IsNullOrWhiteSpace(MemberFirstName)
                                  && !string.IsNullOrWhiteSpace(MemberLastName)
                                  && !string.IsNullOrWhiteSpace(MemberContactNumber)
                                  && ContactNumberRegex().IsMatch(MemberContactNumber)
                                  && (MemberAge >= 18 && MemberAge <= 80)
                                  && !string.IsNullOrWhiteSpace(MemberGender);

            bool hasValidQuantity = (MembershipDuration.HasValue && MembershipDuration > 0);
            bool hasPaymentMethod = IsCashSelected || IsGCashSelected || IsMayaSelected;

            return hasValidInputs && hasValidQuantity && hasPaymentMethod;
        }
    }
    
    private int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;

        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
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
    
    [GeneratedRegex(@"^09\d{9}$")]
    private static partial Regex ContactNumberRegex();
}
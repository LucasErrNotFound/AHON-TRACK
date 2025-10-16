using AHON_TRACK.Models;
using AHON_TRACK.ViewModels;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Converters;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
            "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"];

    [ObservableProperty]
    private List<PackageModel> _memberPackageItems = new();
    [ObservableProperty]
    private PackageModel? _selectedMemberPackageItem;

    [ObservableProperty]
    private string[] _memberStatusItems = ["Active", "Expired"];
    
    [ObservableProperty]
    private Bitmap? _profileImageSource;
    
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

    // Membership Plan 
    private int? _membershipDuration;
    private string _memberStatus = string.Empty;

    // Payment Method
    private bool _isCashSelected;
    private bool _isGCashSelected;
    private bool _isMayaSelected;

    // Status Selection
    private bool _isActiveSelected = true; // Default to Active
    private bool _isInactiveSelected;
    private bool _isTerminatedSelected;

    public bool IsCashVisible => IsCashSelected;
    public bool IsGCashVisible => IsGCashSelected;
    public bool IsMayaVisible => IsMayaSelected;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IMemberService _memberService;

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

    [Required(ErrorMessage = "Package is required")]
    public string MemberPackages
    {
        get => _selectedMemberPackageItem?.packageName ?? _memberPackages;  // Changed from packageName to Name
        set
        {
            SetProperty(ref _memberPackages, value, true);
            // Try to find matching package in list
            if (MemberPackageItems.Any())
            {
                var package = MemberPackageItems.FirstOrDefault(p => p.packageName == value);  // Changed from packageName to Name
                if (package != null && _selectedMemberPackageItem != package)
                {
                    _selectedMemberPackageItem = package;
                    OnPropertyChanged(nameof(SelectedMemberPackageItem));
                }
            }
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

    /*public string MemberPackages
     {
         get => _memberPackages;
         set => SetProperty(ref _memberPackages, value, true);
     }*/

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

    public bool IsActiveSelected
    {
        get => _isActiveSelected;
        set
        {
            if (_isActiveSelected == value) return;
            _isActiveSelected = value;

            if (value)
            {
                _isInactiveSelected = false;
                _isTerminatedSelected = false;
                OnPropertyChanged(nameof(IsInactiveSelected));
                OnPropertyChanged(nameof(IsTerminatedSelected));
            }

            OnPropertyChanged();
        }
    }

    public bool IsInactiveSelected
    {
        get => _isInactiveSelected;
        set
        {
            if (_isInactiveSelected == value) return;
            _isInactiveSelected = value;

            if (value)
            {
                _isActiveSelected = false;
                _isTerminatedSelected = false;
                OnPropertyChanged(nameof(IsActiveSelected));
                OnPropertyChanged(nameof(IsTerminatedSelected));
            }

            OnPropertyChanged();
        }
    }

    public bool IsTerminatedSelected
    {
        get => _isTerminatedSelected;
        set
        {
            if (_isTerminatedSelected == value) return;
            _isTerminatedSelected = value;

            if (value)
            {
                _isActiveSelected = false;
                _isInactiveSelected = false;
                OnPropertyChanged(nameof(IsActiveSelected));
                OnPropertyChanged(nameof(IsInactiveSelected));
            }

            OnPropertyChanged();
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

    public AddNewMemberViewModel(IMemberService memberService, DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _memberService = memberService;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public AddNewMemberViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _memberService = null!;
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        IsActiveSelected = true;
        await LoadPackagesAsync();
    }

    // Fix 3: Correct LoadPackagesAsync method
    private async Task LoadPackagesAsync()
    {
        if (_memberService == null)
        {
            // Fallback to hardcoded packages if service is not available
            MemberPackageItems = new List<PackageModel>
        {
            new PackageModel { packageID = 1, packageName = "Boxing" },
            new PackageModel { packageID = 2, packageName = "Muay Thai" },
            new PackageModel { packageID = 3, packageName = "Crossfit" },
            new PackageModel { packageID = 4, packageName = "Zumba" }
        };
            return;
        }

        try
        {
            var result = await _memberService.GetAllPackagesAsync();  // Changed method name
            if (result.Success && result.Packages != null && result.Packages.Any())
            {
                MemberPackageItems = result.Packages;
            }
            else
            {
                // Fallback to default packages
                MemberPackageItems = new List<PackageModel>
            {
                new PackageModel { packageID = 1, packageName = "Boxing" },
                new PackageModel { packageID = 2, packageName = "Muay Thai" },
                new PackageModel { packageID = 3, packageName = "Crossfit" },
                new PackageModel { packageID = 4, packageName = "Zumba" }
            };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load packages: {ex.Message}");
            // Fallback to default packages - FIXED TYPE HERE
            MemberPackageItems = new List<PackageModel>  // Changed from List<PackageItem>
        {
            new PackageModel { packageID = 1, packageName = "Boxing" },
            new PackageModel { packageID = 2, packageName = "Muay Thai" },
            new PackageModel { packageID = 3, packageName = "Crossfit" },
            new PackageModel { packageID = 4, packageName = "Zumba" }
        };
        }
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
        MemberContactNumber = member.ContactNumber.Replace(" ", "");

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
            return (nameParts[0], string.Empty, string.Empty);
        }

        int middleInitialIndex = FindMiddleInitialIndex(nameParts);

        if (middleInitialIndex != -1)
        {
            string firstName = string.Join(" ", nameParts.Take(middleInitialIndex));
            string middleInitial = nameParts[middleInitialIndex].TrimEnd('.');
            string lastName = string.Join(" ", nameParts.Skip(middleInitialIndex + 1));

            return (firstName, middleInitial, lastName);
        }

        return SplitFirstAndLastName(nameParts);
    }

    private int FindMiddleInitialIndex(string[] nameParts)
    {
        for (int i = 1; i < nameParts.Length - 1; i++)
        {
            string part = nameParts[i];

            if (part.Length == 1 || (part.Length == 2 && part.EndsWith(".")))
            {
                return i;
            }
        }

        return -1;
    }

    private (string FirstName, string MiddleInitial, string LastName) SplitFirstAndLastName(string[] nameParts)
    {
        if (nameParts.Length == 2)
        {
            return (nameParts[0], string.Empty, nameParts[1]);
        }

        if (HasCompoundLastNamePattern(nameParts))
        {
            return SplitWithCompoundLastName(nameParts);
        }

        return (nameParts[0], string.Empty, string.Join(" ", nameParts.Skip(1)));
    }

    private bool HasCompoundLastNamePattern(string[] nameParts)
    {
        var compoundPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "de", "del", "dela", "delos", "delas", "van", "von", "da", "di", "du",
            "san", "santa", "santo", "mc", "mac", "o'"
        };

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

        for (int i = 1; i < nameParts.Length; i++)
        {
            if (compoundPrefixes.Contains(nameParts[i]))
            {
                string firstName = string.Join(" ", nameParts.Take(i));
                string lastName = string.Join(" ", nameParts.Skip(i));
                return (firstName, string.Empty, lastName);
            }
        }

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
                        Patterns = ["*.png", "*.jpg", "*.jpeg"]
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

                stream.Position = 0;
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                ProfileImage = memoryStream.ToArray();
                ProfileImageSource = bitmap;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error from uploading Picture: {ex.Message}");
            _toastManager?.CreateToast("Upload Error")
                .WithContent($"Failed to upload image: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _toastManager.CreateToast("Add new member cancelled").ShowWarning();
        _pageManager.Navigate<ManageMembershipViewModel>();
    }

    private string? GetSelectedPaymentMethod()
    {
        if (IsCashSelected) return "Cash";
        if (IsGCashSelected) return "GCash";
        if (IsMayaSelected) return "Maya";
        return null;
    }

    private string? GetSelectedStatus()
    {
        if (IsActiveSelected) return "Active";
        if (IsInactiveSelected) return "Inactive";
        if (IsTerminatedSelected) return "Terminated";
        return "Active"; // Default to Active
    }

    [RelayCommand]
    private async Task Payment()
    {
        try
        {
            // Calculate ValidUntil date based on membership duration
            DateTime? validUntilDate = null;
            if (MembershipDuration.HasValue && MembershipDuration.Value > 0)
            {
                validUntilDate = DateTime.Now.AddMonths(MembershipDuration.Value);
            }

            // Get PackageID from selected package
            int? packageId = SelectedMemberPackageItem?.packageID;

            // If no package selected but MemberPackages has value, try to find it
            if (packageId == null && !string.IsNullOrEmpty(MemberPackages))
            {
                var package = MemberPackageItems.FirstOrDefault(p =>
                    p.packageName.Equals(MemberPackages, StringComparison.OrdinalIgnoreCase));
                packageId = package?.packageID;
            }

            var member = new ManageMemberModel
            {
                FirstName = MemberFirstName,
                MiddleInitial = string.IsNullOrWhiteSpace(SelectedMiddleInitialItem) ? null : SelectedMiddleInitialItem,
                LastName = MemberLastName,
                Gender = MemberGender,
                ContactNumber = MemberContactNumber,
                Age = MemberAge,
                DateOfBirth = MemberBirthDate,
                ValidUntil = validUntilDate?.ToString("MMM dd, yyyy"),
                PackageID = packageId,  // NOW USING PACKAGEID
                MembershipType = MemberPackages,  // Keep name for display
                Status = GetSelectedStatus() ?? "Active",
                PaymentMethod = GetSelectedPaymentMethod(),
                ProfilePicture = ProfileImage ?? ImageHelper.BitmapToBytes(ImageHelper.GetDefaultAvatar())
            };

            // Use new service method
            var result = await _memberService.AddMemberAsync(member);

            if (!result.Success)
            {
                _toastManager?.CreateToast("Registration Failed")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            Debug.WriteLine($"[Payment] Member registered successfully with ID: {result.MemberId}");
            Debug.WriteLine($"[Payment] Package ID: {packageId}, Package Name: {MemberPackages}");

            _toastManager?.CreateToast("Payment Successful!")
                .WithContent($"Member {member.FirstName} {member.LastName} registered successfully.")
                .DismissOnClick()
                .ShowSuccess();

            ClearAllFields();
            _pageManager.Navigate<ManageMembershipViewModel>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Payment] Error saving member: {ex.Message}");
            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to save member: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
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
        MemberStatus = string.Empty;
        MembershipDuration = null;
        IsCashSelected = false;
        IsGCashSelected = false;
        IsMayaSelected = false;
        IsActiveSelected = true;
        IsInactiveSelected = false;
        IsTerminatedSelected = false;
        ProfileImage = null;
        ProfileImageSource = ImageHelper.GetDefaultAvatar();

        ClearAllErrors();
        
        ImageResetRequested?.Invoke();
    }

    [GeneratedRegex(@"^09\d{9}$")]
    private static partial Regex ContactNumberRegex();
    
    public event Action? ImageResetRequested;
}
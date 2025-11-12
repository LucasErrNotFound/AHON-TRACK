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
using AHON_TRACK.Services.Events;
using AHON_TRACK.Validators;

namespace AHON_TRACK.Components.ViewModels;

public enum MemberViewContext
{
    AddNew,
    Upgrade,
    Renew
}

[Page("add-member")]
public partial class AddNewMemberViewModel : ViewModelBase, INavigableWithParameters
{
    [ObservableProperty]
    private MemberViewContext _viewContext = MemberViewContext.AddNew;

    [ObservableProperty]
    private string[] _middleInitialItems =
        ["A", "B", "C", "D", "E", "F", "G", "H",
            "I", "J", "K", "L", "M", "N", "O", "P",
            "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"];

    [ObservableProperty]
    private List<SellingModel> _monthlyPackages = new();

    [ObservableProperty]
    private SellingModel? _selectedMonthlyPackage;

    [ObservableProperty]
    private string[] _memberStatusItems = ["Active", "Expired"];

    [ObservableProperty]
    private Bitmap? _profileImageSource = ImageHelper.GetDefaultAvatar();

    [ObservableProperty]
    private Image? _memberProfileImageControl;

    [ObservableProperty]
    private Image? _memberProfileImageControl2;
    
    [ObservableProperty]
    private TextBox? _letterConsentTextBoxControl1;
    
    [ObservableProperty]
    private TextBox? _letterConsentTextBoxControl2;
    
    [ObservableProperty]
    private string? _consentFilePath;

    // Personal Details Section
    private string _memberFirstName = string.Empty;
    private string _selectedMiddleInitialItem = string.Empty;
    private string _memberLastName = string.Empty;
    private string? _memberGender = string.Empty;
    private string _memberContactNumber = string.Empty;
    private int? _memberAge;
    private string? _referenceNumber = string.Empty;
    private int _selectedMemberId = 0;
    private DateTime? _memberBirthDate;
    private DateTime _currentMemberValidUntil = DateTime.MinValue;

    // Membership Plan 
    private int? _membershipDuration;
    private string _memberStatus = string.Empty;

    // Payment Method
    private bool _isCashSelected;
    private bool _isGCashSelected;
    private bool _isMayaSelected;

    // Status Selection
    private bool _isActiveSelected = true;
    private bool _isInactiveSelected;
    private bool _isTerminatedSelected;

    public bool IsCashVisible => IsCashSelected;
    public bool IsGCashVisible => IsGCashSelected;
    public bool IsMayaVisible => IsMayaSelected;
    public bool IsReferenceNumberVisible => IsMayaSelected || IsGCashSelected;

    public bool IsReferenceNumberVisibleInReceipt =>
        (IsGCashSelected || IsMayaSelected) && !string.IsNullOrWhiteSpace(ReferenceNumber);
    
    public bool IsMinor => MemberAge.HasValue && MemberAge >= 3 && MemberAge <= 14;
    public bool IsConsentLetterRequired => IsMinor && string.IsNullOrWhiteSpace(ConsentFilePath);
    public bool IsConsentFileSelected => !string.IsNullOrWhiteSpace(ConsentFilePath);

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
    [RegularExpression(@"^[a-zA-Z ]*$", ErrorMessage = "Alphabets only.")]
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
    [RegularExpression(@"^[a-zA-Z ]*$", ErrorMessage = "Alphabets only.")]
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
            SetProperty(ref _memberGender, value, true);
            OnPropertyChanged(nameof(IsMale));
            OnPropertyChanged(nameof(IsFemale));
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    public bool IsMale
    {
        get => MemberGender == "Male";
        set
        {
            if (value)
                MemberGender = "Male";
            else if (MemberGender == "Male")
                MemberGender = string.Empty;
        }
    }

    public bool IsFemale
    {
        get => MemberGender == "Female";
        set
        {
            if (value)
                MemberGender = "Female";
            else if (MemberGender == "Female")
                MemberGender = string.Empty;
        }
    }

    [Required(ErrorMessage = "Contact number is required")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Contact number must start with 09 and be 11 digits long")]
    public string MemberContactNumber
    {
        get => _memberContactNumber;
        set => SetProperty(ref _memberContactNumber, value, true);
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
            OnPropertyChanged(nameof(IsMinor));
            OnPropertyChanged(nameof(IsConsentLetterRequired));
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

                // ✅ DON'T CLEAR CONSENT FILE PATH WHEN UPGRADING/RENEWING
                if (ViewContext == MemberViewContext.AddNew)
                {
                    ConsentFilePath = null;
                }
            }
            else
            {
                MemberAge = null;

                // ✅ DON'T CLEAR CONSENT FILE PATH WHEN UPGRADING/RENEWING
                if (ViewContext == MemberViewContext.AddNew)
                {
                    ConsentFilePath = null;
                }
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

            OnPropertyChanged(nameof(PackageDurationDisplay));
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(ValidFromDate));
            OnPropertyChanged(nameof(ValidUntilDate));
            OnPropertyChanged(nameof(ValidityFrom));
            OnPropertyChanged(nameof(ValidityTo));
            OnPropertyChanged(nameof(MembershipDurationSummary));

            _ = AutoSelectMonthlyPackageAsync();
        }
    }

    [UppercaseReferenceNumberValidator]
    public string? ReferenceNumber
    {
        get => _referenceNumber;
        set
        {
            SetProperty(ref _referenceNumber, value, true);
            OnPropertyChanged(nameof(IsPaymentPossible));
            OnPropertyChanged(nameof(IsReferenceNumberVisibleInReceipt));
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
                ReferenceNumber = null;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsGCashSelected));
            OnPropertyChanged(nameof(IsMayaSelected));
            OnPropertyChanged(nameof(IsCashVisible));
            OnPropertyChanged(nameof(IsGCashVisible));
            OnPropertyChanged(nameof(IsMayaVisible));
            OnPropertyChanged(nameof(IsReferenceNumberVisible));
            OnPropertyChanged(nameof(IsReferenceNumberVisibleInReceipt));
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
            OnPropertyChanged(nameof(IsReferenceNumberVisible));
            OnPropertyChanged(nameof(IsReferenceNumberVisibleInReceipt));
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
            OnPropertyChanged(nameof(IsReferenceNumberVisible));
            OnPropertyChanged(nameof(IsReferenceNumberVisibleInReceipt));
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
                return "0 Months";

            int quantity = MembershipDuration.Value;
            return quantity == 1 ? $"{quantity} Month" : $"{quantity} Months";
        }
    }

    public string MembershipDurationQuantityHeader
    {
        get
        {
            if (!MembershipDuration.HasValue || MembershipDuration == 0 || SelectedMonthlyPackage == null)
                return "0 Months";

            int quantity = MembershipDuration.Value;
            decimal price = SelectedMonthlyPackage.Price;
            return quantity == 1 ? $"{quantity} Month × ₱{price:N2}" : $"{quantity} Months × ₱{price:N2}";
        }
    }

    public string MembershipDurationQuantitySummary
    {
        get
        {
            if (!MembershipDuration.HasValue || MembershipDuration == 0 || SelectedMonthlyPackage == null)
                return "No package selected";

            int quantity = MembershipDuration.Value;
            string packageName = SelectedMonthlyPackage.Title;
            return quantity == 1
                ? $"{packageName} ({quantity} Month)"
                : $"{packageName} ({quantity} Months)";
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

        if (ViewContext == MemberViewContext.AddNew)
        {
            ProfileImageSource = ManageMemberModel.DefaultAvatarSource;
            ProfileImage = null;
        }

        await LoadMonthlyPackagesAsync();
    }

    private async Task LoadMonthlyPackagesAsync()
    {
        try
        {
            Debug.WriteLine("🔹 Loading monthly membership packages for members...");

            var packages = await _memberService.GetAvailablePackagesForMembersAsync();

            if (packages != null && packages.Any())
            {
                MonthlyPackages = packages;

                Debug.WriteLine($"✅ Loaded {MonthlyPackages.Count} monthly packages:");
                foreach (var pkg in MonthlyPackages)
                {
                    Debug.WriteLine($"  📦 {pkg.Title} - ₱{pkg.Price}");
                }
            }
            else
            {
                Debug.WriteLine("⚠️ No packages found or failed to load");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error loading monthly packages: {ex.Message}");
            _toastManager?.CreateToast("Load Error")
                .WithContent($"Failed to load membership packages: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private async Task AutoSelectMonthlyPackageAsync()
    {
        if (!MembershipDuration.HasValue || MembershipDuration <= 0)
        {
            SelectedMonthlyPackage = null;
            OnPropertyChanged(nameof(MembershipDurationQuantityHeader));
            OnPropertyChanged(nameof(MembershipDurationQuantitySummary));

            OnPropertyChanged(nameof(PackageDisplayName));
            OnPropertyChanged(nameof(PackagePriceDisplay));
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(ValidFromDate));
            OnPropertyChanged(nameof(ValidUntilDate));
            OnPropertyChanged(nameof(ValidityFrom));
            OnPropertyChanged(nameof(ValidityTo));
            OnPropertyChanged(nameof(MembershipDurationSummary));

            return;
        }

        if (!MonthlyPackages.Any())
        {
            await LoadMonthlyPackagesAsync();
        }

        if (MonthlyPackages.Any())
        {
            SelectedMonthlyPackage = MonthlyPackages.First();

            OnPropertyChanged(nameof(MembershipDurationQuantityHeader));
            OnPropertyChanged(nameof(MembershipDurationQuantitySummary));
            OnPropertyChanged(nameof(IsPaymentPossible));

            OnPropertyChanged(nameof(PackageDisplayName));
            OnPropertyChanged(nameof(PackagePriceDisplay));
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(ValidFromDate));
            OnPropertyChanged(nameof(ValidUntilDate));
            OnPropertyChanged(nameof(ValidityFrom));
            OnPropertyChanged(nameof(ValidityTo));
            OnPropertyChanged(nameof(MembershipDurationSummary));

            Debug.WriteLine($"✅ Auto-selected package: {SelectedMonthlyPackage.Title} - ₱{SelectedMonthlyPackage.Price}/month");
            Debug.WriteLine($"   Total for {MembershipDuration} months: ₱{SelectedMonthlyPackage.Price * MembershipDuration:N2}");
        }
        else
        {
            Debug.WriteLine("⚠️ No monthly packages available to auto-select");
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
        if (int.TryParse(selectedMember.ID, out int memberId))
        {
            _selectedMemberId = memberId;
        }
        PopulateFormWithMemberData(selectedMember);
    }

    private void PopulateFormWithMemberData(ManageMembersItem member)
    {
        MemberContactNumber = member.ContactNumber.Replace(" ", "");

        var nameResult = ParseFullName(member.Name);

        MemberFirstName = nameResult.FirstName;
        SelectedMiddleInitialItem = nameResult.MiddleInitial;
        MemberLastName = nameResult.LastName;
        MemberStatus = member.Status;
        MemberGender = member.Gender;
        ConsentFilePath = member.ConsentLetter;

        if (member.BirthDate != DateTime.MinValue)
        {
            MemberBirthDate = member.BirthDate;
            MemberAge = CalculateAge(member.BirthDate);
        }

        if (member.Validity != DateTime.MinValue)
        {
            _currentMemberValidUntil = member.Validity;
        }

        ProfileImageSource = member.AvatarSource;

        if (member.AvatarSource != ManageMemberModel.DefaultAvatarSource)
        {
            ProfileImage = ImageHelper.BitmapToBytes(member.AvatarSource);
            Debug.WriteLine($"[PopulateFormWithMemberData] Loaded custom profile image ({ProfileImage?.Length} bytes)");
        }
        else
        {
            ProfileImage = null;
            Debug.WriteLine("[PopulateFormWithMemberData] Using default avatar");
        }

        OnPropertyChanged(nameof(ProfileImageSource));
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

                if (MemberProfileImageControl2 != null)
                {
                    MemberProfileImageControl2.Source = bitmap;
                    MemberProfileImageControl2.IsVisible = true;
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
        return "Active";
    }

    [RelayCommand]
    private async Task Payment()
    {
        try
        {
            // Validate that a monthly package is selected
            if (SelectedMonthlyPackage == null)
            {
                _toastManager?.CreateToast("Package Required")
                    .WithContent("Please enter membership duration to select a package.")
                    .DismissOnClick()
                    .ShowWarning();
                return;
            }

            // Validate membership duration
            if (!MembershipDuration.HasValue || MembershipDuration <= 0)
            {
                _toastManager?.CreateToast("Duration Required")
                    .WithContent("Please enter a valid membership duration (1-12 months).")
                    .DismissOnClick()
                    .ShowWarning();
                return;
            }

            // Calculate ValidUntil based on context
            DateTime validUntilDate;

            if (ViewContext == MemberViewContext.Renew || ViewContext == MemberViewContext.Upgrade)
            {
                DateTime currentValidUntil = _currentMemberValidUntil;

                if (currentValidUntil == DateTime.MinValue && _selectedMemberId > 0)
                {
                    var result = await _memberService.GetMemberByIdAsync(_selectedMemberId);
                    if (result.Success && result.Member != null)
                    {
                        DateTime.TryParse(result.Member.ValidUntil, out currentValidUntil);
                    }
                }

                if (currentValidUntil == DateTime.MinValue)
                {
                    validUntilDate = DateTime.Now.AddMonths(MembershipDuration.Value);
                    Debug.WriteLine($"[Payment] {ViewContext}: No existing date, starting from today → {validUntilDate:MMM dd, yyyy}");
                }
                else
                {
                    validUntilDate = currentValidUntil.AddMonths(MembershipDuration.Value).AddDays(1);

                    string status = currentValidUntil < DateTime.Now ? "expired" : "active";
                    int monthsAdded = MembershipDuration.Value;
                    Debug.WriteLine($"[Payment] {ViewContext}: Member {status}");
                    Debug.WriteLine($"  Current expiry: {currentValidUntil:MMM dd, yyyy}");
                    Debug.WriteLine($"  Adding: {monthsAdded} months");
                    Debug.WriteLine($"  New expiry: {validUntilDate:MMM dd, yyyy}");
                }
            }
            else
            {
                validUntilDate = DateTime.Now.AddMonths(MembershipDuration.Value);
                Debug.WriteLine($"[Payment] ADD NEW: Starting from today to {validUntilDate:MMM dd, yyyy}");
            }

            // ✅ CREATE MEMBER MODEL
            var member = new ManageMemberModel
            {
                FirstName = MemberFirstName,
                MiddleInitial = string.IsNullOrWhiteSpace(SelectedMiddleInitialItem) ? null : SelectedMiddleInitialItem,
                LastName = MemberLastName,
                Gender = MemberGender,
                ContactNumber = MemberContactNumber,
                Age = MemberAge,
                DateOfBirth = MemberBirthDate,
                ValidUntil = validUntilDate.ToString("MMM dd, yyyy"),
                MembershipType = SelectedMonthlyPackage.Title,
                PackageID = SelectedMonthlyPackage.SellingID,
                Status = GetSelectedStatus() ?? "Active",
                PaymentMethod = GetSelectedPaymentMethod(),
                ReferenceNumber = ReferenceNumber,
                ProfilePicture = ProfileImage ?? ImageHelper.BitmapToBytes(ImageHelper.GetDefaultAvatar()),
                ConsentLetter = ConsentFilePath
            };

            // ✅ VALIDATE PAYMENT & REFERENCE NUMBER BEFORE SAVING
            var (isValid, errorMessage) = _memberService.ValidatePaymentReferenceNumber(member);

            if (!isValid)
            {
                _toastManager?.CreateToast("Validation Error")
                    .WithContent(errorMessage)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            bool isSuccess;
            string successMessage;

            if (ViewContext == MemberViewContext.AddNew)
            {
                // ADD NEW MEMBER
                Debug.WriteLine($"[Payment] ========== NEW MEMBER REGISTRATION ==========");
                Debug.WriteLine($"[Payment] Name: {member.FirstName} {member.LastName}");
                Debug.WriteLine($"[Payment] Package: {SelectedMonthlyPackage.Title} (ID: {SelectedMonthlyPackage.SellingID})");
                Debug.WriteLine($"[Payment] Price per month: ₱{SelectedMonthlyPackage.Price:N2}");
                Debug.WriteLine($"[Payment] Duration: {MembershipDuration} months");
                Debug.WriteLine($"[Payment] Total Amount: ₱{SelectedMonthlyPackage.Price * MembershipDuration:N2}");
                Debug.WriteLine($"[Payment] ValidUntil: {member.ValidUntil}");
                Debug.WriteLine($"[Payment] Payment Method: {member.PaymentMethod}");
                Debug.WriteLine($"[Payment] Reference Number: {member.ReferenceNumber ?? "N/A"}");
                Debug.WriteLine($"[Payment] =======================================");

                var result = await _memberService.AddMemberAsync(member);
                isSuccess = result.Success;
                successMessage = $"{member.FirstName} {member.LastName} registered successfully!";

                if (!result.Success)
                {
                    _toastManager?.CreateToast("Registration Failed")
                        .WithContent(result.Message)
                        .DismissOnClick()
                        .ShowError();
                    return;
                }

                member.MemberID = result.MemberId ?? 0;
            }
            else if (ViewContext == MemberViewContext.Upgrade || ViewContext == MemberViewContext.Renew)
            {
                // UPDATE EXISTING MEMBER (Upgrade/Renew)
                string actionType = ViewContext == MemberViewContext.Upgrade ? "UPGRADE" : "RENEW";
                Debug.WriteLine($"[Payment] ========== {actionType} MEMBER ==========");
                Debug.WriteLine($"[Payment] Member ID: {_selectedMemberId}");
                Debug.WriteLine($"[Payment] Name: {member.FirstName} {member.LastName}");
                Debug.WriteLine($"[Payment] New Package: {SelectedMonthlyPackage.Title}");
                Debug.WriteLine($"[Payment] Duration: {MembershipDuration} months added");
                Debug.WriteLine($"[Payment] New ValidUntil: {member.ValidUntil}");
                Debug.WriteLine($"[Payment] Payment Method: {member.PaymentMethod}");
                Debug.WriteLine($"[Payment] Reference Number: {member.ReferenceNumber ?? "N/A"}");
                Debug.WriteLine($"[Payment] =======================================");

                member.MemberID = _selectedMemberId;

                var result = await _memberService.UpdateMemberAsync(member);
                isSuccess = result.Success;

                successMessage = ViewContext == MemberViewContext.Upgrade
                    ? $"{member.FirstName} {member.LastName} upgraded successfully! Extended by {MembershipDuration} months."
                    : $"{member.FirstName} {member.LastName} renewed successfully! Extended by {MembershipDuration} months.";

                if (!result.Success)
                {
                    _toastManager?.CreateToast("Update Failed")
                        .WithContent(result.Message)
                        .DismissOnClick()
                        .ShowError();
                    return;
                }
            }
            else
            {
                Debug.WriteLine($"[Payment] ❌ Unknown ViewContext: {ViewContext}");
                return;
            }

            decimal totalAmount = SelectedMonthlyPackage.Price * MembershipDuration.Value;

            _toastManager?.CreateToast(successMessage)
                .WithContent($"{SelectedMonthlyPackage.Title} ({MembershipDuration} months) = ₱{totalAmount:N2}")
                .DismissOnClick()
                .ShowSuccess();

            ClearAllFields();
            _pageManager.Navigate<ManageMembershipViewModel>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Payment] ❌ Error: {ex.Message}");
            Debug.WriteLine($"[Payment] Stack trace: {ex.StackTrace}");
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
                                  && MemberAge >= 3 && MemberAge <= 100
                                  && !string.IsNullOrWhiteSpace(MemberGender);

            bool hasValidQuantity = (MembershipDuration.HasValue && MembershipDuration > 0);
            bool hasPaymentMethod = IsCashSelected || IsGCashSelected || IsMayaSelected;
            bool hasPackage = SelectedMonthlyPackage != null;
            bool hasConsentLetterIfRequired = !IsConsentLetterRequired;

            // ✅ VALIDATE REFERENCE NUMBER FOR GCASH/MAYA
            bool hasValidReferenceNumber = true;
            if (IsGCashSelected)
            {
                hasValidReferenceNumber = !string.IsNullOrWhiteSpace(ReferenceNumber)
                                          && GCashReferenceRegex().IsMatch(ReferenceNumber);
            }
            else if (IsMayaSelected)
            {
                hasValidReferenceNumber = !string.IsNullOrWhiteSpace(ReferenceNumber)
                                          && MayaReferenceRegex().IsMatch(ReferenceNumber);
            }

            return hasValidInputs && hasValidQuantity && hasPaymentMethod
                   && hasPackage && hasValidReferenceNumber && hasConsentLetterIfRequired;
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
    private async Task SelectContentFile()
    {
        var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (toplevel == null) return;
        
        var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a backup file to restore",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image Files")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg" ]
                }
            ]
        });
        
        if (files.Count > 0)
        {
            var selectedFile = files[0];
            ConsentFilePath = selectedFile.Path.LocalPath;

            _toastManager.CreateToast("Consent letter selected")
                .WithContent($"{selectedFile.Name}")
                .DismissOnClick()
                .ShowInfo();
        }
    }
    
    [RelayCommand]
    private async Task ViewConsentFile()
    {
        if (string.IsNullOrWhiteSpace(ConsentFilePath) || !File.Exists(ConsentFilePath))
        {
            _toastManager?.CreateToast("File Not Found")
                .WithContent("The consent letter file could not be found")
                .DismissOnClick()
                .ShowError();
            return;
        }

        try
        {
            // Open the file with the default image viewer
            var process = new Process
            {
                StartInfo = new ProcessStartInfo(ConsentFilePath)
                {
                    UseShellExecute = true
                }
            };
            process.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error opening consent file: {ex.Message}");
            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to open file: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }
    
    [RelayCommand]
    private void DeleteConsentFile()
    {
        ConsentFilePath = string.Empty;
    
        _toastManager.CreateToast("Consent letter removed")
            .WithContent("The consent letter has been removed")
            .DismissOnClick()
            .ShowWarning();
    }
    
    partial void OnConsentFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(IsConsentFileSelected));
        OnPropertyChanged(nameof(IsConsentLetterRequired));
        OnPropertyChanged(nameof(IsPaymentPossible));
    
        if (LetterConsentTextBoxControl1 != null)
            LetterConsentTextBoxControl1.Text = value;
    
        if (LetterConsentTextBoxControl2 != null)
            LetterConsentTextBoxControl2.Text = value;
    }

    private void ClearAllFields()
    {
        MemberFirstName = string.Empty;
        SelectedMiddleInitialItem = string.Empty;
        MemberLastName = string.Empty;
        MemberGender = null;
        MemberContactNumber = string.Empty;
        MemberAge = null;
        MemberBirthDate = null;
        MemberStatus = string.Empty;
        MembershipDuration = null;
        SelectedMonthlyPackage = null;
        ReferenceNumber = string.Empty; // ✅ Clear reference number
        ReferenceNumber = string.Empty;
        ConsentFilePath = string.Empty;
        IsCashSelected = false;
        IsGCashSelected = false;
        IsMayaSelected = false;
        IsActiveSelected = true;
        IsInactiveSelected = false;
        IsTerminatedSelected = false;
        ProfileImage = null;
        ProfileImageSource = ManageMemberModel.DefaultAvatarSource;

        ClearAllErrors();

        ImageResetRequested?.Invoke();
    }

    #region Payment Summary Display Properties

    public string PackageDisplayName => SelectedMonthlyPackage?.Title ?? "No package selected";

    public string PackagePriceDisplay => SelectedMonthlyPackage != null
        ? $"₱{SelectedMonthlyPackage.Price:N2}"
        : "₱0.00";

    public string PackageDurationDisplay
    {
        get
        {
            if (!MembershipDuration.HasValue || MembershipDuration <= 0)
                return "0 Month";

            int duration = MembershipDuration.Value;
            return duration == 1 ? $"{duration} Month" : $"{duration} Months";
        }
    }

    public string SubtotalDisplay
    {
        get
        {
            if (SelectedMonthlyPackage == null || !MembershipDuration.HasValue || MembershipDuration <= 0)
                return "₱0.00";

            decimal subtotal = SelectedMonthlyPackage.Price * MembershipDuration.Value;
            return $"₱{subtotal:N2}";
        }
    }

    public string MemberIdDisplay => _selectedMemberId > 0
        ? $"ID: GM-2025-{_selectedMemberId:D6}"
        : "ID: GM-2025-001234";

    public string CurrentDate => DateTime.Now.ToString("MMMM dd, yyyy");

    public string CurrentTime => DateTime.Now.ToString("h:mm tt");

    public string TransactionID => $"TX-{DateTime.Now:yyyy}-{new Random().Next(1000, 9999)}";

    public string ValidFromDate
    {
        get
        {
            if (ViewContext == MemberViewContext.Renew || ViewContext == MemberViewContext.Upgrade)
            {
                DateTime currentValidUntil = _currentMemberValidUntil;

                if (currentValidUntil == DateTime.MinValue || currentValidUntil < DateTime.Now)
                {
                    return DateTime.Now.ToString("MMMM dd, yyyy");
                }
                else
                {
                    return currentValidUntil.ToString("MMMM dd, yyyy");
                }
            }
            else
            {
                return DateTime.Now.ToString("MMMM dd, yyyy");
            }
        }
    }

    public string ValidUntilDate
    {
        get
        {
            if (!MembershipDuration.HasValue || MembershipDuration <= 0)
                return DateTime.Now.ToString("MMMM dd, yyyy");

            DateTime validUntilDate;

            if (ViewContext == MemberViewContext.Renew || ViewContext == MemberViewContext.Upgrade)
            {
                DateTime currentValidUntil = _currentMemberValidUntil;

                if (currentValidUntil == DateTime.MinValue || currentValidUntil < DateTime.Now)
                {
                    validUntilDate = DateTime.Now.AddMonths(MembershipDuration.Value);
                }
                else
                {
                    validUntilDate = currentValidUntil.AddMonths(MembershipDuration.Value).AddDays(1);
                }
            }
            else
            {
                validUntilDate = DateTime.Now.AddMonths(MembershipDuration.Value);
            }

            return validUntilDate.ToString("MMMM dd, yyyy");
        }
    }

    public string ValidityFrom => $"Valid from: {ValidFromDate}";
    public string ValidityTo => $"Valid to: {ValidUntilDate}";

    public string MembershipDurationSummary
    {
        get
        {
            if (!MembershipDuration.HasValue || MembershipDuration <= 0 || SelectedMonthlyPackage == null)
                return "No package selected";

            int duration = MembershipDuration.Value;
            string packageName = SelectedMonthlyPackage.Title;

            return duration == 1
                ? $"{packageName} ({duration} Month)"
                : $"{packageName} ({duration} Months)";
        }
    }

    #endregion

    [GeneratedRegex(@"^09\d{9}$")]
    private static partial Regex ContactNumberRegex();

    // GCash: Exactly 13 digits
    [GeneratedRegex(@"^\d{13}$")]
    private static partial Regex GCashReferenceRegex();

    // Maya: Exactly 12 alphanumeric characters
    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex MayaReferenceRegex();

    public event Action? ImageResetRequested;

    protected override void DisposeManagedResources()
    {
        if (ProfileImageSource is IDisposable d0) d0.Dispose();
        ProfileImageSource = null;
        MemberProfileImageControl = null;
        MemberProfileImageControl2 = null;
        ProfileImage = null;

        MonthlyPackages.Clear();
        MonthlyPackages = [];

        MiddleInitialItems = [];
        MemberStatusItems = [];

        SelectedMonthlyPackage = null;

        base.DisposeManagedResources();
    }
}
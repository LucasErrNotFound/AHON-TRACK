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

    // Store monthly membership packages from SellingModel
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

    // Personal Details Section
    private string _memberFirstName = string.Empty;
    private string _selectedMiddleInitialItem = string.Empty;
    private string _memberLastName = string.Empty;
    private string _memberGender = string.Empty;
    private string _memberContactNumber = string.Empty;
    private int? _memberAge;
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

            OnPropertyChanged(nameof(PackageDurationDisplay));
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(ValidFromDate));
            OnPropertyChanged(nameof(ValidUntilDate));
            OnPropertyChanged(nameof(ValidityFrom));
            OnPropertyChanged(nameof(ValidityTo));
            OnPropertyChanged(nameof(MembershipDurationSummary));

            // Auto-select monthly package when duration is set
            _ = AutoSelectMonthlyPackageAsync();
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

    // Load monthly membership packages using GetAvailablePackagesForMembersAsync
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

    // Auto-select monthly package when duration is entered
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

        // If packages not loaded yet, load them
        if (!MonthlyPackages.Any())
        {
            await LoadMonthlyPackagesAsync();
        }

        // Auto-select the first monthly package (you can add logic to select a specific one based on price/tier)
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

            // ✅ FIX: Calculate ValidUntil based on context
            DateTime validUntilDate;

            if (ViewContext == MemberViewContext.Renew || ViewContext == MemberViewContext.Upgrade)
            {
                // For RENEW or UPGRADE: Extend from current ValidUntil date
                DateTime currentValidUntil = _currentMemberValidUntil;

                // If no valid date stored, try to get it from the database
                if (currentValidUntil == DateTime.MinValue && _selectedMemberId > 0)
                {
                    var result = await _memberService.GetMemberByIdAsync(_selectedMemberId);
                    if (result.Success && result.Member != null)
                    {
                        DateTime.TryParse(result.Member.ValidUntil, out currentValidUntil);
                    }
                }

                // ✅ CRITICAL FIX: Always add months to existing expiry (even if expired)
                // This ensures the sale records the EXACT months the user is purchasing
                if (currentValidUntil == DateTime.MinValue)
                {
                    // Only use today if we have NO expiry date at all (shouldn't happen in renew/upgrade)
                    validUntilDate = DateTime.Now.AddMonths(MembershipDuration.Value);
                    Debug.WriteLine($"[Payment] {ViewContext}: No existing date, starting from today → {validUntilDate:MMM dd, yyyy}");
                }
                else
                {
                    // ✅ Always extend from existing expiry, even if it's in the past
                    // Example: Expired on Feb 1, user buys 3 months → Feb 1 + 3 = May 1
                    // CalculateMonthsAdded(Feb 1, May 1) = 3 months ✓
                    validUntilDate = currentValidUntil.AddMonths(MembershipDuration.Value);

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
                // For ADD NEW: Start from today
                validUntilDate = DateTime.Now.AddMonths(MembershipDuration.Value);
                Debug.WriteLine($"[Payment] ADD NEW: Starting from today to {validUntilDate:MMM dd, yyyy}");
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
                ValidUntil = validUntilDate.ToString("MMM dd, yyyy"),
                MembershipType = SelectedMonthlyPackage.Title,
                PackageID = SelectedMonthlyPackage.SellingID,
                Status = GetSelectedStatus() ?? "Active",
                PaymentMethod = GetSelectedPaymentMethod(),
                ProfilePicture = ProfileImage ?? ImageHelper.BitmapToBytes(ImageHelper.GetDefaultAvatar())
            };

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
                Debug.WriteLine($"[Payment] =======================================");

                // Set the MemberID from the member being upgraded/renewed
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

            // Calculate total amount
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
                                  && (MemberAge >= 18 && MemberAge <= 80)
                                  && !string.IsNullOrWhiteSpace(MemberGender);

            bool hasValidQuantity = (MembershipDuration.HasValue && MembershipDuration > 0);
            bool hasPaymentMethod = IsCashSelected || IsGCashSelected || IsMayaSelected;
            bool hasPackage = SelectedMonthlyPackage != null;

            return hasValidInputs && hasValidQuantity && hasPaymentMethod && hasPackage;
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
        MemberAge = null;
        MemberBirthDate = null;
        MemberStatus = string.Empty;
        MembershipDuration = null;
        SelectedMonthlyPackage = null;
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

    // ✅ ADD: Transaction Details
    public string CurrentDate => DateTime.Now.ToString("MMMM dd, yyyy");

    public string CurrentTime => DateTime.Now.ToString("h:mm tt");

    public string TransactionID => $"TX-{DateTime.Now:yyyy}-{new Random().Next(1000, 9999)}";

    // ✅ ADD: Membership Validity Dates
    public string ValidFromDate
    {
        get
        {
            if (ViewContext == MemberViewContext.Renew || ViewContext == MemberViewContext.Upgrade)
            {
                DateTime currentValidUntil = _currentMemberValidUntil;

                // If membership already expired or no valid date, start from today
                if (currentValidUntil == DateTime.MinValue || currentValidUntil < DateTime.Now)
                {
                    return DateTime.Now.ToString("MMMM dd, yyyy");
                }
                else
                {
                    // If still valid, start from current expiry date
                    return currentValidUntil.ToString("MMMM dd, yyyy");
                }
            }
            else
            {
                // For ADD NEW: Start from today
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
                // For RENEW or UPGRADE: Extend from current ValidUntil date
                DateTime currentValidUntil = _currentMemberValidUntil;

                // If membership already expired or no valid date, extend from today
                if (currentValidUntil == DateTime.MinValue || currentValidUntil < DateTime.Now)
                {
                    validUntilDate = DateTime.Now.AddMonths(MembershipDuration.Value);
                }
                else
                {
                    // If still valid, extend from current expiry date
                    validUntilDate = currentValidUntil.AddMonths(MembershipDuration.Value);
                }
            }
            else
            {
                // For ADD NEW: Start from today
                validUntilDate = DateTime.Now.AddMonths(MembershipDuration.Value);
            }

            return validUntilDate.ToString("MMMM dd, yyyy");
        }
    }

    public string ValidityFrom => $"Valid from: {ValidFromDate}";
    public string ValidityTo => $"Valid to: {ValidUntilDate}";

    // ✅ ADD: Formatted membership duration for Purchase Summary
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

    public event Action? ImageResetRequested;
}
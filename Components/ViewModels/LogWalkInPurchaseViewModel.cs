using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
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
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Sharprinter;

namespace AHON_TRACK.Components.ViewModels;

[Page("walk-in-purchase")]
public partial class LogWalkInPurchaseViewModel : ViewModelBase, INavigable
{
    [ObservableProperty]
    private string[] _middleInitialItems = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"];

    [ObservableProperty]
    private string[] _walkInTypeItems = ["Regular", "Free Trial"];

    [ObservableProperty]
    private string[] _specializedPackageItems = [];

    private string _walkInFirstName = string.Empty;
    private string _selectedMiddleInitialItem = string.Empty;
    private string _walkInLastName = string.Empty;
    private string _walkInContactNumber = string.Empty;
    private int? _walkInAge;
    private string? _referenceNumber = string.Empty;
    private string? _walkInGender = string.Empty;
    private string _selectedWalkInTypeItem = string.Empty;
    private string _selectedSpecializedPackageItem = "";
    private int? _specializedPackageQuantity = 1;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _hasFreeTrialWarning;

    [ObservableProperty]
    private string _freeTrialWarningMessage = string.Empty;

    [ObservableProperty]
    private string _quantityHelperMessage = string.Empty;

    [ObservableProperty]
    private List<SellingModel> _availablePackages = new();
    
    [ObservableProperty]
    private TextBox? _letterConsentTextBoxControl1;

    [ObservableProperty]
    private TextBox? _letterConsentTextBoxControl2;

    [ObservableProperty]
    private string? _consentFilePath;
    
    [ObservableProperty]
    private string _currentInvoiceNo = string.Empty;

    [ObservableProperty]
    private bool _isGeneratingInvoice;
    
    private decimal? _tenderedPrice;

    public int? LastRegisteredCustomerID { get; private set; }

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;
    
    private DateTimeOffset? _walkInBirthDate;

    [ObservableProperty]
    private DateTimeOffset _maxSelectableYear = new DateTimeOffset(DateTime.Now.Year - 6, 12, 31, 0, 0, 0, TimeSpan.Zero);

    [ObservableProperty]
    private DateTimeOffset _minSelectableYear = new DateTimeOffset(DateTime.Now.Year - 100, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IWalkInService _walkInService;

    public bool IsCashVisible => IsCashSelected;
    public bool IsGCashVisible => IsGCashSelected;
    public bool IsMayaVisible => IsMayaSelected;
    public bool IsReferenceNumberVisible => IsMayaSelected || IsGCashSelected;
    public string SelectedWalkInType => SelectedWalkInTypeItem;
    
    public string InvoiceNumberDisplay => string.IsNullOrWhiteSpace(CurrentInvoiceNo) 
        ? "Generating..." 
        : CurrentInvoiceNo;
    
    public bool IsReferenceNumberVisibleInReceipt => 
        (IsGCashSelected || IsMayaSelected) && !string.IsNullOrWhiteSpace(ReferenceNumber);
    
    public bool IsMinor => WalkInAge.HasValue && WalkInAge >= 3 && WalkInAge <= 14;
    public bool IsConsentLetterRequired => IsMinor && string.IsNullOrWhiteSpace(ConsentFilePath);
    public bool IsConsentFileSelected => !string.IsNullOrWhiteSpace(ConsentFilePath);

    private bool _isCashSelected;
    private bool _isGCashSelected;
    private bool _isMayaSelected;

    public LogWalkInPurchaseViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, IWalkInService walkInService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _walkInService = walkInService;
        
        _ = LoadAvailablePackagesAsync();
        _ = GenerateNewInvoiceNumberAsync();

        SelectedDate = NavigationDate.SelectedCheckInDate;
    }

    public LogWalkInPurchaseViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _walkInService = null!;
    }

    [AvaloniaHotReload]
    public async void Initialize()
    {
        await LoadAvailablePackagesAsync();
        await GenerateNewInvoiceNumberAsync();
    }

    private async Task LoadAvailablePackagesAsync()
    {
        System.Diagnostics.Debug.WriteLine("🔄 LoadAvailablePackagesAsync started");

        if (_walkInService == null)
        {
            System.Diagnostics.Debug.WriteLine("❌ WalkInService is null");
            return;
        }

        System.Diagnostics.Debug.WriteLine("✅ WalkInService is not null");

        try
        {
            System.Diagnostics.Debug.WriteLine("📡 Calling GetAvailablePackagesForWalkInAsync...");

            // Pass the selected walk-in type to filter packages
            var packages = await _walkInService.GetAvailablePackagesForWalkInAsync(SelectedWalkInTypeItem);
            System.Diagnostics.Debug.WriteLine($"📦 Received {packages?.Count ?? 0} packages");

            if (packages == null || packages.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ No packages returned from database");
                SpecializedPackageItems = new[] { "None" };
                SelectedSpecializedPackageItem = "None"; // Reset selection
                return;
            }

            AvailablePackages = packages;
            System.Diagnostics.Debug.WriteLine($"✅ AvailablePackages set with {packages.Count} items");

            // Update SpecializedPackageItems to show package names
            var packageNames = new List<string> { };
            packageNames.AddRange(packages.Select(p => p.Title ?? "Unknown"));

            System.Diagnostics.Debug.WriteLine($"📋 Package names: {string.Join(", ", packageNames)}");

            SpecializedPackageItems = packageNames.ToArray();
            System.Diagnostics.Debug.WriteLine($"✅ SpecializedPackageItems updated with {SpecializedPackageItems.Length} items");

            // Reset selected package when packages change
            SelectedSpecializedPackageItem = "None";

            // Force UI update
            OnPropertyChanged(nameof(SpecializedPackageItems));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error loading packages: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            _toastManager?.CreateToast("Load Error")
                .WithContent($"Failed to load packages: {ex.Message}")
                .ShowError();
        }
    }

    [Required(ErrorMessage = "First name is required")]
    [RegularExpression(@"^[a-zA-Z ]*$", ErrorMessage = "Alphabets only.")]
    [MinLength(2, ErrorMessage = "Must be at least 2 characters long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string WalkInFirstName
    {
        get => _walkInFirstName;
        set
        {
            SetProperty(ref _walkInFirstName, value, true);
            OnPropertyChanged(nameof(CustomerFullName));
            OnPropertyChanged(nameof(IsPaymentPossible));
            _ = CheckFreeTrialEligibilityAsync();
        }
    }

    public string SelectedMiddleInitialItem
    {
        get => _selectedMiddleInitialItem;
        set
        {
            SetProperty(ref _selectedMiddleInitialItem, value, true);
            OnPropertyChanged(nameof(CustomerFullName));
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    [Required(ErrorMessage = "Last name is required")]
    [RegularExpression(@"^[a-zA-Z ]*$", ErrorMessage = "Alphabets only.")]
    [MinLength(2, ErrorMessage = "Must be at least 2 characters long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string WalkInLastName
    {
        get => _walkInLastName;
        set
        {
            SetProperty(ref _walkInLastName, value, true);
            OnPropertyChanged(nameof(CustomerFullName));
            OnPropertyChanged(nameof(IsPaymentPossible));
            _ = CheckFreeTrialEligibilityAsync();
        }
    }

    [Required(ErrorMessage = "Contact number is required")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Contact number must start with 09 and be 11 digits long")]
    public string WalkInContactNumber
    {
        get => _walkInContactNumber;
        set
        {
            SetProperty(ref _walkInContactNumber, value, true);
            OnPropertyChanged(nameof(IsPaymentPossible));
            _ = CheckFreeTrialEligibilityAsync();
        }
    }
    
    [Required(ErrorMessage = "Age is required")]
    [Range(3, 100, ErrorMessage = "Age must be between 3 and 100")]
    public int? WalkInAge
    {
        get => _walkInAge;
        set
        {
            SetProperty(ref _walkInAge, value, true);
            OnPropertyChanged(nameof(IsPaymentPossible));
            OnPropertyChanged(nameof(IsMinor));
            OnPropertyChanged(nameof(IsConsentLetterRequired));
        }
    }

    [Required(ErrorMessage = "Birth year is required")]
    public DateTimeOffset? WalkInBirthDate
    {
        get => _walkInBirthDate;
        set
        {
            SetProperty(ref _walkInBirthDate, value, true);
            if (value.HasValue)
            {
                // ✅ Calculate age from birth year only
                WalkInAge = CalculateAgeFromYear(value.Value.Year);
            }
            else
            {
                WalkInAge = null;
            }
            OnPropertyChanged(nameof(IsPaymentPossible));
            OnPropertyChanged(nameof(IsMinor));
            OnPropertyChanged(nameof(IsConsentLetterRequired));
        }
    }

    [Required(ErrorMessage = "Select the appropriate gender")]
    public string? WalkInGender
    {
        get => _walkInGender;
        set
        {
            SetProperty(ref _walkInGender, value, true);
            OnPropertyChanged(nameof(IsMale));
            OnPropertyChanged(nameof(IsFemale));
            OnPropertyChanged(nameof(IsPaymentPossible));
        }
    }

    [Required(ErrorMessage = "Select the appropriate walk in type")]
    public string SelectedWalkInTypeItem
    {
        get => _selectedWalkInTypeItem;
        set
        {
            SetProperty(ref _selectedWalkInTypeItem, value, true);
            OnPropertyChanged(nameof(SelectedWalkInType));
            OnPropertyChanged(nameof(IsPlanVisible));
            OnPropertyChanged(nameof(IsPaymentPossible));
            OnPropertyChanged(nameof(IsQuantityEditable));
            OnPropertyChanged(nameof(IsFreeTrial));
            OnPropertyChanged(nameof(IsTenderedPriceEnabled));
            OnPropertyChanged(nameof(IsPaymentMethodEnabled));

            OnPropertyChanged(nameof(PurchaseSummarySubtotal));
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(GrandTotal));

            // Force quantity to 1 for Free Trial
            if (value == "Free Trial" && SpecializedPackageQuantity != 1)
            {
                SpecializedPackageQuantity = 1;
            }
            
            if (value == "Free Trial")
            {
                IsCashSelected = false;
                IsGCashSelected = false;
                IsMayaSelected = false;

                TenderedPrice = 0m;
                _toastManager?.CreateToast("Free Trial Selected")
                    .WithContent("Amount tendered automatically set to ₱0.00")
                    .DismissOnClick()
                    .ShowInfo();
            }
            else if (value == "Regular")
            {
                TenderedPrice = null;
            }
            
            OnPropertyChanged(nameof(SpecializedPackageQuantity));
            OnPropertyChanged(nameof(SessionQuantity));

            _ = LoadAvailablePackagesAsync();

            _ = CheckFreeTrialEligibilityAsync();
        }
    }

    [Required(ErrorMessage = "Select any specialized package/s")]
    public string SelectedSpecializedPackageItem
    {
        get => _selectedSpecializedPackageItem;
        set
        {
            SetProperty(ref _selectedSpecializedPackageItem, value, true);
            OnPropertyChanged(nameof(IsQuantityVisible));
            OnPropertyChanged(nameof(IsPackageDetailsVisible));
            OnPropertyChanged(nameof(IsPaymentPossible));

            OnPropertyChanged(nameof(SelectedPackage));
            OnPropertyChanged(nameof(PackageName));
            OnPropertyChanged(nameof(PackageUnitPrice));
            OnPropertyChanged(nameof(PackageSubtotal));
            OnPropertyChanged(nameof(PackageDiscount));
            OnPropertyChanged(nameof(PackageTotal));
            OnPropertyChanged(nameof(PurchaseSummaryPackage));
            OnPropertyChanged(nameof(PurchaseSummarySubtotal));
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(GrandTotal));

            // Force quantity to 1 for Free Trial when package is selected
            if (SelectedWalkInTypeItem == "Free Trial" && value != "None")
            {
                SpecializedPackageQuantity = 1;
            }

            OnPropertyChanged(nameof(IsQuantityEditable));
        }
    }

    [Required(ErrorMessage = "Quantity is required")]
    [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100")]
    public int? SpecializedPackageQuantity
    {
        get => _specializedPackageQuantity;
        set
        {
            // Enforce quantity = 1 for Free Trial
            var newValue = SelectedWalkInTypeItem == "Free Trial" ? 1 : value;
            SetProperty(ref _specializedPackageQuantity, newValue, true);

            OnPropertyChanged(nameof(SessionQuantity));
            OnPropertyChanged(nameof(IsPaymentPossible));

            OnPropertyChanged(nameof(PackageSubtotal));
            OnPropertyChanged(nameof(PackageTotal));
            OnPropertyChanged(nameof(PurchaseSummaryPackage));
            OnPropertyChanged(nameof(PurchaseSummarySubtotal));
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(GrandTotal));
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
    
    private int CalculateAgeFromYear(int birthYear)
    {
        int currentYear = DateTime.Today.Year;
        int age = currentYear - birthYear;
    
        // Basic validation
        if (age < 0 || age > 150)
        {
            Debug.WriteLine($"[CalculateAgeFromYear] Invalid age calculated: {age} from birth year {birthYear}");
            return 0;
        }
    
        return age;
    }

    public bool IsTenderedPriceEnabled => SelectedWalkInTypeItem != "Free Trial";

    public bool IsQuantityVisible =>
        !string.IsNullOrEmpty(SelectedSpecializedPackageItem) &&
        SelectedSpecializedPackageItem != "None";

    public bool IsQuantityEditable
    {
        get
        {
            // Quantity is not editable for Free Trial (always 1)
            var isEditable = SelectedWalkInTypeItem != "Free Trial" && IsQuantityVisible;

            // Update helper message
            if (SelectedWalkInTypeItem == "Free Trial" && IsQuantityVisible)
            {
                QuantityHelperMessage = "ℹ️ Free Trial customers are limited to 1 session only";
            }
            else if (isEditable)
            {
                QuantityHelperMessage = "You can purchase multiple sessions (1-50)";
            }
            else
            {
                QuantityHelperMessage = string.Empty;
            }

            return isEditable;
        }
    }

    public bool IsMale
    {
        get => WalkInGender == "Male";
        set
        {
            if (value)
                WalkInGender = "Male";
            else if (WalkInGender == "Male")
                WalkInGender = string.Empty;
        }
    }

    public bool IsFemale
    {
        get => WalkInGender == "Female";
        set
        {
            if (value)
                WalkInGender = "Female";
            else if (WalkInGender == "Female")
                WalkInGender = string.Empty;
        }
    }
    
    [Required(ErrorMessage = "Tendered price is required")]
    public decimal? TenderedPrice
    {
        get => _tenderedPrice;
        set
        {
            // ✅ FREE TRIAL VALIDATION: Only allow 0 for Free Trial
            if (SelectedWalkInTypeItem == "Free Trial")
            {
                if (value.HasValue && value.Value > 0)
                {
                    // Reject non-zero values for Free Trial
                    _toastManager?.CreateToast("Invalid Amount")
                        .WithContent("Free Trial customers cannot make payments. Amount must be ₱0.00")
                        .DismissOnClick()
                        .ShowWarning();
                
                    // Keep it at 0
                    SetProperty(ref _tenderedPrice, 0m, true);
                }
                else
                {
                    // Allow 0 or null
                    SetProperty(ref _tenderedPrice, value ?? 0m, true);
                }
            }
            else
            {
                // Regular validation for non-Free Trial
                SetProperty(ref _tenderedPrice, value, true);
            }
        
            // Recalculate change whenever tendered price changes
            OnPropertyChanged(nameof(CalculatedChange));
            OnPropertyChanged(nameof(PurchasedTenderedPrice));
            OnPropertyChanged(nameof(PurchaseChange));
            OnPropertyChanged(nameof(IsPaymentPossible));
            OnPropertyChanged(nameof(IsTenderedPriceSufficient));
            OnPropertyChanged(nameof(ShowInsufficientTenderedWarning));
            OnPropertyChanged(nameof(GrandTenderedPrice));
        }
    }

    public bool IsCashSelected
    {
        get => _isCashSelected;
        set
        {
            if (_isCashSelected != value)
            {
                _isCashSelected = value; // Set directly first
                OnPropertyChanged(nameof(IsCashSelected)); // Notify manually

                if (value)
                {
                    // Directly set backing fields to avoid triggering setters
                    _isGCashSelected = false;
                    _isMayaSelected = false;
                    ReferenceNumber = null;
                    OnPropertyChanged(nameof(IsGCashSelected));
                    OnPropertyChanged(nameof(IsMayaSelected));
                }

                OnPropertyChanged(nameof(IsCashVisible));
                OnPropertyChanged(nameof(IsGCashVisible));
                OnPropertyChanged(nameof(IsMayaVisible));
                OnPropertyChanged(nameof(IsPaymentPossible));
                OnPropertyChanged(nameof(IsReferenceNumberVisible));
                OnPropertyChanged(nameof(IsReferenceNumberVisibleInReceipt));
                OnPropertyChanged(nameof(SelectedPaymentMethod));
            }
        }
    }

    public bool IsGCashSelected
    {
        get => _isGCashSelected;
        set
        {
            if (_isGCashSelected != value)
            {
                _isGCashSelected = value; // Set directly first
                OnPropertyChanged(nameof(IsGCashSelected)); // Notify manually

                if (value)
                {
                    // Directly set backing fields to avoid triggering setters
                    _isCashSelected = false;
                    _isMayaSelected = false;
                    OnPropertyChanged(nameof(IsCashSelected));
                    OnPropertyChanged(nameof(IsMayaSelected));
                }

                OnPropertyChanged(nameof(IsCashVisible));
                OnPropertyChanged(nameof(IsGCashVisible));
                OnPropertyChanged(nameof(IsMayaVisible));
                OnPropertyChanged(nameof(IsPaymentPossible));
                OnPropertyChanged(nameof(IsReferenceNumberVisible));
                OnPropertyChanged(nameof(IsReferenceNumberVisibleInReceipt));
                OnPropertyChanged(nameof(SelectedPaymentMethod));
            }
        }
    }

    public bool IsMayaSelected
    {
        get => _isMayaSelected;
        set
        {
            if (_isMayaSelected != value)
            {
                _isMayaSelected = value; // Set directly first
                OnPropertyChanged(nameof(IsMayaSelected)); // Notify manually

                if (value)
                {
                    // Directly set backing fields to avoid triggering setters
                    _isCashSelected = false;
                    _isGCashSelected = false;
                    OnPropertyChanged(nameof(IsCashSelected));
                    OnPropertyChanged(nameof(IsGCashSelected));
                }

                OnPropertyChanged(nameof(IsCashVisible));
                OnPropertyChanged(nameof(IsGCashVisible));
                OnPropertyChanged(nameof(IsMayaVisible));
                OnPropertyChanged(nameof(IsPaymentPossible));
                OnPropertyChanged(nameof(IsReferenceNumberVisible));
                OnPropertyChanged(nameof(IsReferenceNumberVisibleInReceipt));
                OnPropertyChanged(nameof(SelectedPaymentMethod));
            }
        }
    }

    public string CustomerFullName
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(WalkInFirstName))
                parts.Add(WalkInFirstName);

            if (!string.IsNullOrWhiteSpace(SelectedMiddleInitialItem))
                parts.Add($"{SelectedMiddleInitialItem}.");

            if (!string.IsNullOrWhiteSpace(WalkInLastName))
                parts.Add(WalkInLastName.Trim());

            return parts.Count > 0 ? string.Join(" ", parts) : "Customer Name";
        }
    }
    
    public bool IsPaymentMethodEnabled => SelectedWalkInTypeItem != "Free Trial";

    public bool IsPackageDetailsVisible =>
        !string.IsNullOrEmpty(SelectedSpecializedPackageItem) &&
        SelectedSpecializedPackageItem != "None";

    public bool IsPlanVisible => !string.IsNullOrEmpty(SelectedWalkInTypeItem);

    public string SessionQuantity
    {
        get
        {
            if (!SpecializedPackageQuantity.HasValue || SpecializedPackageQuantity == 0)
                return "0 Sessions";

            int quantity = SpecializedPackageQuantity.Value;

            // Add Free Trial indicator
            if (SelectedWalkInTypeItem == "Free Trial")
            {
                return $"{quantity} Session (Free Trial - Limited to 1)";
            }

            return quantity == 1 ? $"{quantity} Session" : $"{quantity} Sessions";
        }
    }

    public bool IsPaymentPossible
    {
        get
        {
            bool hasValidInputs = !string.IsNullOrWhiteSpace(WalkInFirstName)
                                  && !string.IsNullOrWhiteSpace(WalkInLastName)
                                  && !string.IsNullOrWhiteSpace(WalkInContactNumber)
                                  && ContactNumberRegex().IsMatch(WalkInContactNumber)
                                  && WalkInAge >= 3 && WalkInAge <= 100
                                  && !string.IsNullOrWhiteSpace(WalkInGender)
                                  && !string.IsNullOrWhiteSpace(SelectedWalkInTypeItem)
                                  && !string.IsNullOrWhiteSpace(SelectedSpecializedPackageItem)
                                  && SelectedSpecializedPackageItem != "None";

            bool hasValidQuantity = SpecializedPackageQuantity.HasValue && SpecializedPackageQuantity > 0;
            bool isValidDate = SelectedDate.Date == DateTime.Today;
            bool hasConsentLetterIfRequired = !IsConsentLetterRequired;

            if (SelectedWalkInTypeItem == "Free Trial")
            {
                return hasValidInputs 
                       && hasValidQuantity 
                       && isValidDate 
                       && hasConsentLetterIfRequired;
            }

            bool hasPaymentMethod = IsCashSelected || IsGCashSelected || IsMayaSelected;
        
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

            bool hasSufficientTenderedPrice = IsTenderedPriceSufficient;

            return hasValidInputs 
                   && hasValidQuantity 
                   && hasPaymentMethod 
                   && isValidDate 
                   && hasValidReferenceNumber 
                   && hasConsentLetterIfRequired 
                   && hasSufficientTenderedPrice;
        }
    }

    private async Task CheckFreeTrialEligibilityAsync()
    {
        // Only check if trying to select Free Trial and have name + contact
        if (SelectedWalkInTypeItem != "Free Trial" ||
            string.IsNullOrWhiteSpace(WalkInFirstName) ||
            string.IsNullOrWhiteSpace(WalkInLastName) ||
            string.IsNullOrWhiteSpace(WalkInContactNumber) ||
            !ContactNumberRegex().IsMatch(WalkInContactNumber) ||
            _walkInService == null)
        {
            HasFreeTrialWarning = false;
            FreeTrialWarningMessage = string.Empty;
            return;
        }

        try
        {
            var (success, hasUsed, message) = await _walkInService.CheckFreeTrialEligibilityAsync(
                WalkInFirstName, WalkInLastName, WalkInContactNumber);

            if (success && hasUsed)
            {
                HasFreeTrialWarning = true;
                FreeTrialWarningMessage = "⚠️ This customer has already used their free trial. Please select 'Regular' instead.";
            }
            else
            {
                HasFreeTrialWarning = false;
                FreeTrialWarningMessage = string.Empty;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking free trial eligibility: {ex.Message}");
        }
    }

    #region Getting package details
    // Add this property to get the selected package object
    public SellingModel? SelectedPackage
    {
        get
        {
            if (string.IsNullOrEmpty(SelectedSpecializedPackageItem) ||
                SelectedSpecializedPackageItem == "None" ||
                AvailablePackages == null)
                return null;

            return AvailablePackages.FirstOrDefault(p => p.Title == SelectedSpecializedPackageItem);
        }
    }

    // Package name display
    public string PackageName
    {
        get
        {
            var package = SelectedPackage;
            return package?.Title ?? "No Package Selected";
        }
    }

    // Single package price
    public string PackageUnitPrice
    {
        get
        {
            var package = SelectedPackage;
            if (package == null) return "₱0.00";

            if (SelectedWalkInTypeItem == "Free Trial")
                return "₱0.00";

            return $"₱{package.Price:N2}";
        }
    }

    // Package subtotal (price × quantity)
    public string PackageSubtotal
    {
        get
        {
            var package = SelectedPackage;
            if (package == null || !SpecializedPackageQuantity.HasValue)
                return "₱0.00";

            if (SelectedWalkInTypeItem == "Free Trial")
                return "₱0.00";
            decimal subtotal = package.Price * SpecializedPackageQuantity.Value;
            return $"₱{subtotal:N2}";
        }
    }

    // Package discount (currently 0%)
    public string PackageDiscount => "-₱0";

    // Package total after discount
    public string PackageTotal
    {
        get
        {
            var package = SelectedPackage;
            if (package == null || !SpecializedPackageQuantity.HasValue)
                return "₱0.00";
            if (SelectedWalkInTypeItem == "Free Trial")
                return "₱0.00";
            decimal total = package.Price * SpecializedPackageQuantity.Value;
            return $"₱{total:N2}";
        }
    }
    
    public decimal CalculatedChange
    {
        get
        {
            if (!TenderedPrice.HasValue) return 0;
        
            decimal subtotal = GetSubtotalAsDecimal();
        
            // If Free Trial, no payment needed
            if (SelectedWalkInTypeItem == "Free Trial")
                return TenderedPrice.Value;
        
            // Calculate change
            decimal change = TenderedPrice.Value - subtotal;
        
            // Return 0 if insufficient (negative change)
            return change >= 0 ? change : 0;
        }
    }
    
    public bool IsTenderedPriceSufficient
    {
        get
        {
            // Free Trial: Must be exactly 0
            if (SelectedWalkInTypeItem == "Free Trial")
            {
                return TenderedPrice.HasValue && TenderedPrice.Value == 0;
            }
        
            // Regular: Must have value and be >= subtotal
            if (!TenderedPrice.HasValue) return false;
        
            decimal subtotal = GetSubtotalAsDecimal();
            return TenderedPrice.Value >= subtotal;
        }
    }
    
    private decimal GetSubtotalAsDecimal()
    {
        var package = SelectedPackage;
        if (package == null || !SpecializedPackageQuantity.HasValue)
            return 0;
    
        if (SelectedWalkInTypeItem == "Free Trial")
            return 0;
    
        return package.Price * SpecializedPackageQuantity.Value;
    }
    
    public string PurchasedTenderedPrice
    {
        get
        {
            if (!TenderedPrice.HasValue)
                return "₱0.00";
        
            return $"₱{TenderedPrice.Value:N2}";
        }
    }
    
    public string PurchaseChange
    {
        get
        {
            return $"₱{CalculatedChange:N2}";
        }
    }

    // Purchase Summary - Package amount
    public string PurchaseSummaryPackage
    {
        get
        {
            var package = SelectedPackage;
            if (package == null || !SpecializedPackageQuantity.HasValue)
                return "₱0.00";

            if (SelectedWalkInTypeItem == "Free Trial")
                return "₱0.00";
            decimal amount = package.Price * SpecializedPackageQuantity.Value;
            return $"₱{amount:N2}";
        }
    }

    // Purchase Summary - Subtotal
    public string PurchaseSummarySubtotal
    {
        get
        {
            decimal packageAmount = 0;

            var package = SelectedPackage;
            if (package != null && SpecializedPackageQuantity.HasValue)
            {
                packageAmount = package.Price * SpecializedPackageQuantity.Value;
            }
            if (SelectedWalkInTypeItem == "Free Trial")
                return "₱0.00";

            decimal subtotal = packageAmount;
            return $"₱{subtotal:N2}";
        }
    }

    // Total Amount (same as subtotal for now)
    public string TotalAmount
    {
        get
        {
            decimal packageAmount = 0;

            var package = SelectedPackage;
            if (package != null && SpecializedPackageQuantity.HasValue)
            {
                packageAmount = package.Price * SpecializedPackageQuantity.Value;
            }
            if (SelectedWalkInTypeItem == "Free Trial")
                return "₱0.00";

            decimal total = packageAmount;
            return $"₱{total:N2}";
        }
    }

    // Grand total for Pay button
    public string GrandTotal
    {
        get
        {
            decimal packageAmount = 0;

            var package = SelectedPackage;
            if (package != null && SpecializedPackageQuantity.HasValue)
            {
                packageAmount = package.Price * SpecializedPackageQuantity.Value;
            }
            if (SelectedWalkInTypeItem == "Free Trial")
                return "₱0.00";

            decimal total = packageAmount;
            return $"Pay ₱{total:N2}";
        }
    }
    
    public bool ShowInsufficientTenderedWarning
    {
        get
        {
            // Don't show for Free Trial
            if (SelectedWalkInTypeItem == "Free Trial")
                return false;
        
            // Don't show if no package selected
            if (string.IsNullOrEmpty(SelectedSpecializedPackageItem) || 
                SelectedSpecializedPackageItem == "None")
                return false;
        
            // Show if tendered price is entered but insufficient
            if (!TenderedPrice.HasValue)
                return false;
        
            return !IsTenderedPriceSufficient;
        }
    }
    
    public string GrandTenderedPrice => $"Pay ₱{TenderedPrice:N2}";

    // Selected payment method display
    public string SelectedPaymentMethod
    {
        get
        {
            if (IsCashSelected) return "Cash";
            if (IsGCashSelected) return "GCash";
            if (IsMayaSelected) return "Maya";
            return "None";
        }
    }
    
    public bool IsFreeTrial => SelectedWalkInTypeItem == "Free Trial";

    // Current Date
    public string CurrentDate => DateTime.Now.ToString("MMMM dd, yyyy");

    // Current Time
    public string CurrentTime => DateTime.Now.ToString("h:mm tt");

    // Transaction ID (you can generate this based on your requirements)
    public string TransactionID => $"TX-{DateTime.Now:yyyy-MMdd}-{new Random().Next(1000, 9999)}";

    // Walk-in validity dates
    public string ValidFromDate => DateTime.Now.ToString("MMMM dd, yyyy");

    public string ValidUntilDate =>
        // For 1-day pass, valid until end of the same day
        DateTime.Now.ToString("MMMM dd, yyyy");

    #endregion  

    public void OnNavigatedTo(Dictionary<string, object>? parameters)
    {
        if (parameters != null && parameters.TryGetValue("SelectedDate", out var dateObj))
        {
            if (dateObj is DateTime selectedDate)
            {
                SelectedDate = selectedDate;
            }
        }

        // Always validate on navigation
        ValidateSelectedDate();
    }

    private void ValidateSelectedDate()
    {
        if (SelectedDate.Date != DateTime.Today)
        {
            _toastManager?.CreateToast("Invalid Date")
                .WithContent("Walk-in registration is only allowed for today's date. The form will be locked.")
                .ShowWarning();
        }
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
            Title = "Select consent letter file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image Files")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg"]
                }
            ]
        });
    
        if (files.Count > 0)
        {
            var selectedFile = files[0];
            ConsentFilePath = selectedFile.Path.LocalPath;

            _toastManager?.CreateToast("Consent letter selected")
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

        _toastManager?.CreateToast("Consent letter removed")
            .WithContent("The consent letter has been removed")
            .DismissOnClick()
            .ShowWarning();
    }

    [RelayCommand]
    private async Task PaymentAsync()
    {
        if (_walkInService == null)
        {
            _toastManager.CreateToast("Service Unavailable")
                .WithContent("Walk-in service is not available")
                .ShowError();
            return;
        }

        // ✅ CRITICAL: Validate date before proceeding
        if (SelectedDate.Date != DateTime.Today)
        {
            _toastManager.CreateToast("Invalid Date")
                .WithContent("Walk-in registration is only allowed for today's date. Please return to Check-In page and select today.")
                .ShowError();
            return;
        }

        if (IsProcessing) return;

        try
        {
            IsProcessing = true;

            // Get selected payment method
            string paymentMethod = IsCashSelected ? "Cash"
                : IsGCashSelected ? "GCash"
                : IsMayaSelected ? "Maya"
                : "Cash";

            // Create walk-in model
            var walkIn = new ManageWalkInModel
            {
                FirstName = WalkInFirstName,
                MiddleInitial = SelectedMiddleInitialItem,
                LastName = WalkInLastName,
                ContactNumber = WalkInContactNumber,
                Age = WalkInAge ?? 0,
                BirthYear = WalkInBirthDate?.Year,
                Gender = WalkInGender,
                WalkInType = SelectedWalkInTypeItem,
                WalkInPackage = SelectedSpecializedPackageItem,
                PaymentMethod = paymentMethod,
                ReferenceNumber = ReferenceNumber,
                ConsentLetter = ConsentFilePath,
                Quantity = SelectedSpecializedPackageItem != "None" ? SpecializedPackageQuantity : null,
                TenderedPrice = TenderedPrice,
                Change = CalculatedChange
            };

            // ✅ VALIDATE PAYMENT & REFERENCE NUMBER BEFORE SAVING
            var (isValid, errorMessage) = _walkInService.ValidatePaymentReferenceNumber(walkIn);

            if (!isValid)
            {
                _toastManager?.CreateToast("Validation Error")
                    .WithContent(errorMessage)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            Debug.WriteLine($"[PaymentAsync] ========== WALK-IN REGISTRATION ==========");
            Debug.WriteLine($"[PaymentAsync] Invoice: {CurrentInvoiceNo}");
            Debug.WriteLine($"[PaymentAsync] Name: {walkIn.FirstName} {walkIn.LastName}");
            Debug.WriteLine($"[PaymentAsync] Type: {walkIn.WalkInType}");
            Debug.WriteLine($"[PaymentAsync] Package: {walkIn.WalkInPackage}");
            Debug.WriteLine($"[PaymentAsync] Quantity: {walkIn.Quantity}");
            Debug.WriteLine($"[PaymentAsync] Payment Method: {walkIn.PaymentMethod}");
            Debug.WriteLine($"[PaymentAsync] Reference Number: {walkIn.ReferenceNumber ?? "N/A"}");
            Debug.WriteLine($"[PaymentAsync] =======================================");

            // ✅ CALL SERVICE WITH INVOICE NUMBER BEING GENERATED INTERNALLY
            var (success, message, customerId) = await _walkInService.AddWalkInCustomerAsync(
                walkIn, 
                SelectedDate,
                CurrentInvoiceNo);

            if (success)
            {
                string paymentInfo = paymentMethod == "Cash"
                    ? "Cash payment"
                    : $"{paymentMethod} - Ref: {ReferenceNumber}";
            
                // ✅ GENERATE RECEIPT WITH INVOICE NUMBER FROM SERVICE
                _ = GenerateReceipt();

                _toastManager.CreateToast("Payment Successful!")
                    .WithContent($"Walk-in customer registered with ID: {customerId}\nInvoice: {CurrentInvoiceNo}\n{paymentInfo}")
                    .DismissOnClick()
                    .ShowSuccess();

                ClearForm();
            
                // ✅ GENERATE NEW INVOICE FOR NEXT TRANSACTION
                await GenerateNewInvoiceNumberAsync();

                _pageManager.Navigate<CheckInOutViewModel>();
            }
            else
            {
                _toastManager.CreateToast("Registration Failed")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PaymentAsync] ❌ Error: {ex.Message}");
            Debug.WriteLine($"[PaymentAsync] Stack trace: {ex.StackTrace}");
            _toastManager.CreateToast("Error")
                .WithContent($"An error occurred: {ex.Message}")
                .ShowError();
        }
        finally
        {
            IsProcessing = false;
        }
    }
    
    private async Task GenerateReceipt()
    {
        try
        {
            var options = new PrinterOptions
            {
                PortName = "COM6",
                BaudRate = 9600,
                MaxLineCharacter = 33,
                CutPaper = true
            };

            var receiptContext = new PrinterContext(options);

            receiptContext
                .AddText("AHON VICTORY GYM", x => x.Alignment(HorizontalAlignment.Center))
                .AddText("2nd Flr. Event Hub", x => x.Alignment(HorizontalAlignment.Center))
                .AddText("Victory Central Mall", x => x.Alignment(HorizontalAlignment.Center))
                .AddText("Brgy Balibago, Sta. Rosa, Laguna", x => x.Alignment(HorizontalAlignment.Center))
                .FeedLine(1)
                .AddText("PURCHASE INVOICE", x => x.Alignment(HorizontalAlignment.Center))
                .AddText("================================", x => x.Alignment(HorizontalAlignment.Center))
                .FeedLine(1)
                .AddText($"Invoice ID: {CurrentInvoiceNo}")
                .AddText($"Date: {DateTime.Now:yyyy-MM-dd hh:mm tt}")
                .AddText($"Customer: {CustomerFullName}")
                .AddText($"Type: {SelectedWalkInType}")
                .FeedLine(1)
                .AddText("--------------------------------")
                .FeedLine(1);

            // Add package details if applicable
            if (SelectedSpecializedPackageItem != "None")
            {
                receiptContext
                    .AddText($"{SelectedSpecializedPackageItem}")
                    .AddText($"  {SpecializedPackageQuantity} x {FormatCurrencyForReceipt(PackageUnitPrice)}");
            }

            receiptContext
                .FeedLine(1)
                .AddText("--------------------------------");
            receiptContext
                .AddText($"Payment: {SelectedPaymentMethod}");
            
            if (IsGCashSelected || IsMayaSelected)
            {
                receiptContext.AddText($"Reference No: {ReferenceNumber}");
            }
            
            receiptContext
                .AddText("--------------------------------");

            // ✅ ADD PAYMENT DETAILS SECTION
            if (SelectedWalkInTypeItem != "Free Trial")
            {
                receiptContext
                    .AddText($"TOTAL AMOUNT: {FormatCurrencyForReceipt(PurchaseSummarySubtotal)}", 
                        x => x.Alignment(HorizontalAlignment.Right))
                    .AddText($"AMOUNT TENDERED: {FormatCurrencyForReceipt(PurchasedTenderedPrice)}", 
                        x => x.Alignment(HorizontalAlignment.Right));
                
            }
            
            receiptContext.AddText("--------------------------------");

            receiptContext
                .AddText($"Change: {FormatCurrencyForReceipt(PurchaseChange)}",
                    x => x.Alignment(HorizontalAlignment.Right));

            await receiptContext
                .FeedLine(2)
                .AddText("Thank you for your visit!", x => x.Alignment(HorizontalAlignment.Center))
                .AddText($"Printed by: {CurrentUserModel.Username}", x => x.Alignment(HorizontalAlignment.Left))
                .FeedLine(3)
                .ExecuteAsync();
        
            _toastManager.CreateToast("Invoice Printed")
                .WithContent("Invoice has been printed successfully")
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Print Error")
                .WithContent($"Failed to print invoice: {ex.Message}")
                .ShowError();

            Debug.WriteLine($"Printer error: {ex}");
        }
    }
    
    private async Task GenerateNewInvoiceNumberAsync()
    {
        if (IsGeneratingInvoice) return;

        try
        {
            IsGeneratingInvoice = true;

            if (_walkInService != null)
            {
                CurrentInvoiceNo = await _walkInService.GenerateInvoiceNumberAsync();
                Debug.WriteLine($"[GenerateNewInvoiceNumberAsync] Generated Invoice: {CurrentInvoiceNo}");
            }
            else
            {
                // Fallback for design-time or when service is null
                CurrentInvoiceNo = $"INV-{DateTime.Now:yyyyMMdd}-{new Random().Next(10000, 99999)}";
                Debug.WriteLine($"[GenerateNewInvoiceNumberAsync] Fallback Invoice: {CurrentInvoiceNo}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GenerateNewInvoiceNumberAsync] Error: {ex.Message}");
            CurrentInvoiceNo = $"INV-{DateTime.Now:yyyyMMdd}-ERROR";
        
            _toastManager?.CreateToast("Invoice Generation Error")
                .WithContent("Failed to generate invoice number. Using fallback.")
                .DismissOnClick()
                .ShowWarning();
        }
        finally
        {
            IsGeneratingInvoice = false;
        }
    }
    
    private string FormatCurrencyForReceipt(string currencyText)
    {
        return currencyText.Replace("₱", "P");
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

    private void ClearForm()
    {
        WalkInFirstName = string.Empty;
        SelectedMiddleInitialItem = string.Empty;
        WalkInLastName = string.Empty;
        WalkInContactNumber = string.Empty;
        WalkInBirthDate = null;
        WalkInAge = null;
        WalkInGender = string.Empty;
        SelectedWalkInTypeItem = string.Empty;
        SelectedSpecializedPackageItem = "None";
        SpecializedPackageQuantity = 1;
        ReferenceNumber = string.Empty;
        ConsentFilePath = string.Empty;
        TenderedPrice = null;
        IsCashSelected = false;
        IsGCashSelected = false;
        IsMayaSelected = false;
        HasFreeTrialWarning = false;
        FreeTrialWarningMessage = string.Empty;

        // Reset transaction ID for new transaction
        OnPropertyChanged(nameof(TransactionID));
    }


    [GeneratedRegex(@"^09\d{9}$")]
    private static partial Regex ContactNumberRegex();

    // GCash: Exactly 13 digits
    [GeneratedRegex(@"^\d{13}$")]
    private static partial Regex GCashReferenceRegex();

    // Maya: Exactly 12 alphanumeric characters
    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex MayaReferenceRegex();

    protected override void DisposeManagedResources()
    {
        // Wipe out static arrays to drop references
        MiddleInitialItems = [];
        WalkInTypeItems = [];
        SpecializedPackageItems = [];

        // Clear and replace package collections
        AvailablePackages?.Clear();
        AvailablePackages = [];

        // Reset simple primitives / flags
        LastRegisteredCustomerID = null;
        IsProcessing = false;
        HasFreeTrialWarning = false;
        FreeTrialWarningMessage = string.Empty;
        QuantityHelperMessage = string.Empty;
        
        ConsentFilePath = string.Empty;
        LetterConsentTextBoxControl1 = null;
        LetterConsentTextBoxControl2 = null;

        // Clear all input fields aggressively
        WalkInFirstName = string.Empty;
        SelectedMiddleInitialItem = string.Empty;
        WalkInLastName = string.Empty;
        WalkInContactNumber = string.Empty;
        WalkInAge = null;
        WalkInGender = string.Empty;
        SelectedWalkInTypeItem = string.Empty;
        SelectedSpecializedPackageItem = "None";
        SpecializedPackageQuantity = 1;

        // Reset payment selections
        TenderedPrice = null;
        IsCashSelected = false;
        IsGCashSelected = false;
        IsMayaSelected = false;

        // Clear computed/display properties
        OnPropertyChanged(nameof(CustomerFullName));
        OnPropertyChanged(nameof(IsPaymentPossible));
        OnPropertyChanged(nameof(PackageName));
        OnPropertyChanged(nameof(PackageUnitPrice));
        OnPropertyChanged(nameof(PackageSubtotal));
        OnPropertyChanged(nameof(PackageTotal));
        OnPropertyChanged(nameof(PurchaseSummarySubtotal));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(SelectedPaymentMethod));
        OnPropertyChanged(nameof(TransactionID));
        OnPropertyChanged(nameof(CurrentDate));
        OnPropertyChanged(nameof(CurrentTime));

        // Injected readonly services (dialog/toast/page/walkInService) are not reassigned here.

        base.DisposeManagedResources();
    }
}
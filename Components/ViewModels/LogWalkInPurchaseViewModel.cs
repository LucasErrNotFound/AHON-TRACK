using AHON_TRACK.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using AHON_TRACK.Validators;

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

    public int? LastRegisteredCustomerID { get; private set; }

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IWalkInService _walkInService;

    public bool IsCashVisible => IsCashSelected;
    public bool IsGCashVisible => IsGCashSelected;
    public bool IsMayaVisible => IsMayaSelected;
    public bool IsReferenceNumberVisible => IsMayaSelected || IsGCashSelected;
    public string SelectedWalkInType => SelectedWalkInTypeItem;
    
    public bool IsReferenceNumberVisibleInReceipt => 
        (IsGCashSelected || IsMayaSelected) && !string.IsNullOrWhiteSpace(ReferenceNumber);

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

            OnPropertyChanged(nameof(PurchaseSummarySubtotal));
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(GrandTotal));

            // Force quantity to 1 for Free Trial
            if (value == "Free Trial" && SpecializedPackageQuantity != 1)
            {
                SpecializedPackageQuantity = 1;
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
    
    [Required(ErrorMessage = "Reference number is required")]
    [RegularExpression(@"^\d{13}$", ErrorMessage = "Reference number must be 13 digits long")]
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
                    ReferenceNumber = string.Empty;
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
                    ReferenceNumber = string.Empty;
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
                && !string.IsNullOrWhiteSpace(SelectedSpecializedPackageItem);

            bool hasValidQuantity = SelectedSpecializedPackageItem == "None" ||
                                   (SpecializedPackageQuantity.HasValue && SpecializedPackageQuantity > 0);

            bool hasPaymentMethod = IsCashSelected || IsGCashSelected || IsMayaSelected;

            // ✅ Add date validation
            bool isValidDate = SelectedDate.Date == DateTime.Today;
            
            bool hasValidReferenceNumber = true;
            if (IsGCashSelected || IsMayaSelected)
            {
                hasValidReferenceNumber = !string.IsNullOrWhiteSpace(ReferenceNumber) 
                                          && ReferenceNumberRegex().IsMatch(ReferenceNumber);
            }

            return hasValidInputs && hasValidQuantity && hasPaymentMethod && isValidDate && hasValidReferenceNumber && !IsProcessing;
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
                Gender = WalkInGender,
                WalkInType = SelectedWalkInTypeItem,
                WalkInPackage = SelectedSpecializedPackageItem,
                PaymentMethod = paymentMethod,
                Quantity = SelectedSpecializedPackageItem != "None" ? SpecializedPackageQuantity : null
            };

            // ✅ Pass SelectedDate to the service (even though it should be today)
            var (success, message, customerId) = await _walkInService.AddWalkInCustomerAsync(walkIn, SelectedDate);

            if (success)
            {
                _toastManager.CreateToast("Payment Successful!")
                    .WithContent($"Walk-in customer registered with ID: {customerId}")
                    .DismissOnClick()
                    .ShowSuccess();

                // Clear form
                ClearForm();

                // Go back to previous page
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
            _toastManager.CreateToast("Error")
                .WithContent($"An error occurred: {ex.Message}")
                .ShowError();
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void ClearForm()
    {
        WalkInFirstName = string.Empty;
        SelectedMiddleInitialItem = string.Empty;
        WalkInLastName = string.Empty;
        WalkInContactNumber = string.Empty;
        WalkInAge = null;
        WalkInGender = string.Empty;
        SelectedWalkInTypeItem = string.Empty;
        SelectedSpecializedPackageItem = "None";
        SpecializedPackageQuantity = 1;
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
    
    [GeneratedRegex(@"^\d{13}$")]
    private static partial Regex ReferenceNumberRegex();

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
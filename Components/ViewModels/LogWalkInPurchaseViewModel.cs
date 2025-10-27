using AHON_TRACK.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.CompilerServices;
using AHON_TRACK.Services;
using Microsoft.Extensions.Logging;

namespace AHON_TRACK.Components.ViewModels;

[Page("walk-in-purchase")]
public partial class LogWalkInPurchaseViewModel : ViewModelBase, INavigable
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly IWalkInService _walkInService;
    private readonly INavigationService _navigationService;
    private readonly ILogger _logger;

    [ObservableProperty]
    private string[] _middleInitialItems = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"];

    [ObservableProperty]
    private string[] _walkInTypeItems = ["Regular", "Free Trial"];

    [ObservableProperty]
    private string[] _specializedPackageItems = ["None"];

    private string _walkInFirstName = string.Empty;
    private string _selectedMiddleInitialItem = string.Empty;
    private string _walkInLastName = string.Empty;
    private string _walkInContactNumber = string.Empty;
    private int? _walkInAge;
    private string _walkInGender = string.Empty;
    private string _selectedWalkInTypeItem = string.Empty;
    private string _selectedSpecializedPackageItem = "None";
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
    private bool _isInitialized;

    public int? LastRegisteredCustomerID { get; private set; }

    public bool IsCashVisible => IsCashSelected;
    public bool IsGCashVisible => IsGCashSelected;
    public bool IsMayaVisible => IsMayaSelected;
    public string SelectedWalkInType => SelectedWalkInTypeItem;

    private bool _isCashSelected;
    private bool _isGCashSelected;
    private bool _isMayaSelected;

    public LogWalkInPurchaseViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager, 
        IWalkInService walkInService,
        INavigationService navigationService,
        ILogger logger)
    {
        _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
        _walkInService = walkInService ?? throw new ArgumentNullException(nameof(walkInService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public LogWalkInPurchaseViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _navigationService = null!;
        _walkInService = null!;
        _logger = null!;
    }

    #region INavigable Implementation

    [AvaloniaHotReload]
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (IsInitialized)
        {
            _logger.LogDebug("LogWalkInPurchaseViewModel already initialized");
            return;
        }

        _logger.LogInformation("Initializing LogWalkInPurchaseViewModel");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            await LoadAvailablePackagesAsync(linkedCts.Token).ConfigureAwait(false);

            IsInitialized = true;
            _logger.LogInformation("LogWalkInPurchaseViewModel initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("LogWalkInPurchaseViewModel initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing LogWalkInPurchaseViewModel");
            _toastManager.CreateToast("Initialization Error")
                .WithContent("Failed to load walk-in purchase page")
                .DismissOnClick()
                .ShowError();
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Navigating away from LogWalkInPurchase");
        return ValueTask.CompletedTask;
    }

    #endregion

    /*
    #region HotAvalonia Support

    [AvaloniaHotReload]
    public async void Initialize()
    {
        await InitializeAsync(LifecycleToken).ConfigureAwait(false);
    }

    #endregion
    */

    private async Task LoadAvailablePackagesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LoadAvailablePackagesAsync started");

        if (_walkInService == null)
        {
            _logger.LogWarning("WalkInService is null");
            return;
        }

        try
        {
            _logger.LogDebug("Calling GetAvailablePackagesForWalkInAsync...");
            var packages = await _walkInService.GetAvailablePackagesForWalkInAsync()
                .ConfigureAwait(false);
            
            _logger.LogDebug("Received {Count} packages", packages?.Count ?? 0);

            if (packages == null || packages.Count == 0)
            {
                _logger.LogWarning("No packages returned from database");
                SpecializedPackageItems = ["None"];
                return;
            }

            AvailablePackages = packages;
            _logger.LogDebug("AvailablePackages set with {Count} items", packages.Count);

            // Update SpecializedPackageItems to show package names
            var packageNames = new List<string> { "None" };
            packageNames.AddRange(packages.Select(p => p.Title ?? "Unknown"));

            _logger.LogDebug("Package names: {Names}", string.Join(", ", packageNames));

            SpecializedPackageItems = packageNames.ToArray();
            _logger.LogDebug("SpecializedPackageItems updated with {Count} items", SpecializedPackageItems.Length);

            // Force UI update
            OnPropertyChanged(nameof(SpecializedPackageItems));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading packages");
            _toastManager?.CreateToast("Load Error")
                .WithContent($"Failed to load packages: {ex.Message}")
                .ShowError();
        }
    }

    #region Validated Properties

    [Required(ErrorMessage = "First name is required")]
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
            if (_walkInGender != value)
            {
                _walkInGender = value ?? string.Empty;
                OnPropertyChanged(nameof(WalkInGender));
                OnPropertyChanged(nameof(IsMale));
                OnPropertyChanged(nameof(IsFemale));
                OnPropertyChanged(nameof(IsPaymentPossible));
            }
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

            OnPropertyChanged(nameof(PurchaseSummarySubtotal));
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(GrandTotal));

            // Force quantity to 1 for Free Trial
            if (value == "Free Trial" && SpecializedPackageQuantity != 1)
            {
                SpecializedPackageQuantity = 1;
            }

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
            if (SelectedWalkInTypeItem == "Free Trial" && value > 1)
            {
                SetProperty(ref _specializedPackageQuantity, 1, true);
            }
            else
            {
                SetProperty(ref _specializedPackageQuantity, value, true);
            }

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

    #endregion

    #region Computed Properties

    public bool IsQuantityVisible => !string.IsNullOrEmpty(SelectedSpecializedPackageItem) &&
                                     SelectedSpecializedPackageItem != "None";

    public bool IsQuantityEditable
    {
        get
        {
            var isEditable = SelectedWalkInTypeItem != "Free Trial" && IsQuantityVisible;

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
        set { if (value) WalkInGender = "Male"; }
    }

    public bool IsFemale
    {
        get => WalkInGender == "Female";
        set { if (value) WalkInGender = "Female"; }
    }

    public bool IsCashSelected
    {
        get => _isCashSelected;
        set
        {
            if (SetField(ref _isCashSelected, value))
            {
                if (value)
                {
                    IsGCashSelected = false;
                    IsMayaSelected = false;
                }
                OnPropertyChanged(nameof(IsCashVisible));
                OnPropertyChanged(nameof(IsGCashVisible));
                OnPropertyChanged(nameof(IsMayaVisible));
                OnPropertyChanged(nameof(IsPaymentPossible));

                OnPropertyChanged(nameof(SelectedPaymentMethod));
            }
        }
    }

    public bool IsGCashSelected
    {
        get => _isGCashSelected;
        set
        {
            if (SetField(ref _isGCashSelected, value))
            {
                if (value)
                {
                    IsCashSelected = false;
                    IsMayaSelected = false;
                }
                OnPropertyChanged(nameof(IsCashVisible));
                OnPropertyChanged(nameof(IsGCashVisible));
                OnPropertyChanged(nameof(IsMayaVisible));
                OnPropertyChanged(nameof(IsPaymentPossible));

                OnPropertyChanged(nameof(SelectedPaymentMethod));
            }
        }
    }

    public bool IsMayaSelected
    {
        get => _isMayaSelected;
        set
        {
            if (SetField(ref _isMayaSelected, value))
            {
                if (value)
                {
                    IsCashSelected = false;
                    IsGCashSelected = false;
                }
                OnPropertyChanged(nameof(IsCashVisible));
                OnPropertyChanged(nameof(IsGCashVisible));
                OnPropertyChanged(nameof(IsMayaVisible));
                OnPropertyChanged(nameof(IsPaymentPossible));

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

    public bool IsPackageDetailsVisible => !string.IsNullOrEmpty(SelectedSpecializedPackageItem) &&
                                           SelectedSpecializedPackageItem != "None";

    public bool IsPlanVisible => !string.IsNullOrEmpty(SelectedWalkInTypeItem);

    public string SessionQuantity
    {
        get
        {
            if (!SpecializedPackageQuantity.HasValue || SpecializedPackageQuantity == 0)
                return "0 Sessions";

            int quantity = SpecializedPackageQuantity.Value;

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
                && (WalkInAge >= 3 && WalkInAge <= 100)
                && !string.IsNullOrWhiteSpace(WalkInGender)
                && !string.IsNullOrWhiteSpace(SelectedWalkInTypeItem)
                && !string.IsNullOrWhiteSpace(SelectedSpecializedPackageItem);

            bool hasValidQuantity = SelectedSpecializedPackageItem == "None" ||
                                   (SpecializedPackageQuantity.HasValue && SpecializedPackageQuantity > 0);

            bool hasPaymentMethod = IsCashSelected || IsGCashSelected || IsMayaSelected;

            return hasValidInputs && hasValidQuantity && hasPaymentMethod && !IsProcessing;
        }
    }

    #endregion

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
            _logger.LogWarning(ex, "Error checking free trial eligibility");
        }
    }

    #region Package Details

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

    public string PackageName => SelectedPackage?.Title ?? "No Package Selected";

    public string PackageUnitPrice
    {
        get
        {
            var package = SelectedPackage;
            if (package == null) return "₱0.00";
            if (SelectedWalkInTypeItem == "Free Trial") return "₱0.00";
            return $"₱{package.Price:N2}";
        }
    }

    public string PackageSubtotal
    {
        get
        {
            var package = SelectedPackage;
            if (package == null || !SpecializedPackageQuantity.HasValue) return "₱0.00";
            if (SelectedWalkInTypeItem == "Free Trial") return "₱0.00";
            decimal subtotal = package.Price * SpecializedPackageQuantity.Value;
            return $"₱{subtotal:N2}";
        }
    }

    public string PackageDiscount => "-₱0";

    public string PackageTotal
    {
        get
        {
            var package = SelectedPackage;
            if (package == null || !SpecializedPackageQuantity.HasValue) return "₱0.00";
            if (SelectedWalkInTypeItem == "Free Trial") return "₱0.00";
            decimal total = package.Price * SpecializedPackageQuantity.Value;
            return $"₱{total:N2}";
        }
    }

    public string PurchaseSummaryPackage
    {
        get
        {
            var package = SelectedPackage;
            if (package == null || !SpecializedPackageQuantity.HasValue) return "₱0.00";
            if (SelectedWalkInTypeItem == "Free Trial") return "₱0.00";
            decimal amount = package.Price * SpecializedPackageQuantity.Value;
            return $"₱{amount:N2}";
        }
    }

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
            if (SelectedWalkInTypeItem == "Free Trial") return "₱0.00";
            return $"₱{packageAmount:N2}";
        }
    }

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
            if (SelectedWalkInTypeItem == "Free Trial") return "₱0.00";
            return $"₱{packageAmount:N2}";
        }
    }

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
            if (SelectedWalkInTypeItem == "Free Trial") return "₱0.00";
            return $"Pay ₱{packageAmount:N2}";
        }
    }

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

    public string CurrentDate => DateTime.Now.ToString("MMMM dd, yyyy");
    public string CurrentTime => DateTime.Now.ToString("h:mm tt");
    public string TransactionID => $"TX-{DateTime.Now:yyyy-MMdd}-{new Random().Next(1000, 9999)}";
    public string ValidFromDate => DateTime.Now.ToString("MMMM dd, yyyy");
    public string ValidUntilDate => DateTime.Now.ToString("MMMM dd, yyyy");

    #endregion

    [RelayCommand]
    private async Task PaymentAsync()
    {
        ThrowIfDisposed();

        if (_walkInService == null)
        {
            _toastManager.CreateToast("Service Unavailable")
                .WithContent("Walk-in service is not available")
                .ShowError();
            return;
        }

        if (IsProcessing) return;

        try
        {
            IsProcessing = true;

            string paymentMethod = IsCashSelected ? "Cash"
                : IsGCashSelected ? "GCash"
                : IsMayaSelected ? "Maya"
                : "Cash";

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

            _logger.LogInformation("Registering walk-in customer: {FirstName} {LastName}", 
                WalkInFirstName, WalkInLastName);

            var (success, message, customerId) = await _walkInService.AddWalkInCustomerAsync(walkIn);

            if (success)
            {
                _logger.LogInformation("Walk-in customer registered successfully with ID: {CustomerId}", customerId);
                
                _toastManager.CreateToast("Payment Successful!")
                    .WithContent($"Walk-in customer registered with ID: {customerId}")
                    .DismissOnClick()
                    .ShowSuccess();

                ClearForm();
                await _navigationService.NavigateAsync<CheckInOutViewModel>();
            }
            else
            {
                _logger.LogWarning("Failed to register walk-in customer: {Message}", message);
                
                _toastManager.CreateToast("Registration Failed")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing walk-in payment");
            
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

        OnPropertyChanged(nameof(TransactionID));
        
        _logger.LogDebug("Walk-in purchase form cleared");
    }

    [GeneratedRegex(@"^09\d{9}$")]
    private static partial Regex ContactNumberRegex();
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected new virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #region Disposal

    protected override async ValueTask DisposeAsyncCore()
    {
        _logger.LogInformation("Disposing LogWalkInPurchaseViewModel");

        // Clear collections
        AvailablePackages.Clear();

        // Clear validation errors
        ClearAllErrors();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    #endregion
}
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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.ViewModels;

[Page("walk-in-purchase")]
public partial class LogWalkInPurchaseViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _middleInitialItems = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"];

    [ObservableProperty]
    private string[] _walkInTypeItems = ["Regular", "Free Trial"];

    [ObservableProperty]
    private string[] _specializedPackageItems = ["None", "Boxing", "Muay Thai", "CrossFit"];

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

    public int? LastRegisteredCustomerID { get; private set; }

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IWalkInService _walkInService;

    public bool IsCashVisible => IsCashSelected;
    public bool IsGCashVisible => IsGCashSelected;
    public bool IsMayaVisible => IsMayaSelected;
    public string SelectedWalkInType => SelectedWalkInTypeItem;

    private bool _isCashSelected;
    private bool _isGCashSelected;
    private bool _isMayaSelected;

    public LogWalkInPurchaseViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, IWalkInService walkInService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _walkInService = walkInService;
    }

    public LogWalkInPurchaseViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _walkInService = null!;
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }

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

    [Required(ErrorMessage = "Select your MI")]
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
    [Range(18, 80, ErrorMessage = "Age must be between 18 and 80")]
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

            // Force quantity to 1 for Free Trial
            if (value == "Free Trial" && SpecializedPackageQuantity != 1)
            {
                SpecializedPackageQuantity = 1;
            }

            // Trigger updates for quantity-related properties
            //UpdateQuantityRelatedPropertie();
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

            // Force quantity to 1 for Free Trial when package is selected
            if (SelectedWalkInTypeItem == "Free Trial" && value != "None")
            {
                SpecializedPackageQuantity = 1;
            }

            OnPropertyChanged(nameof(IsQuantityEditable));
        }
    }

    [Required(ErrorMessage = "Quantity is required")]
    [Range(1, 50, ErrorMessage = "Quantity must be between 1 and 50")]
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
        }
    }

    public bool IsQuantityVisible
    {
        get
        {
            return !string.IsNullOrEmpty(SelectedSpecializedPackageItem) &&
                   SelectedSpecializedPackageItem != "None";
        }
    }

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

    public bool IsPackageDetailsVisible
    {
        get
        {
            return !string.IsNullOrEmpty(SelectedSpecializedPackageItem) &&
                   SelectedSpecializedPackageItem != "None";
        }
    }

    public bool IsPlanVisible
    {
        get { return !string.IsNullOrEmpty(SelectedWalkInTypeItem); }
    }

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
                && !string.IsNullOrWhiteSpace(SelectedMiddleInitialItem)
                && !string.IsNullOrWhiteSpace(WalkInLastName)
                && !string.IsNullOrWhiteSpace(WalkInContactNumber)
                && ContactNumberRegex().IsMatch(WalkInContactNumber)
                && (WalkInAge >= 18 && WalkInAge <= 80)
                && !string.IsNullOrWhiteSpace(WalkInGender)
                && !string.IsNullOrWhiteSpace(SelectedWalkInTypeItem)
                && !string.IsNullOrWhiteSpace(SelectedSpecializedPackageItem);

            bool hasValidQuantity = SelectedSpecializedPackageItem == "None" ||
                                   (SpecializedPackageQuantity.HasValue && SpecializedPackageQuantity > 0);

            bool hasPaymentMethod = IsCashSelected || IsGCashSelected || IsMayaSelected;

            return hasValidInputs && hasValidQuantity && hasPaymentMethod && !IsProcessing;
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

    [RelayCommand]
    private async Task Payment()
    {
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

            // Register walk-in customer
            var (success, message, customerId) = await _walkInService.AddWalkInCustomerAsync(walkIn);

            if (success)
            {
                _toastManager.CreateToast("Payment Successful!")
                    .WithContent($"Walk-in customer registered with ID: {customerId}")
                    .DismissOnClick()
                    .ShowSuccess();

                // Clear form
                ClearForm();

                //GoBack to previous page
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
    }

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

    [GeneratedRegex(@"^09\d{9}$")]
    private static partial Regex ContactNumberRegex();
}
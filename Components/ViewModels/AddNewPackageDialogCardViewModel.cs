using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;

namespace AHON_TRACK.Components.ViewModels;

public partial class AddNewPackageDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _discountTypeItems = ["Percentage (%)", "Fixed Amount (₱)"];
    private string _selectedDiscountTypeItem = "Percentage (%)";

    [ObservableProperty]
    private string[] _discountForItems = ["All", "Walk-ins", "Gym Members"];
    private string _selectedDiscountForItem = "All";

    [ObservableProperty]
    private string[] _durationItems = ["/Month", "/Session", "/One-time only"];
    private string _selectedDurationItem = string.Empty;

    private string _packageName = string.Empty;
    private string _description = string.Empty;
    private decimal? _price;
    private string _duration = string.Empty;

    private string _featureDescription1 = string.Empty;
    private string _featureDescription2 = string.Empty;
    private string _featureDescription3 = string.Empty;
    private string _featureDescription4 = string.Empty;
    private string _featureDescription5 = string.Empty;

    private bool _enableDiscount;
    private string _discountType = string.Empty;
    private int? _discountValue;

    private DateOnly? _validFrom;
    private DateOnly? _validTo;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IPackageService _packageService;

    public bool IsDiscountEnabled => EnableDiscount;

    public AddNewPackageDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, IPackageService packageService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _packageService = packageService;
    }

    public AddNewPackageDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _packageService = null!;
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        ClearAllFields();
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }

    [RelayCommand]
    private void CreatePackage()
    {
        ValidateAllProperties();

        if (EnableDiscount)
        {
            ValidateProperty(ValidFrom, nameof(ValidFrom));
            ValidateProperty(ValidTo, nameof(ValidTo));
            ValidateProperty(DiscountValue, nameof(DiscountValue));
        }
        else
        {
            ClearErrors(nameof(ValidFrom));
            ClearErrors(nameof(ValidTo));
            ClearErrors(nameof(DiscountValue));
        }

        if (HasErrors) return;

        var packageData = GetPackageData();
        if (packageData == null)
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Please ensure all required fields are filled out correctly.")
                .DismissOnClick()
                .ShowError();
            return;
        }
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [Required(ErrorMessage = "Package name is required")]
    [MinLength(5, ErrorMessage = "Must be at least 5 characters long")]
    [MaxLength(25, ErrorMessage = "Must not exceed 25 characters")]
    public string PackageName
    {
        get => _packageName;
        set => SetProperty(ref _packageName, value, true);
    }

    [Required(ErrorMessage = "Description is required")]
    [MinLength(6, ErrorMessage = "Must be at least 6 characters long")]
    [MaxLength(45, ErrorMessage = "Must not exceed 45 characters")]
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value, true);
    }

    [Required(ErrorMessage = "Price must be set")]
    [Range(50, 5000, ErrorMessage = "Price must be between 50 and 5,000")]
    public decimal? Price
    {
        get => _price;
        set => SetProperty(ref _price, value, true);
    }

    [Required(ErrorMessage = "Select its Duration")]
    public string SelectedDurationItem
    {
        get => _selectedDurationItem;
        set => SetProperty(ref _selectedDurationItem, value, true);
    }


    [MaxLength(37, ErrorMessage = "Must not exceed 37 characters")]
    public string FeatureDescription1
    {
        get => _featureDescription1;
        set => SetProperty(ref _featureDescription1, value, true);
    }

    [MaxLength(37, ErrorMessage = "Must not exceed 37 characters")]
    public string FeatureDescription2
    {
        get => _featureDescription2;
        set => SetProperty(ref _featureDescription2, value, true);
    }

    [MaxLength(37, ErrorMessage = "Must not exceed 37 characters")]
    public string FeatureDescription3
    {
        get => _featureDescription3;
        set => SetProperty(ref _featureDescription3, value, true);
    }

    [MaxLength(37, ErrorMessage = "Must not exceed 37 characters")]
    public string FeatureDescription4
    {
        get => _featureDescription4;
        set => SetProperty(ref _featureDescription4, value, true);
    }

    [MaxLength(37, ErrorMessage = "Must not exceed 37 characters")]
    public string FeatureDescription5
    {
        get => _featureDescription5;
        set => SetProperty(ref _featureDescription5, value, true);
    }

    public bool EnableDiscount
    {
        get => _enableDiscount;
        set
        {
            if (!SetField(ref _enableDiscount, value)) return;
            OnPropertyChanged(nameof(IsDiscountEnabled));

            if (value) return;
            DiscountValue = null;
            ValidFrom = null;
            ValidTo = null;

            ClearErrors(nameof(DiscountValue));
            ClearErrors(nameof(ValidFrom));
            ClearErrors(nameof(ValidTo));
        }
    }

    public string SelectedDiscountTypeItem
    {
        get => _selectedDiscountTypeItem;
        set
        {
            if (!SetField(ref _selectedDiscountTypeItem, value)) return;
            OnPropertyChanged(nameof(DiscountSymbol));
            OnPropertyChanged(nameof(DiscountFormat));
        }
    }

    public string SelectedDiscountForItem
    {
        get => _selectedDiscountForItem;
        set => SetProperty(ref _selectedDiscountForItem, value, true);
    }

    public string DiscountSymbol => SelectedDiscountTypeItem == "Percentage (%)" ? "%" : "₱";
    public string DiscountFormat => SelectedDiscountTypeItem == "Percentage (%)" ? "N0" : "N2";

    [Required(ErrorMessage = "Discount value must be set")]
    [Range(1, 100, ErrorMessage = "Discount value must be between 1 and 100")]
    public int? DiscountValue
    {
        get => _discountValue;
        set => SetProperty(ref _discountValue, value, true);
    }

    [Required(ErrorMessage = "Start date is required.")]
    [StartDateValidation(nameof(ValidTo), ErrorMessage = "Start date must happen before the end date")]
    public DateOnly? ValidFrom
    {
        get => _validFrom;
        set
        {
            SetProperty(ref _validFrom, value, true);
            ValidateProperty(ValidTo, nameof(ValidTo));
        }
    }

    [EndDateValidation(nameof(ValidFrom), ErrorMessage = "End date should happen after the start date")]
    public DateOnly? ValidTo
    {
        get => _validTo;
        set
        {
            SetProperty(ref _validTo, value, true);
            ValidateProperty(ValidFrom, nameof(ValidFrom));
        }
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

    private void ClearAllFields()
    {
        PackageName = string.Empty;
        Description = string.Empty;
        Price = null;
        SelectedDurationItem = string.Empty;

        FeatureDescription1 = string.Empty;
        FeatureDescription2 = string.Empty;
        FeatureDescription3 = string.Empty;
        FeatureDescription4 = string.Empty;
        FeatureDescription5 = string.Empty;
        EnableDiscount = false;
        DiscountValue = null;
        ValidFrom = null;
        ValidTo = null;
        ClearAllErrors();
    }

    public decimal? GetFormattedValue() => GetFormattedValue(DiscountValue);
    public string GetDisplayValue() => GetDisplayValue(DiscountValue);

    private decimal? GetFormattedValue(int? value)
    {
        if (!value.HasValue) return null;
        return SelectedDiscountTypeItem == "Percentage (%)" ? value.Value / 100m : value.Value;
    }

    private string GetDisplayValue(int? value)
    {
        if (!value.HasValue) return string.Empty;

        return SelectedDiscountTypeItem == "Percentage (%)"
            ? $"{value.Value}%"
            : $"₱{value.Value:N2}";
    }

    public PackageModel? GetPackageData()
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(PackageName) || !Price.HasValue || Price.Value <= 0)
        {
            return null;
        }

        // Get discount information
        decimal discountAmount = 0;
        string discountType = "none";
        string discountFor = ""; // Add this line
        decimal originalPrice = Price.Value;
        decimal discountedPrice = originalPrice;

        if (EnableDiscount && DiscountValue.HasValue)
        {
            discountAmount = DiscountValue.Value;
            discountType = SelectedDiscountTypeItem == "Percentage (%)" ? "percentage" : "fixed";
            discountFor = SelectedDiscountForItem; // Add this line

            // Calculate discounted price
            if (discountType == "percentage")
            {
                discountedPrice = originalPrice - (originalPrice * discountAmount / 100);
            }
            else // fixed amount
            {
                discountedPrice = originalPrice - discountAmount;
                if (discountedPrice < 0) discountedPrice = 0;
            }
        }

        // Convert DateOnly to DateTime
        DateTime validFromDate = ValidFrom?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Now;
        DateTime validToDate = ValidTo?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Now.AddDays(365);

        return new PackageModel
        {
            packageName = PackageName.Trim(),
            price = originalPrice,
            description = Description?.Trim() ?? string.Empty,
            duration = SelectedDurationItem,
            features1 = FeatureDescription1?.Trim() ?? string.Empty,
            features2 = FeatureDescription2?.Trim() ?? string.Empty,
            features3 = FeatureDescription3?.Trim() ?? string.Empty,
            features4 = FeatureDescription4?.Trim() ?? string.Empty,
            features5 = FeatureDescription5?.Trim() ?? string.Empty,
            discount = discountAmount,
            discountType = discountType,
            discountFor = discountFor, // Add this line
            discountedPrice = discountedPrice,
            validFrom = validFromDate,
            validTo = validToDate
        };
    }
    
    protected override void DisposeManagedResources()
    {
        // Clear text fields and large values
        PackageName = string.Empty;
        Description = string.Empty;
        Price = null;
        SelectedDurationItem = string.Empty;

        // Clear feature descriptions
        FeatureDescription1 = string.Empty;
        FeatureDescription2 = string.Empty;
        FeatureDescription3 = string.Empty;
        FeatureDescription4 = string.Empty;
        FeatureDescription5 = string.Empty;

        // Clear arrays
        DiscountTypeItems = [];
        DiscountForItems = [];
        DurationItems = [];

        // Null services (design-time ctor sets these; aggressively null anyway)
        // (readonly services cannot be set here; only clear properties we control)
        base.DisposeManagedResources();
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public partial class EditPackageDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged 
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
    private int? _price;
    
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
    
    public bool IsDiscountEnabled => EnableDiscount;
    public bool IsDeleteAction { get; private set; }
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public EditPackageDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public EditPackageDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }
    
    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }
    
    [RelayCommand]
    private void Delete()
    {
        IsDeleteAction = true;
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }
    
    [RelayCommand]
    private void SaveDetails()
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
        IsDeleteAction = false;
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
    [Range(0, 5000, ErrorMessage = "Price must be between 0 and 5,000")]
    public int? Price
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
        set =>  SetProperty(ref _selectedDiscountForItem, value, true);
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
    
    public void PopulateFromPackage(Package package)
    {
        PackageName = package.Title;
        Description = package.Description;

        Price = package.Price;
    
        SelectedDurationItem = package.PriceUnit;
    
        // Populate features (up to 5)
        FeatureDescription1 = package.Features.Count > 0 ? package.Features[0] : string.Empty;
        FeatureDescription2 = package.Features.Count > 1 ? package.Features[1] : string.Empty;
        FeatureDescription3 = package.Features.Count > 2 ? package.Features[2] : string.Empty;
        FeatureDescription4 = package.Features.Count > 3 ? package.Features[3] : string.Empty;
        FeatureDescription5 = package.Features.Count > 4 ? package.Features[4] : string.Empty;
    
        // Reset discount settings when editing existing package
        EnableDiscount = package.IsDiscountChecked;
        DiscountValue = package.DiscountValue;
        SelectedDiscountForItem = package.SelectedDiscountFor;
        SelectedDiscountTypeItem = package.SelectedDiscountType;
        ValidFrom = package.DiscountValidFrom;
        ValidTo = package.DiscountValidTo;
    
        ClearAllErrors();
    }
    
    public Package ToPackageOption()
    {
        var features = new List<string>();
    
        if (!string.IsNullOrWhiteSpace(FeatureDescription1)) features.Add(FeatureDescription1);
        if (!string.IsNullOrWhiteSpace(FeatureDescription2)) features.Add(FeatureDescription2);
        if (!string.IsNullOrWhiteSpace(FeatureDescription3)) features.Add(FeatureDescription3);
        if (!string.IsNullOrWhiteSpace(FeatureDescription4)) features.Add(FeatureDescription4);
        if (!string.IsNullOrWhiteSpace(FeatureDescription5)) features.Add(FeatureDescription5);
    
        return new Package
        {
            Title = PackageName,
            Description = Description,
            Price = Price ?? 0,
            PriceUnit = SelectedDurationItem,
            Features = features,
            IsDiscountChecked = EnableDiscount,
            DiscountValue = DiscountValue,
            SelectedDiscountFor = SelectedDiscountForItem,
            SelectedDiscountType = SelectedDiscountTypeItem,
            DiscountValidFrom = ValidFrom,
            DiscountValidTo = ValidTo   
        };
    }
}
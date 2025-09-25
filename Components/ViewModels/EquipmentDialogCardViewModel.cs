using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public partial class EquipmentDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty] 
    private string[] _equipmentFilterItems = ["All", "Strength", "Cardio", "Machines", "Accessories"];
    
    [ObservableProperty] 
    private string[] _statusFilterItems = ["Active", "Inactive"];
    
    [ObservableProperty] 
    private string[] _conditionFilterItems = ["Excellent", "Broken"];
    
    [ObservableProperty] 
    private string[] _supplierFilterItems = ["San Miguel", "FitLab", "Optimum"];
    
    private string _brandName = string.Empty;
    private string _category = string.Empty;
    private string _condition = string.Empty;
    private string _status = string.Empty;
    private string _supplier = string.Empty;
    private int? _currentStock;
    private int? _purchasePrice;
    private DateTime? _purchaseDate;
    private DateTime? _warrantyExpiry;
    private DateTime? _lastMaintenance;
    private DateTime? _nextMaintenance;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    
    [Required(ErrorMessage = "Brand name is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string BrandName 
    {
        get => _brandName;
        set => SetProperty(ref _brandName, value, true);
    }
    
    [Required(ErrorMessage = "Select a category")]
    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value, true);
    }
    
    [Required(ErrorMessage = "Select a condition")]
    public string Condition 
    {
        get => _condition;
        set => SetProperty(ref _condition, value, true);
    }
    
    [Required(ErrorMessage = "Select a status")]
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value, true);
    }
    
    [Required(ErrorMessage = "Select a supplier")]
    public string Supplier 
    {
        get => _supplier;
        set => SetProperty(ref _supplier, value, true);
    }
    
    [Required(ErrorMessage = "Stock is required")]
    [Range(1, 500, ErrorMessage = "Stock must be between 1 and 500")]
    public int? CurrentStock 
    {
        get => _currentStock;
        set => SetProperty(ref _currentStock, value, true);
    }
    
    [Required(ErrorMessage = "Price is required")]
    [Range(1, 500, ErrorMessage = "Price must be between 1 and 15000")]
    public int? PurchasePrice 
    {
        get => _purchasePrice;
        set => SetProperty(ref _purchasePrice, value, true);
    }
    
    [Required(ErrorMessage = "Purchased Date is required")]
    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? PurchasedDate
    {
        get => _purchaseDate;
        set => SetProperty(ref _purchaseDate, value, true);
    }
    
    [Required(ErrorMessage = "Warranty expiry is required")]
    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? WarrantyExpiry
    {
        get => _warrantyExpiry;
        set => SetProperty(ref _warrantyExpiry, value, true);
    }
    
    [Required(ErrorMessage = "Last maintenance is required")]
    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? LastMaintenance
    {
        get => _lastMaintenance;
        set => SetProperty(ref _lastMaintenance, value, true);
    }
    
    [Required(ErrorMessage = "Next maintenance is required")]
    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? NextMaintenance
    {
        get => _nextMaintenance;
        set => SetProperty(ref _nextMaintenance, value, true);
    }
    
    public EquipmentDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public EquipmentDialogCardViewModel()
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
    private void AddEquipment()
    {
        ValidateAllProperties();
        
        if (HasErrors) return;
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }
}
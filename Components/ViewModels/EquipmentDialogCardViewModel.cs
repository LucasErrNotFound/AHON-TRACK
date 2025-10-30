using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Services;

namespace AHON_TRACK.Components.ViewModels;

public partial class EquipmentDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _equipmentFilterItems = ["All", "Strength", "Cardio", "Machines", "Accessories"];

    [ObservableProperty]
    private string[] _conditionFilterItems = ["Excellent", "Repairing", "Broken"];

    [ObservableProperty]
    private string[] _statusFilterItems = ["Active", "Inactive", "Under Maintenance", "Retired", "On Loan"];

    // Supplier dropdown items - now contains supplier names as strings
    [ObservableProperty]
    private string[] _supplierFilterItems = [];

    // This maintains the internal list of supplier objects
    private List<SupplierDropdownModel> _supplierModels = [];

    [ObservableProperty]
    private string _dialogTitle = "Add New Equipment";

    [ObservableProperty]
    private string _dialogDescription = "Easily register gym equipment with details like brand name, category, quantity, etc.";

    [ObservableProperty]
    private bool _isEditMode = false;

    [ObservableProperty]
    private bool _isLoadingSuppliers = false;

    private int _equipmentID;
    private string? _brandName = string.Empty;
    private string? _category = string.Empty;
    private string? _condition = string.Empty;
    private string? _status = "Active";
    private string? _supplier = string.Empty;  // This binds to XAML
    private int? _currentStock;
    private decimal? _purchasePrice;
    private DateTime? _purchaseDate;
    private DateTime? _warrantyExpiry;
    private DateTime? _lastMaintenance;
    private DateTime? _nextMaintenance;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IInventoryService _inventoryService;

    public int EquipmentID
    {
        get => _equipmentID;
        set => SetProperty(ref _equipmentID, value);
    }

    [Required(ErrorMessage = "Brand name is required")]
    [RegularExpression("^[a-zA-Z0-9 ]*$", ErrorMessage = "cannot contain special characters.")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string? BrandName
    {
        get => _brandName;
        set => SetProperty(ref _brandName, value, true);
    }

    [Required(ErrorMessage = "Select a category")]
    public string? Category
    {
        get => _category;
        set => SetProperty(ref _category, value, true);
    }

    [Required(ErrorMessage = "Select a condition")]
    public string? Condition
    {
        get => _condition;
        set => SetProperty(ref _condition, value, true);
    }

    [Required(ErrorMessage = "Select a status")]
    public string? Status
    {
        get => _status;
        set => SetProperty(ref _status, value, true);
    }

    // This property binds to XAML - it's the supplier NAME (string)
    [Required(ErrorMessage = "Select a supplier")]
    public string? Supplier
    {
        get => _supplier;
        set => SetProperty(ref _supplier, value, true);
    }

    // Helper property to get the SupplierID based on selected supplier name
    public int? SupplierID
    {
        get
        {
            if (string.IsNullOrEmpty(Supplier))
                return null;

            var supplierModel = _supplierModels.FirstOrDefault(s => s.SupplierName == Supplier);
            return supplierModel?.SupplierID;
        }
    }

    // Helper property to get the selected supplier model (for compatibility)
    public SupplierDropdownModel? SelectedSupplierModel
    {
        get
        {
            if (string.IsNullOrEmpty(Supplier))
                return null;

            return _supplierModels.FirstOrDefault(s => s.SupplierName == Supplier);
        }
    }

    [Required(ErrorMessage = "Stock is required")]
    [Range(0, 500, ErrorMessage = "Stock must be between 0 and 500")]
    public int? CurrentStock
    {
        get => _currentStock;
        set => SetProperty(ref _currentStock, value, true);
    }

    [Required(ErrorMessage = "Price is required")]
    [Range(0.01, 1000000000, ErrorMessage = "Price must be between 0.01 and 1000000000")]
    public decimal? PurchasePrice
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

    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? WarrantyExpiry
    {
        get => _warrantyExpiry;
        set => SetProperty(ref _warrantyExpiry, value, true);
    }

    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? LastMaintenance
    {
        get => _lastMaintenance;
        set => SetProperty(ref _lastMaintenance, value, true);
    }

    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? NextMaintenance
    {
        get => _nextMaintenance;
        set => SetProperty(ref _nextMaintenance, value, true);
    }

    public EquipmentDialogCardViewModel(
        DialogManager dialogManager,
        ToastManager toastManager,
        PageManager pageManager,
        IInventoryService inventoryService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _inventoryService = inventoryService;
    }

    public EquipmentDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _inventoryService = null!;
    }

    [AvaloniaHotReload]
    public async void Initialize()
    {
        DialogTitle = "Add New Equipment";
        DialogDescription = "Easily register gym equipment with details like brand name, category, quantity, etc.";
        IsEditMode = false;
        EquipmentID = 0;
        ClearAllFields();
        await LoadSuppliersAsync();
    }

    public async void InitializeForEditMode(Equipment? equipment)
    {
        IsEditMode = true;
        DialogTitle = "Edit Equipment Details";
        DialogDescription = "Edit gym equipment with details like brand name, category, quantity, etc.";

        ClearAllErrors();

        EquipmentID = equipment?.ID ?? 0;
        BrandName = equipment?.BrandName;
        Category = equipment?.Category;
        Condition = equipment?.Condition;
        Status = equipment?.Status ?? "Active";
        CurrentStock = equipment?.CurrentStock;
        PurchasePrice = equipment?.PurchasedPrice;
        PurchasedDate = equipment?.PurchasedDate;
        WarrantyExpiry = equipment?.Warranty;
        LastMaintenance = equipment?.LastMaintenance;
        NextMaintenance = equipment?.NextMaintenance;

        // Load suppliers first
        await LoadSuppliersAsync();

        // Set the supplier NAME (not ID) to match the XAML binding
        if (equipment?.SupplierID.HasValue == true)
        {
            var supplierModel = _supplierModels.FirstOrDefault(s => s.SupplierID == equipment.SupplierID);
            Supplier = supplierModel?.SupplierName;
        }
        else if (!string.IsNullOrEmpty(equipment?.SupplierName))
        {
            // Fallback: use the supplier name directly if ID lookup fails
            Supplier = equipment.SupplierName;
        }

        else
        {
            Supplier = "None";
        }
    }

    private async Task LoadSuppliersAsync()
    {
        if (_inventoryService == null)
        {
            // Fallback for design-time
            _supplierModels = new List<SupplierDropdownModel>
                    {
                        new() { SupplierID = 1, SupplierName = "San Miguel" },
                        new() { SupplierID = 2, SupplierName = "FitLab" },
                        new() { SupplierID = 3, SupplierName = "Optimum" }
                    };
            // Add "None" option at the beginning
            var supplierNames = new List<string> { "None" };
            supplierNames.AddRange(_supplierModels.Select(s => s.SupplierName));
            SupplierFilterItems = supplierNames.ToArray();
            return;
        }

        IsLoadingSuppliers = true;
        try
        {
            var (success, message, suppliers) = await _inventoryService.GetSuppliersForDropdownAsync();

            if (success && suppliers != null && suppliers.Any())
            {
                _supplierModels = suppliers;

                // Add "None" option at the beginning, followed by supplier names
                var supplierNames = new List<string> { "None" };
                supplierNames.AddRange(_supplierModels.Select(s => s.SupplierName));
                SupplierFilterItems = supplierNames.ToArray();

                // Auto-select first real supplier if adding new equipment (skip "None")
                if (!IsEditMode && _supplierModels.Any())
                {
                    Supplier = "None";
                }
            }
            else
            {
                _toastManager?.CreateToast("Failed to Load Suppliers")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowWarning();

                // Provide "None" option as fallback
                _supplierModels = new List<SupplierDropdownModel>();
                SupplierFilterItems = new[] { "None" };
            }
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Error Loading Suppliers")
                .WithContent($"Failed to load suppliers: {ex.Message}")
                .DismissOnClick()
                .ShowError();

            _supplierModels = new List<SupplierDropdownModel>();
            SupplierFilterItems = new[] { "None" };
        }
        finally
        {
            IsLoadingSuppliers = false;
        }
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

        // Custom validation for supplier
        if (string.IsNullOrEmpty(Supplier))
        {
            _toastManager?.CreateToast("Validation Error")
                .WithContent("Please select a supplier")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        // ? Custom validation for warranty date
        if (PurchasedDate.HasValue && WarrantyExpiry.HasValue)
        {
            if (WarrantyExpiry.Value <= PurchasedDate.Value)
            {
                _toastManager?.CreateToast("Invalid Warranty Date")
                    .WithContent("Warranty expiry must be after the purchase date.")
                    .DismissOnClick()
                    .ShowError();
                return;
            }
        }

        // ? (Optional) If you want to ensure purchase date is not in the future
        if (PurchasedDate.HasValue && PurchasedDate.Value > DateTime.Today)
        {
            _toastManager?.CreateToast("Invalid Purchase Date")
                .WithContent("Purchase date cannot be in the future.")
                .DismissOnClick()
                .ShowError();
            return;
        }

        if (HasErrors) return;

        _dialogManager.Close(this, new CloseDialogOptions { Success = true });

        // Custom validation for supplier
        if (string.IsNullOrEmpty(Supplier))
        {
            _toastManager?.CreateToast("Validation Error")
                .WithContent("Please select a supplier")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        if (HasErrors) return;

        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    private void ClearAllFields()
    {
        BrandName = string.Empty;
        Category = string.Empty;
        Condition = string.Empty;
        Status = "Active";
        Supplier = string.Empty;
        CurrentStock = null;
        PurchasePrice = null;
        PurchasedDate = null;
        WarrantyExpiry = null;
        LastMaintenance = null;
        NextMaintenance = null;
        ClearAllErrors();
    }
    
    protected override void DisposeManagedResources()
    {
        // Clear supplier models and UI lists
        _supplierModels?.Clear();
        SupplierFilterItems = [];

        // Clear text/fields
        BrandName = string.Empty;
        Category = string.Empty;
        Condition = string.Empty;
        Status = string.Empty;
        Supplier = string.Empty;

        // Null services (we can't reassign readonly injected ones) — clear what we own
        base.DisposeManagedResources();
    }
}
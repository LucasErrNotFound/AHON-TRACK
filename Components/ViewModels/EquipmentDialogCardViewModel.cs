using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Services;
using Microsoft.Extensions.Logging;

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

    [ObservableProperty] private bool _isEditMode = false;
    [ObservableProperty] private bool _isLoadingSuppliers = false;
    [ObservableProperty] private bool _isInitialized;

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
    private readonly IInventoryService _inventoryService;
    private readonly ILogger _logger;

    public int EquipmentID
    {
        get => _equipmentID;
        set => SetProperty(ref _equipmentID, value);
    }

    [Required(ErrorMessage = "Brand name is required")]
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
        IInventoryService inventoryService,
        ILogger logger)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public EquipmentDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _inventoryService = null!;
        _logger = null!;
    }

    /*
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
    */
    
    [AvaloniaHotReload]
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
    
        if (IsInitialized)
        {
            _logger?.LogDebug("EquipmentDialogCardViewModel already initialized");
            return;
        }

        _logger?.LogInformation("Initializing EquipmentDialogCardViewModel");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            DialogTitle = "Add New Equipment";
            DialogDescription = "Easily register gym equipment with details like brand name, category, quantity, etc.";
            IsEditMode = false;
            EquipmentID = 0;
            ClearAllFields();
        
            await LoadSuppliersAsync(linkedCts.Token).ConfigureAwait(false);
        
            IsInitialized = true;
            _logger?.LogInformation("EquipmentDialogCardViewModel initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("EquipmentDialogCardViewModel initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing EquipmentDialogCardViewModel");
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Navigating away from EquipmentDialog");
        return ValueTask.CompletedTask;
    }

    public async Task InitializeForEditMode(Equipment? equipment, CancellationToken cancellationToken = default)
    {
        try
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

            _logger?.LogInformation("Initializing edit mode for equipment {EquipmentId}", EquipmentID);

            // Load suppliers first
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);
        
            await LoadSuppliersAsync(linkedCts.Token).ConfigureAwait(false);

            // Set the supplier NAME (not ID) to match the XAML binding
            if (equipment?.SupplierID.HasValue == true)
            {
                var supplierModel = _supplierModels.FirstOrDefault(s => s.SupplierID == equipment.SupplierID);
                Supplier = supplierModel?.SupplierName;
            
                if (string.IsNullOrEmpty(Supplier))
                {
                    _logger?.LogWarning("Supplier ID {SupplierId} not found in loaded suppliers", 
                        equipment.SupplierID);
                }
            }
            else if (!string.IsNullOrEmpty(equipment?.SupplierName))
            {
                Supplier = equipment.SupplierName;
            }
            else
            {
                Supplier = "None";
            }

            _logger?.LogDebug("Edit mode initialized for equipment {EquipmentId}: {BrandName}", 
                EquipmentID, BrandName);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("InitializeForEditMode cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing edit mode");
            _toastManager?.CreateToast("Error")
                .WithContent("Failed to initialize edit mode")
                .DismissOnClick()
                .ShowError();
        }
    }
    
    /*
    public async void InitializeForEditMode(Equipment? equipment)
    {
        await InitializeForEditMode(equipment, LifecycleToken);
    }
    */

    private async Task LoadSuppliersAsync(CancellationToken cancellationToken = default)
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
        
            var supplierNames = new List<string> { "None" };
            supplierNames.AddRange(_supplierModels.Select(s => s.SupplierName));
            SupplierFilterItems = supplierNames.ToArray();
            return;
        }

        IsLoadingSuppliers = true;
    
        try
        {
            var (success, message, suppliers) = await _inventoryService
                .GetSuppliersForDropdownAsync()
                .ConfigureAwait(false);
        
            cancellationToken.ThrowIfCancellationRequested();

            if (success && suppliers != null && suppliers.Any())
            {
                _supplierModels = suppliers;

                var supplierNames = new List<string> { "None" };
                supplierNames.AddRange(_supplierModels.Select(s => s.SupplierName));
                SupplierFilterItems = supplierNames.ToArray();

                if (!IsEditMode && _supplierModels.Any())
                {
                    Supplier = "None";
                }

                _logger?.LogDebug("Loaded {Count} suppliers", _supplierModels.Count);
            }
            else
            {
                _logger?.LogWarning("Failed to load suppliers: {Message}", message);
            
                _toastManager?.CreateToast("Failed to Load Suppliers")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowWarning();

                _supplierModels = new List<SupplierDropdownModel>();
                SupplierFilterItems = new[] { "None" };
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("LoadSuppliersAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading suppliers");
        
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
        try
        {
            _logger?.LogDebug("Equipment dialog cancelled");
            _dialogManager.Close(this);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during cancel");
        }
    }

    [RelayCommand]
    private async Task AddEquipment()
    {
        try
        {
            ValidateAllProperties();

            // Custom validation for supplier
            if (string.IsNullOrEmpty(Supplier) || Supplier == "None")
            {
                _logger?.LogWarning("No supplier selected");
                _toastManager?.CreateToast("Validation Error")
                    .WithContent("Please select a supplier")
                    .DismissOnClick()
                    .ShowWarning();
                return;
            }

            // Custom validation for warranty date
            if (PurchasedDate.HasValue && WarrantyExpiry.HasValue)
            {
                if (WarrantyExpiry.Value <= PurchasedDate.Value)
                {
                    _logger?.LogWarning("Invalid warranty date: expiry before purchase");
                    _toastManager?.CreateToast("Invalid Warranty Date")
                        .WithContent("Warranty expiry must be after the purchase date.")
                        .DismissOnClick()
                        .ShowError();
                    return;
                }
            }

            // Validate purchase date is not in the future
            if (PurchasedDate.HasValue && PurchasedDate.Value > DateTime.Today)
            {
                _logger?.LogWarning("Invalid purchase date: in the future");
                _toastManager?.CreateToast("Invalid Purchase Date")
                    .WithContent("Purchase date cannot be in the future.")
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            if (HasErrors)
            {
                _logger?.LogWarning("Equipment validation failed");
                return;
            }

            _logger?.LogInformation("Equipment {Mode}: {BrandName}", 
                IsEditMode ? "edited" : "added", BrandName);
        
            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding/editing equipment");
            _toastManager?.CreateToast("Error")
                .WithContent($"An error occurred: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
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
    
    protected override async ValueTask DisposeAsyncCore()
    {
        _logger?.LogInformation("Disposing EquipmentDialogCardViewModel");

        // Clear collections
        _supplierModels.Clear();
        SupplierFilterItems = [];

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}
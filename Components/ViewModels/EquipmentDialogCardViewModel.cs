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
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Services;
using AHON_TRACK.Validators;

namespace AHON_TRACK.Components.ViewModels;

public partial class EquipmentDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _equipmentFilterItems = ["All", "Strength", "Cardio", "Machines", "Accessories"];

    [ObservableProperty]
    private string[] _conditionFilterItems = ["Excellent", "Repairing", "Broken"];

    [ObservableProperty]
    private string[] _statusFilterItems = ["Available", "Not Available"];

    [ObservableProperty]
    private string[] _filteredStatusItems = [];

    [ObservableProperty]
    private string[] _supplierFilterItems = [];

    [ObservableProperty]
    private string[] _brandNameItems = [];

    private List<SupplierDropdownModel> _supplierModels = [];

    // ⭐ Store equipment data from purchase orders (SupplierID -> EquipmentName -> Data)
    private Dictionary<int, Dictionary<string, (decimal Price, decimal Quantity, string Unit, DateTime OrderDate)>> _supplierEquipmentData = new();

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
    private string? _status = "Available";
    private string? _supplier = string.Empty;
    private int? _quantity;
    private decimal? _purchasePrice;
    private DateTime? _purchaseDate;
    private DateTime? _warrantyExpiry;
    private DateTime? _lastMaintenance;
    private DateTime? _nextMaintenance;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IInventoryService _inventoryService;
    private readonly IPurchaseOrderService? _purchaseOrderService;

    public int EquipmentID
    {
        get => _equipmentID;
        set => SetProperty(ref _equipmentID, value);
    }

    [Required(ErrorMessage = "Brand name is required")]
    public string? BrandName
    {
        get => _brandName;
        set
        {
            var oldValue = _brandName;
            SetProperty(ref _brandName, value, true);

            // ⭐ Auto-populate fields when brand name changes (only in Add mode)
            if (!IsEditMode && oldValue != value && !string.IsNullOrWhiteSpace(value) &&
                value != "Select supplier first" &&
                value != "No equipment available" &&
                value != "No new equipment - all items already in inventory")
            {
                AutoPopulateEquipmentFields(value);
            }
        }
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
        set
        {
            var oldValue = _condition;
            SetProperty(ref _condition, value, true);

            if (oldValue != value)
            {
                UpdateFilteredStatusItems();
                AutoSelectStatus();
            }
        }
    }

    [Required(ErrorMessage = "Select a status")]
    public string? Status
    {
        get => _status;
        set => SetProperty(ref _status, value, true);
    }

    [Required(ErrorMessage = "Select a supplier")]
    public string? Supplier
    {
        get => _supplier;
        set
        {
            var oldValue = _supplier;
            SetProperty(ref _supplier, value, true);

            // ⭐ Update brand name items when supplier changes
            if (oldValue != value && !IsEditMode)
            {
                UpdateBrandNameItems(value);
            }
        }
    }

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

    public SupplierDropdownModel? SelectedSupplierModel
    {
        get
        {
            if (string.IsNullOrEmpty(Supplier))
                return null;

            return _supplierModels.FirstOrDefault(s => s.SupplierName == Supplier);
        }
    }

    [Required(ErrorMessage = "Quantity is required")]
    [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100")]
    public int? Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value, true);
    }

    public decimal? PurchasePrice
    {
        get => _purchasePrice;
        set => SetProperty(ref _purchasePrice, value, true);
    }

    [PurchasedDateValidation(nameof(WarrantyExpiry), ErrorMessage = "Purchase date must be before warranty expiry")]
    [NotFutureDate(ErrorMessage = "Purchase date cannot be in the future")]
    public DateTime? PurchasedDate
    {
        get => _purchaseDate;
        set
        {
            var oldValue = _purchaseDate;
            SetProperty(ref _purchaseDate, value, true);

            if (oldValue != value)
            {
                ValidateProperty(WarrantyExpiry, nameof(WarrantyExpiry));
            }
        }
    }

    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    [WarrantyDateValidation(nameof(PurchasedDate), ErrorMessage = "Warranty expiry must be after purchase date")]
    public DateTime? WarrantyExpiry
    {
        get => _warrantyExpiry;
        set
        {
            var oldValue = _warrantyExpiry;
            SetProperty(ref _warrantyExpiry, value, true);

            if (oldValue != value)
            {
                ValidateProperty(PurchasedDate, nameof(PurchasedDate));
            }
        }
    }

    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    [LastMaintenanceDateValidation(nameof(NextMaintenance), ErrorMessage = "Last maintenance must be before next maintenance")]
    [NotFutureDate(ErrorMessage = "Last maintenance cannot be in the future")]
    public DateTime? LastMaintenance
    {
        get => _lastMaintenance;
        set
        {
            var oldValue = _lastMaintenance;
            SetProperty(ref _lastMaintenance, value, true);

            if (oldValue != value)
            {
                ValidateProperty(NextMaintenance, nameof(NextMaintenance));
            }
        }
    }

    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    [NextMaintenanceDateValidation(nameof(LastMaintenance), ErrorMessage = "Next maintenance must be after last maintenance")]
    public DateTime? NextMaintenance
    {
        get => _nextMaintenance;
        set
        {
            var oldValue = _nextMaintenance;
            SetProperty(ref _nextMaintenance, value, true);

            if (oldValue != value)
            {
                ValidateProperty(LastMaintenance, nameof(LastMaintenance));
            }
        }
    }

    public EquipmentDialogCardViewModel(
        DialogManager dialogManager,
        ToastManager toastManager,
        PageManager pageManager,
        IInventoryService inventoryService,
        IPurchaseOrderService? purchaseOrderService = null)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _inventoryService = inventoryService;
        _purchaseOrderService = purchaseOrderService;
    }

    public EquipmentDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _inventoryService = null!;
        _purchaseOrderService = null;
    }

    [AvaloniaHotReload]
    public async void Initialize()
    {
        DialogTitle = "Add New Equipment";
        DialogDescription = "Easily register gym equipment with details like brand name, category, quantity, etc.";
        IsEditMode = false;
        EquipmentID = 0;
        ClearAllFields();

        FilteredStatusItems = StatusFilterItems;

        await LoadSuppliersAsync();
        await LoadEquipmentFromPurchaseOrdersAsync();
    }

    private async Task LoadEquipmentFromPurchaseOrdersAsync()
    {
        if (_inventoryService == null || _purchaseOrderService == null)
        {
            BrandNameItems = ["No equipment available"];
            return;
        }

        try
        {
            // Get existing equipment names
            var existingEquipmentResult = await _inventoryService.GetEquipmentAsync();
            var existingEquipmentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (existingEquipmentResult.Success && existingEquipmentResult.Equipment != null)
            {
                foreach (var equipment in existingEquipmentResult.Equipment)
                {
                    if (!string.IsNullOrWhiteSpace(equipment.EquipmentName))
                    {
                        existingEquipmentNames.Add(equipment.EquipmentName);
                    }
                }
                Console.WriteLine($"🏋️ Found {existingEquipmentNames.Count} existing equipment to filter out");
            }

            var poResult = await _purchaseOrderService.GetAllPurchaseOrdersAsync();
            if (!poResult.Success || poResult.PurchaseOrders == null)
            {
                BrandNameItems = ["No equipment available"];
                return;
            }

            // Filter: Only "Equipment" category orders that are delivered+paid and not sent to inventory
            var deliveredPaidEquipmentOrders = poResult.PurchaseOrders
                .Where(po => po.ShippingStatus?.Equals("Delivered", StringComparison.OrdinalIgnoreCase) == true &&
                            po.PaymentStatus?.Equals("Paid", StringComparison.OrdinalIgnoreCase) == true &&
                            !po.SentToInventory &&
                            po.Category?.Equals("Equipment", StringComparison.OrdinalIgnoreCase) == true &&
                            po.SupplierID.HasValue &&
                            po.Items != null && po.Items.Count > 0)
                .ToList();

            Console.WriteLine($"🔍 Found {deliveredPaidEquipmentOrders.Count} delivered+paid EQUIPMENT orders that haven't been sent to inventory");

            _supplierEquipmentData.Clear();

            foreach (var po in deliveredPaidEquipmentOrders)
            {
                if (!po.SupplierID.HasValue) continue;

                // Initialize supplier data dictionary
                if (!_supplierEquipmentData.ContainsKey(po.SupplierID.Value))
                {
                    _supplierEquipmentData[po.SupplierID.Value] = new Dictionary<string, (decimal, decimal, string, DateTime)>();
                }

                foreach (var item in po.Items.Where(i => !string.IsNullOrWhiteSpace(i.ItemName)))
                {
                    var equipmentName = item.ItemName;

                    // Skip if already exists in inventory
                    if (existingEquipmentNames.Contains(equipmentName))
                    {
                        Console.WriteLine($"⏭️ Skipping '{equipmentName}' - already exists in inventory");
                        continue;
                    }

                    // Store equipment data: Price, Quantity, Unit, OrderDate
                    _supplierEquipmentData[po.SupplierID.Value][equipmentName] =
                        (item.Price, item.Quantity, item.Unit ?? "pcs", po.OrderDate);

                    Console.WriteLine($"💾 Stored data for '{equipmentName}': Price=₱{item.Price:N2}, Qty={item.Quantity} {item.Unit}, Date={po.OrderDate:yyyy-MM-dd}");
                }
            }

            // Initially show "Select supplier first"
            BrandNameItems = ["Select supplier first"];
            Console.WriteLine($"✅ Loaded equipment data from {deliveredPaidEquipmentOrders.Count} purchase orders");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading equipment: {ex.Message}");
            BrandNameItems = ["No equipment available"];
        }
    }

    // ⭐ NEW: Update brand name items when supplier changes
    private void UpdateBrandNameItems(string? selectedSupplier)
    {
        if (string.IsNullOrEmpty(selectedSupplier) || selectedSupplier == "None")
        {
            BrandNameItems = ["Select supplier first"];
            BrandName = null;
            return;
        }

        // Find supplier ID
        var supplierModel = _supplierModels.FirstOrDefault(s => s.SupplierName == Supplier);
        if (supplierModel == null || supplierModel.SupplierID == 0)
        {
            Console.WriteLine($"⚠️ Supplier '{Supplier}' not found in supplier list");
            return;
        }

        int supplierId = supplierModel.SupplierID;


        // Get equipment for this supplier
        if (_supplierEquipmentData.TryGetValue(supplierId, out var equipmentDict) && equipmentDict.Count > 0)
        {
            var sortedEquipment = equipmentDict.Keys.OrderBy(k => k).ToArray();
            BrandNameItems = sortedEquipment;
            BrandName = sortedEquipment.FirstOrDefault();

            Console.WriteLine($"✅ Loaded {sortedEquipment.Length} equipment items for supplier '{selectedSupplier}'");
        }
        else
        {
            BrandNameItems = ["No new equipment - all items already in inventory"];
            BrandName = null;
            Console.WriteLine($"ℹ️ No new equipment for supplier '{selectedSupplier}'");
        }
    }

    // ⭐ NEW: Auto-populate equipment fields when brand name is selected
    private void AutoPopulateEquipmentFields(string equipmentName)
    {
        try
        {
            // Get the selected supplier ID
            if (string.IsNullOrEmpty(Supplier) || Supplier == "None")
            {
                Console.WriteLine("⚠️ No supplier selected, cannot auto-populate");
                return;
            }

            var supplierModel = _supplierModels.FirstOrDefault(s => s.SupplierName == Supplier);
            if (supplierModel == null || supplierModel.SupplierID == 0)
            {
                Console.WriteLine($"⚠️ Supplier '{Supplier}' not found in supplier list");
                return;
            }

            int supplierId = supplierModel.SupplierID;


            // Get equipment data
            if (_supplierEquipmentData.TryGetValue(supplierId, out var equipmentDict) &&
                equipmentDict.TryGetValue(equipmentName, out var equipmentData))
            {
                // ⭐ Auto-fill Purchase Price
                PurchasePrice = equipmentData.Price;
                Console.WriteLine($"✅ Auto-filled purchase price: ₱{equipmentData.Price:N2}");

                // ⭐ Auto-fill Quantity (convert from PO unit to stock count)
                int calculatedQuantity = CalculateQuantityFromPO(equipmentData.Quantity, equipmentData.Unit);
                Quantity = calculatedQuantity;
                Console.WriteLine($"✅ Auto-filled quantity: {calculatedQuantity} (from {equipmentData.Quantity} {equipmentData.Unit})");

                // ⭐ Auto-fill Purchase Date
                PurchasedDate = equipmentData.OrderDate;
                Console.WriteLine($"✅ Auto-filled purchase date: {equipmentData.OrderDate:yyyy-MM-dd}");
            }
            else
            {
                Console.WriteLine($"⚠️ No data found for supplier {supplierId}, equipment '{equipmentName}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error auto-populating equipment fields: {ex.Message}");
        }
    }

    // ⭐ NEW: Convert PO quantity to equipment quantity based on unit
    private int CalculateQuantityFromPO(decimal quantity, string unit)
    {
        string unitLower = unit.ToLowerInvariant();

        return unitLower switch
        {
            "pcs" or "unit" or "piece" or "pieces" => (int)Math.Floor(quantity),
            "box" or "pack" or "case" => (int)Math.Floor(quantity),
            "kg" or "kilogram" or "kilograms" => (int)Math.Floor(quantity),
            "lbs" or "pound" or "pounds" => (int)Math.Floor(quantity),
            "set" or "sets" => (int)Math.Floor(quantity),
            _ => (int)Math.Floor(quantity)
        };
    }

    public async void InitializeForEditMode(Equipment? equipment)
    {
        IsEditMode = true;
        DialogTitle = "Edit Equipment Details";
        DialogDescription = "Edit gym equipment with details like brand name, category, quantity, etc.";

        ClearAllErrors();

        FilteredStatusItems = StatusFilterItems;

        EquipmentID = equipment?.ID ?? 0;
        BrandName = equipment?.BrandName;
        Category = equipment?.Category;
        Condition = equipment?.Condition;
        Status = equipment?.Status ?? "Available";
        Quantity = equipment?.Quantity;
        PurchasePrice = equipment?.PurchasedPrice;
        PurchasedDate = equipment?.PurchasedDate;
        WarrantyExpiry = equipment?.Warranty;
        LastMaintenance = equipment?.LastMaintenance;
        NextMaintenance = equipment?.NextMaintenance;

        await LoadSuppliersAsync();

        Condition = equipment?.Condition;
        Status = equipment?.Status ?? "Available";

        if (equipment?.SupplierID.HasValue == true)
        {
            var supplierModel = _supplierModels.FirstOrDefault(s => s.SupplierID == equipment.SupplierID);
            Supplier = supplierModel?.SupplierName;
        }
        else if (!string.IsNullOrEmpty(equipment?.SupplierName))
        {
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
            var (success, message, suppliers) = await _inventoryService.GetSuppliersForDropdownAsync();

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
            }
            else
            {
                _toastManager?.CreateToast("Failed to Load Suppliers")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowWarning();

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

    private void UpdateFilteredStatusItems()
    {
        if (string.IsNullOrEmpty(Condition))
        {
            FilteredStatusItems = StatusFilterItems;
            return;
        }

        FilteredStatusItems = Condition switch
        {
            "Excellent" => new[] { "Available" },
            "Repairing" or "Broken" => new[] { "Not Available" },
            _ => StatusFilterItems
        };
    }

    private void AutoSelectStatus()
    {
        if (string.IsNullOrEmpty(Condition))
        {
            Status = "Available";
            return;
        }

        Status = Condition switch
        {
            "Excellent" => "Available",
            "Repairing" or "Broken" => "Not Available",
            _ => "Available"
        };
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

        if (HasErrors)
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Please correct the form errors")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    private void ClearAllFields()
    {
        BrandName = string.Empty;
        Category = string.Empty;
        Condition = string.Empty;
        Status = "Available";
        Supplier = string.Empty;
        Quantity = null;
        PurchasePrice = null;
        PurchasedDate = null;
        WarrantyExpiry = null;
        LastMaintenance = null;
        NextMaintenance = null;
        FilteredStatusItems = StatusFilterItems;
        ClearAllErrors();
    }

    protected override void DisposeManagedResources()
    {
        _supplierModels?.Clear();
        _supplierEquipmentData?.Clear();
        SupplierFilterItems = [];
        BrandNameItems = [];

        BrandName = string.Empty;
        Category = string.Empty;
        Condition = string.Empty;
        Status = string.Empty;
        Supplier = string.Empty;

        base.DisposeManagedResources();
    }
}
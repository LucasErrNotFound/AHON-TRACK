using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

[Page("purchase-order")]
public partial class PurchaseOrderViewModel : ViewModelBase, INavigableWithParameters
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSendToInventory))]
    [NotifyPropertyChangedFor(nameof(SendToInventoryButtonText))]
    [NotifyPropertyChangedFor(nameof(ShowInventorySentBadge))]
    private bool _sentToInventory;

    [ObservableProperty]
    private DateTime? _sentToInventoryDate;

    [ObservableProperty]
    private int? _sentToInventoryBy;

    [ObservableProperty]
    private ObservableCollection<Item> _items = [];

    [ObservableProperty]
    private PurchaseOrderContext _viewContext = PurchaseOrderContext.AddPurchaseOrder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Vat))]
    [NotifyPropertyChangedFor(nameof(Total))]
    private decimal _subtotal;

    public decimal Vat => Subtotal * 0.12m;

    public decimal Total => Subtotal + Vat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInEditMode))]
    [NotifyPropertyChangedFor(nameof(IsInViewMode))]
    [NotifyPropertyChangedFor(nameof(ShowSupplierValidation))]
    [NotifyPropertyChangedFor(nameof(CanEditFields))]
    private bool _isSaved;

    public bool IsInEditMode => !IsSaved && ViewContext == PurchaseOrderContext.AddPurchaseOrder;
    public bool IsInViewMode => IsSaved || ViewContext == PurchaseOrderContext.ViewPurchaseOrder;

    // Only show supplier validation when creating NEW orders (not editing existing)
    public bool ShowSupplierValidation => ViewContext == PurchaseOrderContext.AddPurchaseOrder
                                          && !PurchaseOrderId.HasValue
                                          && SelectedSupplier == null;

    // Always allow editing these fields when viewing an existing PO
    public bool CanEditFields => true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDelivered))]
    [NotifyPropertyChangedFor(nameof(IsShippingStatusEnabled))]
    [NotifyPropertyChangedFor(nameof(CanSendToInventory))]
    private string? _shippingStatus;

    public bool IsShippingStatusEnabled => HasInvoice;

    public bool IsDelivered => ShippingStatus == "Delivered";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvailablePaymentStatusOptions))]
    [NotifyPropertyChangedFor(nameof(IsPaymentStatusEnabled))]
    [NotifyPropertyChangedFor(nameof(CanSendToInventory))]
    private string? _paymentStatus;

    [ObservableProperty]
    [Required(ErrorMessage = "PO Number is required")]
    private string _poNumber = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "Delivery Date is required")]
    private DateTime? _deliveryDate;

    [ObservableProperty]
    [MaxLength(100, ErrorMessage = "Invoice Number cannot exceed 100 characters")]
    [NotifyPropertyChangedFor(nameof(AvailablePaymentStatusOptions))]
    [NotifyPropertyChangedFor(nameof(IsPaymentStatusEnabled))]
    [NotifyPropertyChangedFor(nameof(IsShippingStatusEnabled))]
    [NotifyPropertyChangedFor(nameof(HasInvoice))]
    private string? _invoiceNumber;

    // ? NEW: Supplier Selection
    [ObservableProperty]
    [Required(ErrorMessage = "Supplier is required")]
    [NotifyPropertyChangedFor(nameof(SupplierDisplayName))]
    private Supplier? _selectedSupplier;

    [ObservableProperty]
    private ObservableCollection<Supplier> _availableSuppliers = [];

    public string SupplierDisplayName => SelectedSupplier?.Name ?? "Select a supplier...";

    [ObservableProperty]
    private int? _purchaseOrderId;

    [ObservableProperty]
    private bool _isLoading;

    // Property to check if invoice exists
    public bool HasInvoice => !string.IsNullOrWhiteSpace(InvoiceNumber);

    // Property to enable/disable payment status control
    public bool IsPaymentStatusEnabled => HasInvoice;

    // Add these UI helper properties:
    public string SendToInventoryButtonText => SentToInventory
        ? "Already Sent to Inventory"
        : "Send to Inventory";

    public bool ShowInventorySentBadge => SentToInventory;

    public string InventorySentMessage => SentToInventoryDate.HasValue
        ? $"Sent to inventory on {SentToInventoryDate.Value:MMM dd, yyyy 'at' h:mm tt}"
        : "Sent to inventory";

    // ? NEW: Check if can send to inventory
    public bool CanSendToInventory =>
    IsSaved &&
    !SentToInventory && // ? NEW: Prevent sending if already sent
    ShippingStatus?.Equals("Delivered", StringComparison.OrdinalIgnoreCase) == true &&
    PaymentStatus?.Equals("Paid", StringComparison.OrdinalIgnoreCase) == true;

    public string[] ShippingStatusOptions { get; } =
    [
        "Pending",
        "Processing",
        "Shipped/In-Transit",
        "Delivered",
        "Cancelled"
    ];

    // Full payment status options (when invoice exists)
    private readonly string[] _fullPaymentStatusOptions =
    [
        "Unpaid",
        "Paid",
        "Cancelled"
    ];

    // Limited payment status options (when no invoice)
    private readonly string[] _limitedPaymentStatusOptions =
    [
        "Unpaid",
        "Cancelled"
    ];

    // Dynamic property that returns appropriate options based on invoice presence
    public string[] AvailablePaymentStatusOptions => HasInvoice
        ? _fullPaymentStatusOptions
        : _limitedPaymentStatusOptions;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly ISupplierService? _supplierService;
    private readonly IPurchaseOrderService? _purchaseOrderService;

    public PurchaseOrderViewModel(DialogManager dialogManager, ToastManager toastManager,
        PageManager pageManager, ISupplierService? supplierService = null,
        IPurchaseOrderService? purchaseOrderService = null)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _supplierService = supplierService;
        _purchaseOrderService = purchaseOrderService;

        Items.CollectionChanged += Items_CollectionChanged;
        AddInitialItem();
        _ = InitializeAsync();
    }

    public PurchaseOrderViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());

        Items.CollectionChanged += Items_CollectionChanged;
        AddInitialItem();
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (Items.Count == 0)
        {
            AddInitialItem();
        }
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await GeneratePONumberAsync();
        await LoadSuppliersAsync();
    }

    private async Task LoadSuppliersAsync()
    {
        if (_supplierService == null) return;

        try
        {
            IsLoading = true;
            var result = await _supplierService.GetAllSuppliersAsync();

            if (result.Success && result.Suppliers != null)
            {
                AvailableSuppliers.Clear();
                foreach (var supplier in result.Suppliers)
                {
                    AvailableSuppliers.Add(new Supplier
                    {
                        ID = supplier.SupplierID,
                        Name = supplier.SupplierName,
                        ContactPerson = supplier.ContactPerson,
                        Email = supplier.Email,
                        PhoneNumber = supplier.PhoneNumber,
                        Address = supplier.Address,
                        Products = supplier.Products,
                        Status = supplier.Status
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Loading Suppliers")
                .WithContent($"Failed to load suppliers: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SetNavigationParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Context", out var context))
        {
            SetViewContext((PurchaseOrderContext)context);
        }

        if (parameters.TryGetValue("SelectedSupplier", out var supplier))
        {
            SelectedSupplier = (Supplier)supplier;
        }

        if (parameters.TryGetValue("PurchaseOrderId", out var poId))
        {
            _ = LoadPurchaseOrderAsync((int)poId);
        }
    }

    private async Task LoadPurchaseOrderAsync(int poId)
    {
        if (_purchaseOrderService == null) return;

        try
        {
            IsLoading = true;
            var result = await _purchaseOrderService.GetPurchaseOrderByIdAsync(poId);

            if (result.Success && result.PurchaseOrder != null)
            {
                var po = result.PurchaseOrder;

                PurchaseOrderId = po.PurchaseOrderID;
                PoNumber = po.PONumber;
                DeliveryDate = po.ExpectedDeliveryDate;
                InvoiceNumber = po.InvoiceNumber;
                ShippingStatus = po.ShippingStatus;
                PaymentStatus = po.PaymentStatus;

                // ? NEW: Load sent to inventory status
                SentToInventory = po.SentToInventory;
                SentToInventoryDate = po.SentToInventoryDate;
                SentToInventoryBy = po.SentToInventoryBy;

                // Load supplier
                if (po.SupplierID.HasValue)
                {
                    SelectedSupplier = AvailableSuppliers.FirstOrDefault(s => s.ID == po.SupplierID);

                    if (SelectedSupplier == null && !string.IsNullOrEmpty(po.SupplierName))
                    {
                        SelectedSupplier = new Supplier
                        {
                            ID = po.SupplierID,
                            Name = po.SupplierName
                        };
                        AvailableSuppliers.Add(SelectedSupplier);
                    }
                }

                // Load items
                Items.Clear();
                if (po.Items != null && po.Items.Count > 0)
                {
                    foreach (var item in po.Items)
                    {
                        Items.Add(new Item
                        {
                            ItemName = item.ItemName,
                            SelectedUnit = item.Unit,
                            Quantity = item.Quantity,
                            Price = item.Price
                        });
                    }
                }
                else
                {
                    AddInitialItem();
                }

                IsSaved = true;
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Loading Purchase Order")
                .WithContent($"Failed to load purchase order: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public string ViewTitle => ViewContext switch
    {
        PurchaseOrderContext.AddPurchaseOrder => "Create Purchase Order",
        PurchaseOrderContext.ViewPurchaseOrder => "View Purchase Order",
        _ => "Create Purchase Order"
    };

    public string ViewDescription => ViewContext switch
    {
        PurchaseOrderContext.AddPurchaseOrder => "Creates and records a new purchase order for acquiring products or supplies from a supplier",
        PurchaseOrderContext.ViewPurchaseOrder => "Displays and reviews existing purchase orders with their details and current status",
        _ => "Creates and records a new purchase order for acquiring products or supplies from a supplier"
    };

    public void SetViewContext(PurchaseOrderContext context)
    {
        ViewContext = context;
        IsSaved = context == PurchaseOrderContext.ViewPurchaseOrder;
        OnPropertyChanged(nameof(ViewTitle));
        OnPropertyChanged(nameof(ViewDescription));
    }

    [RelayCommand]
    private void AddItem()
    {
        Items.Add(new Item());
    }

    [RelayCommand]
    private void RemoveItem(Item item)
    {
        if (Items.Count > 1)
        {
            Items.Remove(item);
        }
        else
        {
            _toastManager.CreateToast("Error on last item")
                .WithContent("Cannot remove the last item")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task SaveOrder()
    {
        if (!ValidateOrder())
        {
            return;
        }

        if (_purchaseOrderService == null)
        {
            _toastManager.CreateToast("Service Error")
                .WithContent("Purchase order service is not available")
                .DismissOnClick()
                .ShowError();
            return;
        }

        try
        {
            IsLoading = true;

            // For UPDATES: Use existing SupplierID if SelectedSupplier is null
            // (This happens when viewing existing PO where supplier is shown as text)
            int? supplierIdToUse = SelectedSupplier?.ID;

            // If updating existing PO and no supplier object, get SupplierID from loaded PO
            if (PurchaseOrderId.HasValue && supplierIdToUse == null && _purchaseOrderService != null)
            {
                var existingPO = await _purchaseOrderService.GetPurchaseOrderByIdAsync(PurchaseOrderId.Value);
                if (existingPO.Success && existingPO.PurchaseOrder != null)
                {
                    supplierIdToUse = existingPO.PurchaseOrder.SupplierID;
                }
            }

            // Create PurchaseOrderModel
            var purchaseOrder = new PurchaseOrderModel
            {
                PONumber = PoNumber,
                SupplierID = supplierIdToUse, // Use the resolved SupplierID
                OrderDate = DateTime.Now,
                ExpectedDeliveryDate = DeliveryDate ?? DateTime.Now.AddDays(7),
                ShippingStatus = string.IsNullOrWhiteSpace(ShippingStatus) ? "Pending" : ShippingStatus,
                PaymentStatus = string.IsNullOrWhiteSpace(PaymentStatus) ? "Unpaid" : PaymentStatus,
                InvoiceNumber = InvoiceNumber,
                TaxRate = 0.12m,
                Items = Items.Select(i => new PurchaseOrderItemModel
                {
                    ItemName = i.ItemName ?? string.Empty,
                    Unit = i.SelectedUnit ?? "pcs",
                    Quantity = i.Quantity,
                    Price = i.Price
                }).ToList()
            };

            if (PurchaseOrderId.HasValue)
            {
                // Update existing
                purchaseOrder.PurchaseOrderID = PurchaseOrderId.Value;
                var result = await _purchaseOrderService.UpdatePurchaseOrderAsync(purchaseOrder);

                if (result.Success)
                {
                    IsSaved = true;
                    _toastManager.CreateToast("Purchase Order Updated")
                        .WithContent($"Purchase Order {PoNumber} has been updated successfully!")
                        .DismissOnClick()
                        .ShowSuccess();
                }
                else
                {
                    _toastManager.CreateToast("Update Failed")
                        .WithContent(result.Message)
                        .DismissOnClick()
                        .ShowError();
                }
            }
            else
            {
                // Create new - validate supplier is required
                if (supplierIdToUse == null)
                {
                    _toastManager.CreateToast("Validation Error")
                        .WithContent("Please select a supplier")
                        .DismissOnClick()
                        .ShowError();
                    return;
                }

                var result = await _purchaseOrderService.CreatePurchaseOrderAsync(purchaseOrder);

                if (result.Success && result.POId.HasValue)
                {
                    PurchaseOrderId = result.POId.Value;
                    IsSaved = true;

                    _toastManager.CreateToast("Purchase Order Created")
                        .WithContent($"Purchase Order {PoNumber} has been created successfully!")
                        .DismissOnClick()
                        .ShowSuccess();
                }
                else
                {
                    _toastManager.CreateToast("Creation Failed")
                        .WithContent(result.Message)
                        .DismissOnClick()
                        .ShowError();
                }
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error")
                .WithContent($"An error occurred: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveDetails()
    {
        await SaveOrder();
    }

    [RelayCommand]
    private async Task SendToInventory()
    {
        if (!CanSendToInventory)
        {
            _toastManager.CreateToast("Invalid Status")
                .WithContent("Purchase order must be Delivered and Paid before sending to inventory")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        if (_purchaseOrderService == null || !PurchaseOrderId.HasValue)
        {
            return;
        }

        _dialogManager.CreateDialog(
            "Send to Inventory",
            "This will add all items from this purchase order to your product inventory. Continue?")
            .WithPrimaryButton("Yes, Send to Inventory", async () => await OnConfirmSendToInventory())
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    private async Task OnConfirmSendToInventory()
    {
        if (_purchaseOrderService == null || !PurchaseOrderId.HasValue) return;

        try
        {
            IsLoading = true;

            var result = await _purchaseOrderService.SendToInventoryAsync(PurchaseOrderId.Value);

            if (result.Success)
            {
                _toastManager.CreateToast("Sent to Inventory")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowSuccess();
            }
            else
            {
                _toastManager.CreateToast("Failed to Send")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error")
                .WithContent($"An error occurred: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CancelOrder()
    {
        _dialogManager.CreateDialog(
            "Cancel Purchase Order",
            "Are you sure you want to cancel? All unsaved changes will be lost.")
            .WithPrimaryButton("Yes, Cancel", () => NavigateBack(), DialogButtonStyle.Destructive)
            .WithCancelButton("No, Continue Editing")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void EditOrder()
    {
        // Don't set IsSaved to false immediately - this causes validation issues
        // Instead, just allow editing of specific fields

        _toastManager.CreateToast("Edit Mode")
            .WithContent("You can now edit the purchase order details")
            .DismissOnClick()
            .ShowInfo();

        // Note: We keep IsSaved = true so supplier validation doesn't trigger
        // Only allow editing invoice, shipping status, payment status, and delivery date
    }

    [RelayCommand]
    private async Task DeleteOrder()
    {
        _dialogManager.CreateDialog(
            "Delete Purchase Order",
            $"Are you sure you want to delete Purchase Order {PoNumber}? This action cannot be undone.")
            .WithPrimaryButton("Delete", async () => await OnConfirmDelete(), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void NavigateBack()
    {
        _pageManager.Navigate<SupplierManagementViewModel>();
    }

    // New method to validate payment status changes
    partial void OnPaymentStatusChanging(string? value)
    {
        // Prevent setting to "Paid" if no invoice exists
        if (value == "Paid" && !HasInvoice)
        {
            _toastManager.CreateToast("Payment Status Error")
                .WithContent("Cannot mark as 'Paid' without an invoice number. Please add invoice number first.")
                .DismissOnClick()
                .ShowError();

            // Keep current value or default to Unpaid
            PaymentStatus = string.IsNullOrWhiteSpace(PaymentStatus) ? "Unpaid" : PaymentStatus;
        }
    }

    private void AddInitialItem()
    {
        Items.Add(new Item());
    }

    private async Task GeneratePONumberAsync()
    {
        if (_purchaseOrderService != null)
        {
            PoNumber = await _purchaseOrderService.GeneratePONumberAsync();
        }
        else
        {
            var random = new Random();
            var randomNumber = random.Next(100000, 999999);
            PoNumber = $"PO-AHON-{DateTime.Now.Year}-{randomNumber}";
        }
    }

    private bool ValidateOrder()
    {
        // Only validate supplier when creating a brand new PO (not when editing existing)
        if (ViewContext == PurchaseOrderContext.AddPurchaseOrder && !PurchaseOrderId.HasValue && SelectedSupplier == null)
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Please select a supplier")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        if (string.IsNullOrWhiteSpace(PoNumber))
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("PO Number is required")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        if (!DeliveryDate.HasValue)
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Delivery Date is required")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        if (Items.Count == 0 || Items.All(i => i.Quantity == 0 || i.Price == 0))
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Please add at least one item with quantity and price")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        var invalidItems = Items.Where(i =>
            string.IsNullOrWhiteSpace(i.ItemName) ||
            string.IsNullOrWhiteSpace(i.SelectedUnit) ||
            i.Quantity <= 0 ||
            i.Price <= 0).ToList();

        if (invalidItems.Any())
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("All items must have name, unit, valid quantity and price")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        // Validate payment status against invoice
        if (PaymentStatus == "Paid" && !HasInvoice)
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Cannot save with 'Paid' status without an invoice number")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        return true;
    }

    private async Task OnConfirmDelete()
    {
        if (_purchaseOrderService == null || !PurchaseOrderId.HasValue)
        {
            NavigateBack();
            return;
        }

        try
        {
            IsLoading = true;
            var result = await _purchaseOrderService.DeletePurchaseOrderAsync(PurchaseOrderId.Value);

            if (result.Success)
            {
                _toastManager.CreateToast("Purchase Order Deleted")
                    .WithContent($"Purchase Order {PoNumber} has been deleted")
                    .DismissOnClick()
                    .ShowSuccess();

                NavigateBack();
            }
            else
            {
                _toastManager.CreateToast("Deletion Failed")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error")
                .WithContent($"Failed to delete: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CalculateTotals()
    {
        Subtotal = Items.Sum(item => item.ItemTotal);
    }

    partial void OnInvoiceNumberChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            bool statusesChanged = false;

            // Reset payment status if it was "Paid"
            if (PaymentStatus == "Paid")
            {
                PaymentStatus = "Unpaid";
                statusesChanged = true;
            }

            // Reset shipping status if it was changed from "Pending"
            if (!string.IsNullOrWhiteSpace(ShippingStatus) && ShippingStatus != "Pending")
            {
                ShippingStatus = "Pending";
                statusesChanged = true;
            }

            // Show single notification for all changes
            if (statusesChanged)
            {
                _toastManager.CreateToast("Statuses Reset")
                    .WithContent("Payment and shipping statuses reverted to defaults because invoice number was removed.")
                    .DismissOnClick()
                    .ShowInfo();
            }
        }
    }

    partial void OnShippingStatusChanging(string? value)
    {
        // Prevent changing from Pending if no invoice exists
        if (value != "Pending" && !HasInvoice && !string.IsNullOrWhiteSpace(value))
        {
            _toastManager.CreateToast("Shipping Status Error")
                .WithContent("Cannot change shipping status without an invoice number. Please add invoice number first.")
                .DismissOnClick()
                .ShowError();

            ShippingStatus = string.IsNullOrWhiteSpace(ShippingStatus) ? "Pending" : ShippingStatus;
        }
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (Item item in e.OldItems)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (Item item in e.NewItems)
            {
                item.PropertyChanged += Item_PropertyChanged;
            }
        }

        CalculateTotals();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Item.Quantity) ||
            e.PropertyName == nameof(Item.Price) ||
            e.PropertyName == nameof(Item.ItemTotal))
        {
            CalculateTotals();
        }
    }
}

public partial class Item : ObservableValidator
{
    [ObservableProperty]
    [Required(ErrorMessage = "Item name is required")]
    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters")]
    private string? _itemName;

    [ObservableProperty]
    [Required(ErrorMessage = "Unit is required")]
    private string? _selectedUnit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemTotal))]
    [Range(0.01, 10000, ErrorMessage = "Quantity must be between 0.01 and 10000")]
    private decimal _quantity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemTotal))]
    [Range(0.01, 1000000, ErrorMessage = "Price must be between 0.01 and 1,000,000")]
    private decimal _price;

    public decimal ItemTotal => Quantity * Price;

    public string[] UnitList { get; } = ["pcs", "kg", "lbs", "box", "pack", "unit"];

    public Item()
    {
        SelectedUnit = "pcs";
        Quantity = 0;
        Price = 0;
    }
}

public enum PurchaseOrderContext
{
    AddPurchaseOrder,
    ViewPurchaseOrder
}
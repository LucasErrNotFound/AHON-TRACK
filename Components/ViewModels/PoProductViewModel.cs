using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

[Page("po-product")]
public partial class PoProductViewModel : ViewModelBase, INavigableWithParameters, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IPurchaseOrderService _purchaseOrderService;
    private readonly ISupplierService _supplierService;
    private readonly IProductService _productService;
    
    [ObservableProperty]
    private int? _supplierId;

    [ObservableProperty]
    private int? _purchaseOrderId;

    [ObservableProperty]
    private string? _supplierName;

    [ObservableProperty]
    private string? _supplierEmail;

    [ObservableProperty]
    private string? _contactPerson;

    [ObservableProperty]
    private string? _phoneNumber;

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string _poNumber = $"PO-AHON-{DateTime.Now.Year}-{System.Random.Shared.Next(100000, 999999)}";

    [ObservableProperty]
    private ObservableCollection<PurchaseOrderProductItem> _items = new();

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _vat;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private bool _isRetrievalMode; // True = viewing existing PO, False = creating new PO

    [ObservableProperty]
    private bool _isInEditMode; // True = edit mode activated (toggle pressed)

    [ObservableProperty]
    private DateTime? _orderDate;

    [ObservableProperty]
    private DateTime? _expectedDeliveryDate;

    [ObservableProperty]
    private string? _shippingStatus;

    [ObservableProperty]
    private string? _paymentStatus;

    public PoProductViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager, 
        PageManager pageManager,
        IPurchaseOrderService purchaseOrderService,
        ISupplierService supplierService,
        IProductService productService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _purchaseOrderService = purchaseOrderService;
        _supplierService = supplierService;
        _productService = productService;
    
        // Subscribe to events
        DashboardEventService.Instance.PurchaseOrderAdded += OnPurchaseOrderChanged;
        DashboardEventService.Instance.PurchaseOrderUpdated += OnPurchaseOrderChanged;
    }

    public PoProductViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }
    
    private async void OnPurchaseOrderChanged(object? sender, EventArgs e)
    {
        // Refresh can be handled at SupplierManagementViewModel level
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        Debug.WriteLine("[PoProductViewModel] Initialize called");
    }

    public void SetNavigationParameters(System.Collections.Generic.Dictionary<string, object> parameters)
    {
        Debug.WriteLine("[PoProductViewModel] SetNavigationParameters called");

        // Handle creation mode (from supplier selection)
        if (parameters.TryGetValue("SupplierData", out var supplierDataObj) && 
            supplierDataObj is SupplierProductData data)
        {
            LoadSupplierData(data);
            IsRetrievalMode = false;
            IsInEditMode = false; // Not applicable in creation mode
            Debug.WriteLine($"[PoProductViewModel] Loaded supplier data: {data.SupplierName}");
        }
        
        // Handle retrieval mode (viewing existing PO)
        if (parameters.TryGetValue("RetrievalData", out var retrievalDataObj) && 
            retrievalDataObj is PurchaseOrderProductRetrievalData retrievalData)
        {
            LoadRetrievalData(retrievalData);
            IsRetrievalMode = true;
            IsInEditMode = false; // Start in view-only mode
            Debug.WriteLine($"[PoProductViewModel] Loaded retrieval data: PO {retrievalData.PoNumber}");
        }
    }

    public void LoadSupplierData(SupplierProductData data)
    {
        Debug.WriteLine($"[PoProductViewModel] Loading supplier: {data.SupplierName}");
        SupplierId = data.SupplierID;
        SupplierName = data.SupplierName;
        SupplierEmail = data.Email;
        ContactPerson = data.ContactPerson;
        PhoneNumber = data.PhoneNumber;
        Address = data.Address;

        Items.Clear();
        foreach (var item in data.SupplierItems)
        {
            var poItem = new PurchaseOrderProductItem
            {
                PoNumber = PoNumber,
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                UnitsOfMeasures = item.SelectedUnit,
                SuppliersPrice = item.SupplierPrice,
                MarkupPrice = item.MarkupPrice,
                SellingPrice = item.SellingPrice,
                Quantity = 1,
                QuantityReceived = 0,
                // ⭐ ADD THESE MAPPINGS
                BatchCode = item.BatchCode,
                Category = item.SelectedCategory,
                Description = item.Description,
                ExpiryDate = item.Expiration?.DateTime
            };

            poItem.PropertyChanged += OnItemPropertyChanged;
            Items.Add(poItem);
        
            Debug.WriteLine($"  Added item: {item.ItemName} - ₱{item.SupplierPrice}");
        }

        CalculateTotals();
    
        Debug.WriteLine($"[PoProductViewModel] Total items loaded: {Items.Count}");
        Debug.WriteLine($"[PoProductViewModel] Subtotal: ₱{Subtotal:N2}, VAT: ₱{Vat:N2}, Total: ₱{Total:N2}");
    }

    public void LoadRetrievalData(PurchaseOrderProductRetrievalData data)
    {
        Debug.WriteLine($"[PoProductViewModel] Loading retrieval data: PO {data.PoNumber}");
    
        PurchaseOrderId = data.PurchaseOrderId;
        PoNumber = data.PoNumber;
        SupplierId = data.SupplierID;
        SupplierName = data.SupplierName;
        SupplierEmail = data.SupplierEmail;
        ContactPerson = data.ContactPerson;
        PhoneNumber = data.PhoneNumber;
        Address = data.Address;
        OrderDate = data.OrderDate;
        ExpectedDeliveryDate = data.ExpectedDeliveryDate;
        ShippingStatus = data.ShippingStatus;
        PaymentStatus = data.PaymentStatus;
        Subtotal = data.Subtotal;
        Vat = data.Vat;
        Total = data.Total;

        Items.Clear();
        foreach (var item in data.Items)
        {
            var poItem = new PurchaseOrderProductItem
            {
                PoNumber = PoNumber,
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                UnitsOfMeasures = item.Unit,
                SuppliersPrice = item.SupplierPrice,
                MarkupPrice = item.MarkupPrice,
                SellingPrice = item.SellingPrice,
                Quantity = item.Quantity,
                QuantityReceived = item.QuantityReceived,
                // ⭐ ADD THESE MAPPINGS
                BatchCode = item.BatchCode,
                Category = item.Category,
                Description = item.Description,
                ExpiryDate = item.ExpiryDate
            };

            poItem.PropertyChanged += OnItemPropertyChanged;
            Items.Add(poItem);
        
            Debug.WriteLine($"  Loaded item: {item.ItemName} - Qty: {item.Quantity}, Received: {item.QuantityReceived}");
        }

        Debug.WriteLine($"[PoProductViewModel] Retrieval data loaded: {Items.Count} items");
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PurchaseOrderProductItem.Quantity) ||
            e.PropertyName == nameof(PurchaseOrderProductItem.SuppliersPrice))
        {
            CalculateTotals();
        }
    }

    private void CalculateTotals()
    {
        Subtotal = Items.Sum(i => (i.SuppliersPrice ?? 0) * (i.Quantity ?? 0));
        Vat = Subtotal * 0.12m;
        Total = Subtotal + Vat;
    }

    [RelayCommand]
    private async Task CreatePurchaseOrder()
    {
        try
        {
            // Validate
            if (string.IsNullOrWhiteSpace(SupplierName))
            {
                _toastManager.CreateToast("Validation Error")
                    .WithContent("Supplier name is required.")
                    .DismissOnClick()
                    .ShowWarning();
                return;
            }

            if (Items.Count == 0)
            {
                _toastManager.CreateToast("Validation Error")
                    .WithContent("At least one product item is required.")
                    .DismissOnClick()
                    .ShowWarning();
                return;
            }

            _dialogManager
                .CreateDialog("Confirm Purchase Order", "Are you sure you want to create this purchase order?")
                .WithPrimaryButton("Yes, create", async () =>
                {
                    await CreatePurchaseOrderConfirmed();
                })
                .WithCancelButton("No")
                .Show();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error")
                .WithContent($"An error occurred: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private async Task CreatePurchaseOrderConfirmed()
    {
        try
        {
            // Get supplier ID
            if (!SupplierId.HasValue || SupplierId.Value <= 0)
            {
                _toastManager.CreateToast("Error")
                    .WithContent("Invalid supplier information.")
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            // Create purchase order model
            var purchaseOrder = new PurchaseOrderModel
            {
                PONumber = PoNumber,
                SupplierID = SupplierId.Value, 
                OrderDate = DateTime.Now,
                ExpectedDeliveryDate = DateTime.Now.AddDays(7),
                ShippingStatus = "Pending",
                PaymentStatus = "Unpaid",
                Category = "Product",
                Subtotal = Subtotal,
                TaxRate = 0.12m,
                TaxAmount = Vat,
                Total = Total,
                Items = Items.Select(item => new PurchaseOrderItemModel
                {
                    ItemID = item.ItemId,
                    ItemName = item.ItemName,
                    Unit = item.UnitsOfMeasures,
                    Price = item.SuppliersPrice ?? 0,  // ⭐ ADD THIS - Use SupplierPrice as the Price for calculations
                    SupplierPrice = item.SuppliersPrice ?? 0,
                    MarkupPrice = item.MarkupPrice ?? 0,
                    SellingPrice = item.SellingPrice ?? 0,
                    Quantity = item.Quantity ?? 1,
                    QuantityReceived = 0,
                    BatchCode = item.BatchCode,
                    Category = item.Category,
                    Description = item.Description,
                    ExpiryDate = item.ExpiryDate?.Date
                }).ToList()
            };

            // Save to database
            var result = await _purchaseOrderService.CreatePurchaseOrderAsync(purchaseOrder);
    
            if (result.Success)
            {
                _toastManager.CreateToast("Success")
                    .WithContent($"Purchase Order {PoNumber} created successfully!")
                    .DismissOnClick()
                    .ShowSuccess();

                // Trigger refresh event
                DashboardEventService.Instance.NotifyPurchaseOrderAdded();
        
                // Navigate back
                _pageManager.Navigate<SupplierManagementViewModel>();
            }
            else
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"Failed to create purchase order: {result.Message}")
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error")
                .WithContent($"An unexpected error occurred: {ex.Message}")
                .DismissOnClick()
                .ShowError();
    
            Debug.WriteLine($"[CreatePurchaseOrderConfirmed] Error: {ex.Message}");
            Debug.WriteLine($"[CreatePurchaseOrderConfirmed] Stack: {ex.StackTrace}");
        }
    }

    [RelayCommand]
    private void SendToInventory()
    {
        // Validate that all items have quantities received
        var itemsWithoutReceived = Items.Where(i => (i.QuantityReceived ?? 0) == 0).ToList();
    
        if (itemsWithoutReceived.Count > 0)
        {
            _dialogManager
                .CreateDialog("Incomplete Quantities", 
                    $"{itemsWithoutReceived.Count} item(s) have no quantity received. Do you want to continue anyway?")
                .WithPrimaryButton("Yes, continue", async () => await SendToInventoryConfirmed())
                .WithCancelButton("No, go back")
                .Show();
            return;
        }

        _dialogManager
            .CreateDialog("Send to Inventory", "Send received items to inventory?")
            .WithPrimaryButton("Yes, send", async () => await SendToInventoryConfirmed())
            .WithCancelButton("No")
            .Show();
    }

    private async Task SendToInventoryConfirmed()
    {
        try
        {
            if (!PurchaseOrderId.HasValue)
            {
                _toastManager.CreateToast("Error")
                    .WithContent("Purchase Order ID not found.")
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            // Update quantities received in database
            var updateResult = await _purchaseOrderService.UpdatePurchaseOrderQuantitiesAsync(
                PurchaseOrderId.Value, 
                Items.Select(i => new PurchaseOrderItemModel
                {
                    ItemID = i.ItemId,
                    ItemName = i.ItemName,
                    QuantityReceived = i.QuantityReceived ?? 0,
                    Quantity = i.Quantity ?? 0
                }).ToList());

            if (!updateResult.Success)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"Failed to update quantities: {updateResult.Message}")
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            // Add products to inventory
            foreach (var item in Items)
            {
                if ((item.QuantityReceived ?? 0) > 0)
                {
                    var productModel = new ProductModel
                    {
                        ProductName = item.ItemName,
                        BatchCode = item.BatchCode ?? $"BATCH-{DateTime.Now.Year}-{System.Random.Shared.Next(100000, 999999)}",
                        SupplierID = SupplierId, // ⭐ ADD SupplierID
                        Description = item.Description, // ⭐ ADD Description
                        Category = item.Category ?? "Products", // ⭐ ADD Category
                        Price = item.SellingPrice ?? 0,
                        PurchasedPrice = item.SuppliersPrice, // ⭐ ADD PurchasedPrice
                        MarkupPrice = item.MarkupPrice, // ⭐ ADD MarkupPrice
                        ExpiryDate = item.ExpiryDate, // ⭐ ADD ExpiryDate
                        CurrentStock = item.QuantityReceived ?? 0,
                        Status = "In Stock"
                    };

                    await _productService.AddProductAsync(productModel);
                }
            }

            // Mark PO as delivered and sent to inventory
            var deliveryResult = await _purchaseOrderService.MarkAsDeliveredAsync(PurchaseOrderId.Value);
    
            if (deliveryResult.Success)
            {
                _toastManager.CreateToast("Success")
                    .WithContent("Items sent to inventory successfully! Status updated to Delivered.")
                    .DismissOnClick()
                    .ShowSuccess();

                // Trigger refresh event
                DashboardEventService.Instance.NotifyPurchaseOrderUpdated();
        
                // Navigate back
                _pageManager.Navigate<SupplierManagementViewModel>();
            }
            else
            {
                _toastManager.CreateToast("Warning")
                    .WithContent("Items added but status update failed.")
                    .DismissOnClick()
                    .ShowWarning();
        
                _pageManager.Navigate<SupplierManagementViewModel>();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error")
                .WithContent($"An unexpected error occurred: {ex.Message}")
                .DismissOnClick()
                .ShowError();
    
            Debug.WriteLine($"[SendToInventoryConfirmed] Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Back()
    {
        Debug.WriteLine("[PoProductViewModel] Back button pressed");
        _pageManager.Navigate<SupplierManagementViewModel>();
    }

    // Remove this command - it's no longer needed
    // The toggle button will directly bind to IsInEditMode property
    protected override void DisposeManagedResources()
    {
        // Unsubscribe from events
        DashboardEventService.Instance.PurchaseOrderAdded -= OnPurchaseOrderChanged;
        DashboardEventService.Instance.PurchaseOrderUpdated -= OnPurchaseOrderChanged;
    
        foreach (var item in Items)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        Items.Clear();
        base.DisposeManagedResources();
    }
}

public partial class PurchaseOrderProductItem : ObservableValidator
{
    [ObservableProperty] 
    private string? _poNumber;
    
    [ObservableProperty] 
    private string? _itemId;
    
    [ObservableProperty] 
    private string? _itemName;
    
    [ObservableProperty] 
    private string? _unitsOfMeasures;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    private decimal? _suppliersPrice;
    
    [ObservableProperty] 
    private decimal? _markupPrice;
    
    [ObservableProperty]
    private decimal? _sellingPrice;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    private int? _quantity = 1;
    
    [ObservableProperty] 
    private int? _quantityReceived = 0;

    // ⭐ ADD THESE FIELDS for inventory
    [ObservableProperty]
    private string? _batchCode;
    
    [ObservableProperty]
    private string? _category;
    
    [ObservableProperty]
    private string? _description;
    
    [ObservableProperty]
    private DateTime? _expiryDate;

    public decimal LineTotal => (SuppliersPrice ?? 0) * (Quantity ?? 0);
}
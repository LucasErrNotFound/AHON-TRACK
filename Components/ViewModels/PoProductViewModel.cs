using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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

    public PoProductViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public PoProductViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
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
                QuantityReceived = 0
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
                QuantityReceived = item.QuantityReceived
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
    private void CreatePurchaseOrder()
    {
        _dialogManager
            .CreateDialog("Confirm Purchase Order", "Are you sure you want to create this purchase order?")
            .WithPrimaryButton("Yes, create", () =>
            {
                _toastManager.CreateToast("Success")
                    .WithContent($"Purchase Order {PoNumber} created successfully!")
                    .DismissOnClick()
                    .ShowSuccess();

                // TODO: Save to database
                Debug.WriteLine($"[PoProductViewModel] Creating PO: {PoNumber}");
                Debug.WriteLine($"[PoProductViewModel] Supplier: {SupplierName}");
                Debug.WriteLine($"[PoProductViewModel] Items: {Items.Count}");
                Debug.WriteLine($"[PoProductViewModel] Total: ₱{Total:N2}");
                
                _pageManager.Navigate<SupplierManagementViewModel>();
            })
            .WithCancelButton("No")
            .Show();
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
                .WithPrimaryButton("Yes, continue", SendToInventoryConfirmed)
                .WithCancelButton("No, go back")
                .Show();
            return;
        }

        SendToInventoryConfirmed();
    }

    private void SendToInventoryConfirmed()
    {
        _dialogManager
            .CreateDialog("Send to Inventory", "Send received items to inventory?")
            .WithPrimaryButton("Yes, send", () =>
            {
                _toastManager.CreateToast("Success")
                    .WithContent("Items sent to inventory successfully!")
                    .DismissOnClick()
                    .ShowSuccess();

                // TODO: Update inventory and mark PO as completed
                Debug.WriteLine($"[PoProductViewModel] Sending {Items.Count} items to inventory");
                foreach (var item in Items)
                {
                    Debug.WriteLine($"  - {item.ItemName}: Received {item.QuantityReceived} of {item.Quantity}");
                }
                
                _pageManager.Navigate<SupplierManagementViewModel>();
            })
            .WithCancelButton("No")
            .Show();
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

    public decimal LineTotal => (SuppliersPrice ?? 0) * (Quantity ?? 0);
}
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
    private string _poNumber = $"#{System.Random.Shared.Next(100000, 999999)}";

    [ObservableProperty]
    private ObservableCollection<PurchaseOrderProductItem> _items = new();

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _vat;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private bool _isRetrievalMode;

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

    // ✅ IMPLEMENT SetNavigationParameters
    public void SetNavigationParameters(System.Collections.Generic.Dictionary<string, object> parameters)
    {
        Debug.WriteLine("[PoProductViewModel] SetNavigationParameters called");

        if (parameters.TryGetValue("SupplierData", out var supplierDataObj) && 
            supplierDataObj is SupplierProductData data)
        {
            LoadSupplierData(data);
            Debug.WriteLine($"[PoProductViewModel] Loaded supplier data: {data.SupplierName}");
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
            })
            .WithCancelButton("No")
            .Show();
    }

    [RelayCommand]
    private void SendToInventory()
    {
        _dialogManager
            .CreateDialog("Send to Inventory", "Send received items to inventory?")
            .WithPrimaryButton("Yes, send", () =>
            {
                _toastManager.CreateToast("Success")
                    .WithContent("Items sent to inventory successfully!")
                    .DismissOnClick()
                    .ShowSuccess();

                // TODO: Update inventory
                Debug.WriteLine($"[PoProductViewModel] Sending {Items.Count} items to inventory");
            })
            .WithCancelButton("No")
            .Show();
    }

    [RelayCommand]
    private void Back()
    {
        // Navigate back or to a specific page
        Debug.WriteLine("[PoProductViewModel] Back button pressed");
        _pageManager.Navigate<SupplierManagementViewModel>();
    }

    [RelayCommand]
    private void ToggleRetrievalMode()
    {
        IsRetrievalMode = !IsRetrievalMode;
        Debug.WriteLine($"[PoProductViewModel] Retrieval mode: {IsRetrievalMode}");
        // TODO: Load PO data if in retrieval mode
    }

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
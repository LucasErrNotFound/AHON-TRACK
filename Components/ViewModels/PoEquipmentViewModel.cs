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

[Page("po-equipment")]
public partial class PoEquipmentViewModel : ViewModelBase, INavigableWithParameters, INotifyPropertyChanged
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
    private ObservableCollection<PurchaseOrderEquipmentItem> _items = new();

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    private decimal _vat;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private bool _isRetrievalMode;

    public PoEquipmentViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public PoEquipmentViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        Debug.WriteLine("[PoEquipmentViewModel] Initialize called");
    }

    // ✅ IMPLEMENT SetNavigationParameters
    public void SetNavigationParameters(System.Collections.Generic.Dictionary<string, object> parameters)
    {
        Debug.WriteLine("[PoEquipmentViewModel] SetNavigationParameters called");

        if (parameters.TryGetValue("SupplierData", out var supplierDataObj) && 
            supplierDataObj is SupplierEquipmentData data)
        {
            LoadSupplierData(data);
            Debug.WriteLine($"[PoEquipmentViewModel] Loaded supplier data: {data.SupplierName}");
        }
    }

    public void LoadSupplierData(SupplierEquipmentData data)
    {
        Debug.WriteLine($"[PoEquipmentViewModel] Loading supplier: {data.SupplierName}");
        
        SupplierName = data.SupplierName;
        SupplierEmail = data.Email;
        ContactPerson = data.ContactPerson;
        PhoneNumber = data.PhoneNumber;
        Address = data.Address;

        Items.Clear();
        foreach (var item in data.EquipmentItems)
        {
            var poItem = new PurchaseOrderEquipmentItem
            {
                PoNumber = PoNumber,
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                UnitsOfMeasures = item.SelectedUnit,
                Category = item.SelectedCategory,
                BatchCode = item.BatchCode,
                SuppliersPrice = item.Price,
                Quantity = 1,
                QuantityReceived = 0
            };

            poItem.PropertyChanged += OnItemPropertyChanged;
            Items.Add(poItem);
            
            Debug.WriteLine($"  Added equipment: {item.ItemName} - ₱{item.Price}");
        }

        CalculateTotals();
        
        Debug.WriteLine($"[PoEquipmentViewModel] Total items loaded: {Items.Count}");
        Debug.WriteLine($"[PoEquipmentViewModel] Subtotal: ₱{Subtotal:N2}, VAT: ₱{Vat:N2}, Total: ₱{Total:N2}");
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PurchaseOrderEquipmentItem.Quantity) ||
            e.PropertyName == nameof(PurchaseOrderEquipmentItem.SuppliersPrice))
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
                Debug.WriteLine($"[PoEquipmentViewModel] Creating PO: {PoNumber}");
                Debug.WriteLine($"[PoEquipmentViewModel] Supplier: {SupplierName}");
                Debug.WriteLine($"[PoEquipmentViewModel] Equipment items: {Items.Count}");
                Debug.WriteLine($"[PoEquipmentViewModel] Total: ₱{Total:N2}");
            })
            .WithCancelButton("No")
            .Show();
    }

    [RelayCommand]
    private void SendToInventory()
    {
        _dialogManager
            .CreateDialog("Send to Inventory", "Send received equipment to inventory?")
            .WithPrimaryButton("Yes, send", () =>
            {
                _toastManager.CreateToast("Success")
                    .WithContent("Equipment sent to inventory successfully!")
                    .DismissOnClick()
                    .ShowSuccess();

                // TODO: Update inventory
                Debug.WriteLine($"[PoEquipmentViewModel] Sending {Items.Count} equipment items to inventory");
            })
            .WithCancelButton("No")
            .Show();
    }

    [RelayCommand]
    private void Back()
    {
        // Navigate back or to a specific page
        Debug.WriteLine("[PoEquipmentViewModel] Back button pressed");
        _pageManager.Navigate<SupplierManagementViewModel>();
    }

    [RelayCommand]
    private void ToggleRetrievalMode()
    {
        IsRetrievalMode = !IsRetrievalMode;
        Debug.WriteLine($"[PoEquipmentViewModel] Retrieval mode: {IsRetrievalMode}");
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

public partial class PurchaseOrderEquipmentItem : ObservableValidator
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
    private string? _category;
    
    [ObservableProperty] 
    private string? _batchCode;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    private decimal? _suppliersPrice;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    private int? _quantity = 1;
    
    [ObservableProperty] 
    private int? _quantityReceived = 0;

    public decimal LineTotal => (SuppliersPrice ?? 0) * (Quantity ?? 0);
}
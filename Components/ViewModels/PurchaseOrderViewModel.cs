using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
    private bool _isSaved;
    
    public bool IsInEditMode => !IsSaved && ViewContext == PurchaseOrderContext.AddPurchaseOrder;
    public bool IsInViewMode => IsSaved || ViewContext == PurchaseOrderContext.ViewPurchaseOrder;
    
    [ObservableProperty]
    private string? _shippingStatus;
    
    [ObservableProperty]
    private string? _paymentStatus;
    
    [ObservableProperty]
    [Required(ErrorMessage = "PO Number is required")]
    private string _poNumber = string.Empty;
    
    [ObservableProperty]
    [Required(ErrorMessage = "Delivery Date is required")]
    private DateTime? _deliveryDate;
    
    [ObservableProperty]
    [MaxLength(100, ErrorMessage = "Invoice Number cannot exceed 100 characters")]
    private string? _invoiceNumber;
    
    public string[] ShippingStatusOptions { get; } = 
    [
        "Pending", 
        "Processing", 
        "Shipped/In-Transit", 
        "Delivered", 
        "Partially Delivered", 
        "Cancelled"
    ];
    
    public string[] PaymentStatusOptions { get; } = 
    [
        "Unpaid", 
        "Paid", 
        "Cancelled"
    ];
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public PurchaseOrderViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        
        Items.CollectionChanged += Items_CollectionChanged;
        AddInitialItem();
        GeneratePONumber();
    }

    public PurchaseOrderViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        
        Items.CollectionChanged += Items_CollectionChanged;
        AddInitialItem();
        GeneratePONumber();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (Items.Count == 0)
        {
            AddInitialItem();
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
            var selectedSupplier = (Supplier)supplier;
            // _ = LoadPurchaseOrderAndPopulateForm(selectedSupplier);
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
    private void SaveOrder()
    {
        if (!ValidateOrder())
        {
            return;
        }
        
        // Set default values for shipping and payment status
        ShippingStatus = "Pending";
        PaymentStatus = "Unpaid";
        
        IsSaved = true;
        
        _toastManager.CreateToast("Purchase Order Saved")
            .WithContent($"Purchase Order {PoNumber} has been saved successfully!")
            .DismissOnClick()
            .ShowSuccess();
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
        IsSaved = false;
        _toastManager.CreateToast("Edit Mode")
            .WithContent("You can now edit the purchase order")
            .DismissOnClick()
            .ShowInfo();
    }
    
    [RelayCommand]
    private void DeleteOrder()
    {
        _dialogManager.CreateDialog(
            "Delete Purchase Order",
            $"Are you sure you want to delete Purchase Order {PoNumber}? This action cannot be undone.")
            .WithPrimaryButton("Delete", OnConfirmDelete, DialogButtonStyle.Destructive)
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
    
    private void AddInitialItem()
    {
        Items.Add(new Item());
    }
    
    private void GeneratePONumber()
    {
        var random = new Random();
        var randomNumber = random.Next(100000, 999999);
        PoNumber = $"PO-AHON-{DateTime.Now.Year}-{randomNumber}";
    }
    
    private bool ValidateOrder()
    {
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
        
        return true;
    }
    
    private void OnConfirmDelete()
    {
        _toastManager.CreateToast("Purchase Order Deleted")
            .WithContent($"Purchase Order {PoNumber} has been deleted")
            .DismissOnClick()
            .ShowSuccess();
        
        NavigateBack();
    }
    
    private void CalculateTotals()
    {
        Subtotal = Items.Sum(item => item.ItemTotal);
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
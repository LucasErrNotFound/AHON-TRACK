using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using QuestPDF.Fluent;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

[Page("supplier-management")]
public sealed partial class SupplierManagementViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _statusFilterItems = ["All", "Active", "Inactive", "Suspended"];

    [ObservableProperty]
    private string _selectedStatusFilterItem = "All";

    [ObservableProperty]
    private string[] _deliveryFilterItems = ["All", "Pending", "Processing", "Shipped/In-Transit", "Delivered", "Cancelled"];

    [ObservableProperty]
    private string _selectedDeliveryFilterItem = "All";

    [ObservableProperty]
    private ObservableCollection<Supplier> _supplierItems = [];

    [ObservableProperty]
    private List<Supplier> _originalSupplierData = [];

    [ObservableProperty]
    private List<Supplier> _currentFilteredSupplierData = [];

    [ObservableProperty]
    private ObservableCollection<PurchaseOrder> _purchaseOrderItems = [];

    [ObservableProperty]
    private List<PurchaseOrder> _originalPurchaseOrderData = [];

    [ObservableProperty]
    private List<PurchaseOrder> _currentFilteredPurchaseOrderData = [];

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private string _searchStringResult = string.Empty;

    [ObservableProperty]
    private bool _isSearchingSupplier;

    [ObservableProperty]
    private string _searchStringOrderResult = string.Empty;

    [ObservableProperty]
    private bool _isSearchingOrders;

    [ObservableProperty]
    private bool _selectAll;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _isLoading;

    private bool _isLoadingData = false;

    [ObservableProperty]
    private Supplier? _selectedSupplier;

    [ObservableProperty]
    private PurchaseOrder? _selectedPO;

    private EventHandler? _supplierPurchaseOrderEventHandler;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly SupplierDialogCardViewModel _supplierProductDialogCardViewModel;
    private readonly SupplierEquipmentDialogCardViewModel _supplierEquipmentDialogCardViewModel;
    private readonly ISupplierService _supplierService;
    private readonly IPurchaseOrderService? _purchaseOrderService;
    private readonly SettingsService _settingsService;
    private AppSettings? _currentSettings;

    public SupplierManagementViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,
        SupplierDialogCardViewModel supplierDialogCardViewModel, SupplierEquipmentDialogCardViewModel supplierEquipmentDialogCardViewModel, 
        SettingsService settingsService, ISupplierService supplierService, IPurchaseOrderService? purchaseOrderService = null)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _supplierProductDialogCardViewModel = supplierDialogCardViewModel;
        _supplierEquipmentDialogCardViewModel = supplierEquipmentDialogCardViewModel;
        _supplierService = supplierService;
        _purchaseOrderService = purchaseOrderService;
        _settingsService = settingsService;

        SelectedStatusFilterItem = "All";
        SelectedDeliveryFilterItem = "All";

        SubsribeToEvents();
        _ = LoadSupplierDataFromDatabaseAsync();
        _ = LoadPurchaseOrderDataFromDatabaseAsync();
        UpdateSupplierCounts();
    }

    public SupplierManagementViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _supplierProductDialogCardViewModel = new SupplierDialogCardViewModel();
        _supplierEquipmentDialogCardViewModel = new SupplierEquipmentDialogCardViewModel();
        _settingsService = new SettingsService();
        _supplierService = null!;
        _purchaseOrderService = null;

        SelectedStatusFilterItem = "All";
        SelectedDeliveryFilterItem = "All";

        _ = LoadSupplierDataFromDatabaseAsync();
        LoadPurchaseOrderData(); // Fallback to sample data
        UpdateSupplierCounts();
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (IsInitialized) return;
        SelectedStatusFilterItem = "All";
        SelectedDeliveryFilterItem = "All";

        await LoadSettingsAsync();

        if (_supplierService != null)
        {
            await LoadSupplierDataFromDatabaseAsync();
        }
        else
        {
            LoadSupplierData();
        }

        if (_purchaseOrderService != null)
        {
            await LoadPurchaseOrderDataFromDatabaseAsync();
        }
        else
        {
            LoadPurchaseOrderData();
        }

        UpdateSupplierCounts();
        IsInitialized = true;
    }

    private void SubsribeToEvents()
    {
        var eventService = DashboardEventService.Instance;

        _supplierPurchaseOrderEventHandler = OnSupplierPurchasedOrder;

        eventService.SupplierUpdated += OnSupplierPurchasedOrder;
        eventService.PurchaseOrderAdded += OnSupplierPurchasedOrder;
        eventService.PurchaseOrderUpdated += OnSupplierPurchasedOrder;
        eventService.PurchaseOrderDeleted += OnSupplierPurchasedOrder;

    }

    private async void OnSupplierPurchasedOrder(object? sender, EventArgs e)
    {
        try
        {
            await LoadPurchaseOrderDataFromDatabaseAsync();
        }
        catch
        {
            // Ignore errors during property change handling
        }
    }

    private async Task LoadSupplierDataFromDatabaseAsync()
    {
        if (_isLoadingData) return;

        _isLoadingData = true;
        IsLoading = true;

        try
        {
            var result = await _supplierService.GetAllSuppliersAsync();

            if (result.Success && result.Suppliers != null)
            {
                var suppliers = result.Suppliers.Select(s => new Supplier
                {
                    ID = s.SupplierID,
                    Name = s.SupplierName,
                    ContactPerson = s.ContactPerson,
                    Email = s.Email,
                    PhoneNumber = s.PhoneNumber,
                    Address = s.Address,
                    Products = s.Products,
                    Status = s.Status,
                    DeliverySchedule = s.DeliverySchedule,
                    ContractTerms = s.ContractTerms,
                    IsSelected = false
                }).ToList();

                OriginalSupplierData = suppliers;

                await CheckAndUpdateSupplierStatusesAsync();

                // ⭐ NEW: Load and merge products from delivered/paid purchase orders
                await LoadAndMergeDeliveredPurchaseOrderProductsAsync();

                ApplySupplierFilter();
                UpdateSupplierCounts();

                if (SupplierItems.Count > 0)
                {
                    SelectedSupplier = SupplierItems[0];
                }
            }
            else
            {
                _toastManager.CreateToast("Data Load Failed")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowWarning();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Loading Data")
                .WithContent($"Failed to load suppliers: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
            _isLoadingData = false;
        }
    }

    private async Task LoadAndMergeDeliveredPurchaseOrderProductsAsync()
    {
        if (_purchaseOrderService == null) return;

        try
        {
            // Get all purchase orders
            var poResult = await _purchaseOrderService.GetAllPurchaseOrdersAsync();
            if (!poResult.Success || poResult.PurchaseOrders == null) return;

            // Filter for delivered AND paid orders only
            var deliveredPaidOrders = poResult.PurchaseOrders
                .Where(po => po.ShippingStatus?.Equals("Delivered", StringComparison.OrdinalIgnoreCase) == true &&
                            po.PaymentStatus?.Equals("Paid", StringComparison.OrdinalIgnoreCase) == true &&
                            po.SupplierID.HasValue &&
                            po.Items != null && po.Items.Count > 0)
                .ToList();

            // Group items by supplier
            var supplierProducts = deliveredPaidOrders
                .GroupBy(po => po.SupplierID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(po => po.Items)
                          .Select(item => item.ItemName)
                          .Distinct()
                          .ToList()
                );

            // Merge products into suppliers
            foreach (var supplier in OriginalSupplierData)
            {
                if (supplier.ID.HasValue && supplierProducts.ContainsKey(supplier.ID.Value))
                {
                    var poProducts = supplierProducts[supplier.ID.Value];

                    // Parse existing products
                    var existingProducts = string.IsNullOrWhiteSpace(supplier.Products)
                        ? new List<string>()
                        : supplier.Products.Split(',').Select(p => p.Trim()).ToList();

                    // Merge and remove duplicates
                    var mergedProducts = existingProducts.Union(poProducts).Distinct().ToList();

                    // Update supplier's products
                    supplier.Products = string.Join(", ", mergedProducts);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadAndMergeDeliveredPurchaseOrderProductsAsync] Error: {ex.Message}");
            // Don't show error to user - this is a background enhancement
        }
    }

    private async Task LoadPurchaseOrderDataFromDatabaseAsync()
    {
        if (_purchaseOrderService == null) return;

        try
        {
            IsLoading = true;
            var result = await _purchaseOrderService.GetAllPurchaseOrdersAsync();

            if (result.Success && result.PurchaseOrders != null)
            {
                var purchaseOrders = result.PurchaseOrders.Select(po => new PurchaseOrder
                {
                    ID = po.PurchaseOrderID,
                    PONumber = po.PONumber,
                    Name = po.SupplierName ?? "Unknown Supplier",
                    Item = string.Join(", ", po.Items?.Select(i => $"{i.ItemName} ({i.Quantity} {i.Unit})") ?? new List<string>()),
                    Amount = po.Total,
                    DeliveryStatus = po.ShippingStatus,
                    PaymentStatus = po.PaymentStatus,
                    OrderDate = po.OrderDate,
                    ExpectedDeliveryDate = po.ExpectedDeliveryDate,
                    IsSelected = false
                }).ToList();

                OriginalPurchaseOrderData = purchaseOrders;
                ApplyPurchaseOrderFilter();
                UpdatePurchaseOrderCounts();

                if (PurchaseOrderItems.Count > 0)
                {
                    SelectedPO = PurchaseOrderItems[0];
                }
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Loading Purchase Orders")
                .WithContent($"Failed to load purchase orders: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadSupplierData()
    {
        var sampleSupplier = GetSampleSupplierData();
        OriginalSupplierData = sampleSupplier;
        ApplySupplierFilter();
        UpdateSupplierCounts();

        if (SupplierItems.Count > 0)
        {
            SelectedSupplier = SupplierItems[0];
        }
    }

    private void LoadPurchaseOrderData()
    {
        var samplePurchaseOrders = GetSamplePurchaseOrderData();
        OriginalPurchaseOrderData = samplePurchaseOrders;
        ApplyPurchaseOrderFilter();
        UpdatePurchaseOrderCounts();

        if (PurchaseOrderItems.Count > 0)
        {
            SelectedPO = PurchaseOrderItems[0];
        }
    }

    private List<Supplier> GetSampleSupplierData()
    {
        return
        [
            new Supplier
            {
                ID = 1001,
                Name = "San Miguel",
                ContactPerson = "Rodolfo Morales",
                Email = "rodolfo.morales21@gmail.com",
                PhoneNumber = "09182938475",
                Products = "Drinks",
                Status = "Active"
            },
            new Supplier
            {
                ID = 1002,
                Name = "AHON Factory",
                ContactPerson = "Joel Abalos",
                Email = "joel.abalos@gmail.com",
                PhoneNumber = "09382293009",
                Products = "Products",
                Status = "Inactive"
            },
            new Supplier
            {
                ID = 1003,
                Name = "Optimum",
                ContactPerson = "Mr. Lopez",
                Email = "ignacio.lopez@gmail.com",
                PhoneNumber = "09339948293",
                Products = "Supplements",
                Status = "Suspended"
            },
            new Supplier
            {
                ID = 1004,
                Name = "Athlene",
                ContactPerson = "Mr. Rome Malubag",
                Email = "rome.malubag@friendster.com",
                PhoneNumber = "09223849981",
                Products = "Supplements",
                Status = "Inactive"
            },
            new Supplier
            {
                ID = 1005,
                Name = "Jump Manila",
                ContactPerson = "Arnold Demakapitan",
                Email = "arnold.demakapitan@yahoo.com",
                PhoneNumber = "09223849981",
                Products = "Accessories",
                Status = "Active"
            }
        ];
    }

    private List<PurchaseOrder> GetSamplePurchaseOrderData()
    {
        return
        [
            new PurchaseOrder
            {
                PONumber = "PO-AHON-2025-839872",
                Name = "San Miguel",
                Item = "Soft Drinks (Coca-Cola, Sprite)",
                Amount = 15000.00m,
                DeliveryStatus = "Delivered",
                PaymentStatus = "Paid"
            },
            new PurchaseOrder
            {
                PONumber = "PO-AHON-2025-232984",
                Name = "Optimum",
                Item = "Whey Protein (Gold Standard)",
                Amount = 25000.00m,
                DeliveryStatus = "Pending",
                PaymentStatus = "Unpaid"
            },
            new PurchaseOrder
            {
                PONumber = "PO-AHON-2025-279373",
                Name = "Jump Manila",
                Item = "Gym Accessories (Gloves, Belts)",
                Amount = 8500.00m,
                DeliveryStatus = "Cancelled",
                PaymentStatus = "Cancelled"
            }
        ];
    }

    [RelayCommand]
    private void ShowSupplierConfirmationDialog()
    {
        _dialogManager
            .CreateDialog(
                "Choose the following",
                "What would be the tye of your purchase?")
            .WithPrimaryButton("Products", ShowAddSupplierProductDialog)
            .WithCancelButton("Equipments", ShowAddSupplierEquipmentDialog)
            .WithMaxWidth(800)
            .Dismissible()
            .Show();
    }

    private void ShowAddSupplierEquipmentDialog()
    {
        _supplierEquipmentDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_supplierEquipmentDialogCardViewModel)
            .WithSuccessCallback(_ =>
            {
                /*
                var newSupplier = new SupplierManagementModel
                {
                    SupplierName = _supplierProductDialogCardViewModel.SupplierName,
                    ContactPerson = _supplierProductDialogCardViewModel.ContactPerson,
                    Email = _supplierProductDialogCardViewModel.Email,
                    PhoneNumber = _supplierProductDialogCardViewModel.PhoneNumber,
                    Address = _supplierProductDialogCardViewModel.Address,
                    Status = _supplierProductDialogCardViewModel.Status ?? "Active",
                };

                var result = await _supplierService.AddSupplierAsync(newSupplier);

                if (result.Success)
                {
                    await LoadSupplierDataFromDatabaseAsync();

                    _toastManager.CreateToast("Supplier Added Successfully")
                        .WithContent($"Successfully added '{newSupplier.SupplierName}' to the database!")
                        .DismissOnClick()
                        .ShowSuccess();
                }
                */
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new supplier equipment cancelled")
                    .WithContent("If you want to add a new supplier equipment, please try again.")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(2000)
            .Show();
    }

    private void ShowAddSupplierProductDialog()
    {
        _supplierProductDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_supplierProductDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                var newSupplier = new SupplierManagementModel
                {
                    SupplierName = _supplierProductDialogCardViewModel.SupplierName,
                    ContactPerson = _supplierProductDialogCardViewModel.ContactPerson,
                    Email = _supplierProductDialogCardViewModel.Email,
                    PhoneNumber = _supplierProductDialogCardViewModel.PhoneNumber,
                    Address = _supplierProductDialogCardViewModel.Address,
                };

                var result = await _supplierService.AddSupplierAsync(newSupplier);

                if (result.Success)
                {
                    await LoadSupplierDataFromDatabaseAsync();

                    _toastManager.CreateToast("Supplier Added Successfully")
                        .WithContent($"Successfully added '{newSupplier.SupplierName}' to the database!")
                        .DismissOnClick()
                        .ShowSuccess();
                }
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new supplier contact cancelled")
                    .WithContent("If you want to add a new supplier contact, please try again.")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(2000)
            .Show();
    }

    [RelayCommand]
    private void ShowProductOrderView()
    {
        if (SelectedSupplier == null)
        {
            _toastManager.CreateToast("No Supplier Selected")
                .WithContent("Please select a supplier from the table first.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        _pageManager.Navigate<PurchaseOrderViewModel>(new Dictionary<string, object>
        {
            ["Context"] = PurchaseOrderContext.AddPurchaseOrder,
            ["SelectedSupplier"] = SelectedSupplier
        });
    }

    [RelayCommand]
    private async void ViewPurchaseOrderView(PurchaseOrder? purchaseOrder)
    {
        if (purchaseOrder == null || !purchaseOrder.ID.HasValue)
        {
            _toastManager.CreateToast("Invalid Selection")
                .WithContent("Please select a valid purchase order.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        if (_purchaseOrderService == null)
        {
            _toastManager.CreateToast("Service Unavailable")
                .WithContent("Purchase order service is not available.")
                .DismissOnClick()
                .ShowError();
            return;
        }

        try
        {
            // Fetch full purchase order details from database
            var result = await _purchaseOrderService.GetPurchaseOrderByIdAsync(purchaseOrder.ID.Value);
        
            if (!result.Success || result.PurchaseOrder == null)
            {
                _toastManager.CreateToast("Failed to Load Purchase Order")
                    .WithContent(result.Message ?? "Could not retrieve purchase order details.")
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            var fullPO = result.PurchaseOrder;

            // Determine if this is a product or equipment purchase order
            // Check if items have MarkupPrice and SellingPrice (products) or Category and BatchCode (equipment)
            bool isProductPO = false;
            bool isEquipmentPO = false;

            if (fullPO.Items != null && fullPO.Items.Count > 0)
            {
                // Check first item to determine type
                var firstItem = fullPO.Items[0];
            
                // If item has markup/selling price fields, it's a product
                // This logic depends on your database schema - adjust as needed
                if (!string.IsNullOrEmpty(firstItem.Category) || !string.IsNullOrEmpty(firstItem.BatchCode))
                {
                    isEquipmentPO = true;
                }
                else
                {
                    isProductPO = true;
                }
            }

            if (isProductPO)
            {
                // Navigate to PoProductViewModel with data
                var productData = new PurchaseOrderProductRetrievalData
                {
                    PurchaseOrderId = fullPO.PurchaseOrderID,
                    PoNumber = fullPO.PONumber ?? string.Empty,
                    SupplierName = fullPO.SupplierName ?? string.Empty,
                    SupplierEmail = GetSupplierEmail(fullPO.SupplierID),
                    ContactPerson = GetSupplierContactPerson(fullPO.SupplierID),
                    PhoneNumber = GetSupplierPhoneNumber(fullPO.SupplierID),
                    Address = GetSupplierAddress(fullPO.SupplierID),
                    Items = fullPO.Items?.Select(item => new PurchaseOrderProductItemData
                    {
                        ItemId = item.ItemID ?? string.Empty,
                        ItemName = item.ItemName ?? string.Empty,
                        Unit = item.Unit ?? string.Empty,
                        SupplierPrice = item.SupplierPrice,
                        MarkupPrice = item.MarkupPrice,
                        SellingPrice = item.SellingPrice,
                        Quantity = item.Quantity,
                        QuantityReceived = item.QuantityReceived
                    }).ToList() ?? new List<PurchaseOrderProductItemData>(),
                    Subtotal = fullPO.Subtotal,
                    Vat = fullPO.TaxRate,
                    Total = fullPO.Total,
                    OrderDate = fullPO.OrderDate,
                    ExpectedDeliveryDate = fullPO.ExpectedDeliveryDate,
                    ShippingStatus = fullPO.ShippingStatus ?? string.Empty,
                    PaymentStatus = fullPO.PaymentStatus ?? string.Empty
                };

                _pageManager.Navigate<PoProductViewModel>(new Dictionary<string, object>
                {
                    ["RetrievalData"] = productData
                });
            }
            else if (isEquipmentPO)
            {
                // Navigate to PoEquipmentViewModel with data
                var equipmentData = new PurchaseOrderEquipmentRetrievalData
                {
                    PurchaseOrderId = fullPO.PurchaseOrderID,
                    PoNumber = fullPO.PONumber ?? string.Empty,
                    SupplierName = fullPO.SupplierName ?? string.Empty,
                    SupplierEmail = GetSupplierEmail(fullPO.SupplierID),
                    ContactPerson = GetSupplierContactPerson(fullPO.SupplierID),
                    PhoneNumber = GetSupplierPhoneNumber(fullPO.SupplierID),
                    Address = GetSupplierAddress(fullPO.SupplierID),
                    Items = fullPO.Items?.Select(item => new PurchaseOrderEquipmentItemData
                    {
                        ItemId = item.ItemID ?? string.Empty,
                        ItemName = item.ItemName ?? string.Empty,
                        Unit = item.Unit ?? string.Empty,
                        Category = item.Category ?? string.Empty,
                        BatchCode = item.BatchCode ?? string.Empty,
                        Price = item.SupplierPrice,
                        Quantity = item.Quantity,
                        QuantityReceived = item.QuantityReceived
                    }).ToList() ?? new List<PurchaseOrderEquipmentItemData>(),
                    Subtotal = fullPO.Subtotal,
                    Vat = fullPO.TaxRate,
                    Total = fullPO.Total,
                    OrderDate = fullPO.OrderDate,
                    ExpectedDeliveryDate = fullPO.ExpectedDeliveryDate,
                    ShippingStatus = fullPO.ShippingStatus ?? string.Empty,
                    PaymentStatus = fullPO.PaymentStatus ?? string.Empty
                };

                _pageManager.Navigate<PoEquipmentViewModel>(new Dictionary<string, object>
                {
                    ["RetrievalData"] = equipmentData
                });
            }
            else
            {
                _toastManager.CreateToast("Unknown Purchase Order Type")
                    .WithContent("Could not determine if this is a product or equipment purchase order.")
                    .DismissOnClick()
                    .ShowWarning();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Loading Purchase Order")
                .WithContent($"An error occurred: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private void ShowSingleSupplierDeletionDialog(Supplier? supplier)
    {
        if (supplier == null) return;

        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete {supplier.Name} and remove the data from your database.")
            .WithPrimaryButton("Continue", async () => await OnSubmitDeleteSingleSupplier(supplier), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowSingleOrderDeletionDialog(PurchaseOrder? order)
    {
        if (order == null) return;

        _dialogManager.CreateDialog(
                "Are you absolutely sure?",
                $"This action cannot be undone. This will permanently delete {order.PONumber} and remove the data from your database.")
            .WithPrimaryButton("Continue", async () => await OnSubmitDeleteSingleOrder(order), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowMultipleSupplierDeletionDialog()
    {
        var selectedSuppliers = SupplierItems.Where(s => s.IsSelected).ToList();

        if (selectedSuppliers.Count == 0)
        {
            _toastManager.CreateToast("No Selection")
                .WithContent("Please select at least one supplier to delete.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete {selectedSuppliers.Count} supplier(s) and remove their data from your database.")
            .WithPrimaryButton("Continue", async () => await OnSubmitDeleteMultipleSuppliers(), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowMultipleOrderDeletionDialog()
    {
        var selectedOrders = PurchaseOrderItems.Where(s => s.IsSelected).ToList();

        if (selectedOrders.Count == 0)
        {
            _toastManager.CreateToast("No Selection")
                .WithContent("Please select at least one order to delete.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        _dialogManager.CreateDialog(
                "Are you absolutely sure?",
                $"This action cannot be undone. This will permanently delete {selectedOrders.Count} order(s) and remove their data from your database.")
            .WithPrimaryButton("Continue", async () => await OnSubmitDeleteMultipleOrders(), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private async Task SearchSupplier()
    {
        if (string.IsNullOrWhiteSpace(SearchStringResult))
        {
            foreach (var supplier in SupplierItems)
            {
                supplier.PropertyChanged -= OnSupplierPropertyChanged;
            }

            SupplierItems.Clear();
            foreach (var equipment in CurrentFilteredSupplierData)
            {
                equipment.PropertyChanged += OnSupplierPropertyChanged;
                SupplierItems.Add(equipment);
            }
            UpdateSupplierCounts();
            return;
        }

        IsSearchingSupplier = true;

        try
        {
            await Task.Delay(500);

            var filteredSuppliers = CurrentFilteredSupplierData.Where(supplier =>
                    supplier is
                    {
                        Name: not null, ContactPerson: not null, Email: not null,
                        PhoneNumber: not null, Products: not null, Status: not null
                    } && (supplier.Name.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                          supplier.ContactPerson.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                          supplier.Email.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                          supplier.PhoneNumber.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                          supplier.Products.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                          supplier.Status.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var supplier in SupplierItems)
            {
                supplier.PropertyChanged -= OnSupplierPropertyChanged;
            }

            SupplierItems.Clear();
            foreach (var supplier in filteredSuppliers)
            {
                supplier.PropertyChanged += OnSupplierPropertyChanged;
                SupplierItems.Add(supplier);
            }
            UpdateSupplierCounts();
        }
        finally
        {
            IsSearchingSupplier = false;
        }
    }

    [RelayCommand]
    private async Task SearchPurchaseOrder()
    {
        if (string.IsNullOrWhiteSpace(SearchStringOrderResult))
        {
            foreach (var po in PurchaseOrderItems)
            {
                po.PropertyChanged -= OnPurchaseOrderPropertyChanged;
            }

            PurchaseOrderItems.Clear();
            foreach (var po in CurrentFilteredPurchaseOrderData)
            {
                po.PropertyChanged += OnPurchaseOrderPropertyChanged;
                PurchaseOrderItems.Add(po);
            }
            UpdatePurchaseOrderCounts();
            return;
        }

        IsSearchingOrders = true;

        try
        {
            await Task.Delay(500);

            var filteredOrders = CurrentFilteredPurchaseOrderData.Where(po =>
                    po is { PONumber: not null, Name: not null, Item: not null } &&
                    (po.PONumber.Contains(SearchStringOrderResult, StringComparison.OrdinalIgnoreCase) ||
                     po.Name.Contains(SearchStringOrderResult, StringComparison.OrdinalIgnoreCase) ||
                     po.Item.Contains(SearchStringOrderResult, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var po in PurchaseOrderItems)
            {
                po.PropertyChanged -= OnPurchaseOrderPropertyChanged;
            }

            PurchaseOrderItems.Clear();
            foreach (var po in filteredOrders)
            {
                po.PropertyChanged += OnPurchaseOrderPropertyChanged;
                PurchaseOrderItems.Add(po);
            }
            UpdatePurchaseOrderCounts();
        }
        finally
        {
            IsSearchingOrders = false;
        }
    }

    [RelayCommand]
    private async Task ExportSupplierList()
    {
        try
        {
            if (SupplierItems.Count == 0)
            {
                _toastManager.CreateToast("No supplier list to export")
                    .WithContent("There are no supplier list available for the selected filter.")
                    .DismissOnClick()
                    .ShowWarning();
                return;
            }

            var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (toplevel == null) return;

            IStorageFolder? startLocation = null;
            if (!string.IsNullOrWhiteSpace(_currentSettings?.DownloadPath))
            {
                try
                {
                    startLocation = await toplevel.StorageProvider.TryGetFolderFromPathAsync(_currentSettings.DownloadPath);
                }
                catch
                {
                    // If path is invalid, startLocation will remain null
                }
            }

            var fileName = $"Supplier_List_{DateTime.Today:yyyy-MM-dd}.pdf";
            var pdfFile = await toplevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Download Supplier List",
                SuggestedStartLocation = startLocation,
                FileTypeChoices = [FilePickerFileTypes.Pdf],
                SuggestedFileName = fileName,
                ShowOverwritePrompt = true
            });

            if (pdfFile == null) return;

            var supplierModel = new SupplierDocumentModel
            {
                GeneratedDate = DateTime.Today,
                GymName = "AHON Victory Fitness Gym",
                GymAddress = "2nd Flr. Event Hub, Victory Central Mall, Brgy. Balibago, Sta. Rosa City, Laguna",
                GymPhone = "+63 123 456 7890",
                GymEmail = "info@ahonfitness.com",
                Items = SupplierItems.Select(supplier => new SupplierItem
                {
                    ID = supplier.ID ?? 0,
                    Name = supplier.Name,
                    ContactPerson = supplier.ContactPerson,
                    Email = supplier.Email,
                    PhoneNumber = supplier.PhoneNumber,
                    Address = supplier.Address,
                    Products = supplier.Products,
                    Status = supplier.Status,
                    DeliverySchedule = supplier.DeliverySchedule,
                    ContractTerms = supplier.ContractTerms
                }).ToList()
            };

            var document = new SupplierDocument(supplierModel);

            await using var stream = await pdfFile.OpenWriteAsync();
            document.GeneratePdf(stream);

            _toastManager.CreateToast("Supplier list exported successfully")
                .WithContent($"Supplier list has been saved to {pdfFile.Name}")
                .DismissOnClick()
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Export failed")
                .WithContent($"Failed to export supplier list: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    public bool CanDeleteSelectedSuppliers
    {
        get
        {
            var selectedSuppliers = SupplierItems.Where(item => item.IsSelected).ToList();
            if (selectedSuppliers.Count == 0) return false;

            if (SelectedSupplier?.Status != null &&
                !SelectedSupplier.Status.Equals("Suspended", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return selectedSuppliers.All(supplier
                => supplier.Status != null &&
                   supplier.Status.Equals("Suspended", StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task LoadSettingsAsync() => _currentSettings = await _settingsService.LoadSettingsAsync();

    private void ApplySupplierFilter()
    {
        if (OriginalSupplierData.Count == 0) return;
        List<Supplier> filteredList = OriginalSupplierData.ToList();

        if (SelectedStatusFilterItem != "All")
        {
            filteredList = filteredList
                .Where(supplier => supplier.Status != null &&
                                  supplier.Status.Equals(SelectedStatusFilterItem, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        CurrentFilteredSupplierData = filteredList;

        foreach (var item in SupplierItems)
        {
            item.PropertyChanged -= OnSupplierPropertyChanged;
        }

        SupplierItems.Clear();
        foreach (var supplier in filteredList)
        {
            supplier.PropertyChanged += OnSupplierPropertyChanged;
            SupplierItems.Add(supplier);
        }

        UpdateSupplierCounts();
    }

    private void ApplyPurchaseOrderFilter()
    {
        if (OriginalPurchaseOrderData.Count == 0) return;
        List<PurchaseOrder> filteredList = OriginalPurchaseOrderData.ToList();

        if (SelectedDeliveryFilterItem != "All")
        {
            filteredList = filteredList
                .Where(po => po.DeliveryStatus != null &&
                             po.DeliveryStatus.Equals(SelectedDeliveryFilterItem, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        CurrentFilteredPurchaseOrderData = filteredList;

        foreach (var item in PurchaseOrderItems)
        {
            item.PropertyChanged -= OnPurchaseOrderPropertyChanged;
        }

        PurchaseOrderItems.Clear();
        foreach (var po in filteredList)
        {
            po.PropertyChanged += OnPurchaseOrderPropertyChanged;
            PurchaseOrderItems.Add(po);
        }

        UpdatePurchaseOrderCounts();
    }

    [RelayCommand]
    private void ToggleSupplierSelection(bool? isChecked)
    {
        var shouldSelect = isChecked ?? false;

        foreach (var equipmentItem in SupplierItems)
        {
            equipmentItem.IsSelected = shouldSelect;
        }
        UpdateSupplierCounts();
    }

    [RelayCommand]
    private void TogglePurchaseOrderSelection(bool? isChecked)
    {
        var shouldSelect = isChecked ?? false;

        foreach (var purchaseItem in PurchaseOrderItems)
        {
            purchaseItem.IsSelected = shouldSelect;
        }
        UpdatePurchaseOrderCounts();
    }

    private async Task OnSubmitDeleteSingleSupplier(Supplier supplier)
    {
        if (supplier.ID == null) return;

        var result = await _supplierService.DeleteSupplierAsync(supplier.ID.Value);

        if (result.Success)
        {
            supplier.PropertyChanged -= OnSupplierPropertyChanged;
            SupplierItems.Remove(supplier);
            OriginalSupplierData.Remove(supplier);
            CurrentFilteredSupplierData.Remove(supplier);
            UpdateSupplierCounts();

            _toastManager.CreateToast("Supplier Deleted")
                .WithContent($"{supplier.Name} has been deleted successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowSuccess();
        }
    }

    private async Task OnSubmitDeleteSingleOrder(PurchaseOrder order)
    {
        if (order.ID == null || _purchaseOrderService == null) return;

        var result = await _purchaseOrderService.DeletePurchaseOrderAsync(order.ID.Value);

        if (result.Success)
        {
            order.PropertyChanged -= OnPurchaseOrderPropertyChanged;
            PurchaseOrderItems.Remove(order);
            OriginalPurchaseOrderData.Remove(order);
            CurrentFilteredPurchaseOrderData.Remove(order);
            UpdatePurchaseOrderCounts();

            _toastManager.CreateToast("Purchase Order Deleted")
                .WithContent($"{order.PONumber} has been deleted successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowSuccess();
        }
    }

    private async Task OnSubmitDeleteMultipleSuppliers()
    {
        var selectedSuppliers = SupplierItems.Where(item => item.IsSelected).ToList();
        if (selectedSuppliers.Count == 0) return;

        var supplierIds = selectedSuppliers
            .Where(s => s.ID.HasValue)
            .Select(s => s.ID!.Value)
            .ToList();

        var result = await _supplierService.DeleteMultipleSuppliersAsync(supplierIds);

        if (result.Success)
        {
            foreach (var supplier in selectedSuppliers)
            {
                supplier.PropertyChanged -= OnSupplierPropertyChanged;
                SupplierItems.Remove(supplier);
                OriginalSupplierData.Remove(supplier);
                CurrentFilteredSupplierData.Remove(supplier);
            }
            UpdateSupplierCounts();

            _toastManager.CreateToast("Suppliers Deleted")
                .WithContent($"{result.DeletedCount} supplier(s) deleted successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowSuccess();
        }
    }

    private async Task OnSubmitDeleteMultipleOrders()
    {
        if (_purchaseOrderService == null) return;

        var selectedOrders = PurchaseOrderItems.Where(item => item.IsSelected).ToList();
        if (selectedOrders.Count == 0) return;

        var orderIds = selectedOrders
            .Where(s => s.ID.HasValue)
            .Select(s => s.ID!.Value)
            .ToList();

        // Delete each order individually since we don't have bulk delete
        int deletedCount = 0;
        foreach (var orderId in orderIds)
        {
            var result = await _purchaseOrderService.DeletePurchaseOrderAsync(orderId);
            if (result.Success)
            {
                deletedCount++;
            }
        }

        if (deletedCount > 0)
        {
            foreach (var order in selectedOrders)
            {
                order.PropertyChanged -= OnPurchaseOrderPropertyChanged;
                PurchaseOrderItems.Remove(order);
                OriginalPurchaseOrderData.Remove(order);
                CurrentFilteredPurchaseOrderData.Remove(order);
            }
            UpdatePurchaseOrderCounts();

            _toastManager.CreateToast("Purchase Orders Deleted")
                .WithContent($"{deletedCount} purchase order(s) deleted successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowSuccess();
        }
    }

    private async Task CheckAndUpdateSupplierStatusesAsync()
    {
        var today = DateTime.Today;
        var suppliersToUpdate = new List<Supplier>();

        foreach (var supplier in OriginalSupplierData)
        {
            if (supplier.ContractTerms.HasValue && supplier.Status != null)
            {
                if (supplier.ContractTerms.Value.Date <= today &&
                    supplier.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                {
                    suppliersToUpdate.Add(supplier);
                }
                else if (supplier.ContractTerms.Value.Date > today &&
                         supplier.Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
                {
                    suppliersToUpdate.Add(supplier);
                }
            }
        }

        foreach (var supplier in suppliersToUpdate)
        {
            if (supplier.ID.HasValue)
            {
                var newStatus = supplier.ContractTerms!.Value.Date <= today ? "Inactive" : "Active";

                var updatedSupplier = new SupplierManagementModel
                {
                    SupplierID = supplier.ID.Value,
                    SupplierName = supplier.Name,
                    ContactPerson = supplier.ContactPerson,
                    Email = supplier.Email,
                    PhoneNumber = supplier.PhoneNumber,
                    Address = supplier.Address,
                    Products = supplier.Products,
                    Status = newStatus,
                    DeliverySchedule = supplier.DeliverySchedule,
                    ContractTerms = supplier.ContractTerms
                };

                await _supplierService.UpdateSupplierAsync(updatedSupplier);
                supplier.Status = newStatus;
            }
        }
    }

    private void UpdateSupplierCounts()
    {
        SelectedCount = SupplierItems.Count(x => x.IsSelected);
        TotalCount = SupplierItems.Count;
        SelectAll = SupplierItems.Count > 0 && SupplierItems.All(x => x.IsSelected);
    }
    
    private string GetSupplierEmail(int? supplierId)
    {
        if (!supplierId.HasValue) return string.Empty;
        var supplier = OriginalSupplierData.FirstOrDefault(s => s.ID == supplierId.Value);
        return supplier?.Email ?? string.Empty;
    }

    private string GetSupplierContactPerson(int? supplierId)
    {
        if (!supplierId.HasValue) return string.Empty;
        var supplier = OriginalSupplierData.FirstOrDefault(s => s.ID == supplierId.Value);
        return supplier?.ContactPerson ?? string.Empty;
    }

    private string GetSupplierPhoneNumber(int? supplierId)
    {
        if (!supplierId.HasValue) return string.Empty;
        var supplier = OriginalSupplierData.FirstOrDefault(s => s.ID == supplierId.Value);
        return supplier?.PhoneNumber ?? string.Empty;
    }

    private string GetSupplierAddress(int? supplierId)
    {
        if (!supplierId.HasValue) return string.Empty;
        var supplier = OriginalSupplierData.FirstOrDefault(s => s.ID == supplierId.Value);
        return supplier?.Address ?? string.Empty;
    }

    private void OnSupplierPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Supplier.IsSelected))
        {
            UpdateSupplierCounts();
        }
    }

    partial void OnSearchStringResultChanged(string value)
    {
        SearchSupplierCommand.Execute(null);
    }

    partial void OnSearchStringOrderResultChanged(string value)
    {
        SearchPurchaseOrderCommand.Execute(null);
    }

    partial void OnSelectedStatusFilterItemChanged(string value)
    {
        ApplySupplierFilter();
    }

    partial void OnSelectedDeliveryFilterItemChanged(string value)
    {
        ApplyPurchaseOrderFilter();
    }

    partial void OnSelectedSupplierChanged(Supplier? value)
    {
        OnPropertyChanged(nameof(CanDeleteSelectedSuppliers));
    }

    private void UpdatePurchaseOrderCounts()
    {
        var selectedCount = PurchaseOrderItems.Count(x => x.IsSelected);
        SelectAll = PurchaseOrderItems.Count > 0 && PurchaseOrderItems.All(x => x.IsSelected);
    }

    private void OnPurchaseOrderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PurchaseOrder.IsSelected))
        {
            UpdatePurchaseOrderCounts();
        }
    }

    protected override void DisposeManagedResources()
    {
        var eventService = DashboardEventService.Instance;

        if (_supplierPurchaseOrderEventHandler != null)
        {
            eventService.PurchaseOrderAdded -= _supplierPurchaseOrderEventHandler;
        }
        if (_supplierPurchaseOrderEventHandler != null)
        {
            eventService.PurchaseOrderUpdated -= _supplierPurchaseOrderEventHandler;
        }
        if (_supplierPurchaseOrderEventHandler != null)
        {
            eventService.PurchaseOrderDeleted -= _supplierPurchaseOrderEventHandler;
        }

        foreach (var supplier in SupplierItems)
        {
            supplier.PropertyChanged -= OnSupplierPropertyChanged;
        }

        foreach (var po in PurchaseOrderItems)
        {
            po.PropertyChanged -= OnPurchaseOrderPropertyChanged;
        }

        SupplierItems.Clear();
        OriginalSupplierData.Clear();
        CurrentFilteredSupplierData.Clear();
        PurchaseOrderItems.Clear();
        OriginalPurchaseOrderData.Clear();
        CurrentFilteredPurchaseOrderData.Clear();
    }
}

public partial class Supplier : ObservableObject
{
    [ObservableProperty]
    private int? _iD;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _contactPerson;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _phoneNumber;

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string? _products;

    [ObservableProperty]
    private string? _deliverySchedule;

    [ObservableProperty]
    private DateTime? _contractTerms;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private bool _isSelected;

    public string FormattedContractTerms => ContractTerms.HasValue ? $"{ContractTerms.Value:MM/dd/yyyy}" : string.Empty;

    public IBrush StatusForeground => Status?.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        "inactive" => new SolidColorBrush(Color.FromRgb(100, 116, 139)),
        "suspended" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
    };

    public IBrush StatusBackground => Status?.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),
        "inactive" => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139)),
        "suspended" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))
    };

    public string? StatusDisplayText => Status?.ToLowerInvariant() switch
    {
        "active" => "● Active",
        "inactive" => "● Inactive",
        "suspended" => "● Suspended",
        _ => Status
    };

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusDisplayText));
    }
}

public partial class PurchaseOrder : ObservableObject
{
    [ObservableProperty]
    private int? _iD;

    [ObservableProperty]
    private string? _pONumber;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _item;

    [ObservableProperty]
    private decimal? _amount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeliveryForeground))]
    [NotifyPropertyChangedFor(nameof(DeliveryBackground))]
    [NotifyPropertyChangedFor(nameof(DeliveryDisplayText))]
    private string? _deliveryStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaymentForeground))]
    [NotifyPropertyChangedFor(nameof(PaymentBackground))]
    [NotifyPropertyChangedFor(nameof(PaymentDisplayText))]
    private string? _paymentStatus;

    [ObservableProperty]
    private DateTime? _orderDate;

    [ObservableProperty]
    private DateTime? _expectedDeliveryDate;

    [ObservableProperty]
    private bool _isSelected;

    public string FormattedAmount => $"₱{Amount:N2}";

    public string FormattedOrderDate => OrderDate.HasValue ? $"{OrderDate.Value:MM/dd/yyyy}" : string.Empty;

    public string FormattedExpectedDelivery => ExpectedDeliveryDate.HasValue ? $"{ExpectedDeliveryDate.Value:MM/dd/yyyy}" : string.Empty;

    public IBrush DeliveryForeground => DeliveryStatus?.ToLowerInvariant() switch
    {
        "pending" => new SolidColorBrush(Color.FromRgb(234, 179, 8)),
        "processing" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
        "shipped/in-transit" => new SolidColorBrush(Color.FromRgb(168, 85, 247)),
        "delivered" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        "cancelled" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
    };

    public IBrush DeliveryBackground => DeliveryStatus?.ToLowerInvariant() switch
    {
        "pending" => new SolidColorBrush(Color.FromArgb(25, 234, 179, 8)),
        "processing" => new SolidColorBrush(Color.FromArgb(25, 59, 130, 246)),
        "shipped/in-transit" => new SolidColorBrush(Color.FromArgb(25, 168, 85, 247)),
        "delivered" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),
        "cancelled" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))
    };

    public string? DeliveryDisplayText => DeliveryStatus?.ToLowerInvariant() switch
    {
        "pending" => "● Pending",
        "processing" => "● Processing",
        "shipped/in-transit" => "● Shipped",
        "delivered" => "● Delivered",
        "cancelled" => "● Cancelled",
        _ => DeliveryStatus
    };

    public IBrush PaymentForeground => PaymentStatus?.ToLowerInvariant() switch
    {
        "unpaid" => new SolidColorBrush(Color.FromRgb(234, 179, 8)),
        "paid" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        "cancelled" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
    };

    public IBrush PaymentBackground => PaymentStatus?.ToLowerInvariant() switch
    {
        "unpaid" => new SolidColorBrush(Color.FromArgb(25, 234, 179, 8)),
        "paid" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),
        "cancelled" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))
    };

    public string? PaymentDisplayText => PaymentStatus?.ToLowerInvariant() switch
    {
        "unpaid" => "● Unpaid",
        "paid" => "● Paid",
        "cancelled" => "● Cancelled",
        _ => PaymentStatus
    };

    partial void OnDeliveryStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(DeliveryForeground));
        OnPropertyChanged(nameof(DeliveryBackground));
        OnPropertyChanged(nameof(DeliveryDisplayText));
    }

    partial void OnPaymentStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(PaymentForeground));
        OnPropertyChanged(nameof(PaymentBackground));
        OnPropertyChanged(nameof(PaymentDisplayText));
    }
}

public class PurchaseOrderProductRetrievalData
{
    public int PurchaseOrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierEmail { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public List<PurchaseOrderProductItemData> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Vat { get; set; }
    public decimal Total { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string ShippingStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}

public class PurchaseOrderProductItemData
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal SupplierPrice { get; set; }
    public decimal MarkupPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int Quantity { get; set; }
    public int QuantityReceived { get; set; }
}

public class PurchaseOrderEquipmentRetrievalData
{
    public int PurchaseOrderId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierEmail { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public List<PurchaseOrderEquipmentItemData> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Vat { get; set; }
    public decimal Total { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string ShippingStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}

public class PurchaseOrderEquipmentItemData
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BatchCode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int QuantityReceived { get; set; }
}
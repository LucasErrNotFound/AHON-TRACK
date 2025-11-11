using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using AHON_TRACK.Services;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using QuestPDF.Companion;
using QuestPDF.Fluent;

namespace AHON_TRACK.ViewModels;

[Page("supplier-management")]
public sealed partial class SupplierManagementViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _statusFilterItems = ["All", "Active", "Inactive", "Suspended"];

    [ObservableProperty]
    private string _selectedStatusFilterItem = "All";
    
    [ObservableProperty]
    private string[] _deliveryFilterItems = ["All", "Pending", "Delivered", "Cancelled"];

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

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly SupplierDialogCardViewModel _supplierDialogCardViewModel;
    private readonly ISupplierService _supplierService;
    private readonly SettingsService _settingsService;
    private AppSettings? _currentSettings;

    public SupplierManagementViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,
        SupplierDialogCardViewModel supplierDialogCardViewModel, SettingsService settingsService, ISupplierService supplierService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _supplierDialogCardViewModel = supplierDialogCardViewModel;
        _supplierService = supplierService;
        _settingsService = settingsService;

        SelectedStatusFilterItem = "All";
        SelectedDeliveryFilterItem = "All";

        _ = LoadSupplierDataFromDatabaseAsync();
        LoadPurchaseOrderData(); // Default Value
        UpdateSupplierCounts();
    }

    public SupplierManagementViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _supplierDialogCardViewModel = new SupplierDialogCardViewModel();
        _settingsService = new SettingsService();
        _supplierService = null!; // This should be injected in real scenario

        SelectedStatusFilterItem = "All";
        SelectedDeliveryFilterItem = "All";

        _ = LoadSupplierDataFromDatabaseAsync();
        LoadPurchaseOrderData(); // Default Value
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
            LoadPurchaseOrderData();
        }
        UpdateSupplierCounts();
        IsInitialized = true;
    }

    private async Task LoadSupplierDataFromDatabaseAsync()
    {
        if (_isLoadingData) return; // ✅ Prevent duplicate loads

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
                    Products = s.Products,
                    Status = s.Status,
                    DeliverySchedule = s.DeliverySchedule,
                    ContractTerms = s.ContractTerms,
                    IsSelected = false
                }).ToList();

                OriginalSupplierData = suppliers;

                await CheckAndUpdateSupplierStatusesAsync();

                // Let ApplySupplierFilter populate SupplierItems according to the current SelectedStatusFilterItem
                ApplySupplierFilter();

                // Ensure selection / counts are correct after filtering
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
            _isLoadingData = false; // ✅ Reset flag
        }
    }


    private void LoadSupplierData()
    {
        var sampleSupplier = GetSampleSupplierData();
        OriginalSupplierData = sampleSupplier;

        // ✅ ApplySupplierFilter already handles unsubscribe correctly
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

        // Apply filter to populate PurchaseOrderItems
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
                DeliveryStatus = "Delivered"
            },
            new PurchaseOrder
            {
                PONumber = "PO-AHON-2025-232984",
                Name = "Optimum",
                Item = "Whey Protein (Gold Standard)",
                Amount = 25000.00m,
                DeliveryStatus = "Pending"
            },
            new PurchaseOrder
            {
                PONumber = "PO-AHON-2025-279373",
                Name = "Jump Manila",
                Item = "Gym Accessories (Gloves, Belts)",
                Amount = 8500.00m,
                DeliveryStatus = "Cancelled"
            }
        ];
    }

    [RelayCommand]
    private void ShowAddSupplierDialog()
    {
        _supplierDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_supplierDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                var newSupplier = new SupplierManagementModel
                {
                    SupplierName = _supplierDialogCardViewModel.SupplierName,
                    ContactPerson = _supplierDialogCardViewModel.ContactPerson,
                    Email = _supplierDialogCardViewModel.Email,
                    PhoneNumber = _supplierDialogCardViewModel.PhoneNumber,
                    Status = _supplierDialogCardViewModel.Status ?? "Active",
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
            .WithMaxWidth(650)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowEditSupplierDialog(Supplier? supplier)
    {
        if (supplier == null) return;

        _supplierDialogCardViewModel.InitializeForEditMode(supplier);
        _dialogManager.CreateDialog(_supplierDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                var currentStatus = _supplierDialogCardViewModel.Status ?? "Active";

                var updatedSupplier = new SupplierManagementModel
                {
                    SupplierID = supplier.ID ?? 0,
                    SupplierName = _supplierDialogCardViewModel.SupplierName,
                    ContactPerson = _supplierDialogCardViewModel.ContactPerson,
                    Email = _supplierDialogCardViewModel.Email,
                    PhoneNumber = _supplierDialogCardViewModel.PhoneNumber,
                    Status = currentStatus,
                };

                var result = await _supplierService.UpdateSupplierAsync(updatedSupplier);

                if (result.Success)
                {
                    await LoadSupplierDataFromDatabaseAsync();

                    _toastManager.CreateToast("Supplier Updated")
                        .WithContent($"Successfully updated '{updatedSupplier.SupplierName}'!")
                        .DismissOnClick()
                        .ShowSuccess();
                }
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Modifying supplier Details Cancelled")
                    .WithContent($"Try again if you really want to modify {supplier.Name}'s details")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(950)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowProductOrderView()
    {
        _pageManager.Navigate<PurchaseOrderViewModel>(new Dictionary<string, object>
        {
            ["Context"] = PurchaseOrderContext.AddPurchaseOrder
        });
    }
    
    [RelayCommand]
    private void ViewProductOrderView()
    {
        _pageManager.Navigate<PurchaseOrderViewModel>(new Dictionary<string, object>
        {
            ["Context"] = PurchaseOrderContext.ViewPurchaseOrder,
            ["SelectedSupplier"] = SelectedSupplier
        });
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
                $"This action cannot be undone. This will permanently delete {order.Name} and remove the data from your database.")
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
            // ✅ Unsubscribe before clearing
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

            // ✅ Unsubscribe before clearing
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
            // Unsubscribe before clearing
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
    
            // Unsubscribe before clearing
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
            // Check if there are any supplier list to export
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
                    Products = supplier.Products,
                    Status = supplier.Status,
                    DeliverySchedule = supplier.DeliverySchedule,
                    ContractTerms = supplier.ContractTerms
                }).ToList()
            };

            var document = new SupplierDocument(supplierModel);

            await using var stream = await pdfFile.OpenWriteAsync();

            // Both cannot be enabled at the same time. Disable one of them 
            document.GeneratePdf(stream); // Generate the PDF
            // await document.ShowInCompanionAsync(); // For Hot-Reload Debugging

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
            // If there is no checked items, can't delete
            var selectedSuppliers = SupplierItems.Where(item => item.IsSelected).ToList();
            if (selectedSuppliers.Count == 0) return false;

            // If the currently selected row is present and its status is not expired,
            // then "Delete Selected" should be disabled when opening the menu for that row.
            if (SelectedSupplier?.Status != null &&
                !SelectedSupplier.Status.Equals("Suspended", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Only allow deletion if ALL selected members are Expired
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

        // Apply Status filter
        if (SelectedStatusFilterItem != "All")
        {
            filteredList = filteredList
                .Where(supplier => supplier.Status != null &&
                                  supplier.Status.Equals(SelectedStatusFilterItem, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        CurrentFilteredSupplierData = filteredList;

        // Unsubscribe from old items
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

        // Apply Delivery Status filter
        if (SelectedDeliveryFilterItem != "All")
        {
            filteredList = filteredList
                .Where(po => po.DeliveryStatus != null &&
                             po.DeliveryStatus.Equals(SelectedDeliveryFilterItem, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        CurrentFilteredPurchaseOrderData = filteredList;

        // Unsubscribe from old items
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

        // Call service to delete from database
        var result = await _supplierService.DeleteSupplierAsync(supplier.ID.Value);

        if (result.Success)
        {
            // Remove from UI
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
        if (order.ID == null) return;

        // Call service to delete from database
        var result = await _supplierService.DeleteSupplierAsync(order.ID.Value);

        if (result.Success)
        {
            // Remove from UI
            order.PropertyChanged -= OnSupplierPropertyChanged;
            PurchaseOrderItems.Remove(order);
            OriginalPurchaseOrderData.Remove(order);
            CurrentFilteredPurchaseOrderData.Remove(order);
            UpdateSupplierCounts();

            _toastManager.CreateToast("Purchase Order Deleted")
                .WithContent($"{order.Name} has been deleted successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowSuccess();
        }
    }

    private async Task OnSubmitDeleteMultipleSuppliers()
    {
        var selectedSuppliers = SupplierItems.Where(item => item.IsSelected).ToList();
        if (selectedSuppliers.Count == 0) return;

        // Prepare list of IDs
        var supplierIds = selectedSuppliers
            .Where(s => s.ID.HasValue)
            .Select(s => s.ID!.Value)
            .ToList();

        // Call service to delete multiple from database
        var result = await _supplierService.DeleteMultipleSuppliersAsync(supplierIds);

        if (result.Success)
        {
            // Remove from UI
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
        var selectedOrders = PurchaseOrderItems.Where(item => item.IsSelected).ToList();
        if (selectedOrders.Count == 0) return;

        // Prepare list of IDs
        var orderIds = selectedOrders 
            .Where(s => s.ID.HasValue)
            .Select(s => s.ID!.Value)
            .ToList();

        // Call service to delete multiple from database
        var result = await _supplierService.DeleteMultipleSuppliersAsync(orderIds);

        if (result.Success)
        {
            // Remove from UI
            foreach (var order in selectedOrders)
            {
                order.PropertyChanged -= OnPurchaseOrderPropertyChanged;
                PurchaseOrderItems.Remove(order);
                OriginalPurchaseOrderData.Remove(order);
                CurrentFilteredPurchaseOrderData.Remove(order);
            }
            UpdateSupplierCounts();

            _toastManager.CreateToast("Purchase Order Deleted")
                .WithContent($"{result.DeletedCount} purchase order(s) deleted successfully!")
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
        // Update counts similar to supplier counts
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
        foreach (var supplier in SupplierItems)
        {
            supplier.PropertyChanged -= OnSupplierPropertyChanged;
        }
    
        foreach (var po in PurchaseOrderItems)
        {
            po.PropertyChanged -= OnPurchaseOrderPropertyChanged;
        }

        // Clear collections
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
        "active" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),     // Green-500
        "inactive" => new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Gray-500
        "suspended" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red-500
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))           // Default Gray-500
    };

    public IBrush StatusBackground => Status?.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),     // Green-500 with alpha
        "inactive" => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139)), // Gray-500 with alpha
        "suspended" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)), // Red-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))           // Default Gray-500 with alpha
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

// Add PurchaseOrder class at the bottom of the file (after Supplier class)

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
    private string? _deliveryStatus;

    [ObservableProperty]
    private bool _isSelected;

    public string FormattedAmount => $"₱{Amount:N2}";
    
    public IBrush DeliveryForeground => DeliveryStatus?.ToLowerInvariant() switch
    {
        "pending" => new SolidColorBrush(Color.FromRgb(234, 179, 8)),    // Yellow-500
        "delivered" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),  // Green-500
        "cancelled" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),  // Red-500
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))           // Default Gray-500
    };

    public IBrush DeliveryBackground => DeliveryStatus?.ToLowerInvariant() switch
    {
        "pending" => new SolidColorBrush(Color.FromArgb(25, 234, 179, 8)),   // Yellow-500 with alpha
        "delivered" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)), // Green-500 with alpha
        "cancelled" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)), // Red-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))          // Default Gray-500 with alpha
    };

    public string? DeliveryDisplayText => DeliveryStatus?.ToLowerInvariant() switch
    {
        "pending" => "● Pending",
        "delivered" => "● Delivered",
        "cancelled" => "● Cancelled",
        _ => DeliveryStatus
    };

    partial void OnDeliveryStatusChanged(string value)
    {
        OnPropertyChanged(nameof(DeliveryForeground));
        OnPropertyChanged(nameof(DeliveryBackground));
        OnPropertyChanged(nameof(DeliveryDisplayText));
    }
}
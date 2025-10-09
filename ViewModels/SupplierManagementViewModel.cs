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

namespace AHON_TRACK.ViewModels;

[Page("supplier-management")]
public sealed partial class SupplierManagementViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _supplierFilterItems = ["All", "Products", "Drinks", "Supplements"];

    [ObservableProperty]
    private string _selectedSupplierFilterItem = "All";

    [ObservableProperty]
    private ObservableCollection<Supplier> _supplierItems = [];

    [ObservableProperty]
    private List<Supplier> _originalSupplierData = [];

    [ObservableProperty]
    private List<Supplier> _currentFilteredSupplierData = [];

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private string _searchStringResult = string.Empty;

    [ObservableProperty]
    private bool _isSearchingSupplier;

    [ObservableProperty]
    private bool _selectAll;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private Supplier? _selectedSupplier;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly SupplierDialogCardViewModel _supplierDialogCardViewModel;
    private readonly ISupplierService _supplierService;

    public SupplierManagementViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, SupplierDialogCardViewModel supplierDialogCardViewModel, ISupplierService supplierService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _supplierDialogCardViewModel = supplierDialogCardViewModel;
        _supplierService = supplierService;

        _ = LoadSupplierDataFromDatabaseAsync();
        UpdateSupplierCounts();
    }

    public SupplierManagementViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _supplierDialogCardViewModel = new SupplierDialogCardViewModel();
        _supplierService = null!; // This should be injected in real scenario

        _ = LoadSupplierDataFromDatabaseAsync();
        UpdateSupplierCounts();
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (IsInitialized) return;
        if (_supplierService != null)
        {
            await LoadSupplierDataFromDatabaseAsync();
        }
        else
        {
            LoadSupplierData();
        }
        UpdateSupplierCounts();
        IsInitialized = true;
    }

    private async Task LoadSupplierDataFromDatabaseAsync()
    {
        IsLoading = true;

        try
        {
            // Call the service to get suppliers from database
            var result = await _supplierService.GetAllSuppliersAsync();

            if (result.Success && result.Suppliers != null)
            {
                // Convert SupplierManagementModel to Supplier (UI model)
                var suppliers = result.Suppliers.Select(s => new Supplier
                {
                    ID = s.SupplierID,
                    Name = s.SupplierName,
                    ContactPerson = s.ContactPerson,
                    Email = s.Email,
                    PhoneNumber = s.PhoneNumber,
                    Products = s.Products,
                    Status = s.Status,
                    IsSelected = false
                }).ToList();

                OriginalSupplierData = suppliers;
                CurrentFilteredSupplierData = [.. suppliers];

                SupplierItems.Clear();
                foreach (var supplier in suppliers)
                {
                    supplier.PropertyChanged += OnSupplierPropertyChanged;
                    SupplierItems.Add(supplier);
                }

                TotalCount = SupplierItems.Count;

                if (SupplierItems.Count > 0)
                {
                    SelectedSupplier = SupplierItems[0];
                }
            }
            else
            {
                // Handle failure - show error or load sample data
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
        }
    }


    private void LoadSupplierData()
    {
        var sampleSupplier = GetSampleSupplierData();
        OriginalSupplierData = sampleSupplier;
        CurrentFilteredSupplierData = [.. sampleSupplier];

        SupplierItems.Clear();
        foreach (var supplier in sampleSupplier)
        {
            SupplierItems.Add(supplier);
        }
        TotalCount = SupplierItems.Count;

        if (SupplierItems.Count > 0)
        {
            SelectedSupplier = SupplierItems[0];
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
                Email = "rodolfo.morales21@gmai.com",
                PhoneNumber = "09182938475",
                Products = "Drinks",
                Status = "Active"
            },
            new Supplier
            {
                ID = 1002,
                Name = "AHON Factory",
                ContactPerson = "Joel Abalos",
                Email = "joel.abalos@gmai.com",
                PhoneNumber = "09382293009",
                Products = "Products",
                Status = "Inactive"
            },
            new Supplier
            {
                ID = 1003,
                Name = "Optimum",
                ContactPerson = "Mr. Lopez",
                Email = "ignacio.lopez@gmai.com",
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

    [RelayCommand]
    private void ShowAddSupplierDialog()
    {
        _supplierDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_supplierDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                // Get the supplier data from dialog
                var newSupplier = new SupplierManagementModel
                {
                    SupplierName = _supplierDialogCardViewModel.SupplierName,
                    ContactPerson = _supplierDialogCardViewModel.ContactPerson,
                    Email = _supplierDialogCardViewModel.Email,
                    PhoneNumber = _supplierDialogCardViewModel.PhoneNumber,
                    Products = _supplierDialogCardViewModel.Products,
                    Status = _supplierDialogCardViewModel.Status ?? "Active"
                };

                // Call service to add to database
                var result = await _supplierService.AddSupplierAsync(newSupplier);

                if (result.Success)
                {
                    // Reload data from database to refresh UI
                    await LoadSupplierDataFromDatabaseAsync();

                    _toastManager.CreateToast("Supplier Added Successfully")
                        .WithContent($"Successfully added '{newSupplier.SupplierName}' to the database!")
                        .DismissOnClick()
                        .ShowSuccess();
                }
                // Error handling is done inside the service with toast notifications
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
                // Prepare updated supplier data
                var updatedSupplier = new SupplierManagementModel
                {
                    SupplierID = supplier.ID ?? 0,
                    SupplierName = _supplierDialogCardViewModel.SupplierName,
                    ContactPerson = _supplierDialogCardViewModel.ContactPerson,
                    Email = _supplierDialogCardViewModel.Email,
                    PhoneNumber = _supplierDialogCardViewModel.PhoneNumber,
                    Products = _supplierDialogCardViewModel.Products,
                    Status = _supplierDialogCardViewModel.Status ?? "Active"
                };

                // Call service to update in database
                var result = await _supplierService.UpdateSupplierAsync(updatedSupplier);

                if (result.Success)
                {
                    // Reload data to refresh UI
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
    private void ShowSingleItemDeletionDialog(Supplier? supplier)
    {
        if (supplier == null) return;

        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete {supplier.Name} and remove the data from your database.")
            .WithPrimaryButton("Continue", async () => await OnSubmitDeleteSingleItem(supplier), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowMultipleItemDeletionDialog()
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
            .WithPrimaryButton("Continue", async () => await OnSubmitDeleteMultipleItems(), DialogButtonStyle.Destructive)
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

    private void ApplySupplierFilter()
    {
        if (OriginalSupplierData.Count == 0) return;
        List<Supplier> filteredList;

        if (SelectedSupplierFilterItem == "All")
        {
            filteredList = OriginalSupplierData.ToList();
        }
        else
        {
            filteredList = OriginalSupplierData
                .Where(equipment => equipment.Products == SelectedSupplierFilterItem)
                .ToList();
        }
        CurrentFilteredSupplierData = filteredList;

        SupplierItems.Clear();
        foreach (var supplier in filteredList)
        {
            supplier.PropertyChanged += OnSupplierPropertyChanged;
            SupplierItems.Add(supplier);
        }
        UpdateSupplierCounts();
    }

    [RelayCommand]
    private void ToggleSelection(bool? isChecked)
    {
        var shouldSelect = isChecked ?? false;

        foreach (var equipmentItem in SupplierItems)
        {
            equipmentItem.IsSelected = shouldSelect;
        }
        UpdateSupplierCounts();
    }

    private async Task OnSubmitDeleteSingleItem(Supplier supplier)
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

    private async Task OnSubmitDeleteMultipleItems()
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

    partial void OnSelectedSupplierFilterItemChanged(string value)
    {
        ApplySupplierFilter();
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
    private string? _status;

    [ObservableProperty]
    private bool _isSelected;

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
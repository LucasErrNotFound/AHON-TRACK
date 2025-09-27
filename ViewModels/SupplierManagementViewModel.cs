using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using AHON_TRACK.Components.ViewModels;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

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
    private Supplier? _selectedSupplier;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly SupplierDialogCardViewModel _supplierDialogCardViewModel;

    public SupplierManagementViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,  SupplierDialogCardViewModel supplierDialogCardViewModel)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _supplierDialogCardViewModel = supplierDialogCardViewModel;
        
        LoadSupplierData();
        UpdateSupplierCounts();
    }

    public SupplierManagementViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _supplierDialogCardViewModel = new SupplierDialogCardViewModel();
        
        LoadSupplierData();
        UpdateSupplierCounts();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        LoadSupplierData();
        UpdateSupplierCounts();
        IsInitialized = true;
    }

    private void LoadSupplierData()
    {
        var sampleSupplier = GetSampleSupplierData();
        OriginalSupplierData = sampleSupplier;
        CurrentFilteredSupplierData = [..sampleSupplier];

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
            .WithSuccessCallback(_ =>
                _toastManager.CreateToast("Added a new supplier contact")
                    .WithContent($"You just added a new supplier contact to the database!")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new supplier contact cancelled")
                    .WithContent("If you want to add a new supplier contact, please try again.")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(650)
            .Dismissible()
            .Show();
    }
    
    [RelayCommand]
    private void ShowEditSupplierDialog(Supplier? supplier)
    {
        if (supplier == null) return;

        _supplierDialogCardViewModel.InitializeForEditMode(supplier);
        _dialogManager.CreateDialog(_supplierDialogCardViewModel)
            .WithSuccessCallback(_ =>
            {
                _toastManager.CreateToast("Modified supplier details")
                    .WithContent($"You have successfully modified {supplier.Name}!")
                    .DismissOnClick()
                    .ShowSuccess();
            })
            .WithCancelCallback(() => 
                _toastManager.CreateToast("Modifying supplier Details Cancelled")
                    .WithContent($"Try again if you really want to modify the {supplier.Name}'s details")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(950)
            .Dismissible()
            .Show();
    }
    
    [RelayCommand]
    private void ShowSingleItemDeletionDialog(Supplier? supplier)
    {
        if (supplier == null) return;
        
        _dialogManager.CreateDialog("" + 
            "Are you absolutely sure?", $"This action cannot be undone. This will permanently delete {supplier.Name} and remove the data from your database.")
            .WithPrimaryButton("Continue", () => OnSubmitDeleteSingleItem(supplier), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }
    
    [RelayCommand]
    private void ShowMultipleItemDeletionDialog(Supplier? supplier)
    {
        if (supplier == null) return;

        _dialogManager.CreateDialog("" + 
            "Are you absolutely sure?", $"This action cannot be undone. This will permanently delete multiple equipments and remove their data from your database.")
            .WithPrimaryButton("Continue", () => OnSubmitDeleteMultipleItems(supplier), DialogButtonStyle.Destructive)
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
        await DeleteEquipmentFromDatabase(supplier);
        supplier.PropertyChanged -= OnSupplierPropertyChanged;
        SupplierItems.Remove(supplier);
        UpdateSupplierCounts();

        _toastManager.CreateToast("Delete Supplier")
            .WithContent($"{supplier.Name} has been deleted successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowSuccess();
    }
    private async Task OnSubmitDeleteMultipleItems(Supplier supplier)
    {
        var selectedSuppliers = SupplierItems.Where(item => item.IsSelected).ToList();
        if (selectedSuppliers.Count == 0) return;

        foreach (var suppliers in selectedSuppliers)
        {
            await DeleteEquipmentFromDatabase(supplier);
            suppliers.PropertyChanged -= OnSupplierPropertyChanged;
            SupplierItems.Remove(suppliers);
        }
        UpdateSupplierCounts();

        _toastManager.CreateToast($"Delete Selected Suppliers")
            .WithContent($"Multiple suppliers deleted successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowSuccess();
    }
    
    private async Task DeleteEquipmentFromDatabase(Supplier supplier)
    {
        await Task.Delay(100); // Just an animation/simulation of async operation
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
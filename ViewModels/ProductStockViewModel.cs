using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using AHON_TRACK.Components.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

[Page("item-stock")]
public sealed partial class ProductStockViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty] 
    private string[] _productFilterItems = ["All", "Products", "Drinks", "Supplements"];

    [ObservableProperty] 
    private string _selectedProductFilterItem = "All";
    
    [ObservableProperty]
    private ObservableCollection<ProductStock> _productItems = [];
    
    [ObservableProperty]
    private List<ProductStock> _originalProductData = [];
    
    [ObservableProperty]
    private List<ProductStock> _currentFilteredProductData = [];
    
    [ObservableProperty]
    private bool _isInitialized;
    
    [ObservableProperty]
    private string _searchStringResult = string.Empty;
    
    [ObservableProperty]
    private bool _isSearchingProduct;
    
    [ObservableProperty]
    private bool _selectAll;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _totalCount;
    
    [ObservableProperty]
    private ProductStock? _selectedProduct;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public ProductStockViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        
        LoadProductData();
        UpdateProductCounts();
    }

    public ProductStockViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        
        LoadProductData();
        UpdateProductCounts();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        LoadProductData();
        UpdateProductCounts();
        IsInitialized = true;
    }

    private void LoadProductData()
    {
        var sampleProduct = GetSampleProductdata();
        OriginalProductData = sampleProduct;
        CurrentFilteredProductData = [..sampleProduct];
        
        ProductItems.Clear();
        foreach (var product in sampleProduct)
        {
            product.PropertyChanged += OnProductPropertyChanged;
            ProductItems.Add(product);
        }
        TotalCount = ProductItems.Count;

        if (ProductItems.Count > 0)
        {
            SelectedProduct = ProductItems[0];
        }
        ApplyProductFilter();
        UpdateProductCounts();
    }

    private List<ProductStock> GetSampleProductdata()
    {
        var today = DateTime.Today;
        return
        [
            new ProductStock
            {
                ID = 1001,
                Name = "Cobra Energy Drink",
                Description = "Yellow Blast Flavor",
                Category = "Drinks",
                CurrentStock = 17,
                Price = 35,
                Supplier = "San Miguel",
                Expiry = today.AddYears(6).AddDays(32),
                Status = "In Stock",
                Poster = "avares://AHON_TRACK/Assets/ProductStockView/cobra-yellow-drink-display.png"
            },
            new ProductStock
            {
                ID = 1002,
                Name = "Gold Standard Whey Protein",
                Description = "5lbs Premium Whey Protein",
                Category = "Supplements",
                CurrentStock = 17,
                Price = 2500,
                Supplier = "Optimum",
                Expiry = today.AddYears(6).AddDays(32),
                Status = "In Stock",
                Poster = "avares://AHON_TRACK/Assets/ProductStockView/protein-powder-display.png"
            },
            new ProductStock
            {
                ID = 1003,
                Name = "Creatine XPLODE Powder",
                Description = "1.1lbs Creatine Monohydrate",
                Category = "Supplements",
                CurrentStock = 0,
                Price = 1050,
                Supplier = "Optimum",
                Expiry = today.AddYears(6).AddDays(32),
                Status = "Out of Stock",
                Poster = "avares://AHON_TRACK/Assets/ProductStockView/creatine-display.png"
            },
            new ProductStock
            {
                ID = 1004,
                Name = "Insane Labz PSYCHOTIC",
                Description = "7.6oz PreWorkout Peaches & Cream",
                Category = "Supplements",
                CurrentStock = 3,
                Price = 900,
                Supplier = "Optimum",
                Expiry = today.AddYears(6).AddDays(32),
                Status = "In Stock",
                Poster = "avares://AHON_TRACK/Assets/ProductStockView/preworkout-display.png"
            }
        ];
    }
    
    [RelayCommand]
    private void ShowAddProductView()
    {
        _pageManager.Navigate<AddEditProductViewModel>(new Dictionary<string, object>
        {
            ["Context"] = ProductViewContext.AddProduct
        });
    }

    [RelayCommand]
    private void ShowEditProductView()
    {
        if (SelectedProduct == null) return;
        _pageManager.Navigate<AddEditProductViewModel>(new Dictionary<string, object>
        {
            ["Context"] = ProductViewContext.EditProduct,
            ["SelectedProduct"] = SelectedProduct
        });
    }
    
    [RelayCommand]
    private void ShowSingleItemDeletionDialog(ProductStock? product)
    {
        if (product == null) return;
        
        _dialogManager.CreateDialog("" + 
            "Are you absolutely sure?", $"This action cannot be undone. This will permanently delete {product.Name} and remove the data from your database.")
            .WithPrimaryButton("Continue", () => OnSubmitDeleteSingleItem(product), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }
    
    [RelayCommand]
    private void ShowMultipleItemDeletionDialog(ProductStock? product)
    {
        if (product == null) return;

        _dialogManager.CreateDialog("" + 
            "Are you absolutely sure?", $"This action cannot be undone. This will permanently delete multiple products and remove their data from your database.")
            .WithPrimaryButton("Continue", () => OnSubmitDeleteMultipleItems(product), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }
    
    private async Task OnSubmitDeleteSingleItem(ProductStock? product)
    {
        if (product == null) return;
        
        await DeleteProductFromDatabase(product);
        product.PropertyChanged -= OnProductPropertyChanged;
        ProductItems.Remove(product);
        UpdateProductCounts();

        _toastManager.CreateToast("Delete product")
            .WithContent($"{product.Name} has been deleted successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowSuccess();
    }
    private async Task OnSubmitDeleteMultipleItems(ProductStock? product)
    {
        if (product == null) return;
        
        var selectedProducts = ProductItems.Where(item => item.IsSelected).ToList();
        if (selectedProducts.Count == 0) return;

        foreach (var products in selectedProducts)
        {
            await DeleteProductFromDatabase(products);
            products.PropertyChanged -= OnProductPropertyChanged;
            ProductItems.Remove(products);
        }
        UpdateProductCounts();

        _toastManager.CreateToast("Delete Selected Products")
            .WithContent("Multiple products deleted successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowSuccess();
    }
    
    private async Task DeleteProductFromDatabase(ProductStock? product)
    {
        await Task.Delay(100); // Just an animation/simulation of async operation
    }
    
    [RelayCommand]
    private void ToggleSelection(bool? isChecked)
    {
        var shouldSelect = isChecked ?? false;

        foreach (var productItem in ProductItems)
        {
            productItem.IsSelected = shouldSelect;
        }
        UpdateProductCounts();
    }
    
    [RelayCommand]
    private async Task SearchProduct()
    {
        if (string.IsNullOrWhiteSpace(SearchStringResult))
        {
            ProductItems.Clear();
            foreach (var product in CurrentFilteredProductData)
            {
                product.PropertyChanged += OnProductPropertyChanged;
                ProductItems.Add(product);
            }
            UpdateProductCounts();
            return;
        }
        
        IsSearchingProduct = true;
        
        try
        {
            await Task.Delay(500);

            var filteredEquipments = CurrentFilteredProductData.Where(equipment =>
                    equipment is { Name: not null, Category: not null, Supplier: not null, Status: not null } && 
                    (equipment.Name.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) || 
                     equipment.Category.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                     equipment.Supplier.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                     equipment.Status.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ProductItems.Clear();
            foreach (var equipment in filteredEquipments)
            {
                equipment.PropertyChanged += OnProductPropertyChanged;
                ProductItems.Add(equipment);
            }
            UpdateProductCounts();
        }
        finally
        {
            IsSearchingProduct = false;
        }
    }
    
    private void ApplyProductFilter()
    {
        if (OriginalProductData.Count == 0) return;
        List<ProductStock> filteredList;
        
        if (SelectedProductFilterItem == "All")
        {
            filteredList = OriginalProductData.ToList();
        }
        else
        {
            filteredList = OriginalProductData 
                .Where(equipment => equipment.Category == SelectedProductFilterItem)
                .ToList();
        }
        CurrentFilteredProductData = filteredList;

        ProductItems.Clear();
        foreach (var product in filteredList)
        {
            product.PropertyChanged += OnProductPropertyChanged;
            ProductItems.Add(product);
        }
        UpdateProductCounts();
    }
    
    private void UpdateProductCounts()
    {
        SelectedCount = ProductItems.Count(x => x.IsSelected);
        TotalCount = ProductItems.Count;

        SelectAll = ProductItems.Count > 0 && ProductItems.All(x => x.IsSelected);
    }
    
    private void OnProductPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProductStock.IsSelected))
        {
            UpdateProductCounts();
        }
    }
    
    partial void OnSearchStringResultChanged(string value)
    {
        SearchProductCommand.Execute(null);
    }
    
    partial void OnSelectedProductFilterItemChanged(string value)
    {
        ApplyProductFilter();
    }
}

public partial class ProductStock : ObservableObject
{
    [ObservableProperty] 
    private int? _iD;

    [ObservableProperty] 
    private string? _name;
    
    [ObservableProperty] 
    private string? _description;
    
    [ObservableProperty] 
    private string? _category; 
    
    [ObservableProperty] 
    private int? _currentStock;
    
    [ObservableProperty] 
    private int? _price;
    
    [ObservableProperty] 
    private string? _supplier;
    
    [ObservableProperty] 
    private DateTime? _expiry;
    
    [ObservableProperty] 
    private string? _status;
    
    [ObservableProperty] 
    private string? _poster;
    
    [ObservableProperty] 
    private bool _isSelected;
    
    public string FormattedExpiry => Expiry.HasValue ? $"{Expiry.Value:MM/dd/yyyy}" :  string.Empty;
    public string FormattedPrice => Price.HasValue ? $"₱{Price:N2}" : string.Empty;
    
    public IBrush StatusForeground => Status?.ToLowerInvariant() switch
    {
        "in stock" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),      // Green-500
        "out of stock" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),  // Red-500
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))              // Default Gray-500
    };

    public IBrush StatusBackground => Status?.ToLowerInvariant() switch
    {
        "in stock" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),     // Green-500 with alpha
        "out of stock" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)), // Red-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))             // Default Gray-500 with alpha
    };

    public string? StatusDisplayText => Status?.ToLowerInvariant() switch
    {
        "in stock" => "● In Stock",
        "out of stock" => "● Out of Stock",
        _ => Status 
    };

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusDisplayText));
    }
}
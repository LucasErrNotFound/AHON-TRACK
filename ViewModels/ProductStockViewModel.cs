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
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Models;

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

    [ObservableProperty]
    private bool _isLoading;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly ISystemService _systemService;

    public ProductStockViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, ISystemService systemService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _systemService = systemService;

        _ = LoadProductDataAsync();
        // LoadProductData();
        UpdateProductCounts();
    }

    public ProductStockViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _systemService = null!;

        LoadProductData();
        UpdateProductCounts();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        _ = LoadProductDataAsync();
        //LoadProductData();
        UpdateProductCounts();
        IsInitialized = true;
    }

    private async Task LoadProductDataAsync()
    {
        if (_systemService == null) return;

        IsLoading = true;
        try
        {
            var productModels = await _systemService.GetProductsAsync();
            var productStocks = productModels.Select(MapToProductStock).ToList();

            OriginalProductData = productStocks;
            CurrentFilteredProductData = [.. productStocks];

            ProductItems.Clear();
            foreach (var product in productStocks)
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
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Error Loading Products")
                .WithContent($"Failed to load products: {ex.Message}")
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadProductData()
    {
        var sampleProduct = GetSampleProductdata();
        OriginalProductData = sampleProduct;
        CurrentFilteredProductData = [.. sampleProduct];

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
                Sku = "1AU3OTE0923U",
                Description = "Yellow Blast Flavor",
                Category = "Drinks",
                CurrentStock = 17,
                Price = 35,
                DiscountInPercentage = true,
                DiscountedPrice = 5,
                Supplier = "San Miguel",
                Expiry = today.AddYears(6).AddDays(32),
                Status = "In Stock",
                Poster = "avares://AHON_TRACK/Assets/ProductStockView/cobra-yellow-drink-display.png"
            },
            new ProductStock
            {
                ID = 1002,
                Name = "Gold Standard Whey Protein",
                Sku = "1AU3OTE0923U",
                Description = "5lbs Premium Whey Protein",
                Category = "Supplements",
                CurrentStock = 17,
                DiscountInPercentage = true,
                DiscountedPrice = 50,
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
                Sku = "1AU3OTE0923U",
                Description = "1.1lbs Creatine Monohydrate",
                Category = "Supplements",
                CurrentStock = 0,
                DiscountInPercentage = false,
                DiscountedPrice = 550,
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
                Sku = "1AU3OTE0923U",
                Description = "7.6oz PreWorkout Peaches & Cream",
                Category = "Supplements",
                CurrentStock = 3,
                DiscountInPercentage = false,
                DiscountedPrice = null,
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

        var success = await DeleteProductFromDatabase(product);

        if (success)
        {
            product.PropertyChanged -= OnProductPropertyChanged;
            ProductItems.Remove(product);
            OriginalProductData.Remove(product);
            CurrentFilteredProductData.Remove(product);
            UpdateProductCounts();

            _toastManager.CreateToast("Delete product")
                .WithContent($"{product.Name} has been deleted successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowSuccess();
        }
    }

    private async Task OnSubmitDeleteMultipleItems(ProductStock? product)
    {
        if (product == null) return;

        var selectedProducts = ProductItems.Where(item => item.IsSelected).ToList();
        if (selectedProducts.Count == 0) return;

        var successCount = 0;
        foreach (var products in selectedProducts)
        {
            var success = await DeleteProductFromDatabase(products);
            if (success)
            {
                products.PropertyChanged -= OnProductPropertyChanged;
                ProductItems.Remove(products);
                OriginalProductData.Remove(products);
                CurrentFilteredProductData.Remove(products);
                successCount++;
            }
        }

        UpdateProductCounts();

        _toastManager.CreateToast("Delete Selected Products")
            .WithContent($"{successCount} product(s) deleted successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowSuccess();
    }

    private async Task<bool> DeleteProductFromDatabase(ProductStock? product)
    {
        if (product == null || _systemService == null) return false;

        try
        {
            return await _systemService.DeleteProductAsync(product.ID);
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Delete Failed")
                .WithContent($"Failed to delete product: {ex.Message}")
                .ShowError();
            return false;
        }
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

    private ProductStock MapToProductStock(ProductModel model)
    {
        return new ProductStock
        {
            ID = model.ProductID,
            Name = model.ProductName,
            Sku = model.SKU,
            Category = model.Category,
            CurrentStock = model.CurrentStock,
            Price = (int)model.Price,
            Supplier = model.ProductSupplier ?? "",
            Expiry = model.ExpiryDate,
            Status = model.Status,
            Description = model.Description ?? "",
            DiscountedPrice = model.DiscountedPrice.HasValue ? (int)model.DiscountedPrice.Value : null,
            DiscountInPercentage = model.IsPercentageDiscount,
            Poster = model.ProductImagePath ?? "avares://AHON_TRACK/Assets/ProductStockView/default-product.png"
        };
    }

    public async Task RefreshProductsAsync()
    {
        await LoadProductDataAsync();
    }

    public void SetNavigationParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("ShouldRefresh", out var shouldRefresh) && (bool)shouldRefresh)
        {
            _ = LoadProductDataAsync();
        }
    }
}

public partial class ProductStock : ObservableObject
{
    [ObservableProperty]
    private int _iD;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _sku;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _category;

    [ObservableProperty]
    private int? _currentStock;

    [ObservableProperty]
    private int? _price;

    [ObservableProperty]
    private int? _discountedPrice;

    [ObservableProperty]
    private bool _discountInPercentage;

    [ObservableProperty]
    private string? _supplier;

    [ObservableProperty]
    private DateTime? _expiry;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private string? _poster; // Image path or URL

    [ObservableProperty]
    private bool _isSelected;

    public string FormattedExpiry => Expiry.HasValue ? $"{Expiry.Value:MM/dd/yyyy}" : string.Empty;
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
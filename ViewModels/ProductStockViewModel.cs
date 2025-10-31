using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using AHON_TRACK.Components.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using QuestPDF.Companion;
using QuestPDF.Fluent;
using ShadUI;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Events;

namespace AHON_TRACK.ViewModels;

[Page("item-stock")]
public sealed partial class ProductStockViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _productFilterItems = ["All", "Products", "Drinks", "Supplements", "Apparel"];

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
    private readonly IProductService _productService;
    private readonly SettingsService _settingsService;
    private AppSettings? _currentSettings;
    private bool _disposed = false;

    public ProductStockViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,
        SettingsService settingsService, IProductService productService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _settingsService = settingsService;
        _productService = productService;
        SubscribeToEvent();
        _ = LoadProductDataAsync(showAlerts: true);
        UpdateProductCounts();
    }

    public ProductStockViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _settingsService = new SettingsService();
        _productService = null!;

        SubscribeToEvent();
        UpdateProductCounts();
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (IsInitialized) return;
        SubscribeToEvent();
        await LoadSettingsAsync();
        await LoadProductDataAsync();

        UpdateProductCounts();
        IsInitialized = true;
    }

    private void SubscribeToEvent()
    {
        var eventService = DashboardEventService.Instance;

        eventService.ProductAdded -= OnProductDataChanged;  // Unsubscribe first to prevent double subscription
        eventService.ProductUpdated -= OnProductDataChanged;
        eventService.ProductDeleted -= OnProductDataChanged;
        eventService.ProductPurchased -= OnProductPurchased;

        eventService.ProductAdded += OnProductDataChanged;
        eventService.ProductUpdated += OnProductDataChanged;
        eventService.ProductDeleted += OnProductDataChanged;
        eventService.ProductPurchased += OnProductPurchased;
    }

    private async void OnProductDataChanged(object? sender, EventArgs e)
    {
        try
        {
            await LoadProductDataAsync(showAlerts: false);
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast($"Failed to load: {ex.Message}");
        }
    }

    private async void OnProductPurchased(object? sender, EventArgs e)
    {
        try
        {
            await LoadProductDataAsync(showAlerts: true);
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast($"Failed to load: {ex.Message}");
        }
    }

    private async Task LoadProductDataAsync(bool showAlerts = false)
    {
        if (_productService == null)
        {
            LoadProductData(); // Fallback to sample data
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _productService.GetAllProductsAsync();

            if (!result.Success || result.Products == null)
            {
                _toastManager?.CreateToast("Error Loading Products")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            var productStocks = result.Products.Select(MapToProductStock).ToList();

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

            if (showAlerts)
            {
                await _productService.ShowProductAlertsAsync();
            }
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Error Loading Products")
                .WithContent($"Failed to load products: {ex.Message}")
                .DismissOnClick()
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
        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete {product.Name} and remove the data from your database.")
            .WithPrimaryButton("Continue", () => OnSubmitDeleteSingleItem(product), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowMultipleItemDeletionDialog()
    {
        var selectedCount = ProductItems.Count(x => x.IsSelected);
        if (selectedCount == 0)
        {
            _toastManager.CreateToast("No Selection")
                .WithContent("Please select products to delete")
                .DismissOnClick()
                .ShowWarning();
            return;
        }
        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete {selectedCount} product(s) and remove their data from your database.")
            .WithPrimaryButton("Continue", OnSubmitDeleteMultipleItems, DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    private async Task OnSubmitDeleteSingleItem(ProductStock? product)
    {
        if (product == null || _productService == null) return;

        var result = await _productService.DeleteProductAsync(product.ID);

        if (result.Success)
        {
            product.PropertyChanged -= OnProductPropertyChanged;
            ProductItems.Remove(product);
            OriginalProductData.Remove(product);
            CurrentFilteredProductData.Remove(product);
            UpdateProductCounts();
            _ = LoadProductDataAsync();

            // Service already shows success toast
        }
    }

    private async Task OnSubmitDeleteMultipleItems()
    {
        if (_productService == null) return;

        var selectedProducts = ProductItems.Where(item => item.IsSelected).ToList();
        if (selectedProducts.Count == 0) return;

        var productIds = selectedProducts.Select(p => p.ID).ToList();
        var result = await _productService.DeleteMultipleProductsAsync(productIds);

        if (result.Success)
        {
            foreach (var product in selectedProducts)
            {
                product.PropertyChanged -= OnProductPropertyChanged;
                ProductItems.Remove(product);
                OriginalProductData.Remove(product);
                CurrentFilteredProductData.Remove(product);
            }

            UpdateProductCounts();
            _ = LoadProductDataAsync();
            // Service already shows success toast
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
            await Task.Delay(300); // Debounce

            var filteredProducts = CurrentFilteredProductData.Where(product =>
                    product is { Name: not null, Category: not null, Supplier: not null, Status: not null } &&
                    (product.Name.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                     product.Category.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                     product.Supplier.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                     product.Sku?.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) == true ||
                     product.Status.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ProductItems.Clear();
            foreach (var product in filteredProducts)
            {
                product.PropertyChanged += OnProductPropertyChanged;
                ProductItems.Add(product);
            }
            UpdateProductCounts();
        }
        finally
        {
            IsSearchingProduct = false;
        }
    }

    [RelayCommand]
    private async Task ExportProductStock()
    {
        try
        {
            // Check if there are any product list to export
            if (ProductItems.Count == 0)
            {
                _toastManager.CreateToast("No product stock list to export")
                    .WithContent("There are no product stock list available for the selected filter.")
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

            var fileName = $"Product_Stock_List_{DateTime.Today:yyyy-MM-dd}.pdf";
            var pdfFile = await toplevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Download Product Stock List",
                SuggestedStartLocation = startLocation,
                FileTypeChoices = [FilePickerFileTypes.Pdf],
                SuggestedFileName = fileName,
                ShowOverwritePrompt = true
            });

            if (pdfFile == null) return;

            var productStockModel = new ProductStockDocumentModel
            {
                GeneratedDate = DateTime.Today,
                GymName = "AHON Victory Fitness Gym",
                GymAddress = "2nd Flr. Event Hub, Victory Central Mall, Brgy. Balibago, Sta. Rosa City, Laguna",
                GymPhone = "+63 123 456 7890",
                GymEmail = "info@ahonfitness.com",
                Items = ProductItems.Select(product => new ProductItem
                {
                    ID = product.ID,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    Category = product.Category,
                    CurrentStock = product.CurrentStock ?? 0,
                    Price = product.Price,
                    Supplier = product.Supplier,
                    Expiry = product.Expiry,
                    Status = product.Status,
                }).ToList()
            };

            var document = new ProductStockDocument(productStockModel);

            await using var stream = await pdfFile.OpenWriteAsync();

            // Both cannot be enabled at the same time. Disable one of them 
            document.GeneratePdf(stream); // Generate the PDF
                                          // await document.ShowInCompanionAsync(); // For Hot-Reload Debugging

            _toastManager.CreateToast("Product stock list exported successfully")
                .WithContent($"Product stock list has been saved to {pdfFile.Name}")
                .DismissOnClick()
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Export failed")
                .WithContent($"Failed to export product stock list: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private async Task LoadSettingsAsync() => _currentSettings = await _settingsService.LoadSettingsAsync();

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
                .Where(product => product.Category == SelectedProductFilterItem)
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
        // ✅ Handle Base64 image properly
        string posterPath = "avares://AHON_TRACK/Assets/ProductStockView/default-product.png";

        if (!string.IsNullOrEmpty(model.ProductImageBase64))
        {
            posterPath = $"data:image/png;base64,{model.ProductImageBase64}";
        }

        var productStock = new ProductStock
        {
            ID = model.ProductID,
            Name = model.ProductName ?? "",
            Sku = model.SKU ?? "",
            Category = model.Category ?? "",
            CurrentStock = model.CurrentStock,
            Price = model.Price,
            Supplier = model.SupplierName ?? "None", // ✅ Use SupplierName from join
            Expiry = model.ExpiryDate,
            Status = model.Status ?? "",
            Description = model.Description ?? "",
            DiscountedPrice = model.DiscountedPrice,
            DiscountInPercentage = model.IsPercentageDiscount,
            Poster = posterPath
        };
        
        if (productStock.Expiry.HasValue && productStock.Expiry.Value.Date <= DateTime.Today)
        {
            productStock.Status = "Expired";
        }
    
        return productStock;
    }

    protected override void DisposeManagedResources()
    {
        var eventService = DashboardEventService.Instance;
        eventService.ProductAdded -= OnProductDataChanged;
        eventService.ProductUpdated -= OnProductDataChanged;
        eventService.ProductDeleted -= OnProductDataChanged;
        eventService.ProductPurchased -= OnProductPurchased;

        foreach (var product in ProductItems)
        {
            product.PropertyChanged -= OnProductPropertyChanged;
        }

        ProductItems.Clear();
        OriginalProductData.Clear();
        CurrentFilteredProductData.Clear();
    }
}

// ProductStock class remains the same as in your document
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
    private decimal _price;

    [ObservableProperty]
    private decimal? _discountedPrice;

    [ObservableProperty]
    private bool _discountInPercentage;

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

    public decimal FinalPrice
    {
        get
        {
            if (DiscountedPrice.HasValue && DiscountedPrice.Value > 0)
            {
                if (DiscountInPercentage)
                {
                    var discountAmount = Price * (DiscountedPrice.Value / 100);
                    return Price - discountAmount;
                }
                else
                {
                    return DiscountedPrice.Value;
                }
            }
            return Price;
        }
    }

    public string FormattedExpiry => Expiry.HasValue ? $"{Expiry.Value:MM/dd/yyyy}" : string.Empty;

    public string FormattedPrice => $"₱{FinalPrice:N2}";

    public string OriginalPrice => (DiscountedPrice.HasValue && DiscountedPrice.Value > 0)
        ? $"₱{Price:N2}"
        : string.Empty;

    public string DiscountDisplay
    {
        get
        {
            if (DiscountedPrice.HasValue && DiscountedPrice.Value > 0)
            {
                return DiscountInPercentage
                    ? $"{DiscountedPrice:N0}% OFF"
                    : $"₱{DiscountedPrice:N2} OFF";
            }
            return string.Empty;
        }
    }

    public IBrush StatusForeground => Status?.ToLowerInvariant() switch
    {
        "in stock" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        "out of stock" => new SolidColorBrush(Color.FromRgb(249, 115, 22)),
        "expired" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
    };

    public IBrush StatusBackground => Status?.ToLowerInvariant() switch
    {
        "in stock" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),
        "out of stock" => new SolidColorBrush(Color.FromArgb(25, 249, 115, 22)),
        "expired" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))
    };

    public string? StatusDisplayText => Status?.ToLowerInvariant() switch
    {
        "in stock" => "● In Stock",
        "out of stock" => "● Out of Stock",
        "expired" => "● Expired",
        _ => Status
    };

    partial void OnStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusDisplayText));
    }

    partial void OnPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(FinalPrice));
        OnPropertyChanged(nameof(FormattedPrice));
        OnPropertyChanged(nameof(OriginalPrice));
    }

    partial void OnDiscountedPriceChanged(decimal? value)
    {
        OnPropertyChanged(nameof(FinalPrice));
        OnPropertyChanged(nameof(FormattedPrice));
        OnPropertyChanged(nameof(OriginalPrice));
        OnPropertyChanged(nameof(DiscountDisplay));
    }

    partial void OnDiscountInPercentageChanged(bool value)
    {
        OnPropertyChanged(nameof(FinalPrice));
        OnPropertyChanged(nameof(FormattedPrice));
        OnPropertyChanged(nameof(DiscountDisplay));
    }
}
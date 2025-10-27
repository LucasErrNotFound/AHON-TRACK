using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AHON_TRACK.Components.ViewModels;

[Page("add-new-product")]
public partial class AddEditProductViewModel : ViewModelBase, INavigableWithParameters
{
    [ObservableProperty] private ProductViewContext _viewContext = ProductViewContext.AddProduct;
    
    [ObservableProperty] 
    private string[] _productStatusItems = ["In Stock", "Out of Stock"];
    private string? _selectedProductStatusItem = "In Stock";

    [ObservableProperty]
    private string[] _productCategoryItems = ["None", "Drinks", "Supplements", "Apparel", "Products"];
    private string? _selectedProductCategoryItem = "None";

    [ObservableProperty]
    private string[] _productSupplierItems = ["None"];
    private string? _selectedSupplierCategoryItem = "None";


    [ObservableProperty] private byte[]? _productImageBytes;
    [ObservableProperty] private bool _isPercentageModeOn;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private bool _isLoadingSuppliers;
    [ObservableProperty] private Image? _productImageControl;
    
    private Dictionary<string, int> _supplierNameToIdMap = new();

    private bool _suppliersLoaded = false;
    private int? _productID;
    private string? _productName = string.Empty;
    private string? _productSKU = string.Empty;
    private string? _productDescription = string.Empty;
    private DateTime? _productExpiry;
    private Image? _productImage;

    private string? _productImageFilePath;

    private decimal? _price;
    private decimal? _discountedPrice;

    private string? _productStatus;
    private string? _productCategory;
    private string? _productSupplier;
    private int? _productCurrentStock;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly IProductService _productService;
    private readonly ISupplierService _supplierService;
    private readonly INavigationService _navigationService;
    private readonly ILogger _logger;
    private bool _disposed = false;

    public string DiscountSymbol => IsPercentageModeOn ? "%" : "₱";
    public string DiscountFormat => IsPercentageModeOn ? "N2" : "N0";

    public AddEditProductViewModel(DialogManager dialogManager, 
        ToastManager toastManager, 
        IProductService productService, 
        ISupplierService supplierService,
        INavigationService navigationService,
        ILogger logger)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _productService = productService;
        _supplierService = supplierService;
        _navigationService = navigationService;
        _logger = logger;

        _ = LoadSuppliersAsync();
    }

    public AddEditProductViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _productService = null!;
        _supplierService = null!;
        _navigationService = null!;
        _logger = null!;
    }

    /*
    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (!_suppliersLoaded)
        {
            await LoadSuppliersAsync();
        }
    }
    */
    
    [AvaloniaHotReload]
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
    
        _logger?.LogInformation("Initializing AddEditProductViewModel");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            if (!_suppliersLoaded)
            {
                await LoadSuppliersAsync(linkedCts.Token).ConfigureAwait(false);
            }

            _logger?.LogInformation("AddEditProductViewModel initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("AddEditProductViewModel initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing AddEditProductViewModel");
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Navigating away from AddEditProduct");
        return ValueTask.CompletedTask;
    }

    private async Task LoadSuppliersAsync(CancellationToken cancellationToken = default)
    {
        if (_supplierService == null) return;

        IsLoadingSuppliers = true;

        try
        {
            var result = await _supplierService.GetAllSuppliersAsync().ConfigureAwait(false);
        
            cancellationToken.ThrowIfCancellationRequested();

            if (result.Success && result.Suppliers != null)
            {
                _supplierNameToIdMap.Clear();

                var supplierNames = result.Suppliers
                    .Where(s => !string.IsNullOrEmpty(s.SupplierName))
                    .OrderBy(s => s.SupplierName)
                    .ToList();

                foreach (var supplier in supplierNames)
                {
                    _supplierNameToIdMap[supplier.SupplierName] = supplier.SupplierID;
                }

                var names = supplierNames.Select(s => s.SupplierName).ToList();
                names.Insert(0, "None");

                ProductSupplierItems = names.ToArray();

                if (string.IsNullOrEmpty(SelectedProductSupplier))
                {
                    SelectedProductSupplier = "None";
                }
            }
            else
            {
                ProductSupplierItems = ["None"];
                SelectedProductSupplier = "None";

                if (!result.Success)
                {
                    _toastManager?.CreateToast("Warning")
                        .WithContent("Could not load suppliers from database. Using defaults.")
                        .DismissOnClick()
                        .ShowWarning();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("LoadSuppliersAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            ProductSupplierItems = ["None"];
            SelectedProductSupplier = "None";

            _logger?.LogError(ex, "Failed to load suppliers");
            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to load suppliers: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoadingSuppliers = false;
            _suppliersLoaded = true;
        }
    }

    [RelayCommand]
    private async Task RefreshSuppliers()
    {
        try
        {
            await LoadSuppliersAsync(LifecycleToken).ConfigureAwait(false);

            _toastManager?.CreateToast("Suppliers Refreshed")
                .WithContent("Supplier list has been updated")
                .DismissOnClick()
                .ShowSuccess();
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error refreshing suppliers");
        }
    }

    public void SetNavigationParameters(Dictionary<string, object> parameters)
    {
        if (!_suppliersLoaded)
        {
            _ = LoadSuppliersAsync(LifecycleToken);
        }

        if (parameters.TryGetValue("Context", out var context))
        {
            SetViewContext((ProductViewContext)context);
        }

        if (parameters.TryGetValue("SelectedProduct", out var product))
        {
            var selectedProduct = (ProductStock)product;
            _ = LoadSuppliersAndPopulateForm(selectedProduct);
        }
    }

    private async Task LoadSuppliersAndPopulateForm(ProductStock product)
    {
        try
        {
            // Wait for suppliers to load with timeout
            var timeout = TimeSpan.FromSeconds(5);
            var startTime = DateTime.UtcNow;
        
            while (!_suppliersLoaded && IsLoadingSuppliers)
            {
                LifecycleToken.ThrowIfCancellationRequested();
            
                if (DateTime.UtcNow - startTime > timeout)
                {
                    _logger?.LogWarning("Timeout waiting for suppliers to load");
                    break;
                }
            
                await Task.Delay(50, LifecycleToken).ConfigureAwait(false);
            }

            PopulateFormWithProductData(product);

            OnPropertyChanged(nameof(ProductCurrentStock));
            OnPropertyChanged(nameof(CurrentStock));
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("LoadSuppliersAndPopulateForm cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading suppliers and populating form");
        }
    }

    [RelayCommand]
    private async Task PublishProduct()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            _toastManager?.CreateToast("Validation Error")
                .WithContent("Please fix all validation errors before saving")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        if (_productService == null)
        {
            _toastManager?.CreateToast("Service Error")
                .WithContent("Product service is not available")
                .DismissOnClick()
                .ShowError();
            return;
        }

        IsSaving = true;
        try
        {
            LifecycleToken.ThrowIfCancellationRequested();
        
            int? supplierIdToSave = null;
            if (!string.IsNullOrEmpty(SelectedProductSupplier) && SelectedProductSupplier != "None")
            {
                if (_supplierNameToIdMap.TryGetValue(SelectedProductSupplier, out int supplierId))
                {
                    supplierIdToSave = supplierId;
                }
            }

            int currentStock = ProductCurrentStock ?? 0;
            string calculatedStatus = currentStock > 0 ? "In Stock" : "Out Of Stock";
            byte[]? imageBytesToSave = ProductImageBytes;

            var productModel = new ProductModel
            {
                ProductID = ProductID ?? 0,
                ProductName = ProductName ?? "",
                SKU = ProductSKU ?? "",
                SupplierID = supplierIdToSave,
                Description = ProductDescription,
                Price = ProductPrice ?? 0,
                DiscountedPrice = ProductDiscountedPrice,
                IsPercentageDiscount = IsPercentageModeOn,
                ProductImageFilePath = _productImageFilePath,
                ProductImageBytes = imageBytesToSave,
                ExpiryDate = ProductExpiry,
                Status = calculatedStatus,
                Category = SelectedProductCategory ?? "None",
                CurrentStock = currentStock
            };

            (bool success, string message, int? productId) result;
            if (ViewContext == ProductViewContext.EditProduct)
            {
                var updateResult = await _productService.UpdateProductAsync(productModel)
                    .ConfigureAwait(false);
                result = (updateResult.Success, updateResult.Message, productModel.ProductID);
            }
            else
            {
                result = await _productService.AddProductAsync(productModel)
                    .ConfigureAwait(false);
            }

            if (result.success)
            {
                _logger?.LogInformation("Product saved successfully: {ProductId}", result.productId);
                PublishSwitchBack();
            }
            else
            {
                _logger?.LogWarning("Failed to save product: {Message}", result.message);
                _toastManager?.CreateToast("Save Failed")
                    .WithContent(result.message)
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Product save cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving product");
            _toastManager?.CreateToast("Error")
                .WithContent($"An unexpected error occurred: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void CancelEditProduct()
    {
        var title = ViewContext == ProductViewContext.EditProduct
            ? "Cancel editing?"
            : "Cancel product creation?";

        var message = ViewContext == ProductViewContext.EditProduct
            ? "This action will cancel your editing and return to the product list. Any unsaved changes will be lost."
            : "This action will cancel the product creation process. All entered information will be lost.";

        _dialogManager
            .CreateDialog(title, message)
            .WithPrimaryButton("Continue", CancelSwitchBack, DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private async Task ChooseFile()
    {
        try
        {
            var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (toplevel == null) return;

            var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Image File",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Image Files")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg"]
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = ["*.*"]
                }
                ]
            });

            if (files.Count > 0)
            {
                var selectedFile = files[0];
                _toastManager.CreateToast("Image file selected")
                    .WithContent($"{selectedFile.Name}")
                    .DismissOnClick()
                    .ShowInfo();

                // ✅ Read the file and convert to bytes for database
                await using var stream = await selectedFile.OpenReadAsync();

                // Create bitmap for display
                var bitmap = new Bitmap(stream);

                if (ProductImageControl != null)
                {
                    ProductImageControl.Source = bitmap;
                    ProductImageControl.IsVisible = true;
                }

                // ✅ IMPORTANT: Reset stream position and read bytes for database
                stream.Position = 0;
                using var memoryStream = new System.IO.MemoryStream();
                await stream.CopyToAsync(memoryStream);
                ProductImageBytes = memoryStream.ToArray();

                Console.WriteLine($"✅ Image loaded: {ProductImageBytes.Length} bytes");

                // ✅ Store the file path as well (optional, for reference)
                _productImageFilePath = selectedFile.Path.LocalPath;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error from uploading Picture: {ex.Message}");
            _toastManager.CreateToast("Error")
                .WithContent($"Failed to load image: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private async Task PublishSwitchBack()
    {
        await _navigationService.NavigateAsync<ProductStockViewModel>(new Dictionary<string, object>
        {
            { "ShouldRefresh", true }
        });
    }

    private async Task CancelSwitchBack()
    {
        var toastTitle = ViewContext == ProductViewContext.EditProduct
            ? "Edit Cancelled"
            : "Product Discarded";

        var toastMessage = ViewContext == ProductViewContext.EditProduct
            ? "Product editing cancelled"
            : "Product discarded successfully";

        _toastManager.CreateToast(toastTitle)
            .WithContent(toastMessage)
            .DismissOnClick()
            .ShowWarning();
        
        await _navigationService.NavigateAsync<ProductStockViewModel>();
    }

    private void PopulateFormWithProductData(ProductStock product)
    {
        ProductID = product.ID;
        ProductName = product.Name;
        ProductDescription = product.Description;
        ProductPrice = product.Price;
        ProductExpiry = product.Expiry;
        IsPercentageModeOn = product.DiscountInPercentage;
        ProductDiscountedPrice = product.DiscountedPrice;
        ProductSKU = product.Sku;
        ProductCurrentStock = product.CurrentStock; // ✅ This includes 0

        if (!string.IsNullOrEmpty(product.Supplier) &&
            ProductSupplierItems.Contains(product.Supplier))
        {
            SelectedProductSupplier = product.Supplier;
        }
        else
        {
            SelectedProductSupplier = "None";
        }

        SelectedProductCategory = product.Category;

        // ✅ FIX: Properly handle image display
        if (!string.IsNullOrEmpty(product.Poster))
        {
            if (product.Poster.StartsWith("data:image/png;base64,"))
            {
                var base64Data = product.Poster.Replace("data:image/png;base64,", "");
                try
                {
                    ProductImageBytes = Convert.FromBase64String(base64Data);

                    // ✅ Create bitmap and display it
                    if (ProductImageControl != null)
                    {
                        using var memoryStream = new System.IO.MemoryStream(ProductImageBytes);
                        var bitmap = new Avalonia.Media.Imaging.Bitmap(memoryStream);
                        ProductImageControl.Source = bitmap;
                        ProductImageControl.IsVisible = true;
                    }

                    Console.WriteLine($"✅ Image loaded successfully: {ProductImageBytes.Length} bytes");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to convert Base64 image: {ex.Message}");
                }
            }
            else if (!product.Poster.StartsWith("avares://"))
            {
                // Handle file path
                ProductImageFilePath = product.Poster;
            }
        }
    }
    
    protected override async ValueTask DisposeAsyncCore()
    {
        _logger?.LogInformation("Disposing AddEditProductViewModel");
    
        // Clear any loaded data
        _supplierNameToIdMap.Clear();
        ProductImageBytes = null;
    
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    public string ViewTitle => ViewContext switch
    {
        ProductViewContext.AddProduct => "Add Product",
        ProductViewContext.EditProduct => "Edit Product",
        _ => "Add Product"
    };

    public string ViewDescription => ViewContext switch
    {
        ProductViewContext.AddProduct => "Add new gym products with details like name, price, and stock to keep your inventory up to date",
        ProductViewContext.EditProduct => "Edit existing gym products with details like name, price, and stock to keep your inventory up to date",
        _ => "Add new gym products with details like name, price, and stock to keep your inventory up to date"
    };

    public void SetViewContext(ProductViewContext context)
    {
        ViewContext = context;
        OnPropertyChanged(nameof(ViewTitle));
        OnPropertyChanged(nameof(ViewDescription));
    }

    public int? ProductID
    {
        get => _productID;
        set => SetProperty(ref _productID, value, true);
    }

    [Required(ErrorMessage = "Product Name is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string? ProductName
    {
        get => _productName;
        set => SetProperty(ref _productName, value, true);
    }

    [Required(ErrorMessage = "Product SKU is required")]
    [MinLength(8, ErrorMessage = "Must be at least 8-12 characters long")]
    [MaxLength(12, ErrorMessage = "Must not exceed 12 characters")]
    public string? ProductSKU
    {
        get => _productSKU;
        set => SetProperty(ref _productSKU, value, true);
    }

    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string? ProductDescription
    {
        get => _productDescription;
        set => SetProperty(ref _productDescription, value, true);
    }

    [Required(ErrorMessage = "Price must be set")]
    [Range(20, 5000, ErrorMessage = "Price must be between 20 and 5,000")]
    public decimal? ProductPrice
    {
        get => _price;
        set => SetProperty(ref _price, value, true);
    }

    [Range(1, 15000, ErrorMessage = "Price must be between 1 and 15,000")]
    public decimal? ProductDiscountedPrice
    {
        get => _discountedPrice;
        set => SetProperty(ref _discountedPrice, value, true);
    }

    [Range(0, 100000, ErrorMessage = "Stock must be between 0 and 100,000")]
    public int? ProductCurrentStock
    {
        get => _productCurrentStock;
        set => SetProperty(ref _productCurrentStock, value, true);
    }

    public int? CurrentStock
    {
        get => ProductCurrentStock;
        set => ProductCurrentStock = value;
    }

    public string? SelectedProductStatus
    {
        get => _selectedProductStatusItem;
        set => SetProperty(ref _selectedProductStatusItem, value, true);
    }

    public DateTime? ProductExpiry
    {
        get => _productExpiry;
        set => SetProperty(ref _productExpiry, value, true);
    }

    public string? SelectedProductCategory
    {
        get => _selectedProductCategoryItem;
        set => SetProperty(ref _selectedProductCategoryItem, value, true);
    }

    public string? SelectedProductSupplier
    {
        get => _selectedSupplierCategoryItem;
        set => SetProperty(ref _selectedSupplierCategoryItem, value, true);
    }

    public Image? ProductImage
    {
        get => _productImage;
        set => SetProperty(ref _productImage, value, true);
    }

    public string? ProductImageFilePath
    {
        get => _productImageFilePath;
        set => SetProperty(ref _productImageFilePath, value, true);
    }

    partial void OnIsPercentageModeOnChanged(bool value)
    {
        OnPropertyChanged(nameof(DiscountSymbol));
        OnPropertyChanged(nameof(DiscountFormat));
    }
}

public enum ProductViewContext
{
    AddProduct,
    EditProduct
}
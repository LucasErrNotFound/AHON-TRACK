using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.ViewModels;

[Page("add-new-product")]
public partial class AddEditProductViewModel : ViewModelBase, INavigableWithParameters
{
    [ObservableProperty]
    private ProductViewContext _viewContext = ProductViewContext.AddProduct;

    [ObservableProperty]
    private string[] _productStatusItems = ["In Stock", "Out of Stock"];
    private string? _selectedProductStatusItem = "In Stock";

    [ObservableProperty]
    private string[] _productCategoryItems = ["None", "Drinks", "Supplements", "Apparel", "Products"];
    private string? _selectedProductCategoryItem = "None";

    [ObservableProperty]
    private string[] _productSupplierItems = ["None"];
    private string? _selectedSupplierCategoryItem = "None";

    private Dictionary<string, int> _supplierNameToIdMap = new();

    [ObservableProperty]
    private byte[]? _productImageBytes;

    [ObservableProperty]
    private bool _isPercentageModeOn;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isLoadingSuppliers;

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
    private readonly PageManager _pageManager;
    private readonly IProductService _productService;
    private readonly ISupplierService _supplierService;

    public string DiscountSymbol => IsPercentageModeOn ? "%" : "₱";
    public string DiscountFormat => IsPercentageModeOn ? "N2" : "N0";

    public AddEditProductViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, IProductService productService, ISupplierService supplierService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _productService = productService;
        _supplierService = supplierService;

        _ = LoadSuppliersAsync();
    }

    public AddEditProductViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _productService = null!;
        _supplierService = null!;
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (!_suppliersLoaded)
        {
            await LoadSuppliersAsync();
        }
    }

    private async Task LoadSuppliersAsync()
    {
        if (_supplierService == null) return;

        IsLoadingSuppliers = true;

        try
        {
            var result = await _supplierService.GetAllSuppliersAsync();

            if (result.Success && result.Suppliers != null)
            {
                // ✅ Build name-to-ID mapping
                _supplierNameToIdMap.Clear();

                var supplierNames = result.Suppliers
                    .Where(s => !string.IsNullOrEmpty(s.SupplierName))
                    .OrderBy(s => s.SupplierName)
                    .ToList();

                foreach (var supplier in supplierNames)
                {
                    _supplierNameToIdMap[supplier.SupplierName] = supplier.SupplierID;
                }

                // Add "None" as first option
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
        catch (Exception ex)
        {
            ProductSupplierItems = ["None"];
            SelectedProductSupplier = "None";

            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to load suppliers: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoadingSuppliers = false;
        }
    }

    [RelayCommand]
    private async Task RefreshSuppliers()
    {
        await LoadSuppliersAsync();

        _toastManager?.CreateToast("Suppliers Refreshed")
            .WithContent("Supplier list has been updated")
            .DismissOnClick()
            .ShowSuccess();
    }

    public void SetNavigationParameters(Dictionary<string, object> parameters)
    {
        Console.WriteLine("🔵 SetNavigationParameters called");
        Console.WriteLine($"🔵 Current suppliers count: {ProductSupplierItems.Length}");
        Console.WriteLine($"🔵 Suppliers loaded: {_suppliersLoaded}");
        // ✅ Ensure suppliers are loaded before processing parameters
        if (!_suppliersLoaded)
        {
            _ = LoadSuppliersAsync();
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
        Console.WriteLine($"🔄 Loading suppliers and populating form for product: {product.Name}");

        // Wait for suppliers to load if they haven't yet
        while (!_suppliersLoaded && IsLoadingSuppliers)
        {
            await Task.Delay(50);
        }

        PopulateFormWithProductData(product);

        // Force UI update
        OnPropertyChanged(nameof(ProductCurrentStock));
        OnPropertyChanged(nameof(CurrentStock));
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
            // ✅ Convert supplier name to ID
            int? supplierIdToSave = null;
            if (!string.IsNullOrEmpty(SelectedProductSupplier) && SelectedProductSupplier != "None")
            {
                if (_supplierNameToIdMap.TryGetValue(SelectedProductSupplier, out int supplierId))
                {
                    supplierIdToSave = supplierId;
                }
            }

            var productModel = new ProductModel
            {
                ProductID = ProductID ?? 0,
                ProductName = ProductName ?? "",
                SKU = ProductSKU ?? "",
                SupplierID = supplierIdToSave, // ✅ Use ID instead of name
                Description = ProductDescription,
                Price = ProductPrice ?? 0,
                DiscountedPrice = ProductDiscountedPrice,
                IsPercentageDiscount = IsPercentageModeOn,
                ProductImageFilePath = _productImageFilePath,
                ProductImageBytes = _productImageBytes,
                ExpiryDate = ProductExpiry,
                Status = SelectedProductStatus ?? "In Stock",
                Category = SelectedProductCategory ?? "None",
                CurrentStock = ProductCurrentStock ?? 0
            };

            (bool success, string message, int? productId) result;

            if (ViewContext == ProductViewContext.EditProduct)
            {
                var updateResult = await _productService.UpdateProductAsync(productModel);
                result = (updateResult.Success, updateResult.Message, productModel.ProductID);
            }
            else
            {
                result = await _productService.AddProductAsync(productModel);
            }

            if (result.success)
            {
                PublishSwitchBack();
            }
            else
            {
                Console.WriteLine($"Failed to save product: {result.message}");
            }
        }
        catch (Exception ex)
        {
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

    private void PublishSwitchBack()
    {
        var successMessage = ViewContext == ProductViewContext.EditProduct
            ? "Product updated successfully"
            : "Product added successfully";

        // Note: Service already shows toast, but we keep this for consistency
        _pageManager.Navigate<ProductStockViewModel>(new Dictionary<string, object>
        {
            { "ShouldRefresh", true }
        });
    }

    private void CancelSwitchBack()
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
        _pageManager.Navigate<ProductStockViewModel>();
    }

    private void PopulateFormWithProductData(ProductStock product)
    {
        Console.WriteLine($"🔄 Populating form with product ID: {product.ID}");

        ProductID = product.ID;
        ProductName = product.Name;
        ProductDescription = product.Description;
        ProductPrice = product.Price;
        ProductExpiry = product.Expiry;
        IsPercentageModeOn = product.DiscountInPercentage;
        ProductDiscountedPrice = product.DiscountedPrice;
        ProductSKU = product.Sku;
        ProductCurrentStock = product.CurrentStock;

        // ✅ Set supplier by name (dropdown displays names)
        if (!string.IsNullOrEmpty(product.Supplier) &&
            ProductSupplierItems.Contains(product.Supplier))
        {
            SelectedProductSupplier = product.Supplier;
        }
        else
        {
            SelectedProductSupplier = "None";
        }

        SelectedProductStatus = product.Status;
        SelectedProductCategory = product.Category;

        // Handle Base64 images from database
        if (!string.IsNullOrEmpty(product.Poster))
        {
            if (product.Poster.StartsWith("data:image/png;base64,"))
            {
                var base64Data = product.Poster.Replace("data:image/png;base64,", "");
                try
                {
                    ProductImageBytes = Convert.FromBase64String(base64Data);
                    OnPropertyChanged(nameof(ProductImageBytes));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to convert Base64 image: {ex.Message}");
                }
            }
            else if (!product.Poster.StartsWith("avares://"))
            {
                ProductImageFilePath = product.Poster;
            }
        }
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

    // ✅ NEW: Property to store actual file path
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
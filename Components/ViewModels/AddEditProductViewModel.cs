using AHON_TRACK.Models;
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
using System.Threading.Tasks;
using AHON_TRACK.ViewModels;
using Microsoft.Identity.Client;

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
    private string[] _productSupplierItems = ["None", "San Miguel", "Optimum", "AHON Factory", "Nike"];
    private string? _selectedSupplierCategoryItem = "Tender Juicy";

    [ObservableProperty]
    private bool _isPercentageModeOn;

    private int? _productID;
    private string? _productName = string.Empty;
    private string? _productSKU = string.Empty;
    private string? _productDescription = string.Empty;
    private DateTime? _productExpiry;
    private Image? _productImage;

    private int? _price;
    private int? _discountedPrice;
    private bool? _inStock;

    private string? _productStatus;
    private string? _productCategory;
    private string? _productSupplier;
    private int? _productCurrentStock;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly ISystemService _systemService;

    public string DiscountSymbol => IsPercentageModeOn ? "%" : "₱";
    public string DiscountFormat => IsPercentageModeOn ? "N2" : "N0";

    public AddEditProductViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, ISystemService systemService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _systemService = systemService;
    }

    public AddEditProductViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _systemService = null!;
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }

    public void SetNavigationParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Context", out var context))
        {
            SetViewContext((ProductViewContext)context);
        }

        if (!parameters.TryGetValue("SelectedProduct", out var product)) return;
        var selectedProduct = (ProductStock)product;
        PopulateFormWithProductData(selectedProduct);
    }

    [RelayCommand]
    private async Task PublishProduct()
    {
        ValidateAllProperties();

        if (HasErrors)
        {
            _toastManager?.CreateToast("Validation Error")
                .WithContent("Please fix all validation errors before saving")
                .ShowWarning();
            return;
        }

        if (_systemService == null) return;

        try
        {
            var productModel = new ProductModel
            {
                ProductID = ProductID ?? 0,
                ProductName = ProductName ?? "",
                SKU = ProductSKU ?? "",
                ProductSupplier = SelectedProductSupplier,
                Description = ProductDescription,
                Price = ProductPrice ?? 0,
                DiscountedPrice = ProductDiscountedPrice,
                IsPercentageDiscount = IsPercentageModeOn,
                ProductImagePath = ProductImage?.Source?.ToString(),
                ExpiryDate = ProductExpiry,
                Status = SelectedProductStatus ?? "In Stock",
                Category = SelectedProductCategory ?? "None",
                CurrentStock = ProductCurrentStock ?? 0
            };

            bool success;
            if (ViewContext == ProductViewContext.EditProduct)
            {
                success = await _systemService.UpdateProductAsync(productModel);
            }
            else
            {
                success = await _systemService.AddProductAsync(productModel);
            }

            if (success) PublishSwitchBack();
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to save product: {ex.Message}")
                .ShowError();
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
        _toastManager.CreateToast("Publish product")
            .ShowSuccess();

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
        ProductID = product.ID;

        ProductName = product.Name;
        ProductDescription = product.Description;
        ProductPrice = product.Price;
        ProductExpiry = product.Expiry;
        IsPercentageModeOn = product.DiscountInPercentage;
        ProductDiscountedPrice = product.DiscountedPrice;
        ProductSKU = product.Sku;
        ProductCurrentStock = product.CurrentStock;

        SelectedProductSupplier = product.Supplier;
        SelectedProductStatus = product.Status;
        SelectedProductCategory = product.Category;
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
    public int? ProductPrice
    {
        get => _price;
        set => SetProperty(ref _price, value, true);
    }

    [Range(1, 15000, ErrorMessage = "Price must be between 1 and 15,000")]
    public int? ProductDiscountedPrice
    {
        get => _discountedPrice;
        set => SetProperty(ref _discountedPrice, value, true);
    }

    public int? ProductCurrentStock
    {
        get => _productCurrentStock;
        set => SetProperty(ref _productCurrentStock, value, true);
    }

    // Alias for XAML binding compatibility
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
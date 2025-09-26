using System;
using System.Collections.Generic;
using ShadUI;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AHON_TRACK.ViewModels;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;

namespace AHON_TRACK.Components.ViewModels;

[Page("add-new-product")]
public partial class AddEditProductViewModel : ViewModelBase, INavigableWithParameters
{
    [ObservableProperty] 
    private ProductViewContext _viewContext = ProductViewContext.AddProduct;
    
    [ObservableProperty]
    private string[] _productStatusItems = ["In Stock", "Out of Stock"];
    private string _selectedProductStatusItem = "In Stock";
    
    [ObservableProperty]
    private string[] _productCategoryItems = ["Drinks", "Supplements", "Apparel", "Products"];
    private string  _selectedProductCategoryItem = "Drinks";
    
    [ObservableProperty]
    private string[] _productSupplierItems = ["None", "San Miguel Foods", "Tender Juicy", "AHON Factory", "Nike"];
    private string  _selectedSupplierCategoryItem = "Tender Juicy";
    
    private string _productName = string.Empty;
    private string _productSKU = string.Empty;
    private string _productDescription = string.Empty;
    private DateTime? _productExpiry;
    private Image _productImage;

    private int? _price;
    private int? _discountedPrice;
    private bool? _inStock;

    private string _productStatus;
    private string _productCategory;
    private string _productSupplier;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    
    public AddEditProductViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }
    
    public AddEditProductViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
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
        PopulateFormWithProductdata(selectedProduct);
    }

    [RelayCommand]
    private void PublishProduct()
    {
        ValidateAllProperties();

        if (HasErrors) return;
        PublishSwitchBack();
    }

    [RelayCommand]
    private void DiscardProduct()
    {
        _dialogManager
            .CreateDialog(
                "Are you absolutely sure?",
                "This action cannot be undone. This will permanently discard your product creation.")
            .WithPrimaryButton("Continue", DiscardSwitchBack ,DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }
    
    private void PublishSwitchBack()
    {
        _toastManager.CreateToast("Publish Product")
            .WithContent("Product published successfully")
            .DismissOnClick()
            .ShowSuccess();
        _pageManager.Navigate<ProductStockViewModel>();
    }

    private void DiscardSwitchBack()
    {
        _toastManager.CreateToast("Discard Product")
            .WithContent("Product discarded successfully")
            .DismissOnClick()
            .ShowSuccess();
        _pageManager.Navigate<ProductStockViewModel>();
    }

    private void PopulateFormWithProductdata(ProductStock product)
    {
        
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

    [Required(ErrorMessage = "Product Name is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string ProductName
    {
        get => _productName;
        set => SetProperty(ref _productName, value, true);
    }
    
    [Required(ErrorMessage = "Product SKU is required")]
    [MinLength(8, ErrorMessage = "Must be at least 8-12 characters long")]
    [MaxLength(12, ErrorMessage = "Must not exceed 12 characters")]
    public string ProductSKU
    {
        get => _productSKU;
        set => SetProperty(ref _productSKU, value, true);
    }
    
    public string ProductDescription 
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
    
    [Range(5, 15000, ErrorMessage = "Price must be between 5 and 15,000")]
    public int? ProductDiscountedPrice
    {
        get => _discountedPrice;
        set => SetProperty(ref _discountedPrice, value, true);
    }
    
    public string SelectedProductStatus 
    {
        get => _selectedProductStatusItem;
        set =>  SetProperty(ref _selectedProductStatusItem, value, true);
    }

    public DateTime? ProductExpiry
    {
        get => _productExpiry;
        set => SetProperty(ref _productExpiry, value, true);
    }
    
    public string SelectedProductCategory
    {
        get => _selectedProductCategoryItem;
        set =>  SetProperty(ref _selectedProductCategoryItem, value, true);
    }
    
    public string SelectedProductSupplier
    {
        get => _selectedSupplierCategoryItem;
        set =>  SetProperty(ref _selectedSupplierCategoryItem, value, true);
    }
}

public enum ProductViewContext
{
    AddProduct,
    EditProduct
}
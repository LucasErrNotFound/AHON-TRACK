using System;
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
public partial class AddNewProductViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _productStatusItems = ["Draft", "Active", "Archived"];
    private string _selectedProductStatusItem = "Draft";
    
    [ObservableProperty]
    private string[] _productCategoryItems = ["Drinks", "Supplements", "Apparel", "Products"];
    private string  _selectedProductCategoryItem = "Drinks";
    
    private string _productName = string.Empty;
    private string _productSKU = string.Empty;
    private string _productBarcode = string.Empty;
    private string _productDescription = string.Empty;
    private Image _productImage;

    private int? _price;
    private int? _discountedPrice;
    private bool? _inStock;

    private string _productStatus;
    private string _productCategory;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    
    public AddNewProductViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }
    
    public AddNewProductViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }

    [RelayCommand]
    private void PublishProduct()
    {
        ValidateAllProperties();

        if (HasErrors) return;
        PublishSwitchBack();
    }
    
    private void PublishSwitchBack()
    {
        _toastManager.CreateToast("Publish Product")
            .WithContent("Product published successfully")
            .DismissOnClick()
            .ShowSuccess();
        _pageManager.Navigate<ManageBillingViewModel>();
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

    private void DiscardSwitchBack()
    {
        _toastManager.CreateToast("Discard Product")
            .WithContent("Product discarded successfully")
            .DismissOnClick()
            .ShowSuccess();
        _pageManager.Navigate<ManageBillingViewModel>();
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
    
    [Required(ErrorMessage = "Product Barcode is required")]
    [MinLength(12, ErrorMessage = "Must be at least 12 characters long")]
    [MaxLength(12, ErrorMessage = "Must not exceed 12 characters")]
    public string ProductBarcode
    {
        get => _productBarcode;
        set => SetProperty(ref _productBarcode, value, true);
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

    public bool? IsProductInStock
    {
        get => _inStock;
        set => SetProperty(ref _inStock, value, true);
    }
    
    public string SelectedProductStatus 
    {
        get => _selectedProductStatusItem;
        set =>  SetProperty(ref _selectedProductStatusItem, value, true);
    }
    
    public string SelectedProductCategory
    {
        get => _selectedProductCategoryItem;
        set =>  SetProperty(ref _selectedProductCategoryItem, value, true);
    }
}
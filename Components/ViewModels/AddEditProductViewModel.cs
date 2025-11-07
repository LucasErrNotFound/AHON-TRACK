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
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform;

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
    private string[] _productCategoryItems = ["Drinks", "Supplements", "Apparel", "Products", "Merchandise"];
    private string? _selectedProductCategoryItem = "Drinks";

    [ObservableProperty]
    private string[] _productSupplierItems = ["None"];
    private string? _selectedSupplierCategoryItem = "None";

    private Dictionary<string, int> _supplierNameToIdMap = new();

    [ObservableProperty]
    private byte[]? _productImageBytes;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isLoadingSuppliers;

    [ObservableProperty]
    private Image? _productImageControl;
    
    [ObservableProperty]
    private DateTime _minimumExpiryDate = DateTime.Today.AddDays(1);

    public bool CanEditExpiry => !ProductExpiry.HasValue || ProductExpiry.Value.Date > DateTime.Today;
    public DateTime TodayDate => DateTime.Today;
    private const string DEFAULT_IMAGE_PATH = "avares://AHON_TRACK/Assets/ProductStockView/DefaultProductImage.png";
    
    private bool _suppliersLoaded = false;
    private int? _productID;
    private string? _productName = string.Empty;
    private string? _batchCode = string.Empty;
    private string? _productDescription = string.Empty;
    private DateTime? _productExpiry;
    private Image? _productImage;

    private string? _productImageFilePath;

    private decimal? _price;
    private decimal? _discountedPrice;

    private string? _productStatus;
    private string? _productCategory;
    private string? _productSupplier;
    private int? _currentStock;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IProductService _productService;
    private readonly ISupplierService _supplierService;
    private bool _disposed = false;

    public AddEditProductViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, IProductService productService, ISupplierService supplierService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _productService = productService;
        _supplierService = supplierService;
        _productImageFilePath = DEFAULT_IMAGE_PATH;

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

        // ✅ Wait for control and set default image for new products
        if (ViewContext == ProductViewContext.AddProduct)
        {
            int maxAttempts = 20;
            int attempts = 0;
            while (ProductImageControl == null && attempts < maxAttempts)
            {
                await Task.Delay(50);
                attempts++;
            }

            if (ProductImageControl != null)
            {
                ProductImageControl.Source = DefaultProductBitmap;
                ProductImageControl.IsVisible = true;
                Console.WriteLine("ℹ️ Set default image for new product");
            }
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
            _suppliersLoaded = true;
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
        while (!_suppliersLoaded && IsLoadingSuppliers)
        {
            await Task.Delay(50);
        }

        await PopulateFormWithProductDataAsync(product);

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
            int? supplierIdToSave = null;
            if (!string.IsNullOrEmpty(SelectedProductSupplier) && SelectedProductSupplier != "None")
            {
                if (_supplierNameToIdMap.TryGetValue(SelectedProductSupplier, out int supplierId))
                {
                    supplierIdToSave = supplierId;
                }
            }

            int currentStock = CurrentStock.HasValue ? CurrentStock.Value : 0;
            Console.WriteLine($"📦 Current Stock Value: {currentStock}");

            string calculatedStatus;
            if (ProductExpiry.HasValue && ProductExpiry.Value.Date <= DateTime.Today)
            {
                calculatedStatus = "Expired";
            }
            else
            {
                calculatedStatus = currentStock > 0 ? "In Stock" : "Out Of Stock";
            }

            byte[]? imageBytesToSave = ProductImageBytes;
            string? imagePathToSave = _productImageFilePath;

            if (imageBytesToSave == null && string.IsNullOrEmpty(imagePathToSave))
            {
                imagePathToSave = DEFAULT_IMAGE_PATH; // Save the default path
            }

            var productModel = new ProductModel
            {
                ProductID = ProductID ?? 0,
                ProductName = ProductName ?? "",
                BatchCode = BatchCode ?? "",
                SupplierID = supplierIdToSave,
                Description = ProductDescription,
                Price = ProductPrice ?? 0,
                DiscountedPrice = ProductDiscountedPrice,
                ProductImageFilePath = imagePathToSave,
                ProductImageBytes = imageBytesToSave,
                ExpiryDate = ProductExpiry,
                Status = calculatedStatus,
                Category = SelectedProductCategory ?? "None",
                CurrentStock = currentStock
            };

            Console.WriteLine($"💾 Saving product with Stock: {productModel.CurrentStock}, Status: {productModel.Status}, Image: {imagePathToSave}");

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

    [RelayCommand]
    private async Task ChooseFile()
    {
        try
        {
            if (ProductImageControl == null)
            {
                _toastManager.CreateToast("Error")
                    .WithContent("Image control not initialized")
                    .DismissOnClick()
                    .ShowError();
                return;
            }

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

                await using var stream = await selectedFile.OpenReadAsync();
                var bitmap = new Bitmap(stream);

                ProductImageControl.Source = bitmap;
                ProductImageControl.IsVisible = true;

                stream.Position = 0;
                using var memoryStream = new System.IO.MemoryStream();
                await stream.CopyToAsync(memoryStream);
                ProductImageBytes = memoryStream.ToArray();

                Console.WriteLine($"✅ Image loaded: {ProductImageBytes.Length} bytes");
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

    private void PublishSwitchBack()
    {
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

    private async Task PopulateFormWithProductDataAsync(ProductStock product)
    {
        ProductID = product.ID;
        ProductName = product.Name;
        ProductDescription = product.Description;
        ProductPrice = product.Price;
        ProductExpiry = product.Expiry;
        ProductDiscountedPrice = product.DiscountedPrice;
        BatchCode = product.BatchCode;
        CurrentStock = product.CurrentStock;

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

        // Wait for ProductImageControl to be initialized
        int maxAttempts = 20; // 1 second max wait
        int attempts = 0;
        while (ProductImageControl == null && attempts < maxAttempts)
        {
            await Task.Delay(50);
            attempts++;
        }

        if (ProductImageControl == null)
        {
            Console.WriteLine("⚠️ ProductImageControl still null after waiting");
            return;
        }

        bool imageLoaded = await TryLoadProductImageAsync(product.Poster);

        if (!imageLoaded)
        {
            ProductImageControl.Source = DefaultProductBitmap;
            ProductImageControl.IsVisible = true;
            ProductImageBytes = null;
            Console.WriteLine("ℹ️ Using default product image");
        }
    }
    
    private async Task<bool> TryLoadProductImageAsync(string? posterData)
    {
        if (string.IsNullOrEmpty(posterData))
        {
            return false;
        }

        try
        {
            // Case 1: Base64 encoded image
            if (posterData.StartsWith("data:image/"))
            {
                string base64Data = posterData;
                if (base64Data.Contains("base64,"))
                {
                    base64Data = base64Data.Substring(base64Data.IndexOf("base64,") + 7);
                }

                ProductImageBytes = Convert.FromBase64String(base64Data);

                if (ProductImageBytes.Length > 0)
                {
                    await using var memoryStream = new System.IO.MemoryStream(ProductImageBytes);
                    var bitmap = new Bitmap(memoryStream);
                
                    ProductImageControl!.Source = bitmap;
                    ProductImageControl.IsVisible = true;
                
                    Console.WriteLine($"✅ Base64 image loaded: {ProductImageBytes.Length} bytes");
                    return true;
                }
            }
            // Case 2: File path
            else if (!posterData.StartsWith("avares://") && System.IO.File.Exists(posterData))
            {
                ProductImageFilePath = posterData;
                var fileBytes = await System.IO.File.ReadAllBytesAsync(posterData);
                ProductImageBytes = fileBytes;

                await using var memoryStream = new System.IO.MemoryStream(fileBytes);
                var bitmap = new Bitmap(memoryStream);
            
                ProductImageControl!.Source = bitmap;
                ProductImageControl.IsVisible = true;
            
                Console.WriteLine($"✅ File image loaded: {fileBytes.Length} bytes");
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to load image: {ex.Message}");
            ProductImageBytes = null;
            ProductImageFilePath = null;
        }

        return false;
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
    
    private static Bitmap? _defaultProductBitmap;
    public static Bitmap DefaultProductBitmap
    {
        get
        {
            if (_defaultProductBitmap != null) return _defaultProductBitmap;
            try
            {
                var uri = new Uri(DEFAULT_IMAGE_PATH);
                _defaultProductBitmap = new Bitmap(AssetLoader.Open(uri));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load default product image: {ex.Message}");
            }
            return _defaultProductBitmap!;
        }
    }

    public int? ProductID
    {
        get => _productID;
        set => SetProperty(ref _productID, value, true);
    }

    [Required(ErrorMessage = "Product Name is required")]
    [RegularExpression("^[a-zA-Z0-9 ]*$", ErrorMessage = "cannot contain special characters.")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string? ProductName
    {
        get => _productName;
        set => SetProperty(ref _productName, value, true);
    }

    [Required(ErrorMessage = "Batch Code is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4-25 characters long")]
    [MaxLength(25, ErrorMessage = "Must not exceed 25 characters")]
    public string? BatchCode
    {
        get => _batchCode;
        set => SetProperty(ref _batchCode, value, true);
    }

    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string? ProductDescription
    {
        get => _productDescription;
        set => SetProperty(ref _productDescription, value, true);
    }

    [Required(ErrorMessage = "Price must be set")]
    [Range(5, 5000, ErrorMessage = "Price must be between 5 and 5,000")]
    public decimal? ProductPrice
    {
        get => _price;
        set => SetProperty(ref _price, value, true);
    }

    [Range(0, 100, ErrorMessage = "Percentage must be between 0 and 100")]
    public decimal? ProductDiscountedPrice
    {
        get => _discountedPrice;
        set => SetProperty(ref _discountedPrice, value, true);
    }

    [Required(ErrorMessage = "Stock must be set")]
    [Range(0, 100, ErrorMessage = "Stock must be between 0 and 100")]
    public int? CurrentStock
    {
        get => _currentStock;
        set => SetProperty(ref _currentStock, value, true);
    }

    public DateTime? ProductExpiry
    {
        get => _productExpiry;
        set
        {
            if (_productExpiry != value)
            {
                SetProperty(ref _productExpiry, value, true);
                OnPropertyChanged(nameof(CanEditExpiry));
            }
        }
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

    protected override void DisposeManagedResources()
    {
        // Clear large blobs / image buffers
        ProductImage = null;
        ProductImageBytes = null;
        ProductImageFilePath = null;
        ProductImageControl = null;

        // Clear supplier maps/lists
        _supplierNameToIdMap?.Clear();
        ProductSupplierItems = [];

        // Mark disposed
        _disposed = true;

        base.DisposeManagedResources();
    }
}

public enum ProductViewContext
{
    AddProduct,
    EditProduct
}
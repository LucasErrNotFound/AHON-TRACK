using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Services.Interface;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

[Page("product-purchase")]
public sealed partial class ProductPurchaseViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _productFilterItems = ["Supplements", "Drinks", "Products", "Gym Packages"];

    [ObservableProperty]
    private string _selectedProductFilterItem = "Supplements";

    [ObservableProperty]
    private string[] _customerTypeFilterItems = ["All", "Walk-in", "Gym Member"];

    [ObservableProperty]
    private string _selectedCustomerTypeFilterItem = "All";

    [ObservableProperty]
    private ObservableCollection<Customer> _customerList = [];

    [ObservableProperty]
    private ObservableCollection<Product> _productList = [];

    [ObservableProperty]
    private List<Customer> _originalCustomerList = [];

    [ObservableProperty]
    private List<Customer> _currentCustomerList = [];

    [ObservableProperty]
    private List<Product> _originalProductList = [];

    [ObservableProperty]
    private List<Product> _currentProductList = [];

    [ObservableProperty]
    private ObservableCollection<Package> _packageList = [];

    [ObservableProperty]
    private List<Package> _originalPackageList = [];

    [ObservableProperty]
    private string _customerSearchStringResult = string.Empty;

    [ObservableProperty]
    private string _productSearchStringResult = string.Empty;

    [ObservableProperty]
    private bool _isSearchingCustomer;

    [ObservableProperty]
    private bool _isSearchingProduct;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private bool _selectAll;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private Customer? _selectedCustomer;

    [ObservableProperty]
    private string _customerFullName = "Customer Name";

    [ObservableProperty]
    private ObservableCollection<CartItem> _cartItems = [];

    [ObservableProperty]
    private decimal _totalPrice;

    [ObservableProperty]
    private string _formattedTotalPrice = "₱0.00";

    [ObservableProperty]
    private bool _isCartEmpty = true;

    [ObservableProperty]
    private string _emptyCartMessage = "Customer Name's cart is currently empty";

    [ObservableProperty]
    private bool _isCashSelected;

    [ObservableProperty]
    private bool _isGCashSelected;

    [ObservableProperty]
    private bool _isMayaSelected;

    [ObservableProperty]
    private string _currentTransactionId = "GM-2025-001234"; // Initial/default ID

    private int _lastIdNumber = 1234; // Track the numeric part

    private readonly Dictionary<string, Bitmap> _imageCache = new();

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IProductPurchaseService _productPurchaseService;

    public ProductPurchaseViewModel(
        DialogManager dialogManager,
        ToastManager toastManager,
        PageManager pageManager,
        IProductPurchaseService productPurchaseService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;

        // += OnPackagesChanged;

        LoadCustomerList();
        LoadProductOptions();
        LoadPackageOptions();
        _productPurchaseService = productPurchaseService;
    }

    public ProductPurchaseViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _productPurchaseService = null!;

        LoadCustomerList();
        LoadProductOptions();
        LoadPackageOptions();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        _ = LoadCustomerListFromDatabaseAsync();
        _ = LoadProductsFromDatabaseAsync();
        _ = LoadPackagesFromDatabaseAsync();
        if (_productPurchaseService == null)
        {
            LoadCustomerList();
            LoadProductOptions();
            LoadPackageOptions();
        }
        IsInitialized = true;
    }

    private void LoadCustomerList()
    {
        var customers = GetCustomerData();

        OriginalCustomerList = customers;
        CurrentCustomerList = customers.ToList();

        CustomerList.Clear();
        foreach (var customer in CurrentCustomerList)
        {
            customer.PropertyChanged += OnCustomerPropertyChanged;
            CustomerList.Add(customer);
        }
        ApplyCustomerFilter();
        UpdateCustomerCounts();
    }

    private async Task LoadCustomerListFromDatabaseAsync()
    {
        var customers = await _productPurchaseService.GetAllCustomersAsync();

        OriginalCustomerList = customers.Select(c => new Customer
        {
            ID = c.CustomerID,
            FirstName = c.FirstName,
            LastName = c.LastName,
            CustomerType = c.CustomerType
        }).ToList();

        CurrentCustomerList = OriginalCustomerList.ToList();

        CustomerList.Clear();
        foreach (var customer in CurrentCustomerList)
        {
            customer.PropertyChanged += OnCustomerPropertyChanged;
            CustomerList.Add(customer);
        }

        // Apply the filter based on the currently selected filter value
        ApplyCustomerFilter();
        UpdateCustomerCounts();

        // 🔥 Trigger UI update if filter combo changes after loading
        OnSelectedCustomerTypeFilterItemChanged(SelectedCustomerTypeFilterItem);
    }

    private Product ConvertToProduct(SellingModel selling)
    {
        return new Product
        {
            Title = selling.Title,
            Description = selling.Description,
            Category = selling.Category,
            Price = (int)selling.Price,
            StockCount = selling.Stock,
            Poster = selling.ImagePath != null ? new Bitmap(new MemoryStream(selling.ImagePath)) : null
        };
    }

    private Package ConvertToPackage(SellingModel selling)
    {
        return new Package
        {
            Title = selling.Title,
            Description = selling.Description,
            Price = (int)selling.Price,
            IsAddedToCart = false
        };
    }

    private async Task LoadProductsFromDatabaseAsync()
    {
        var products = await _productPurchaseService.GetAllProductsAsync();
        var productModels = products.Select(ConvertToProduct).ToList();

        // 🔹 Keep master copy
        OriginalProductList = productModels;
        CurrentProductList = productModels.ToList();

        // 🔹 Update displayed list
        ProductList.Clear();
        foreach (var product in productModels)
            ProductList.Add(product);
        ApplyProductFilter();
    }

    private async Task LoadPackagesFromDatabaseAsync()
    {
        var packages = await _productPurchaseService.GetAllGymPackagesAsync();
        var packageModels = packages.Select(ConvertToPackage).ToList();

        // 🔹 Keep master copy
        OriginalPackageList = packageModels;

        // 🔹 Update displayed list
        PackageList.Clear();
        foreach (var package in packageModels)
            PackageList.Add(package);
        ApplyProductFilter();
    }

    private void LoadProductOptions()
    {
        // Prevent overwriting DB-loaded list
        if (OriginalProductList != null && OriginalProductList.Count >= 0)
            return;

        var products = GetProductData();
        OriginalProductList = products;
        CurrentProductList = products.ToList();

        ProductList.Clear();
        foreach (var product in CurrentProductList)
            ProductList.Add(product);

        ApplyProductFilter();
    }

    private void LoadPackageOptions()
    {
        if (OriginalPackageList != null && OriginalPackageList.Count >= 0)
            return;

        ApplyProductFilter();
    }

    private List<Customer> GetCustomerData()
    {
        return
        [
            new Customer
                {
                    ID = 1001,
                    FirstName = "Robert Xyz",
                    LastName = "Lucas",
                    CustomerType = "Gym Member"
                },
                new Customer
                {
                    ID = 1002,
                    FirstName = "Sianrey",
                    LastName = "Flora",
                    CustomerType = "Gym Member"
                },
                new Customer
                {
                    ID = 1003,
                    FirstName = "Mardie",
                    LastName = "Dela Cruz",
                    CustomerType = "Walk-in"
                },
                new Customer
                {
                    ID = 1004,
                    FirstName = "Mark",
                    LastName = "Dela Cruz",
                    CustomerType = "Gym Member"
                }
        ];
    }

    private List<Product> GetProductData()
    {
        return
        [
            new Product
                {
                    Title = "Gold Standard Whey Protein",
                    Description = "5lbs Premium Whey Protein",
                    Category = "Supplements",
                    Price = 2500,
                    StockCount = 15,
                    Poster = GetCachedImage("protein-powder-display.png")
                },
                new Product
                {
                    Title = "Creatine XPLODE Powder",
                    Description = "1.1lbs Creatine Monohydrate",
                    Category = "Supplements",
                    Price = 1050,
                    StockCount = 8,
                    Poster = GetCachedImage("creatine-display.png")
                },
                new Product
                {
                    Title = "Insane Labz PSYCHOTIC",
                    Description = "7.6oz PreWorkout Peaches & Cream",
                    Category = "Supplements",
                    Price = 900,
                    StockCount = 7,
                    Poster = GetCachedImage("preworkout-display.png")
                },
                new Product
                {
                    Title = "Cobra Energy Drink",
                    Description = "Yellow Blast Flavor",
                    Category = "Drinks",
                    Price = 35,
                    StockCount = 5,
                    Poster = GetCachedImage("cobra-yellow-drink-display.png")
                },
                new Product
                {
                    Title = "Cobra Energy Drink",
                    Description = "Yellow Blast Flavor",
                    Category = "Drinks",
                    Price = 35,
                    StockCount = 3,
                    Poster = GetCachedImage("cobra-yellow-drink-display.png")
                }
        ];
    }

    private Bitmap GetCachedImage(string imageName)
    {
        if (_imageCache.TryGetValue(imageName, out Bitmap? value)) return value;
        try
        {
            var uri = new Uri($"avares://AHON_TRACK/Assets/ProductPurchaseView/{imageName}");
            _imageCache[imageName] = new Bitmap(AssetLoader.Open(uri));
        }
        catch
        {
            // If image fails to load, return null (will fall back to default in CartItem)
            return CartItem.DefaultProductBitmap;
        }
        return _imageCache[imageName];
    }

    private void ApplyCustomerFilter()
    {
        if (OriginalCustomerList is null || OriginalCustomerList.Count == 0)
            return;

        var filteredList = SelectedCustomerTypeFilterItem switch
        {
            "Walk-in" => OriginalCustomerList
                .Where(c => c.CustomerType.Equals("Walk-in", StringComparison.OrdinalIgnoreCase))
                .ToList(),

            "Gym Member" => OriginalCustomerList
                .Where(c => c.CustomerType.Equals("Gym Member", StringComparison.OrdinalIgnoreCase))
                .ToList(),

            _ => OriginalCustomerList.ToList() // "All"
        };

        CurrentCustomerList = filteredList;

        CustomerList.Clear();
        foreach (var customer in filteredList)
        {
            customer.PropertyChanged += OnCustomerPropertyChanged;
            CustomerList.Add(customer);
        }

        UpdateCustomerCounts();
    }

    private void ApplyProductFilter()
    {
        // Prevent null errors
        if (OriginalProductList == null || _originalPackageList == null)
            return;

        ProductList.Clear();
        PackageList.Clear();

        // 🔹 If user selects "Gym Packages"
        if (SelectedProductFilterItem == "Gym Packages")
        {
            foreach (var package in _originalPackageList)
                PackageList.Add(package);

            ProductSearchStringResult = string.Empty;
            return;
        }

        // 🔹 Otherwise, filter products
        IEnumerable<Product> filteredList = OriginalProductList;

        if (!string.IsNullOrWhiteSpace(SelectedProductFilterItem) && SelectedProductFilterItem != "All")
        {
            filteredList = filteredList
                .Where(p => p.Category.Equals(SelectedProductFilterItem, StringComparison.OrdinalIgnoreCase));
        }

        // 🔹 Optional: search filter
        if (!string.IsNullOrWhiteSpace(ProductSearchStringResult))
        {
            filteredList = filteredList
                .Where(p =>
                    (!string.IsNullOrEmpty(p.Title) && p.Title.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(p.Description) && p.Description.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase))
                );
        }

        // 🔹 Apply result to view
        foreach (var product in filteredList)
            ProductList.Add(product);

        ProductSearchStringResult = string.Empty;
    }

    [RelayCommand]
    private async Task SearchCustomers()
    {
        if (string.IsNullOrWhiteSpace(CustomerSearchStringResult))
        {
            CustomerList.Clear();
            foreach (var customer in CurrentCustomerList)
            {
                customer.PropertyChanged += OnCustomerPropertyChanged;
                CustomerList.Add(customer);
            }
            UpdateCustomerCounts();
            return;
        }

        IsSearchingCustomer = true;

        try
        {
            await Task.Delay(500);

            var filteredCustomers = CurrentCustomerList.Where(customer =>
                customer.FirstName.Contains(CustomerSearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                customer.LastName.Contains(CustomerSearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                customer.CustomerType.Contains(CustomerSearchStringResult, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            CustomerList.Clear();
            foreach (var customers in filteredCustomers)
            {
                customers.PropertyChanged += OnCustomerPropertyChanged;
                CustomerList.Add(customers);
            }
            UpdateCustomerCounts();
        }
        finally
        {
            IsSearchingCustomer = false;
        }
    }

    [RelayCommand]
    private async Task SearchProducts()
    {
        if (string.IsNullOrWhiteSpace(ProductSearchStringResult))
        {
            ApplyProductFilter();
            return;
        }

        IsSearchingProduct = true;

        try
        {
            await Task.Delay(300);

            ProductList.Clear();
            PackageList.Clear();

            if (SelectedProductFilterItem == "Gym Packages")
            {
                var filteredPackages = OriginalPackageList.Where(package =>
                    package.Title.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                    package.Description.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                foreach (var package in filteredPackages)
                {
                    PackageList.Add(package);
                }
            }
            else
            {
                var filteredProducts = CurrentProductList.Where(product =>
                    product.Title.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                    product.Description.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                foreach (var product in filteredProducts)
                {
                    ProductList.Add(product);
                }
            }
        }
        finally
        {
            IsSearchingProduct = false;
        }
    }

    [RelayCommand]
    private void AddToCart(object item)
    {
        CartItem? cartItem = null;

        if (item is Product product)
        {
            cartItem = new CartItem
            {
                Id = Guid.NewGuid(),
                ItemType = CartItemType.Product,
                Title = product.Title,
                Description = product.Description,
                Price = product.Price,
                MaxQuantity = product.StockCount,
                Quantity = 1,
                Poster = product.Poster,
                SourceProduct = product
            };
            product.IsAddedToCart = true;
        }
        else if (item is Package package)
        {
            cartItem = new CartItem
            {
                Id = Guid.NewGuid(),
                ItemType = CartItemType.Package,
                Title = package.Title,
                Description = package.Description,
                Price = package.Price,
                MaxQuantity = 999, // Packages typically don't have stock limits
                Quantity = 1,
                Poster = null, // Packages don't have posters in your current setup
                SourcePackage = package
            };
            package.IsAddedToCart = true;
        }

        if (cartItem != null)
        {
            cartItem.PropertyChanged += OnCartItemPropertyChanged;
            CartItems.Add(cartItem);
            UpdateCartTotals();
            UpdateCartEmptyState();
        }
    }

    [RelayCommand]
    private void RemoveFromCart(CartItem cartItem)
    {
        if (cartItem.SourceProduct != null)
        {
            cartItem.SourceProduct.IsAddedToCart = false;
        }

        if (cartItem.SourcePackage != null)
        {
            cartItem.SourcePackage.IsAddedToCart = false;
        }

        cartItem.PropertyChanged -= OnCartItemPropertyChanged;
        CartItems.Remove(cartItem);
        UpdateCartTotals();
        UpdateCartEmptyState();
    }

    [RelayCommand]
    private void Payment()
    {
        var customerName = SelectedCustomer != null
            ? $"{SelectedCustomer.FirstName} {SelectedCustomer.LastName}"
            : "No customer selected";

        var paymentMethod = IsCashSelected ? "Cash" :
            IsGCashSelected ? "GCash" :
            IsMayaSelected ? "Maya" :
            "No payment method selected";

        var cartItemsList = string.Join(", ", CartItems.Select(item =>
            $"{item.Title} (Qty: {item.Quantity})"));

        /* Alternative: More detailed cart info
        var detailedCartInfo = string.Join("\n", CartItems.Select(item => 
            $"• {item.Title} - Qty: {item.Quantity} - {item.FormattedTotalPrice}"));
        */

        var toastContent = $"Customer: {customerName}\n" +
                           $"Payment Method: {paymentMethod}\n" +
                           $"Total: {FormattedTotalPrice}\n" +
                           $"Items:\n{cartItemsList}"; // or detailedCartInfo

        _toastManager.CreateToast("Gym Purchase")
            .WithContent(toastContent)
            .DismissOnClick()
            .ShowSuccess();

        CurrentTransactionId = GenerateNewTransactionId();
        ClearCart();
    }

    private void ClearCart()
    {
        foreach (var cartItem in CartItems.ToList())
        {
            if (cartItem.SourceProduct != null)
            {
                cartItem.SourceProduct.IsAddedToCart = false;
            }

            if (cartItem.SourcePackage != null)
            {
                cartItem.SourcePackage.IsAddedToCart = false;
            }

            cartItem.PropertyChanged -= OnCartItemPropertyChanged;
        }

        CartItems.Clear();
        UpdateCartTotals();
        UpdateCartEmptyState();

        IsCashSelected = false;
        IsGCashSelected = false;
        IsMayaSelected = false;
        SelectedCustomer = null;
    }

    private void UpdateCustomerCounts()
    {
        SelectedCount = CustomerList.Count(x => x.IsSelected);
        TotalCount = CustomerList.Count;

        SelectAll = CustomerList.Count > 0 && CustomerList.All(x => x.IsSelected);
    }

    partial void OnCustomerSearchStringResultChanged(string value)
    {
        SearchCustomersCommand.Execute(null);
    }

    partial void OnProductSearchStringResultChanged(string value)
    {
        SearchProductsCommand.Execute(null);
    }

    private void OnCustomerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Customer.IsSelected))
        {
            UpdateCustomerCounts();
        }
    }

    partial void OnSelectedCustomerTypeFilterItemChanged(string value)
    {
        ApplyCustomerFilter();
    }

    partial void OnSelectedProductFilterItemChanged(string value)
    {
        ApplyProductFilter();
    }

    partial void OnSelectedCustomerChanged(Customer? value)
    {
        CustomerFullName = value != null ? $"{value.FirstName} {value.LastName}" : "Customer Name";
        UpdateCartEmptyState(); // Update empty cart message when customer changes
        OnPropertyChanged(nameof(IsPaymentPossible));
    }

    private void OnPackagesChanged()
    {
        LoadPackageOptions();

        if (SelectedProductFilterItem == "Gym Packages")
        {
            ApplyProductFilter();
        }
    }

    private void OnCartItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CartItem.Quantity))
        {
            UpdateCartTotals();
        }
    }

    private void UpdateCartTotals()
    {
        TotalPrice = CartItems.Sum(item => item.TotalPrice);
        FormattedTotalPrice = $"₱{TotalPrice:N2}";
    }

    private void UpdateCartEmptyState()
    {
        IsCartEmpty = !CartItems.Any();
        EmptyCartMessage = SelectedCustomer != null
            ? $"{SelectedCustomer.FirstName} {SelectedCustomer.LastName}'s cart is currently empty"
            : "Customer Name's cart is currently empty";
        OnPropertyChanged(nameof(IsPaymentPossible));
    }

    public void Dispose()
    {
        //if (_packageService != null)
        //  _packageService.PackagesChanged -= OnPackagesChanged;
    }

    private string GenerateNewTransactionId()
    {
        _lastIdNumber++;
        var year = DateTime.Today.Year;
        return $"GM-{year}-{_lastIdNumber:D6}";
    }

    partial void OnIsCashSelectedChanged(bool value)
    {
        if (value)
        {
            IsGCashSelected = false;
            IsMayaSelected = false;
        }
        OnPropertyChanged(nameof(IsPaymentPossible));
    }

    partial void OnIsGCashSelectedChanged(bool value)
    {
        if (value)
        {
            IsCashSelected = false;
            IsMayaSelected = false;
        }
        OnPropertyChanged(nameof(IsPaymentPossible));
    }

    partial void OnIsMayaSelectedChanged(bool value)
    {
        if (value)
        {
            IsCashSelected = false;
            IsGCashSelected = false;
        }
        OnPropertyChanged(nameof(IsPaymentPossible));
    }

    public bool IsPaymentPossible => SelectedCustomer != null && !IsCartEmpty && (IsCashSelected || IsGCashSelected || IsMayaSelected);
}

public partial class Customer : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int? _iD;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string _customerType = string.Empty;
}

public partial class Product : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private int _price;

    [ObservableProperty]
    private int _stockCount;

    [ObservableProperty]
    private Bitmap? _poster;

    [ObservableProperty]
    private bool _isAddedToCart;

    public string FormattedPrice => $"₱{Price:N2}";
    public string FormattedStockCount => $"{StockCount} Left";

    public IBrush StockForeground => StockCount switch
    {
        < 5 => new SolidColorBrush(Color.FromRgb(239, 68, 68)),   // Red-500 (Critical)
        < 10 => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber-500 (Warning)
        _ => new SolidColorBrush(Color.FromRgb(34, 197, 94))      // Green-500 (Good)
    };

    public IBrush StockBackground => StockCount switch
    {
        < 5 => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),   // Red-500 with alpha
        < 10 => new SolidColorBrush(Color.FromArgb(25, 245, 158, 11)), // Amber-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94))      // Green-500 with alpha
    };

    public IBrush StockBorder => StockCount switch
    {
        < 5 => new SolidColorBrush(Color.FromRgb(239, 68, 68)),   // Red-500 (Critical)
        < 10 => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber-500 (Warning)
        _ => new SolidColorBrush(Color.FromRgb(34, 197, 94))      // Green-500 (Good)
    };

    partial void OnStockCountChanged(int value)
    {
        OnPropertyChanged(nameof(StockForeground));
        OnPropertyChanged(nameof(StockBackground));
    }
}

public partial class CartItem : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private CartItemType _itemType;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private decimal _price;

    [ObservableProperty]
    private int _quantity = 1;

    [ObservableProperty]
    private int _maxQuantity = 1;

    [ObservableProperty]
    private Bitmap? _poster;

    public Product? SourceProduct { get; set; }
    public Package? SourcePackage { get; set; }

    public decimal TotalPrice => Price * Quantity;
    public string FormattedPrice => $"₱{Price:N2}";
    public string FormattedTotalPrice => $"₱{TotalPrice:N2}";

    private static Bitmap? _defaultProductBitmap;

    public static Bitmap DefaultProductBitmap
    {
        get
        {
            if (_defaultProductBitmap != null) return _defaultProductBitmap;
            try
            {
                var uri = new Uri("avares://AHON_TRACK/Assets/ProductPurchaseView/DefaultPurchaseIcon.png");
                _defaultProductBitmap = new Bitmap(AssetLoader.Open(uri));
            }
            catch
            {
                // If default image also fails, _defaultProductBitmap remains null
            }
            return _defaultProductBitmap;
        }
    }

    public Bitmap DisplayPoster => Poster ?? DefaultProductBitmap;

    partial void OnPosterChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(DisplayPoster));
    }

    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(TotalPrice));
        OnPropertyChanged(nameof(FormattedTotalPrice));
    }

    partial void OnPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(TotalPrice));
        OnPropertyChanged(nameof(FormattedPrice));
        OnPropertyChanged(nameof(FormattedTotalPrice));
    }
}

public enum CartItemType
{
    Product,
    Package
}
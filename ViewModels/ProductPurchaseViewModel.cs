using AHON_TRACK.Converters;
using AHON_TRACK.Models;
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
using System.Threading.Tasks;
using AHON_TRACK.Services.Events;

namespace AHON_TRACK.ViewModels;

[Page("product-purchase")]
public sealed partial class ProductPurchaseViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _productFilterItems = [CategoryConstants.Supplements, CategoryConstants.Drinks, CategoryConstants.Equipment, CategoryConstants.GymPackage];

    [ObservableProperty]
    private string _selectedProductFilterItem = CategoryConstants.Supplements;

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
    private string _currentTransactionId = "GM-2025-001234";

    private int _lastIdNumber = 1234;

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
        _productPurchaseService = productPurchaseService;
        SubscribeToEvent();
        _ = LoadCustomerListFromDatabaseAsync();
        _ = LoadProductsFromDatabaseAsync();
        _ = LoadPackagesFromDatabaseAsync();
    }

    public ProductPurchaseViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _productPurchaseService = null!;
        SubscribeToEvent();
        _ = LoadCustomerListFromDatabaseAsync();
        _ = LoadProductsFromDatabaseAsync();
        _ = LoadPackagesFromDatabaseAsync();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;

        SubscribeToEvent();
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

    private void SubscribeToEvent()
    {
        var eventService = DashboardEventService.Instance;

        eventService.CheckinAdded += OnCheckInOutDataChanged;
        eventService.CheckoutAdded += OnCheckInOutDataChanged;
        eventService.ProductAdded += OnProductDataChanged;
        eventService.ProductUpdated += OnProductDataChanged;
        eventService.ProductDeleted += OnProductDataChanged;
        eventService.PackageAdded += OnPackageDataChanged;
        eventService.PackageUpdated += OnPackageDataChanged;
        eventService.PackageDeleted += OnPackageDataChanged;
    }

    private async void OnCheckInOutDataChanged(object? sender, EventArgs e)
    {
        await LoadCustomerListFromDatabaseAsync();
    }

    private async void OnProductDataChanged(object? sender, EventArgs e)
    {
        await LoadProductsFromDatabaseAsync();
    }

    private async void OnPackageDataChanged(object? sender, EventArgs e)
    {
        await LoadPackagesFromDatabaseAsync();
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
        try
        {
            var customers = await _productPurchaseService.GetAllCustomersAsync();

            OriginalCustomerList = customers.Select(c => new Customer
            {
                ID = c.CustomerID,
                FirstName = c.FirstName ?? string.Empty,
                LastName = c.LastName ?? string.Empty,
                CustomerType = c.CustomerType ?? string.Empty
            }).ToList();

            CurrentCustomerList = OriginalCustomerList.ToList();

            CustomerList.Clear();
            foreach (var customer in CurrentCustomerList)
            {
                customer.PropertyChanged += OnCustomerPropertyChanged;
                CustomerList.Add(customer);
            }

            ApplyCustomerFilter();
            UpdateCustomerCounts();
            OnSelectedCustomerTypeFilterItemChanged(SelectedCustomerTypeFilterItem);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Load Error")
                .WithContent($"Failed to load customers: {ex.Message}")
                .ShowError();
        }
    }

    private Product ConvertToProduct(SellingModel selling)
    {
        return new Product
        {
            ID = selling.SellingID,
            Title = selling.Title ?? string.Empty,
            Description = selling.Description ?? string.Empty,
            Category = selling.Category ?? string.Empty,
            Price = (int)selling.Price,
            StockCount = selling.Stock,
            Poster = selling.ImagePath != null ? new Bitmap(new MemoryStream(selling.ImagePath)) : null
        };
    }

    private Package ConvertToPackage(SellingModel selling)
    {
        return new Package
        {
            PackageId = selling.SellingID,
            Title = selling.Title ?? string.Empty,
            Description = selling.Description ?? string.Empty,
            Price = (int)selling.Price,
            IsAddedToCart = false,
            Features = selling.Features?.Split('|').ToList() ?? new List<string>()
        };
    }

    private async Task LoadProductsFromDatabaseAsync()
    {
        try
        {
            var products = await _productPurchaseService.GetAllProductsAsync();
            var productModels = products.Select(ConvertToProduct).ToList();

            OriginalProductList = productModels;
            CurrentProductList = productModels.ToList();

            ProductList.Clear();
            foreach (var product in productModels)
                ProductList.Add(product);

            ApplyProductFilter();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Load Error")
                .WithContent($"Failed to load products: {ex.Message}")
                .ShowError();
        }
    }

    private async Task LoadPackagesFromDatabaseAsync()
    {
        try
        {
            var packages = await _productPurchaseService.GetAllGymPackagesAsync();
            var packageModels = packages.Select(ConvertToPackage).ToList();
            OriginalPackageList = packageModels;
            PackageList.Clear();
            foreach (var package in packageModels)
                PackageList.Add(package);
            ApplyProductFilter();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Load Error")
                .WithContent($"Failed to load packages: {ex.Message}")
                .ShowError();
        }
    }

    private void LoadProductOptions()
    {
        if (OriginalProductList != null && OriginalProductList.Count > 0)
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
        if (OriginalPackageList != null && OriginalPackageList.Count > 0)
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
        if (OriginalCustomerList == null || OriginalCustomerList.Count == 0)
            return;

        var filteredList = SelectedCustomerTypeFilterItem switch
        {
            "Walk-in" => OriginalCustomerList
                .Where(c => c.CustomerType.Equals(CategoryConstants.WalkIn, StringComparison.OrdinalIgnoreCase))
                .ToList(),

            "Gym Member" => OriginalCustomerList
                .Where(c => c.CustomerType.Equals(CategoryConstants.Member, StringComparison.OrdinalIgnoreCase))
                .ToList(),

            _ => OriginalCustomerList.ToList()
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
        if (OriginalProductList == null || OriginalPackageList == null)
            return;

        ProductList.Clear();
        PackageList.Clear();

        if (SelectedProductFilterItem == CategoryConstants.GymPackage)
        {
            foreach (var package in OriginalPackageList)
                PackageList.Add(package);

            ProductSearchStringResult = string.Empty;
            return;
        }

        IEnumerable<Product> filteredList = OriginalProductList;

        if (!string.IsNullOrWhiteSpace(SelectedProductFilterItem))
        {
            filteredList = filteredList.Where(p =>
                p.Category.Equals(SelectedProductFilterItem, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(ProductSearchStringResult))
        {
            filteredList = filteredList.Where(p =>
                (!string.IsNullOrEmpty(p.Title) && p.Title.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(p.Description) && p.Description.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase))
            );
        }

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

            if (SelectedProductFilterItem == CategoryConstants.GymPackage)
            {
                var filteredPackages = OriginalPackageList.Where(package =>
                    package.Title.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                    package.Description.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                foreach (var package in filteredPackages)
                    PackageList.Add(package);
            }
            else
            {
                var filteredProducts = CurrentProductList.Where(product =>
                    product.Title.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                    product.Description.Contains(ProductSearchStringResult, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                foreach (var product in filteredProducts)
                    ProductList.Add(product);
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
        if (item == null) return;

        CartItem? cartItem = null;

        if (item is Product product)
        {
            // Check if already in cart
            var existingItem = CartItems.FirstOrDefault(c =>
                c.ItemType == CartItemType.Product && c.SellingID == product.ID);

            if (existingItem != null)
            {
                _toastManager.CreateToast("Already in Cart")
                    .WithContent($"{product.Title} is already in the cart.")
                    .ShowWarning();
                return;
            }

            cartItem = new CartItem
            {
                Id = Guid.NewGuid(),
                SellingID = product.ID,
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
            var existingItem = CartItems.FirstOrDefault(c =>
                c.ItemType == CartItemType.Package && c.SellingID == package.PackageId);

            if (existingItem != null)
            {
                _toastManager.CreateToast("Already in Cart")
                    .WithContent($"{package.Title} is already in the cart.")
                    .ShowWarning();
                return;
            }

            cartItem = new CartItem
            {
                Id = Guid.NewGuid(),
                SellingID = package.PackageId,
                ItemType = CartItemType.Package,
                Title = package.Title,
                Description = package.Description,
                Price = package.Price,
                MaxQuantity = 999,
                Quantity = 1,
                Poster = null,
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
        if (cartItem == null) return;

        if (cartItem.SourceProduct != null)
            cartItem.SourceProduct.IsAddedToCart = false;

        if (cartItem.SourcePackage != null)
            cartItem.SourcePackage.IsAddedToCart = false;

        cartItem.PropertyChanged -= OnCartItemPropertyChanged;
        CartItems.Remove(cartItem);
        UpdateCartTotals();
        UpdateCartEmptyState();
    }

    //

    [RelayCommand]
    private async Task PaymentAsync()
    {
        // Validation
        if (SelectedCustomer == null)
        {
            _toastManager.CreateToast("No Customer")
                .WithContent("Please select a customer first.")
                .ShowWarning();
            return;
        }

        if (CartItems.Count == 0)
        {
            _toastManager.CreateToast("Empty Cart")
                .WithContent("Add products to the cart before checking out.")
                .ShowWarning();
            return;
        }

        if (!IsCashSelected && !IsGCashSelected && !IsMayaSelected)
        {
            _toastManager.CreateToast("Select Payment Method")
                .WithContent("Please select Cash, GCash, or Maya before proceeding.")
                .ShowWarning();
            return;
        }

        try
        {
            // Determine payment method
            string paymentMethod = IsCashSelected ? CategoryConstants.Cash :
                                  IsGCashSelected ? CategoryConstants.GCash :
                                  CategoryConstants.Maya;

            // Convert cart items to SellingModel list
            var sellingItems = CartItems.Select(item => new SellingModel
            {
                SellingID = item.SellingID,
                Title = item.Title,
                Category = item.ItemType == CartItemType.Product ? CategoryConstants.Product : CategoryConstants.GymPackage,
                Price = item.Price,
                Quantity = item.Quantity
            }).ToList();

            // Create customer model
            var customerModel = new CustomerModel
            {
                CustomerID = SelectedCustomer.ID ?? 0,
                FirstName = SelectedCustomer.FirstName,
                LastName = SelectedCustomer.LastName,
                CustomerType = SelectedCustomer.CustomerType
            };

            // Get logged-in employee ID
            int employeeId = CurrentUserModel.UserId ?? 0;

            // Process payment
            bool success = await _productPurchaseService.ProcessPaymentAsync(
                sellingItems,
                customerModel,
                employeeId,
                paymentMethod
            );

            if (success)
            {
                // I remove this toast for complexity of discount from the packages if available to lessen confussion.

                /* _toastManager.CreateToast("Purchase Complete")
                     .WithContent($"Purchase successful for {SelectedCustomer.FirstName} {SelectedCustomer.LastName}!\nPayment: {paymentMethod}")
                     .ShowSuccess(); */

                ClearCart();
                CurrentTransactionId = GenerateNewTransactionId();

                // Reload products to update stock counts
                await LoadProductsFromDatabaseAsync();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error")
                .WithContent($"Failed to process payment: {ex.Message}")
                .ShowError();
        }
    }

    //

    private void ClearCart()
    {
        foreach (var cartItem in CartItems.ToList())
        {
            if (cartItem.SourceProduct != null)
                cartItem.SourceProduct.IsAddedToCart = false;

            if (cartItem.SourcePackage != null)
                cartItem.SourcePackage.IsAddedToCart = false;

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
        UpdateCartEmptyState();
        OnPropertyChanged(nameof(IsPaymentPossible));
    }

    private void OnPackagesChanged()
    {
        _ = LoadPackagesFromDatabaseAsync();

        if (SelectedProductFilterItem == "Gym Packages")
        {
            ApplyProductFilter();
        }
    }

    private void OnCartItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CartItem.Quantity))
            UpdateCartTotals();
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
        foreach (var customer in CustomerList)
            customer.PropertyChanged -= OnCustomerPropertyChanged;

        foreach (var cartItem in CartItems)
            cartItem.PropertyChanged -= OnCartItemPropertyChanged;
        DashboardEventService.Instance.PackageAdded -= (s, e) => OnPackagesChanged();
        DashboardEventService.Instance.PackageUpdated -= (s, e) => OnPackagesChanged();
        DashboardEventService.Instance.PackageDeleted -= (s, e) => OnPackagesChanged();
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

    public bool IsPaymentPossible =>
        SelectedCustomer != null &&
        !IsCartEmpty &&
        (IsCashSelected || IsGCashSelected || IsMayaSelected);
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

    public string FullName => $"{FirstName} {LastName}";
}

public partial class Product : ObservableObject
{
    [ObservableProperty]
    private int _iD;

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
        < 5 => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        < 10 => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        _ => new SolidColorBrush(Color.FromRgb(34, 197, 94))
    };

    public IBrush StockBackground => StockCount switch
    {
        < 5 => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),
        < 10 => new SolidColorBrush(Color.FromArgb(25, 245, 158, 11)),
        _ => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94))
    };

    public IBrush StockBorder => StockCount switch
    {
        < 5 => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        < 10 => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        _ => new SolidColorBrush(Color.FromRgb(34, 197, 94))
    };

    partial void OnStockCountChanged(int value)
    {
        OnPropertyChanged(nameof(StockForeground));
        OnPropertyChanged(nameof(StockBackground));
        OnPropertyChanged(nameof(StockBorder));
        OnPropertyChanged(nameof(FormattedStockCount));
    }

    partial void OnPriceChanged(int value)
    {
        OnPropertyChanged(nameof(FormattedPrice));
    }
}

public partial class CartItem : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private int _sellingID;

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
                // Default bitmap remains null if image fails to load
            }
            return DefaultProductBitmap;
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
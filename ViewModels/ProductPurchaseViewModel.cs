using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HotAvalonia;
using ShadUI;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    
    private readonly Dictionary<string, Bitmap> _imageCache = new();
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IPackageService _packageService;

    public ProductPurchaseViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager, 
        PageManager pageManager, IPackageService packageService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _packageService = packageService;

        _packageService.PackagesChanged += OnPackagesChanged;

        LoadCustomerList();
        LoadProductOptions();
        LoadPackageOptions();
    }

    public ProductPurchaseViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _packageService = new PackageService();

        LoadCustomerList();
        LoadProductOptions();
        LoadPackageOptions();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        LoadCustomerList();
        LoadProductOptions();
        LoadPackageOptions();
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

    private void LoadProductOptions()
    {
        var products = GetProductData();
        OriginalProductList = products;
        CurrentProductList = products.ToList();
        
        ProductList.Clear();
        foreach (var product in CurrentProductList)
        {
            ProductList.Add(product);
        }
        ApplyProductFilter();
    }
    
    private void LoadPackageOptions()
    {
        var packages = _packageService.GetPackages();
        OriginalPackageList = packages;
        ApplyProductFilter(); // This will handle package filtering
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
            },
            new Customer
            {
                ID = 1005,
                FirstName = "John Carlo",
                LastName = "Casidor",
                CustomerType = "Walk-in"
            },
            new Customer
            {
                ID = 1006,
                FirstName = "Marc",
                LastName = "Torres",
                CustomerType = "Gym Member"
            },
            new Customer
            {
                ID = 1007,
                FirstName = "John Maverick",
                LastName = "Lim",
                CustomerType = "Gym Member"
            },
            new Customer
            {
                ID = 1008,
                FirstName = "Jav",
                LastName = "Agustin",
                CustomerType = "Walk-in"
            },
            new Customer
            {
                ID = 1009,
                FirstName = "Adriel",
                LastName = "Del Rosario",
                CustomerType = "Walk-in"
            },
            new Customer
            {
                ID = 1010,
                FirstName = "Uriel Simon",
                LastName = "Rivera",
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
        if (!_imageCache.ContainsKey(imageName))
        {
            var uri = new Uri($"avares://AHON_TRACK/Assets/ProductPurchaseView/{imageName}");
            _imageCache[imageName] = new Bitmap(AssetLoader.Open(uri));
        }
        return _imageCache[imageName];
    }
    
    private void ApplyCustomerFilter()
    {
        if (OriginalCustomerList == null || OriginalCustomerList.Count == 0) return;

        List<Customer> filteredList;

        if (SelectedCustomerTypeFilterItem == "All")
        {
            filteredList = OriginalCustomerList.ToList();
        }
        else
        {
            filteredList = OriginalCustomerList
                .Where(customer => customer.CustomerType == SelectedCustomerTypeFilterItem)
                .ToList();
        }
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
        ProductList.Clear();
        PackageList.Clear();
    
        if (SelectedProductFilterItem == "Gym Packages")
        {
            if (_originalPackageList != null && _originalPackageList.Count > 0)
            {
                foreach (var package in _originalPackageList)
                {
                    PackageList.Add(package);
                }
            }
        }
        else
        {
            if (OriginalProductList == null || OriginalProductList.Count == 0) return;

            List<Product> filteredList;

            if (SelectedProductFilterItem == "All")
            {
                filteredList = OriginalProductList.ToList();
            }
            else
            {
                filteredList = OriginalProductList
                    .Where(product => product.Category == SelectedProductFilterItem)
                    .ToList();
            }
        
            CurrentProductList = filteredList;
            foreach (var product in filteredList)
            {
                ProductList.Add(product);
            }
        }
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
    }
    
    private void OnPackagesChanged()
    {
        LoadPackageOptions();
    
        if (SelectedProductFilterItem == "Gym Packages")
        {
            ApplyProductFilter();
        }
    }

    public void Dispose()
    {
        if (_packageService != null)
            _packageService.PackagesChanged -= OnPackagesChanged;
    }
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
    private Bitmap _poster; 
    
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
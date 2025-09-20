using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HotAvalonia;
using ShadUI;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AHON_TRACK.ViewModels;

[Page("product-purchase")]
public sealed partial class ProductPurchaseViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty] 
    private string[] _productFilterItems = ["Products", "Gym Packages", "Supplements"];

    [ObservableProperty] 
    private string _selectedProductFilterItem = "Products";

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
    private string _searchStringResult = string.Empty;

    [ObservableProperty] 
    private bool _isSearchingCustomer;

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

    public ProductPurchaseViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;

        LoadCustomerList();
        LoadProductOptions();
    }

    public ProductPurchaseViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());

        LoadCustomerList();
        LoadProductOptions();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        LoadCustomerList();
        LoadProductOptions();
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
        ProductList = new ObservableCollection<Product>(products);
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
                StockCount = 5,
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

    [RelayCommand]
    private async Task SearchCustomers()
    {
        if (string.IsNullOrWhiteSpace(SearchStringResult))
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
                customer.FirstName.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                customer.LastName.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                customer.CustomerType.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase)
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
    
    private void UpdateCustomerCounts()
    {
        SelectedCount = CustomerList.Count(x => x.IsSelected);
        TotalCount = CustomerList.Count;

        SelectAll = CustomerList.Count > 0 && CustomerList.All(x => x.IsSelected);
    }
    
    partial void OnSearchStringResultChanged(string value)
    {
        SearchCustomersCommand.Execute(null);
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
    
    partial void OnSelectedCustomerChanged(Customer? value)
    {
        CustomerFullName = value != null ? $"{value.FirstName} {value.LastName}" : "Customer Name";
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

public class Product
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Price { get; set; }
    public string FormattedPrice => $"₱{Price:N2}";
    public int StockCount { get; set; }
    public Bitmap Poster { get; set; }
}
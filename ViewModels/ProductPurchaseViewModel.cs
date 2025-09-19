using System.Collections.Generic;
using System.Collections.ObjectModel;
using HotAvalonia;
using ShadUI;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AHON_TRACK.ViewModels;

[Page("product-purchase")]
public sealed partial class ProductPurchaseViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty] 
    private string[] _productFilterItems = ["Products", "Gym Packages"];

    [ObservableProperty] 
    private string _selectedProductFilterItem = "Products";
    
    [ObservableProperty] 
    private string[] _customerTypeFilterItems = ["All", "Walk-in", "Gym Member"];

    [ObservableProperty] 
    private string _selectedCustomerTypeFilterItem = "All";
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    
    [ObservableProperty]
    private ObservableCollection<Customer> _customerList = [];

    public ProductPurchaseViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        
        LoadCustomerList();
    }

    public ProductPurchaseViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        
        LoadCustomerList();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        LoadCustomerList();
    }
    
    private void LoadCustomerList()
    {
        var customers = GetCustomerData();
        CustomerList = new ObservableCollection<Customer>(customers);
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
}

public partial class Customer : ObservableObject
{
    [ObservableProperty] 
    private int? _iD;

    [ObservableProperty] 
    private string? _firstName = string.Empty;
    
    [ObservableProperty] 
    private string? _lastName = string.Empty;
    
    [ObservableProperty] 
    private string? _customerType = string.Empty;
}
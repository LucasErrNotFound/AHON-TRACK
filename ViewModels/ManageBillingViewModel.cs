using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

[Page("manage-billing")]
public sealed partial class ManageBillingViewModel : ViewModelBase, INavigable
{
    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;
    
    [ObservableProperty]
    private List<Invoices> _originalInvoiceData = [];
    
    [ObservableProperty]
    private List<Invoices> _currentInvoiceData = [];
    
    [ObservableProperty]
    private ObservableCollection<Package> _packageOptions = [];
    
    [ObservableProperty]
    private bool _isInitialized;
    
    [ObservableProperty]
    private bool _selectAll;

    [ObservableProperty]
    private int _selectedCount;
    
    [ObservableProperty]
    private int _totalCount;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly AddNewPackageDialogCardViewModel _addNewPackageDialogCardViewModel;
    private readonly EditPackageDialogCardViewModel _editPackageDialogCardViewModel;
    private ObservableCollection<RecentActivity> _recentActivities = [];
    
    [ObservableProperty]
    private ObservableCollection<Invoices> _invoiceList = [];

    public ManageBillingViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,  AddNewPackageDialogCardViewModel addNewPackageDialogCardViewModel,  EditPackageDialogCardViewModel editPackageDialogCardViewModel)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _addNewPackageDialogCardViewModel = addNewPackageDialogCardViewModel;
        _editPackageDialogCardViewModel = editPackageDialogCardViewModel;
        LoadSampleSalesData();
        LoadInvoiceData();
        LoadPackageOptions();
        UpdateInvoiceDataCounts();
    }

    public ManageBillingViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _addNewPackageDialogCardViewModel = new AddNewPackageDialogCardViewModel();
        _editPackageDialogCardViewModel = new EditPackageDialogCardViewModel();
        LoadSampleSalesData();
        LoadInvoiceData();
        LoadPackageOptions();
        UpdateInvoiceDataCounts();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        LoadSampleSalesData();
        LoadInvoiceData();
        LoadPackageOptions();
        UpdateInvoiceDataCounts();
        IsInitialized = true;
    }
    
    public ObservableCollection<RecentActivity> RecentActivity
    {
        get => _recentActivities;
        set
        {
            _recentActivities = value;
            OnPropertyChanged();
        }
    }
    
    private void LoadSampleSalesData()
    {
        var sampleData = GetSampleSalesData();
        RecentActivity = new ObservableCollection<RecentActivity>(sampleData);
    }

    private void LoadInvoiceData()
    {
        var sampleData = GetInvoiceData();
        OriginalInvoiceData = sampleData;
        FilterInvoiceDataByPackageAndDate();
    }

    private void LoadPackageOptions()
    {
        var packages = GetPackageData();
        PackageOptions = new ObservableCollection<Package>(packages);
    }
    
    private List<RecentActivity> GetSampleSalesData()
    {
        return
        [
            new RecentActivity { CustomerName = "Jedd Calubayan", ProductName = "Red Horse Mucho", PurchaseDate = DateTime.Now, PurchaseTime = DateTime.Now.AddHours(1), Amount = 300.00m },
            new RecentActivity { CustomerName = "Sianrey Flora", ProductName = "Membership Renewal", PurchaseDate = DateTime.Now, PurchaseTime = DateTime.Now.AddHours(1),Amount = 500.00m },
            new RecentActivity { CustomerName = "JC Casidor", ProductName = "Protein Milk Shake", PurchaseDate = DateTime.Now, PurchaseTime = DateTime.Now.AddHours(1), Amount = 35.00m },
            new RecentActivity { CustomerName = "Mardie Dela Cruz", ProductName = "AHON T-Shirt", PurchaseDate = DateTime.Now, PurchaseTime = DateTime.Now.AddHours(1), Amount = 135.00m },
            new RecentActivity { CustomerName = "JL Taberdo", ProductName = "Lifting Straps", PurchaseDate = DateTime.Now, PurchaseTime = DateTime.Now.AddHours(1), Amount = 360.00m },
            new RecentActivity { CustomerName = "Jav Agustin", ProductName = "AHON Tumbler", PurchaseDate = DateTime.Now, PurchaseTime = DateTime.Now.AddHours(1), Amount = 235.00m },
            new RecentActivity { CustomerName = "Marc Torres", ProductName = "Gym Membership", PurchaseDate = DateTime.Now, PurchaseTime = DateTime.Now.AddHours(1), Amount = 499.00m },
            new RecentActivity { CustomerName = "Maverick Lim", ProductName = "Cobra Berry", PurchaseDate = DateTime.Now, PurchaseTime = DateTime.Now.AddHours(1), Amount = 40.00m }
        ];
    }

    private List<Invoices> GetInvoiceData()
    {
        var today = DateTime.Today;
        return 
        [
            new Invoices { ID = 1001, CustomerName = "Mardie Dela Cruz", PurchasedItem = "Red Horse Mucho", Quantity = 2, Amount = 280, DatePurchased = today.AddHours(15) },
            new Invoices { ID = 1002, CustomerName = "JL Taberdo", PurchasedItem = "Cobra yellow", Quantity = 3, Amount = 80, DatePurchased = today.AddHours(16) },
            new Invoices { ID = 1003, CustomerName = "Marion James Dela Roca", PurchasedItem = "Protein Shake", Quantity = 1, Amount = 180, DatePurchased = today.AddHours(17) },
            new Invoices { ID = 1004, CustomerName = "Sianrey Flora", PurchasedItem = "AHON T-Shirt", Quantity = 1, Amount = 580, DatePurchased = today.AddHours(17) },
            new Invoices { ID = 1005, CustomerName = "Rome Jedd Calubayan", PurchasedItem = "Sting Red", Quantity = 5, Amount = 280, DatePurchased = today.AddHours(17) },
            new Invoices { ID = 1006, CustomerName = "Marc Torres", PurchasedItem = "Pre-workout powder", Quantity = 1, Amount = 1280, DatePurchased = today.AddHours(17) },
            new Invoices { ID = 1007, CustomerName = "Nash Floralde", PurchasedItem = "Pre-workout powder", Quantity = 3, Amount = 4480, DatePurchased = today.AddHours(18) },
            new Invoices { ID = 1008, CustomerName = "Ry Christian", PurchasedItem = "Abalos T-Shirt", Quantity = 1, Amount = 180, DatePurchased = today.AddHours(18) },
            new Invoices { ID = 1009, CustomerName = "John Maverick Lim", PurchasedItem = "Red Bull", Quantity = 7, Amount = 880, DatePurchased = today.AddHours(19) },
            new Invoices { ID = 1010, CustomerName = "Raymart Soneja", PurchasedItem = "Protein Powder", Quantity = 1, Amount = 1280, DatePurchased = today.AddDays(-1).AddHours(17) },
            new Invoices { ID = 1011, CustomerName = "Vince Abellada", PurchasedItem = "Protein Powder", Quantity = 1, Amount = 1280, DatePurchased = today.AddDays(-1).AddHours(18) }
        ];
    }

    private List<Package> GetPackageData()
    {
        return
        [
            new Package
            {
                Title = "Free Trial",
                Description = "Try any class or gym session-no charge",
                Price = "₱0.00",
                PriceUnit = "/one-time only",
                Features = 
                [
                    "Risk-free first session",
                    "Explore the gym or class",
                    "Meet our trainers",
                    "Decide before committing",
                    "No hidden charges"
                ],
                IsDiscountChecked = false,
                DiscountValue = null,
                SelectedDiscountFor = "All",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = null,
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Walk-In",
                Description = "Pay per session with no commitment",
                Price = "₱150.00",
                PriceUnit = "/session",
                Features = 
                [
                    "Unlimited time",
                    "Perfect for casual visits",
                    "Pay only when you train",
                    "No membership needed",
                    "Great for one-time guests"
                ],
                IsDiscountChecked = false,
                DiscountValue = null,
                SelectedDiscountFor = "All",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = null,
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Monthly Membership",
                Description = "Unlimited gym access for 30 days",
                Price = "₱500.00",
                PriceUnit = "/month",
                Features = 
                [
                    "Unlimited gym sessions",
                    "Best value for regulars",
                    "Discounted charges",
                    "30 days of full access",
                    "Unli health checkups"
                ],
                IsDiscountChecked = false,
                DiscountValue = null,
                SelectedDiscountFor = "All",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = null,
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Boxing",
                Description = "High-intensity training focused on boxing",
                Price = "₱450.00",
                PriceUnit = "/session",
                Features = 
                [
                    "Boosts cardio and strength",
                    "Learn real boxing skills",
                    "Stress relieving workouts",
                    "Burns high calories fast",
                    "Builds confidence and discipline"
                ],
                IsDiscountChecked = true,
                DiscountValue = 100,
                SelectedDiscountFor = "Gym Members",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = DateOnly.FromDateTime(DateTime.Now.AddYears(-1)),
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Muay Thai",
                Description = "Training for strength and endurance",
                Price = "₱500.00",
                PriceUnit = "/session",
                Features = 
                [
                    "Full body conditioning",
                    "Improves flexibility and balance",
                    "Learn about self-defense",
                    "Culturally rich experience",
                    "Great for strength and stamina"
                ],
                IsDiscountChecked = true,
                DiscountValue = 100,
                SelectedDiscountFor = "Gym Members",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = DateOnly.FromDateTime(DateTime.Now.AddYears(-1)),
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "CrossFit",
                Description = "Functional workouts with high-intensity moves",
                Price = "₱300.00",
                PriceUnit = "/session",
                Features = 
                [
                    "Builds functional strength",
                    "Constantly varied workouts",
                    "Time efficient and intense",
                    "Encourages community support",
                    "Suitable for all fitness levels"
                ],
                IsDiscountChecked = true,
                DiscountValue = 50,
                SelectedDiscountFor = "Gym Members",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = DateOnly.FromDateTime(DateTime.Now.AddYears(-1)),
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Personal Training",
                Description = "One-on-one session to reach your fitness goal",
                Price = "₱200.00",
                PriceUnit = "/session",
                Features = 
                [
                    "Personalized workout plans",
                    "One-on-one coaching",
                    "Faster progress tracking",
                    "Motivation and accountability",
                    "Focus on your specific goals"
                ],
                IsDiscountChecked = true,
                DiscountValue = 50,
                SelectedDiscountFor = "Gym Members",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = DateOnly.FromDateTime(DateTime.Now.AddYears(-1)),
                DiscountValidTo = null 
            },
            new Package
            {
                Title = "Thai Massage",
                Description = "Massage therapy for relaxation and healing",
                Price = "₱350.00",
                PriceUnit = "/session",
                Features = 
                [
                    "Relieves muscle tension and stiffness",
                    "Enhances flexibility and circulation",
                    "Promotes deep relaxation",
                    "Reduces stress and fatigue",
                    "Supports overall wellness"
                ],
                IsDiscountChecked = true,
                DiscountValue = 50,
                SelectedDiscountFor = "Gym Members",
                SelectedDiscountType = "Fixed Amount (₱)",
                DiscountValidFrom = DateOnly.FromDateTime(DateTime.Now.AddYears(-1)),
                DiscountValidTo = null 
            }
        ];
    }

    [RelayCommand]
    private void OpenAddNewPackage()
    {
        _addNewPackageDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_addNewPackageDialogCardViewModel)
            .WithSuccessCallback(_ =>
                _toastManager.CreateToast("Added a new package")
                    .WithContent($"You just added a new package to the database!")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new package cancelled")
                    .WithContent("If you want to add a new package, please try again.")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(550)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void OpenEditPackage(Package package)
    {
        _editPackageDialogCardViewModel.Initialize();
        _editPackageDialogCardViewModel.PopulateFromPackage(package);
        _dialogManager.CreateDialog(_editPackageDialogCardViewModel)
            .WithSuccessCallback(_ =>
            {
                var index = PackageOptions.IndexOf(package);
                if (index >= 0)
                {
                    PackageOptions[index] = _editPackageDialogCardViewModel.ToPackageOption();
                }
                _toastManager.CreateToast("Package updated")
                    .WithContent($"You just updated the {package.Title} package!")
                    .DismissOnClick()
                    .ShowSuccess();
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Editing an existing package cancelled")
                    .WithContent("If you want to edit an existing package, please try again.")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(550)
            .Dismissible()
            .Show();
    }
    
    private void FilterInvoiceDataByPackageAndDate()
    {
        var filteredInvoiceData = OriginalInvoiceData 
            .Where(w => w.DatePurchased?.Date == SelectedDate.Date)
            .ToList();
        
        CurrentInvoiceData = filteredInvoiceData;
        InvoiceList.Clear();
        
        foreach (var schedule in filteredInvoiceData)
        {
            schedule.PropertyChanged -= OnDatePurchasedChanged;
            schedule.PropertyChanged += OnDatePurchasedChanged;
            InvoiceList.Add(schedule);
        }
        UpdateInvoiceDataCounts();
    }
    
    private void UpdateInvoiceDataCounts()
    {
        SelectedCount = InvoiceList.Count(x => x.IsSelected);
        TotalCount = InvoiceList.Count;

        SelectAll = InvoiceList.Count > 0 && InvoiceList.All(x => x.IsSelected);
    }
    
    private void OnDatePurchasedChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Invoices.IsSelected))
        {
            UpdateInvoiceDataCounts();
        }
    }
    
    partial void OnSelectedDateChanged(DateTime value)
    {
        FilterInvoiceDataByPackageAndDate();
    }
}

public class RecentActivity
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerType { get; set; } = "Gym Member";
    public string ProductName { get; set; } = string.Empty;
    public DateTime? PurchaseDate { get; set; }
    public DateTime? PurchaseTime { get; set; }
    public decimal Amount { get; init; }
    public string AvatarSource { get; set; } = "avares://AHON_TRACK/Assets/MainWindowView/user.png";

    // Formatted currency for display
    public string FormattedAmount => $"+₱{Amount:F2}";
    public string DateFormatted => PurchaseDate?.ToString("MMMM dd, yyyy dddd") ?? string.Empty;
    public string PicturePath => string.IsNullOrEmpty(AvatarSource) || AvatarSource == "null"
        ? "avares://AHON_TRACK/Assets/MainWindowView/user.png"
        : AvatarSource;
}

public partial class Invoices : ObservableObject
{
    [ObservableProperty] 
    private int? _iD;
    
    [ObservableProperty]
    private string _customerName = string.Empty;
    
    [ObservableProperty]
    private string _purchasedItem  = string.Empty;
    
    [ObservableProperty]
    private int? _quantity;
    
    [ObservableProperty]
    private int? _amount;
    
    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty] 
    private bool _isSelected;
    
    public DateTime? DatePurchased { get; set; }
    public string DateFormatted => DatePurchased?.ToString("MMMM dd, yyyy dddd") ?? string.Empty;
}

public class Package
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string PriceUnit { get; set; } = string.Empty;
    public List<string> Features { get; set; } = [];
    public bool IsDiscountChecked { get; set; }
    public int? DiscountValue { get; set; }
    public string SelectedDiscountFor { get; set; } =  string.Empty;
    public string SelectedDiscountType { get; set; } =  string.Empty;
    public DateOnly? DiscountValidFrom { get; set; }
    public DateOnly? DiscountValidTo { get; set; }
}
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
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        LoadSampleSalesData();
        LoadInvoiceData();
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
    private void OpenEditPackage()
    {
        _editPackageDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_editPackageDialogCardViewModel)
            .WithSuccessCallback(_ =>
                _toastManager.CreateToast("Edit an existing package")
                    .WithContent($"You just edited an existing package!")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Editing an existing package cancelled")
                    .WithContent("If you want to edit an existing package, please try again.")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(550)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void OpenAddNewProduct()
    {
        _pageManager.Navigate<AddNewProductViewModel>();
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
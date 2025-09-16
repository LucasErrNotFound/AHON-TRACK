using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using AHON_TRACK.Services.Interface;


namespace AHON_TRACK.ViewModels;

[Page("manage-billing")]
public sealed partial class ManageBillingViewModel : ViewModelBase, INavigable
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly AddNewPackageDialogCardViewModel _addNewPackageDialogCardViewModel;
    private readonly EditPackageDialogCardViewModel _editPackageDialogCardViewModel;
    private ObservableCollection<RecentActivity> _recentActivities = [];
    private readonly ISystemService _systemService;

    public ManageBillingViewModel(DialogManager dialogManager, ISystemService systemService, ToastManager toastManager, PageManager pageManager, AddNewPackageDialogCardViewModel addNewPackageDialogCardViewModel, EditPackageDialogCardViewModel editPackageDialogCardViewModel)
    {
        _systemService = systemService;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _addNewPackageDialogCardViewModel = addNewPackageDialogCardViewModel;
        _editPackageDialogCardViewModel = editPackageDialogCardViewModel;
        LoadSampleSalesData();
    }

    public ManageBillingViewModel()
    {
        _systemService = null!;
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _addNewPackageDialogCardViewModel = new AddNewPackageDialogCardViewModel();
        _editPackageDialogCardViewModel = new EditPackageDialogCardViewModel();
        LoadSampleSalesData();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
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

    [RelayCommand]
    private async void OpenAddNewPackage() // Made async
    {
        _addNewPackageDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_addNewPackageDialogCardViewModel)
            .WithSuccessCallback(async _ => // Made async
            {
                try
                {
                    // Get the package data from the dialog view model
                    var packageData = _addNewPackageDialogCardViewModel.GetPackageData();

                    if (packageData != null && _systemService != null)
                    {
                        // Save to database using the service
                        await _systemService.AddPackageAsync(packageData);

                        // Show additional success feedback
                        _toastManager.CreateToast("Package Created Successfully")
                            .WithContent($"Package '{packageData.packageName}' has been added to the database!")
                            .DismissOnClick()
                            .ShowSuccess();
                    }
                    else if (packageData == null)
                    {
                        // Show validation error
                        _toastManager.CreateToast("Validation Error")
                            .WithContent("Please fill in all required fields (Package Name and Price).")
                            .DismissOnClick()
                            .ShowError();
                    }
                    else
                    {
                        // Show error if service is null
                        _toastManager.CreateToast("Service Error")
                            .WithContent("Database service is not available.")
                            .DismissOnClick()
                            .ShowError();
                    }
                }
                catch (Exception ex)
                {
                    // Handle any errors that occur during saving
                    _toastManager.CreateToast("Database Error")
                        .WithContent($"Failed to save package: {ex.Message}")
                        .DismissOnClick()
                        .ShowError();
                }
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new package cancelled")
                    .WithContent("If you want to add a new package, please try again.")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(550)
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
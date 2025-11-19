using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using QuestPDF.Companion;
using QuestPDF.Fluent;
using ShadUI;
using AHON_TRACK.Services.Events;

namespace AHON_TRACK.ViewModels;

[Page("manage-billing")]
public sealed partial class ManageBillingViewModel : ViewModelBase, INavigable
{
    private EventHandler? _salesUpdatedHandler;
    private EventHandler? _productPurchasedHandler;
    private EventHandler? _chartDataUpdatedHandler;
    private EventHandler? _memberAddedHandler;
    private EventHandler? _memberUpdatedHandler;

    private bool _isLoadingData = false;

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

    [ObservableProperty]
    private ObservableCollection<Invoices> _invoiceList = [];

    [ObservableProperty]
    private ObservableCollection<RecentActivity> _recentActivities = [];

    [ObservableProperty]
    private bool _isLoadingRecentActivity;

    [ObservableProperty]
    private bool _isLoadingInvoices;

    [ObservableProperty]
    private DateTime _selectedActivityDate = DateTime.Today;

    [ObservableProperty]
    private ObservableCollection<RecentActivity> _filteredRecentActivities = [];

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly AddNewPackageDialogCardViewModel _addNewPackageDialogCardViewModel;
    private readonly EditPackageDialogCardViewModel _editPackageDialogCardViewModel;
    private readonly IPackageService _packageService;
    private readonly IProductPurchaseService _productPurchaseService;
    private readonly SettingsService _settingsService;
    private AppSettings? _currentSettings;

    public ManageBillingViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,
        AddNewPackageDialogCardViewModel addNewPackageDialogCardViewModel, EditPackageDialogCardViewModel editPackageDialogCardViewModel,
        SettingsService settingsService, IPackageService packageService, IProductPurchaseService productPurchaseService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _addNewPackageDialogCardViewModel = addNewPackageDialogCardViewModel;
        _editPackageDialogCardViewModel = editPackageDialogCardViewModel;
        _packageService = packageService;
        _productPurchaseService = productPurchaseService;
        _settingsService = settingsService;

        LoadPackageOptionsAsync();
        _ = LoadRecentPurchasesFromDatabaseAsync();
        _ = LoadInvoicesFromDatabaseAsync();
        SubscribeToEvents();

        UpdateInvoiceDataCounts();
    }

    public ManageBillingViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _addNewPackageDialogCardViewModel = new AddNewPackageDialogCardViewModel();
        _editPackageDialogCardViewModel = new EditPackageDialogCardViewModel();
        _settingsService = new SettingsService();
        _packageService = null!;
        _productPurchaseService = null!;

        _ = LoadRecentPurchasesFromDatabaseAsync();
        _ = LoadInvoicesFromDatabaseAsync();
        SubscribeToEvents();
        UpdateInvoiceDataCounts();
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (IsInitialized) return;
        await LoadSettingsAsync();

        if (_productPurchaseService != null)
        {
            await LoadInvoicesFromDatabaseAsync();
            await LoadRecentPurchasesFromDatabaseAsync();
        }
        else
        {
            LoadSampleSalesData();
            LoadInvoiceData();
        }
        LoadPackageOptionsAsync();
        UpdateInvoiceDataCounts();
        FilterRecentActivitiesByDate();
        IsInitialized = true;
    }

    private void SubscribeToEvents()
    {
        var eventService = DashboardEventService.Instance;

        // Store handlers in fields so we can unsubscribe later
        _salesUpdatedHandler = OnBillingDataChanged;
        _productPurchasedHandler = OnBillingDataChanged;
        _chartDataUpdatedHandler = OnBillingDataChanged;
        _memberAddedHandler = OnBillingDataChanged;
        _memberUpdatedHandler = OnBillingDataChanged;

        eventService.SalesUpdated += _salesUpdatedHandler;
        eventService.ProductPurchased += _productPurchasedHandler;
        eventService.ChartDataUpdated += _chartDataUpdatedHandler;
        eventService.MemberAdded += _memberAddedHandler;
        eventService.MemberUpdated += _memberUpdatedHandler;

    }

    private async void OnBillingDataChanged(object? sender, EventArgs e)
    {
        if (_isLoadingData) return;
        try
        {
            _isLoadingData = true;
            await LoadInvoicesFromDatabaseAsync();
            await LoadRecentPurchasesFromDatabaseAsync();
        }
        catch (Exception ex)
        {
            _toastManager?.CreateToast($"Failed to load: {ex.Message}");
        }
        finally
        {
            _isLoadingData = false;
        }
    }


    public ObservableCollection<RecentActivity> RecentActivity
    {
        get => RecentActivities;
        set
        {
            RecentActivities = value;
            OnPropertyChanged();
        }
    }

    private async Task LoadRecentPurchasesFromDatabaseAsync()
    {
        if (_productPurchaseService == null)
        {
            LoadSampleSalesData();
            return;
        }

        IsLoadingRecentActivity = true;

        try
        {
            var purchases = await _productPurchaseService.GetRecentPurchasesAsync(50);

            RecentActivities.Clear();
            foreach (var purchase in purchases)
            {
                RecentActivities.Add(new RecentActivity
                {
                    CustomerName = purchase.CustomerName,
                    CustomerType = purchase.CustomerType,
                    ProductName = purchase.ItemName,
                    PurchaseDate = purchase.PurchaseDate,
                    PurchaseTime = purchase.PurchaseDate,
                    Amount = purchase.Amount,
                    AvatarSource = purchase.AvatarSource != null
                        ? ConvertByteArrayToImagePath(purchase.AvatarSource)
                        : "avares://AHON_TRACK/Assets/MainWindowView/user.png"
                });
            }
            FilterRecentActivitiesByDate();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Load Error")
                .WithContent($"Failed to load recent purchases: {ex.Message}")
                .ShowError();
            LoadSampleSalesData(); // Fallback to sample data
        }
        finally
        {
            IsLoadingRecentActivity = false;
        }
    }

    private async Task LoadInvoicesFromDatabaseAsync()
    {
        if (_productPurchaseService == null)
        {
            LoadInvoiceData();
            return;
        }

        IsLoadingInvoices = true;

        try
        {
            var invoices = await _productPurchaseService.GetInvoicesByDateAsync(SelectedDate);

            OriginalInvoiceData = invoices.Select(i => new Invoices
            {
                ID = i.ID,
                InvoiceId = i.InvoiceId,
                CustomerName = i.CustomerName,
                PurchasedItem = i.PurchasedItem,
                Quantity = i.Quantity,
                PaymentMethod = i.PaymentMethod,
                ReferenceNumber = i.ReferenceNumber,
                Amount = i.Amount,  // ✅ Remove (int)Math.Round() - keep as decimal
                TenderedPrice = i.TenderedPrice,  // ✅ Remove casting - keep as decimal?
                Change = i.Change,
                DatePurchased = i.DatePurchased
            }).ToList();

            FilterInvoiceDataByPackageAndDate();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Load Error")
                .WithContent($"Failed to load purchases: {ex.Message}")
                .ShowError();
            LoadInvoiceData(); // Fallback to sample data
        }
        finally
        {
            IsLoadingInvoices = false;
        }
    }

    private string ConvertByteArrayToImagePath(byte[] imageBytes)
    {
        try
        {
            using var ms = new MemoryStream(imageBytes);
            var bitmap = new Bitmap(ms);
            // For Avalonia, we need to return the byte array or create a temporary file
            // Since Avalonia can't directly use byte[] for Image.Source, 
            // you might want to save it temporarily or use a different approach
            return "avares://AHON_TRACK/Assets/MainWindowView/user.png"; // Fallback for now
        }
        catch
        {
            return "avares://AHON_TRACK/Assets/MainWindowView/user.png";
        }
    }

    private void LoadSampleSalesData()
    {
        var sampleData = GetSampleSalesData();
        RecentActivity = new ObservableCollection<RecentActivity>(sampleData);
        FilterRecentActivitiesByDate();
    }

    private void LoadInvoiceData()
    {
        var sampleData = GetInvoiceData();
        OriginalInvoiceData = sampleData;
        FilterInvoiceDataByPackageAndDate();
    }

    private async void LoadPackageOptionsAsync()
    {
        if (_packageService != null)
        {
            var packages = await _packageService.GetPackagesAsync();
            PackageOptions = new ObservableCollection<Package>(packages);
        }
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
            new Invoices { ID = 1011, CustomerName = "Vince Abellada", PurchasedItem = "Protein Powder", Quantity = 1, Amount = 1280, DatePurchased = today.AddDays(-1).AddHours(18) },
            // Test Test Test
            new Invoices { ID = 1012, CustomerName = "Mardie Dela Cruz", PurchasedItem = "Red Horse Mucho", Quantity = 2, Amount = 280, DatePurchased = today.AddHours(15) },
            new Invoices { ID = 1013, CustomerName = "JL Taberdo", PurchasedItem = "Cobra yellow", Quantity = 3, Amount = 80, DatePurchased = today.AddHours(16) },
            new Invoices { ID = 1014, CustomerName = "Marion James Dela Roca", PurchasedItem = "Protein Shake", Quantity = 1, Amount = 180, DatePurchased = today.AddHours(17) },
            new Invoices { ID = 1004, CustomerName = "Sianrey Flora", PurchasedItem = "AHON T-Shirt", Quantity = 1, Amount = 580, DatePurchased = today.AddHours(17) },
            new Invoices { ID = 1015, CustomerName = "Rome Jedd Calubayan", PurchasedItem = "Sting Red", Quantity = 5, Amount = 280, DatePurchased = today.AddHours(17) },
            new Invoices { ID = 1016, CustomerName = "Marc Torres", PurchasedItem = "Pre-workout powder", Quantity = 1, Amount = 1280, DatePurchased = today.AddHours(17) },
            new Invoices { ID = 1017, CustomerName = "Nash Floralde", PurchasedItem = "Pre-workout powder", Quantity = 3, Amount = 4480, DatePurchased = today.AddHours(18) },
            new Invoices { ID = 1018, CustomerName = "Ry Christian", PurchasedItem = "Abalos T-Shirt", Quantity = 1, Amount = 180, DatePurchased = today.AddHours(18) },
            new Invoices { ID = 1019, CustomerName = "John Maverick Lim", PurchasedItem = "Red Bull", Quantity = 7, Amount = 880, DatePurchased = today.AddHours(19) },
            // Ends Here
        ];
    }

    [RelayCommand]
    private async Task RefreshRecentActivity()
    {
        await LoadRecentPurchasesFromDatabaseAsync();
    }

    [RelayCommand]
    private async Task RefreshInvoices()
    {
        await LoadInvoicesFromDatabaseAsync();
    }

    [RelayCommand]
    private async Task DownloadInvoices()
    {
        try
        {
            // Check if there are any purchases to export
            if (InvoiceList.Count == 0)
            {
                _toastManager.CreateToast("No purchases to export")
                    .WithContent("There are no purchase list available for the selected date.")
                    .DismissOnClick()
                    .ShowWarning();
                return;
            }

            var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (toplevel == null) return;

            IStorageFolder? startLocation = null;
            if (!string.IsNullOrWhiteSpace(_currentSettings?.DownloadPath))
            {
                try
                {
                    startLocation = await toplevel.StorageProvider.TryGetFolderFromPathAsync(_currentSettings.DownloadPath);
                }
                catch
                {
                    // If path is invalid, startLocation will remain null
                }
            }

            var fileName = $"PurchaseList_{SelectedDate:yyyy-MM-dd}.pdf";
            var pdfFile = await toplevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Download Purchases",
                SuggestedStartLocation = startLocation,
                FileTypeChoices = [FilePickerFileTypes.Pdf],
                SuggestedFileName = fileName,
                ShowOverwritePrompt = true
            });

            if (pdfFile == null) return;

            var invoiceModel = new InvoiceDocumentModel
            {
                GeneratedDate = DateTime.Today,
                GymName = "AHON Victory Fitness Gym",
                GymAddress = "2nd Flr. Event Hub, Victory Central Mall, Brgy. Balibago, Sta. Rosa City, Laguna",
                GymPhone = "+63 123 456 7890",
                GymEmail = "info@ahonfitness.com",
                Items = InvoiceList.Select(invoice => new InvoiceItem
                {
                    ID = invoice.ID ?? 0,
                    CustomerName = invoice.CustomerName,
                    PurchasedItem = invoice.PurchasedItem,
                    Quantity = invoice.Quantity ?? 0,
                    Amount = invoice.Amount ?? 0,
                    PaymentMethod = invoice.PaymentMethod,
                    ReferenceNumber = invoice.ReferenceNumber,
                    DatePurchased = invoice.DatePurchased ?? DateTime.Today
                }).ToList()
            };

            var document = new InvoiceDocument(invoiceModel);

            await using var stream = await pdfFile.OpenWriteAsync();

            // Both cannot be enabled at the same time. Disable one of them 
            document.GeneratePdf(stream); // Generate the PDF
            // await document.ShowInCompanionAsync(); // For Hot-Reload Debugging

            _toastManager.CreateToast("Purchases exported successfully")
                .WithContent($"Purchase list has been saved to {pdfFile.Name}")
                .DismissOnClick()
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Export failed")
                .WithContent($"Failed to export purchases: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private void OpenAddNewPackage()
    {
        _addNewPackageDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_addNewPackageDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                try
                {
                    // Get the package data for database
                    var packageData = _addNewPackageDialogCardViewModel.GetPackageData();
                    if (packageData != null && _packageService != null)
                    {
                        // Save to database using SystemService
                        var result = await _packageService.AddPackageAsync(packageData);

                        if (result.Success)
                        {
                            LoadPackageOptionsAsync();

                            // Show success message
                            _toastManager.CreateToast("Package Created Successfully")
                                .WithContent($"Package '{packageData.packageName}' has been added to the database!")
                                .DismissOnClick()
                                .ShowSuccess();
                        }
                    }
                    else if (packageData == null)
                    {
                        _toastManager.CreateToast("Validation Error")
                            .WithContent("Please fill in all required fields.")
                            .DismissOnClick()
                            .ShowError();
                    }
                    else
                    {
                        _toastManager.CreateToast("Service Error")
                            .WithContent("Database service is not available.")
                            .DismissOnClick()
                            .ShowError();
                    }
                }
                catch (Exception ex)
                {
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
    private async void OpenEditPackage(Package package)
    {
        _editPackageDialogCardViewModel.Initialize();
        _editPackageDialogCardViewModel.PopulateFromPackage(package);
        _dialogManager.CreateDialog(_editPackageDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                if (_editPackageDialogCardViewModel.IsDeleteAction)
                {
                    if (_packageService != null)
                    {
                        var success = await _packageService.DeletePackageAsync(package.PackageId);
                        if (success)
                        {
                            PackageOptions.Remove(package);
                            _toastManager.CreateToast("Package deleted")
                                .WithContent($"The {package.Title} package has been successfully deleted!")
                                .DismissOnClick()
                                .ShowSuccess();
                        }
                        else
                        {
                            _toastManager.CreateToast("Deletion failed")
                                .WithContent("Failed to delete the package from the database.")
                                .DismissOnClick()
                                .ShowError();
                        }
                    }
                }
                else
                {
                    if (_packageService != null)
                    {
                        // Convert updated VM data into PackageModel
                        var updatedModel = _editPackageDialogCardViewModel.ToPackageModel(package.PackageId);

                        try
                        {
                            var success = await _packageService.UpdatePackageAsync(updatedModel);
                            if (success)
                            {
                                // Convert back to display Package
                                var updatedPackage = ConvertToDisplayPackage(updatedModel);

                                // Update local collection
                                var index = PackageOptions.IndexOf(package);
                                if (index >= 0)
                                    PackageOptions[index] = updatedPackage;

                                _toastManager.CreateToast("Package updated")
                                    .WithContent($"You just updated the {package.Title} package!")
                                    .DismissOnClick()
                                    .ShowSuccess();
                            }
                            else
                            {
                                _toastManager.CreateToast("Update failed")
                                    .WithContent("Could not update package in database.")
                                    .DismissOnClick()
                                    .ShowError();
                            }
                        }
                        catch (Exception ex)
                        {
                            _toastManager.CreateToast("Database Error")
                                .WithContent($"Failed to update package: {ex.Message}")
                                .DismissOnClick()
                                .ShowError();
                        }
                    }
                    else
                    {
                        _toastManager.CreateToast("Service Error")
                            .WithContent("Database service is not available.")
                            .DismissOnClick()
                            .ShowError();
                    }
                }
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Editing an existing package cancelled")
                    .WithContent("If you want to edit an existing package, please try again.")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(550)
            .Dismissible()
            .Show();
    }

    private Package ConvertToDisplayPackage(PackageModel packageModel)
    {
        var features = new List<string>();

        // Add non-empty features to the list
        if (!string.IsNullOrWhiteSpace(packageModel.features1)) features.Add(packageModel.features1.Trim());
        if (!string.IsNullOrWhiteSpace(packageModel.features2)) features.Add(packageModel.features2.Trim());
        if (!string.IsNullOrWhiteSpace(packageModel.features3)) features.Add(packageModel.features3.Trim());
        if (!string.IsNullOrWhiteSpace(packageModel.features4)) features.Add(packageModel.features4.Trim());
        if (!string.IsNullOrWhiteSpace(packageModel.features5)) features.Add(packageModel.features5.Trim());

        return new Package
        {
            PackageId = packageModel.packageID,
            Title = packageModel.packageName,
            Description = packageModel.description,
            Price = (int)packageModel.price, // Convert decimal to int
            DiscountedPrice = (int)packageModel.discountedPrice, // Convert decimal to int
            Duration = packageModel.duration,
            Features = features,
            IsDiscountChecked = packageModel.discount > 0,
            DiscountValue = packageModel.discount > 0 ? (int)packageModel.discount : null,
            SelectedDiscountFor = packageModel.discountFor ?? string.Empty,
            SelectedDiscountType = packageModel.discountType ?? string.Empty,
            DiscountValidFrom = packageModel.validFrom != default(DateTime) ? DateOnly.FromDateTime(packageModel.validFrom) : null,
            DiscountValidTo = packageModel.validTo != default(DateTime) ? DateOnly.FromDateTime(packageModel.validTo) : null
        };
    }

    private void FilterInvoiceDataByPackageAndDate()
    {
        var filteredInvoiceData = OriginalInvoiceData
            .Where(w => w.DatePurchased.HasValue && w.DatePurchased.Value.Date == SelectedDate.Date)
            .ToList();

        CurrentInvoiceData = filteredInvoiceData;
        InvoiceList.Clear();

        foreach (var invoice in filteredInvoiceData)
        {
            invoice.PropertyChanged -= OnInvoicePropertyChanged;
            invoice.PropertyChanged += OnInvoicePropertyChanged;
            InvoiceList.Add(invoice);
        }
        UpdateInvoiceDataCounts();
    }

    private void FilterRecentActivitiesByDate()
    {
        if (RecentActivities == null || RecentActivities.Count == 0)
        {
            FilteredRecentActivities.Clear();
            return;
        }

        var filtered = RecentActivities
            .Where(activity => activity.PurchaseDate.HasValue &&
                               activity.PurchaseDate.Value.Date == SelectedActivityDate.Date)
            .ToList();

        FilteredRecentActivities.Clear();
        foreach (var activity in filtered)
        {
            FilteredRecentActivities.Add(activity);
        }
    }

    private void UpdateInvoiceDataCounts()
    {
        SelectedCount = InvoiceList.Count(x => x.IsSelected);
        TotalCount = InvoiceList.Count;

        SelectAll = InvoiceList.Count > 0 && InvoiceList.All(x => x.IsSelected);
    }

    partial void OnSelectAllChanged(bool value)
    {
        foreach (var invoice in InvoiceList)
        {
            invoice.IsSelected = value;
        }
        UpdateInvoiceDataCounts();
    }

    private void OnInvoicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Invoices.IsSelected))
        {
            UpdateInvoiceDataCounts();
        }
    }

    private async Task LoadSettingsAsync() => _currentSettings = await _settingsService.LoadSettingsAsync();

    private void OnDatePurchasedChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Invoices.IsSelected))
        {
            UpdateInvoiceDataCounts();
        }
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        _ = LoadInvoicesFromDatabaseAsync();
    }

    partial void OnSelectedActivityDateChanged(DateTime value)
    {
        FilterRecentActivitiesByDate();
    }

    protected override void DisposeManagedResources()
    {
        // Unsubscribe from events
        var eventService = DashboardEventService.Instance;

        if (_salesUpdatedHandler != null)
            eventService.SalesUpdated -= _salesUpdatedHandler;
        if (_productPurchasedHandler != null)
            eventService.ProductPurchased -= _productPurchasedHandler;
        if (_chartDataUpdatedHandler != null)
            eventService.ChartDataUpdated -= _chartDataUpdatedHandler;
        if (_memberAddedHandler != null)
            eventService.MemberAdded -= _memberAddedHandler;
        if (_memberUpdatedHandler != null)
            eventService.MemberUpdated -= _memberUpdatedHandler;

        // Unsubscribe from property changed events
        foreach (var invoice in InvoiceList)
        {
            invoice.PropertyChanged -= OnInvoicePropertyChanged;
        }

        // Clear collections
        InvoiceList.Clear();
        OriginalInvoiceData.Clear();
        CurrentInvoiceData.Clear();
        PackageOptions.Clear();
        RecentActivities.Clear();
        FilteredRecentActivities.Clear();

        base.DisposeManagedResources();
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
    private string _purchasedItem = string.Empty;

    [ObservableProperty]
    private int? _quantity;

    [ObservableProperty]
    private decimal? _amount;  // ✅ Changed from int? to decimal?
        
    [ObservableProperty]
    private decimal? _tenderedPrice;  // ✅ Changed from int? to decimal?
        
    [ObservableProperty]
    private decimal? _change;  // ✅ Changed from int? to decimal?

    [ObservableProperty]
    private string _paymentMethod = string.Empty;
    
    [ObservableProperty]
    private string _referenceNumber = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty] 
    private string? _invoiceId = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public DateTime? DatePurchased { get; set; }
    public string DateFormatted => DatePurchased?.ToString("MM/dd/yyyy") ?? string.Empty;
    public string AmountFormatted => $"₱{Amount:N2}";
    public string TenderedPriceFormatted => $"₱{TenderedPrice:N2}";
    public string ChangeFormatted => $"₱{Change:N2}";
}

public partial class Package : ObservableObject
{
    public int PackageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; }
    public int DiscountedPrice { get; set; }

    // ADD THIS PROPERTY to know if discount should be displayed:
    public bool HasDiscount => IsDiscountChecked && DiscountedPrice > 0 && DiscountedPrice < Price;

    // UPDATE THIS to use HasDiscount:
    public string FormattedPrice => HasDiscount
        ? $"₱{DiscountedPrice:N2}"
        : $"₱{Price:N2}";

    // ADD THIS for strikethrough original price:
    public string? OriginalPriceFormatted => HasDiscount
        ? $"₱{Price:N2}"
        : null;

    // ADD THIS for discount badge:
    public string DiscountBadge
    {
        get
        {
            if (!HasDiscount || !DiscountValue.HasValue) return string.Empty;

            if (SelectedDiscountType?.Equals("percentage", StringComparison.OrdinalIgnoreCase) == true)
            {
                return $"{DiscountValue}% OFF";
            }
            else
            {
                return $"₱{DiscountValue} OFF";
            }
        }
    }

    public string Duration { get; set; } = string.Empty;
    public List<string> Features { get; set; } = [];
    public bool IsDiscountChecked { get; set; }
    public int? DiscountValue { get; set; }
    public string SelectedDiscountFor { get; set; } = string.Empty;
    public string SelectedDiscountType { get; set; } = string.Empty;
    public DateOnly? DiscountValidFrom { get; set; }
    public DateOnly? DiscountValidTo { get; set; }
    public int Id { get; internal set; }

    [ObservableProperty]
    private bool _isAddedToCart;
}
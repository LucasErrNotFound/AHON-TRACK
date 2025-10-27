using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using Avalonia.Media.Imaging;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using QuestPDF.Fluent;
using ShadUI;
using AHON_TRACK.Services.Events;
using Microsoft.Extensions.Logging;

namespace AHON_TRACK.ViewModels;

[Page("manage-billing")]
public sealed partial class ManageBillingViewModel : ViewModelBase, INavigable
{
    #region Private Fields

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly AddNewPackageDialogCardViewModel _addNewPackageDialogCardViewModel;
    private readonly EditPackageDialogCardViewModel _editPackageDialogCardViewModel;
    private readonly IPackageService _packageService;
    private readonly IProductPurchaseService _productPurchaseService;
    private readonly SettingsService _settingsService;
    private readonly ILogger _logger;
    private AppSettings? _currentSettings;

    [ObservableProperty] private ObservableCollection<Invoices> _invoiceList = [];
    [ObservableProperty] private ObservableCollection<RecentActivity> _recentActivities = [];
    [ObservableProperty] private ObservableCollection<Package> _packageOptions = [];
    [ObservableProperty] private List<Invoices> _originalInvoiceData = [];
    [ObservableProperty] private List<Invoices> _currentInvoiceData = [];
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private bool _isInitialized;
    [ObservableProperty] private bool _selectAll;
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _isLoadingRecentActivity;
    [ObservableProperty] private bool _isLoadingInvoices;

    #endregion

    #region Constructors

    public ManageBillingViewModel(
        DialogManager dialogManager,
        ToastManager toastManager,
        AddNewPackageDialogCardViewModel addNewPackageDialogCardViewModel,
        EditPackageDialogCardViewModel editPackageDialogCardViewModel,
        SettingsService settingsService,
        IPackageService packageService,
        IProductPurchaseService productPurchaseService,
        ILogger logger)
    {
        _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
        _addNewPackageDialogCardViewModel = addNewPackageDialogCardViewModel ?? throw new ArgumentNullException(nameof(addNewPackageDialogCardViewModel));
        _editPackageDialogCardViewModel = editPackageDialogCardViewModel ?? throw new ArgumentNullException(nameof(editPackageDialogCardViewModel));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        _productPurchaseService = productPurchaseService ?? throw new ArgumentNullException(nameof(productPurchaseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        SubscribeToEvents();
    }

    // Design-time constructor
    public ManageBillingViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _addNewPackageDialogCardViewModel = new AddNewPackageDialogCardViewModel();
        _editPackageDialogCardViewModel = new EditPackageDialogCardViewModel();
        _settingsService = new SettingsService();
        _packageService = null!;
        _productPurchaseService = null!;
        _logger = null!;

        LoadSampleData();
    }

    #endregion

    #region INavigable Implementation

    [AvaloniaHotReload]
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsInitialized)
        {
            _logger?.LogDebug("ManageBillingViewModel already initialized");
            return;
        }

        _logger?.LogInformation("Initializing ManageBillingViewModel");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            await LoadSettingsAsync(linkedCts.Token).ConfigureAwait(false);
            await LoadPackageOptionsAsync(linkedCts.Token).ConfigureAwait(false);
            await LoadInvoicesFromDatabaseAsync(linkedCts.Token).ConfigureAwait(false);
            await LoadRecentPurchasesFromDatabaseAsync(linkedCts.Token).ConfigureAwait(false);
            await UpdateInvoiceDataCounts(linkedCts.Token).ConfigureAwait(false);

            IsInitialized = true;
            _logger?.LogInformation("ManageBillingViewModel initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("ManageBillingViewModel initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing ManageBillingViewModel");
            LoadSampleData(); // Fallback
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Navigating away from ManageBilling");
        return ValueTask.CompletedTask;
    }

    #endregion

    #region Subscription Management

    private void SubscribeToEvents()
    {
        var eventService = DashboardEventService.Instance;
        eventService.SalesUpdated += OnBillingDataChanged;
        eventService.ProductPurchased += OnBillingDataChanged;
        eventService.ChartDataUpdated += OnBillingDataChanged;
        eventService.MemberAdded += OnBillingDataChanged;
        eventService.MemberUpdated += OnBillingDataChanged;
    }

    private void UnsubscribeFromEvents()
    {
        var eventService = DashboardEventService.Instance;
        eventService.SalesUpdated -= OnBillingDataChanged;
        eventService.ProductPurchased -= OnBillingDataChanged;
        eventService.ChartDataUpdated -= OnBillingDataChanged;
        eventService.MemberAdded -= OnBillingDataChanged;
        eventService.MemberUpdated -= OnBillingDataChanged;
    }

    #endregion

    #region Data Loading

    private async Task LoadRecentPurchasesFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        if (_productPurchaseService == null)
        {
            LoadSampleSalesData();
            return;
        }

        IsLoadingRecentActivity = true;

        try
        {
            var purchases = await _productPurchaseService.GetRecentPurchasesAsync(50)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

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

            _logger?.LogDebug("Loaded {Count} recent purchases", purchases.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading recent purchases");
            _toastManager?.CreateToast("Load Error")
                .WithContent($"Failed to load recent purchases: {ex.Message}")
                .ShowError();
            LoadSampleSalesData();
        }
        finally
        {
            IsLoadingRecentActivity = false;
        }
    }

    private async Task LoadInvoicesFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        if (_productPurchaseService == null)
        {
            LoadInvoiceData();
            return;
        }

        IsLoadingInvoices = true;

        try
        {
            var invoices = await _productPurchaseService.GetInvoicesByDateAsync(SelectedDate)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // Unsubscribe from old items
            foreach (var invoice in OriginalInvoiceData)
            {
                invoice.PropertyChanged -= OnInvoicePropertyChanged;
            }

            OriginalInvoiceData = invoices.Select(i => new Invoices
            {
                ID = i.ID,
                CustomerName = i.CustomerName,
                PurchasedItem = i.PurchasedItem,
                Quantity = i.Quantity,
                Amount = (int)Math.Round(i.Amount),
                DatePurchased = i.DatePurchased
            }).ToList();

            FilterInvoiceDataByPackageAndDate();
            _logger?.LogDebug("Loaded {Count} invoices for {Date}", invoices.Count, SelectedDate.ToShortDateString());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading invoices");
            _toastManager?.CreateToast("Load Error")
                .WithContent($"Failed to load invoices: {ex.Message}")
                .ShowError();
            LoadInvoiceData();
        }
        finally
        {
            IsLoadingInvoices = false;
        }
    }

    private async Task LoadPackageOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (_packageService == null) return;

        try
        {
            var packages = await _packageService.GetPackagesAsync()
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            PackageOptions.Clear();
            foreach (var package in packages)
            {
                PackageOptions.Add(package);
            }

            _logger?.LogDebug("Loaded {Count} packages", packages.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading packages");
        }
    }

    private async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _currentSettings = await _settingsService.LoadSettingsAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading settings");
        }
    }

    private void LoadSampleData()
    {
        LoadSampleSalesData();
        LoadInvoiceData();
    }

    private void LoadSampleSalesData()
    {
        var sampleData = GetSampleSalesData();
        RecentActivities.Clear();
        foreach (var activity in sampleData)
        {
            RecentActivities.Add(activity);
        }
    }

    private void LoadInvoiceData()
    {
        var sampleData = GetInvoiceData();
        
        // Unsubscribe from old items
        foreach (var invoice in OriginalInvoiceData)
        {
            invoice.PropertyChanged -= OnInvoicePropertyChanged;
        }

        OriginalInvoiceData = sampleData;
        FilterInvoiceDataByPackageAndDate();
    }

    private string ConvertByteArrayToImagePath(byte[] imageBytes)
    {
        try
        {
            using var ms = new MemoryStream(imageBytes);
            var bitmap = new Bitmap(ms);
            return "avares://AHON_TRACK/Assets/MainWindowView/user.png";
        }
        catch
        {
            return "avares://AHON_TRACK/Assets/MainWindowView/user.png";
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
        ];
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
        _ = UpdateInvoiceDataCounts(LifecycleToken);
    }

    private async Task UpdateInvoiceDataCounts(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Yield(); // Ensure async context
            SelectedCount = InvoiceList.Count(x => x.IsSelected);
            TotalCount = InvoiceList.Count;
            SelectAll = InvoiceList.Count > 0 && InvoiceList.All(x => x.IsSelected);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error updating invoice counts");
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task RefreshRecentActivityAsync()
    {
        await LoadRecentPurchasesFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RefreshInvoicesAsync()
    {
        await LoadInvoicesFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task DownloadInvoicesAsync()
    {
        try
        {
            if (InvoiceList.Count == 0)
            {
                _toastManager.CreateToast("No invoices to export")
                    .WithContent("There are no invoices available for the selected date.")
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

            var fileName = $"Invoice_{SelectedDate:yyyy-MM-dd}.pdf";
            var pdfFile = await toplevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Download Invoice",
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
                    DatePurchased = invoice.DatePurchased ?? DateTime.Today
                }).ToList()
            };

            var document = new InvoiceDocument(invoiceModel);

            await using var stream = await pdfFile.OpenWriteAsync();
            // Both cannot be enabled at the same time. Disable one of them 
            document.GeneratePdf(stream); // Generate the PDF
            // await document.ShowInCompanionAsync(); // For Hot-Reload Debugging


            _toastManager.CreateToast("Invoice exported successfully")
                .WithContent($"Invoice has been saved to {pdfFile.Name}")
                .DismissOnClick()
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error exporting invoice");
            _toastManager.CreateToast("Export failed")
                .WithContent($"Failed to export invoice: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task OpenAddNewPackage()
    {
        await _addNewPackageDialogCardViewModel.InitializeAsync().ConfigureAwait(false);
        _dialogManager.CreateDialog(_addNewPackageDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                try
                {
                    var packageData = _addNewPackageDialogCardViewModel.GetPackageData();
                    if (packageData != null && _packageService != null)
                    {
                        var result = await _packageService.AddPackageAsync(packageData);

                        if (result.Success)
                        {
                            await LoadPackageOptionsAsync(LifecycleToken).ConfigureAwait(false);

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
                    _logger?.LogError(ex, "Error adding package");
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
    private async Task OpenEditPackage(Package package)
    {
        await _editPackageDialogCardViewModel.InitializeAsync();
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
                        var updatedModel = _editPackageDialogCardViewModel.ToPackageModel(package.PackageId);

                        try
                        {
                            var success = await _packageService.UpdatePackageAsync(updatedModel);
                            if (success)
                            {
                                var updatedPackage = ConvertToDisplayPackage(updatedModel);

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
                            _logger?.LogError(ex, "Error updating package");
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
            Price = (int)packageModel.price,
            DiscountedPrice = (int)packageModel.discountedPrice,
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

    #endregion

    #region Property Changed Handlers

    private async void OnBillingDataChanged(object? sender, EventArgs e)
    {
        try
        {
            _logger?.LogDebug("Detected billing data change — refreshing");
            await LoadInvoicesFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);
            await LoadRecentPurchasesFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);
            await UpdateInvoiceDataCounts(LifecycleToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error refreshing billing data after change event");
        }
    }

    private void OnInvoicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Invoices.IsSelected))
        {
            _ = UpdateInvoiceDataCounts(LifecycleToken);
        }
    }
    
    partial void OnSelectAllChanged(bool value)
    {
        foreach (var invoice in InvoiceList)
        {
            invoice.IsSelected = value;
        }
        _ = UpdateInvoiceDataCounts(LifecycleToken);
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        _ = LoadInvoicesFromDatabaseAsync(LifecycleToken);
    }

    #endregion

    #region Disposal

    protected override async ValueTask DisposeAsyncCore()
    {
        _logger?.LogInformation("Disposing ManageBillingViewModel");

        // Unsubscribe from events
        UnsubscribeFromEvents();

        // Unsubscribe from invoice property changes
        foreach (var invoice in InvoiceList)
        {
            invoice.PropertyChanged -= OnInvoicePropertyChanged;
        }
        foreach (var invoice in OriginalInvoiceData)
        {
            invoice.PropertyChanged -= OnInvoicePropertyChanged;
        }

        // Clear collections
        InvoiceList.Clear();
        RecentActivities.Clear();
        PackageOptions.Clear();
        OriginalInvoiceData.Clear();
        CurrentInvoiceData.Clear();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    #endregion
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
    private int? _amount;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public DateTime? DatePurchased { get; set; }
    public string DateFormatted => DatePurchased?.ToString("MMMM dd, yyyy dddd") ?? string.Empty;
}

public partial class Package : ObservableObject
{
    public int PackageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; }
    public int DiscountedPrice { get; set; }
    public string FormattedPrice => IsDiscountChecked
        ? $"₱{DiscountedPrice:N2}"
        : $"₱{Price:N2}";
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
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.Services;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using QuestPDF.Companion;
using QuestPDF.Fluent;

namespace AHON_TRACK.ViewModels;

[Page("supplier-management")]
public sealed partial class SupplierManagementViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty] private string[] _supplierFilterItems = ["All", "Products", "Drinks", "Supplements"];
    [ObservableProperty] private string _selectedSupplierFilterItem = "All";
    
    [ObservableProperty] private ObservableCollection<Supplier> _supplierItems = [];
    [ObservableProperty] private List<Supplier> _originalSupplierData = [];
    [ObservableProperty] private List<Supplier> _currentFilteredSupplierData = [];
    [ObservableProperty] private Supplier? _selectedSupplier;
    
    [ObservableProperty] private string _searchStringResult = string.Empty; 
    [ObservableProperty] private bool _isInitialized;
    [ObservableProperty] private bool _isSearchingSupplier;
    [ObservableProperty] private bool _selectAll;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _selectedCount; 
    [ObservableProperty] private int _totalCount;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly SupplierDialogCardViewModel _supplierDialogCardViewModel;
    private readonly ISupplierService _supplierService;
    private readonly ILogger _logger;
    private readonly SettingsService _settingsService;
    private AppSettings? _currentSettings;

    public SupplierManagementViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager,
        SupplierDialogCardViewModel supplierDialogCardViewModel, 
        SettingsService settingsService, 
        ISupplierService supplierService,
        ILogger logger)
    {
        _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
        _supplierDialogCardViewModel = supplierDialogCardViewModel ?? throw new ArgumentNullException(nameof(supplierDialogCardViewModel));
        _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SupplierManagementViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _supplierDialogCardViewModel = new SupplierDialogCardViewModel();
        _settingsService = new SettingsService();
        _supplierService = null!;
        _logger = null!;

        LoadSupplierData();
    }

    /*
    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (IsInitialized) return;
        await LoadSettingsAsync();

        if (_supplierService != null)
        {
            await LoadSupplierDataFromDatabaseAsync();
        }
        else
        {
            LoadSupplierData();
        }
        UpdateSupplierCounts();
        IsInitialized = true;
    }
    */
    
    [AvaloniaHotReload]
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
    
        if (IsInitialized)
        {
            _logger?.LogDebug("SupplierManagementViewModel already initialized");
            return;
        }

        _logger?.LogInformation("Initializing SupplierManagementViewModel");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            await LoadSettingsAsync(linkedCts.Token).ConfigureAwait(false);
            await LoadSupplierDataFromDatabaseAsync(linkedCts.Token).ConfigureAwait(false);
            await UpdateSupplierCounts(linkedCts.Token).ConfigureAwait(false);

            IsInitialized = true;
            _logger?.LogInformation("SupplierManagementViewModel initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("SupplierManagementViewModel initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing SupplierManagementViewModel");
            LoadSupplierData(); // Fallback
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Navigating away from SupplierManagement");
        return ValueTask.CompletedTask;
    }

    private async Task LoadSupplierDataFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        try
        {
            var result = await _supplierService.GetAllSuppliersAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (result.Success && result.Suppliers != null)
            {
                foreach (var supplier in OriginalSupplierData)
                {
                    supplier.PropertyChanged -= OnSupplierPropertyChanged;
                }
                
                var suppliers = result.Suppliers.Select(s => new Supplier
                {
                    ID = s.SupplierID,
                    Name = s.SupplierName,
                    ContactPerson = s.ContactPerson,
                    Email = s.Email,
                    PhoneNumber = s.PhoneNumber,
                    Products = s.Products,
                    Status = s.Status,
                    DeliverySchedule = s.DeliverySchedule,
                    ContractTerms = s.ContractTerms,
                    IsSelected = false
                }).ToList();

                OriginalSupplierData = suppliers;
                CurrentFilteredSupplierData = [.. suppliers];

                SupplierItems.Clear();
                foreach (var supplier in suppliers)
                {
                    supplier.PropertyChanged += OnSupplierPropertyChanged;
                    SupplierItems.Add(supplier);
                }

                TotalCount = SupplierItems.Count;

                if (SupplierItems.Count > 0)
                {
                    SelectedSupplier = SupplierItems[0];
                }
                
                _logger?.LogDebug("Loaded {Count} suppliers from database", result.Suppliers.Count);
            }
            else
            {
                _logger?.LogWarning("Failed to load suppliers: {Message}", result.Message);
                _toastManager.CreateToast("Data Load Failed")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowWarning();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading suppliers from database");
            _toastManager.CreateToast("Error Loading Data")
                .WithContent($"Failed to load suppliers: {ex.Message}")
                .DismissOnClick()
                .ShowError();
            LoadSupplierData(); // Fallback
        }
        finally
        {
            IsLoading = false;
        }
    }


    private void LoadSupplierData()
    {
        var sampleSupplier = GetSampleSupplierData();
        OriginalSupplierData = sampleSupplier;
        CurrentFilteredSupplierData = [.. sampleSupplier];

        SupplierItems.Clear();
        foreach (var supplier in sampleSupplier)
        {
            SupplierItems.Add(supplier);
        }
        TotalCount = SupplierItems.Count;

        if (SupplierItems.Count > 0)
        {
            SelectedSupplier = SupplierItems[0];
        }
    }

    private List<Supplier> GetSampleSupplierData()
    {
        return
        [
            new Supplier
            {
                ID = 1001,
                Name = "San Miguel",
                ContactPerson = "Rodolfo Morales",
                Email = "rodolfo.morales21@gmail.com",
                PhoneNumber = "09182938475",
                Products = "Drinks",
                Status = "Active"
            },
            new Supplier
            {
                ID = 1002,
                Name = "AHON Factory",
                ContactPerson = "Joel Abalos",
                Email = "joel.abalos@gmail.com",
                PhoneNumber = "09382293009",
                Products = "Products",
                Status = "Inactive"
            },
            new Supplier
            {
                ID = 1003,
                Name = "Optimum",
                ContactPerson = "Mr. Lopez",
                Email = "ignacio.lopez@gmail.com",
                PhoneNumber = "09339948293",
                Products = "Supplements",
                Status = "Suspended"
            },
            new Supplier
            {
                ID = 1004,
                Name = "Athlene",
                ContactPerson = "Mr. Rome Malubag",
                Email = "rome.malubag@friendster.com",
                PhoneNumber = "09223849981",
                Products = "Supplements",
                Status = "Inactive"
            },
            new Supplier
            {
                ID = 1005,
                Name = "Jump Manila",
                ContactPerson = "Arnold Demakapitan",
                Email = "arnold.demakapitan@yahoo.com",
                PhoneNumber = "09223849981",
                Products = "Accessories",
                Status = "Active"
            }
        ];
    }

    [RelayCommand]
    private async Task ShowAddSupplierDialog()
    {
        await _supplierDialogCardViewModel.InitializeAsync().ConfigureAwait(false);
        _dialogManager.CreateDialog(_supplierDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                var newSupplier = new SupplierManagementModel
                {
                    SupplierName = _supplierDialogCardViewModel.SupplierName,
                    ContactPerson = _supplierDialogCardViewModel.ContactPerson,
                    Email = _supplierDialogCardViewModel.Email,
                    PhoneNumber = _supplierDialogCardViewModel.PhoneNumber,
                    Products = _supplierDialogCardViewModel.Products,
                    Status = _supplierDialogCardViewModel.Status ?? "Active",
                    DeliverySchedule = _supplierDialogCardViewModel.DeliverySchedule,
                    DeliveryPattern = _supplierDialogCardViewModel.DeliveryPattern,
                    ContractTerms = _supplierDialogCardViewModel.ContractTerms,
                    ContractPattern = _supplierDialogCardViewModel.ContractPattern
                };

                var result = await _supplierService.AddSupplierAsync(newSupplier);

                if (result.Success)
                {
                    await LoadSupplierDataFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);

                    _toastManager.CreateToast("Supplier Added Successfully")
                        .WithContent($"Successfully added '{newSupplier.SupplierName}' to the database!")
                        .DismissOnClick()
                        .ShowSuccess();
                }
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new supplier contact cancelled")
                    .WithContent("If you want to add a new supplier contact, please try again.")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(650)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowEditSupplierDialog(Supplier? supplier)
    {
        if (supplier == null) return;

        _supplierDialogCardViewModel.InitializeForEditMode(supplier);
        _dialogManager.CreateDialog(_supplierDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                var updatedSupplier = new SupplierManagementModel
                {
                    SupplierID = supplier.ID ?? 0,
                    SupplierName = _supplierDialogCardViewModel.SupplierName,
                    ContactPerson = _supplierDialogCardViewModel.ContactPerson,
                    Email = _supplierDialogCardViewModel.Email,
                    PhoneNumber = _supplierDialogCardViewModel.PhoneNumber,
                    Products = _supplierDialogCardViewModel.Products,
                    Status = _supplierDialogCardViewModel.Status ?? "Active",
                    DeliverySchedule = _supplierDialogCardViewModel.DeliverySchedule,
                    DeliveryPattern = _supplierDialogCardViewModel.DeliveryPattern,
                    ContractTerms = _supplierDialogCardViewModel.ContractTerms,
                    ContractPattern = _supplierDialogCardViewModel.ContractPattern
                };

                var result = await _supplierService.UpdateSupplierAsync(updatedSupplier);

                if (result.Success)
                {
                    await LoadSupplierDataFromDatabaseAsync(LifecycleToken).ConfigureAwait(false);

                    _toastManager.CreateToast("Supplier Updated")
                        .WithContent($"Successfully updated '{updatedSupplier.SupplierName}'!")
                        .DismissOnClick()
                        .ShowSuccess();
                }
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Modifying supplier Details Cancelled")
                    .WithContent($"Try again if you really want to modify {supplier.Name}'s details")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(950)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowSingleItemDeletionDialog(Supplier? supplier)
    {
        if (supplier == null) return;

        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete {supplier.Name} and remove the data from your database.")
            .WithPrimaryButton("Continue", async () => await OnSubmitDeleteSingleItem(supplier), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowMultipleItemDeletionDialog()
    {
        var selectedSuppliers = SupplierItems.Where(s => s.IsSelected).ToList();

        if (selectedSuppliers.Count == 0)
        {
            _toastManager.CreateToast("No Selection")
                .WithContent("Please select at least one supplier to delete.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete {selectedSuppliers.Count} supplier(s) and remove their data from your database.")
            .WithPrimaryButton("Continue", async () => await OnSubmitDeleteMultipleItems(), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private async Task SearchSupplier(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SearchStringResult))
        {
            SupplierItems.Clear();
            foreach (var equipment in CurrentFilteredSupplierData)
            {
                equipment.PropertyChanged += OnSupplierPropertyChanged;
                SupplierItems.Add(equipment);
            }
            _ = UpdateSupplierCounts(LifecycleToken);
            return;
        }

        IsSearchingSupplier = true;

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(LifecycleToken, cancellationToken);

            await Task.Delay(500, linkedCts.Token).ConfigureAwait(false);

            var filteredSuppliers = CurrentFilteredSupplierData.Where(supplier =>
                    supplier is
                    {
                        Name: not null, ContactPerson: not null, Email: not null,
                        PhoneNumber: not null, Products: not null, Status: not null
                    } && (supplier.Name.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                          supplier.ContactPerson.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                          supplier.Email.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                          supplier.PhoneNumber.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                          supplier.Products.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                          supplier.Status.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            SupplierItems.Clear();
            foreach (var supplier in filteredSuppliers)
            {
                supplier.PropertyChanged += OnSupplierPropertyChanged;
                SupplierItems.Add(supplier);
            }

            _ = UpdateSupplierCounts(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        finally
        {
            IsSearchingSupplier = false;
        }
    }

    [RelayCommand]
    private async Task ExportSupplierList()
    {
        try
        {
            // Check if there are any supplier list to export
            if (SupplierItems.Count == 0)
            {
                _toastManager.CreateToast("No supplier list to export")
                    .WithContent("There are no supplier list available for the selected filter.")
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

            var fileName = $"Supplier_List_{DateTime.Today:yyyy-MM-dd}.pdf";
            var pdfFile = await toplevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Download Supplier List",
                SuggestedStartLocation = startLocation,
                FileTypeChoices = [FilePickerFileTypes.Pdf],
                SuggestedFileName = fileName,
                ShowOverwritePrompt = true
            });

            if (pdfFile == null) return;

            var supplierModel = new SupplierDocumentModel
            {
                GeneratedDate = DateTime.Today,
                GymName = "AHON Victory Fitness Gym",
                GymAddress = "2nd Flr. Event Hub, Victory Central Mall, Brgy. Balibago, Sta. Rosa City, Laguna",
                GymPhone = "+63 123 456 7890",
                GymEmail = "info@ahonfitness.com",
                Items = SupplierItems.Select(supplier => new SupplierItem
                {
                    ID = supplier.ID ?? 0,
                    Name = supplier.Name,
                    ContactPerson = supplier.ContactPerson,
                    Email = supplier.Email,
                    PhoneNumber = supplier.PhoneNumber,
                    Products = supplier.Products,
                    Status = supplier.Status,
                    DeliverySchedule = supplier.DeliverySchedule,
                    ContractTerms = supplier.ContractTerms
                }).ToList()
            };

            var document = new SupplierDocument(supplierModel);

            await using var stream = await pdfFile.OpenWriteAsync();

            // Both cannot be enabled at the same time. Disable one of them 
            document.GeneratePdf(stream); // Generate the PDF
            // await document.ShowInCompanionAsync(); // For Hot-Reload Debugging

            _toastManager.CreateToast("Supplier list exported successfully")
                .WithContent($"Supplier list has been saved to {pdfFile.Name}")
                .DismissOnClick()
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Export failed")
                .WithContent($"Failed to export supplier list: {ex.Message}")
                .DismissOnClick()
                .ShowError();
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

    private void ApplySupplierFilter()
    {
        if (OriginalSupplierData.Count == 0) return;
        List<Supplier> filteredList;

        if (SelectedSupplierFilterItem == "All")
        {
            filteredList = OriginalSupplierData.ToList();
        }
        else
        {
            filteredList = OriginalSupplierData
                .Where(equipment => equipment.Products == SelectedSupplierFilterItem)
                .ToList();
        }
        CurrentFilteredSupplierData = filteredList;

        SupplierItems.Clear();
        foreach (var supplier in filteredList)
        {
            supplier.PropertyChanged += OnSupplierPropertyChanged;
            SupplierItems.Add(supplier);
        }
        _ = UpdateSupplierCounts(LifecycleToken);
    }

    [RelayCommand]
    private void ToggleSelection(bool? isChecked)
    {
        var shouldSelect = isChecked ?? false;

        foreach (var equipmentItem in SupplierItems)
        {
            equipmentItem.IsSelected = shouldSelect;
        }
        _ = UpdateSupplierCounts(LifecycleToken);
    }

    private async Task OnSubmitDeleteSingleItem(Supplier supplier)
    {
        if (supplier.ID == null) return;

        try
        {
            var result = await _supplierService.DeleteSupplierAsync(supplier.ID.Value).ConfigureAwait(false);

            if (result.Success)
            {
                supplier.PropertyChanged -= OnSupplierPropertyChanged;
                SupplierItems.Remove(supplier);
                OriginalSupplierData.Remove(supplier);
                CurrentFilteredSupplierData.Remove(supplier);
                await UpdateSupplierCounts(LifecycleToken).ConfigureAwait(false);

                _toastManager.CreateToast("Supplier Deleted")
                    .WithContent($"{supplier.Name} has been deleted successfully!")
                    .DismissOnClick()
                    .WithDelay(6)
                    .ShowSuccess();
                
                _logger?.LogInformation("Deleted supplier {SupplierId}", supplier.ID.Value);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting supplier {SupplierId}", supplier.ID);
        }
    }

    private async Task OnSubmitDeleteMultipleItems()
    {
        var selectedSuppliers = SupplierItems.Where(item => item.IsSelected).ToList();
        if (selectedSuppliers.Count == 0) return;

        try
        {
            var supplierIds = selectedSuppliers
                .Where(s => s.ID.HasValue)
                .Select(s => s.ID!.Value)
                .ToList();

            var result = await _supplierService.DeleteMultipleSuppliersAsync(supplierIds)
                .ConfigureAwait(false);

            if (result.Success)
            {
                foreach (var supplier in selectedSuppliers)
                {
                    supplier.PropertyChanged -= OnSupplierPropertyChanged;
                    SupplierItems.Remove(supplier);
                    OriginalSupplierData.Remove(supplier);
                    CurrentFilteredSupplierData.Remove(supplier);
                }
                await UpdateSupplierCounts(LifecycleToken).ConfigureAwait(false);

                _toastManager.CreateToast("Suppliers Deleted")
                    .WithContent($"{result.DeletedCount} supplier(s) deleted successfully!")
                    .DismissOnClick()
                    .WithDelay(6)
                    .ShowSuccess();
                
                _logger?.LogInformation("Deleted {Count} suppliers", supplierIds.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting multiple suppliers");
        }
    }

    private async Task UpdateSupplierCounts(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Yield(); // Ensure async context
            SelectedCount = SupplierItems.Count(x => x.IsSelected);
            TotalCount = SupplierItems.Count;
            SelectAll = SupplierItems.Count > 0 && SupplierItems.All(x => x.IsSelected);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error updating supplier counts");
        }
    }

    private void OnSupplierPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Supplier.IsSelected))
        {
            _ = UpdateSupplierCounts(LifecycleToken);
        }
    }

    partial void OnSearchStringResultChanged(string value)
    {
        SearchSupplierCommand.Execute(null);
    }

    partial void OnSelectedSupplierFilterItemChanged(string value)
    {
        ApplySupplierFilter();
    }
    
    protected override async ValueTask DisposeAsyncCore()
    {
        _logger?.LogInformation("Disposing SupplierManagementViewModel");

        // Unsubscribe from supplier property changes
        foreach (var supplier in SupplierItems)
        {
            supplier.PropertyChanged -= OnSupplierPropertyChanged;
        }
        foreach (var supplier in OriginalSupplierData)
        {
            supplier.PropertyChanged -= OnSupplierPropertyChanged;
        }

        // Clear collections
        SupplierItems.Clear();
        OriginalSupplierData.Clear();
        CurrentFilteredSupplierData.Clear();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}

public partial class Supplier : ObservableObject
{
    [ObservableProperty]
    private int? _iD;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _contactPerson;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _phoneNumber;

    [ObservableProperty]
    private string? _products;

    [ObservableProperty]
    private string? _deliverySchedule;

    [ObservableProperty]
    private string? _contractTerms;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private bool _isSelected;

    public IBrush StatusForeground => Status?.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),     // Green-500
        "inactive" => new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Gray-500
        "suspended" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red-500
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))           // Default Gray-500
    };

    public IBrush StatusBackground => Status?.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),     // Green-500 with alpha
        "inactive" => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139)), // Gray-500 with alpha
        "suspended" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)), // Red-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))           // Default Gray-500 with alpha
    };

    public string? StatusDisplayText => Status?.ToLowerInvariant() switch
    {
        "active" => "● Active",
        "inactive" => "● Inactive",
        "suspended" => "● Suspended",
        _ => Status
    };

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusDisplayText));
    }
}
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using QuestPDF.Companion;
using QuestPDF.Fluent;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Notification = AHON_TRACK.Models.Notification;

namespace AHON_TRACK.ViewModels;

[Page("equipment-inventory")]
public sealed partial class EquipmentInventoryViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _equipmentFilterItems = ["All", "Strength", "Cardio", "Machines", "Accessories"];

    [ObservableProperty]
    private string[] _conditionFilterItems = ["All", "Excellent", "Repairing", "Broken"];

    [ObservableProperty]
    private string[] _statusFilterItems = ["All", "Available", "Not Available"];

    [ObservableProperty]
    private string[] _filteredStatusFilterItems = ["All", "Available", "Not Available"];

    [ObservableProperty]
    private string _selectedConditionFilterItem = "All";

    [ObservableProperty]
    private string _selectedStatusFilterItem = "All";

    [ObservableProperty]
    private string _selectedEquipmentFilterItem = "All";

    [ObservableProperty]
    private ObservableCollection<Equipment> _equipmentItems = [];

    [ObservableProperty]
    private List<Equipment> _originalEquipmentData = [];

    [ObservableProperty]
    private List<Equipment> _currentFilteredEquipmentData = [];

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private string _searchStringResult = string.Empty;

    [ObservableProperty]
    private bool _isSearchingEquipment;

    [ObservableProperty]
    private bool _selectAll;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private Equipment? _selectedEquipment;

    [ObservableProperty]
    private bool _isLoadingData;

    private bool _isLoadingDataFlag = false;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly EquipmentDialogCardViewModel _equipmentDialogCardViewModel;
    private readonly IInventoryService _inventoryService;
    private readonly SettingsService _settingsService;
    private AppSettings? _currentSettings;

    public EquipmentInventoryViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,
        EquipmentDialogCardViewModel equipmentDialogCardViewModel, SettingsService settingsService, IInventoryService inventoryService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _equipmentDialogCardViewModel = equipmentDialogCardViewModel;
        _inventoryService = inventoryService;
        _settingsService = settingsService;

        SelectedEquipmentFilterItem = "All";
        SelectedConditionFilterItem = "All";
        SelectedStatusFilterItem = "All";
        FilteredStatusFilterItems = StatusFilterItems;

        SubscribeToEvents();
        _ = LoadEquipmentDataAsync();
        UpdateEquipmentCounts();
    }

    public EquipmentInventoryViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _equipmentDialogCardViewModel = new EquipmentDialogCardViewModel();
        _settingsService = new SettingsService();
        _inventoryService = null!;

        SelectedEquipmentFilterItem = "All";
        SelectedConditionFilterItem = "All";
        SelectedStatusFilterItem = "All";
        FilteredStatusFilterItems = StatusFilterItems;

        SubscribeToEvents();
        _ = LoadEquipmentDataAsync();
        UpdateEquipmentCounts();
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (IsInitialized) return;
        SubscribeToEvents();
        await LoadSettingsAsync();

        SelectedEquipmentFilterItem = "All";
        SelectedConditionFilterItem = "All";
        SelectedStatusFilterItem = "All";
        FilteredStatusFilterItems = StatusFilterItems;

        _ = LoadEquipmentDataAsync();
        IsInitialized = true;
    }

    private void SubscribeToEvents()
    {
        var eventService = DashboardEventService.Instance;

        // Unsubscribe first to prevent double subscription
        eventService.EquipmentAdded -= OnEquipmentDataChanged;
        eventService.EquipmentUpdated -= OnEquipmentDataChanged;
        eventService.EquipmentDeleted -= OnEquipmentDataChanged;

        // Subscribe to events
        eventService.EquipmentAdded += OnEquipmentDataChanged;
        eventService.EquipmentUpdated += OnEquipmentDataChanged;
        eventService.EquipmentDeleted += OnEquipmentDataChanged;

        Console.WriteLine($"[SubscribeToEvents] ✅ Subscribed to equipment events");
    }

    private async Task LoadEquipmentDataAsync()
    {
        Console.WriteLine($"[LoadEquipmentDataAsync] 📊 START - _isLoadingDataFlag: {_isLoadingDataFlag}");

        if (_inventoryService == null)
        {
            Console.WriteLine("[LoadEquipmentDataAsync] ❌ _inventoryService is NULL!");
            return;
        }

        // ❌ REMOVE THIS DUPLICATE CHECK - it's preventing reloads!
        // if (_isLoadingDataFlag) return;

        // ✅ Set flags
        _isLoadingDataFlag = true;
        IsLoadingData = true;

        try
        {
            Console.WriteLine("[LoadEquipmentDataAsync] 🔍 Calling GetEquipmentAsync()...");

            var (success, message, equipmentModels) = await _inventoryService.GetEquipmentAsync();

            Console.WriteLine($"[LoadEquipmentDataAsync] GetEquipmentAsync returned - Success: {success}, Count: {equipmentModels?.Count ?? 0}");

            if (!success || equipmentModels == null)
            {
                Console.WriteLine($"[LoadEquipmentDataAsync] ❌ Failed: {message}");
                _toastManager.CreateToast("Error Loading Equipment")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            Console.WriteLine($"[LoadEquipmentDataAsync] ✅ Got {equipmentModels.Count} items from database");

            // Check if Root beer is in the results
            var rootBeerInDb = equipmentModels.FirstOrDefault(e => e.EquipmentName.Contains("Root beer"));
            if (rootBeerInDb != null)
            {
                Console.WriteLine($"[LoadEquipmentDataAsync] 🍺 Root beer in DB: Qty={rootBeerInDb.Quantity}");
            }

            var equipmentList = equipmentModels.Select(MapToEquipment).ToList();

            // Check mapped data
            var rootBeerMapped = equipmentList.FirstOrDefault(e => e.BrandName?.Contains("Root beer") == true);
            if (rootBeerMapped != null)
            {
                Console.WriteLine($"[LoadEquipmentDataAsync] 🍺 Root beer after mapping: Qty={rootBeerMapped.Quantity}");
            }

            OriginalEquipmentData = equipmentList;
            CurrentFilteredEquipmentData = [.. equipmentList];

            Console.WriteLine($"[LoadEquipmentDataAsync] 📋 Updated data collections");

            // ✅ CRITICAL: Unsubscribe before clearing
            Console.WriteLine($"[LoadEquipmentDataAsync] 🧹 Cleaning {EquipmentItems.Count} old items...");
            foreach (var item in EquipmentItems)
            {
                item.PropertyChanged -= OnEquipmentPropertyChanged;
            }

            EquipmentItems.Clear();
            Console.WriteLine($"[LoadEquipmentDataAsync] 🗑️ Cleared EquipmentItems");

            foreach (var equipment in equipmentList)
            {
                equipment.PropertyChanged += OnEquipmentPropertyChanged;
                EquipmentItems.Add(equipment);
            }

            Console.WriteLine($"[LoadEquipmentDataAsync] ➕ Added {EquipmentItems.Count} items to EquipmentItems");

            TotalCount = EquipmentItems.Count;

            if (EquipmentItems.Count > 0)
            {
                SelectedEquipment = EquipmentItems[0];
            }

            // ✅ Check if Root beer is in EquipmentItems
            var rootBeerInItems = EquipmentItems.FirstOrDefault(e => e.BrandName?.Contains("Root beer") == true);
            if (rootBeerInItems != null)
            {
                Console.WriteLine($"[LoadEquipmentDataAsync] 🍺 Root beer in EquipmentItems: Qty={rootBeerInItems.Quantity}");
            }

            ApplyEquipmentFilter();
            UpdateEquipmentCounts();

            Console.WriteLine($"[LoadEquipmentDataAsync] ✅ COMPLETE - Final count: {EquipmentItems.Count}");

            await _inventoryService.ShowEquipmentAlertsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadEquipmentDataAsync] ❌ EXCEPTION: {ex.Message}");
            Console.WriteLine($"[LoadEquipmentDataAsync] Stack: {ex.StackTrace}");

            _toastManager.CreateToast("Error Loading Equipment")
                .WithContent($"Failed to load equipment data: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoadingData = false;
            _isLoadingDataFlag = false;
            Console.WriteLine($"[LoadEquipmentDataAsync] 🏁 Flags reset - _isLoadingDataFlag: {_isLoadingDataFlag}");
        }
    }

    /// <summary>
    /// Maps EquipmentModel (from database) to Equipment (UI model)
    /// </summary>
    private Equipment MapToEquipment(EquipmentModel model)
    {
        return new Equipment
        {
            ID = model.EquipmentID,
            BrandName = model.EquipmentName,
            Category = model.Category,
            Quantity = model.Quantity,
            SupplierID = model.SupplierID,  // Store the ID
            SupplierName = model.SupplierName,  // Display name from JOIN
            PurchasedPrice = model.PurchasePrice,
            PurchasedDate = model.PurchaseDate,
            Warranty = model.WarrantyExpiry,
            Condition = model.Condition,
            Status = model.Status,
            LastMaintenance = model.LastMaintenance,
            NextMaintenance = model.NextMaintenance
        };
    }

    /// <summary>
    /// Maps Equipment (UI model) to EquipmentModel (for database)
    /// </summary>
    private EquipmentModel MapToEquipmentModel(Equipment equipment)
    {
        return new EquipmentModel
        {
            EquipmentID = equipment.ID,
            EquipmentName = equipment.BrandName ?? string.Empty,
            Category = equipment.Category ?? string.Empty,
            Quantity = equipment.Quantity ?? 0,
            SupplierID = equipment.SupplierID,  // Use the ID, not the name
            PurchasePrice = equipment.PurchasedPrice,
            PurchaseDate = equipment.PurchasedDate,
            WarrantyExpiry = equipment.Warranty,
            Condition = equipment.Condition ?? string.Empty,
            Status = equipment.Status ?? "Active",
            LastMaintenance = equipment.LastMaintenance,
            NextMaintenance = equipment.NextMaintenance
        };
    }

    [RelayCommand]
    private void ShowAddEquipmentDialog()
    {
        _equipmentDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_equipmentDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                await AddEquipmentToDatabase();
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new equipment cancelled")
                    .WithContent("If you want to add a new equipment, please try again.")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(650)
            .Dismissible()
            .Show();
    }

    private async Task AddEquipmentToDatabase()
    {
        try
        {
            var newEquipmentModel = new EquipmentModel
            {
                EquipmentName = _equipmentDialogCardViewModel.BrandName ?? string.Empty,
                Category = _equipmentDialogCardViewModel.Category ?? string.Empty,
                Quantity = _equipmentDialogCardViewModel.Quantity ?? 0,
                SupplierID = _equipmentDialogCardViewModel.SupplierID,  // Now using SupplierID
                PurchasePrice = _equipmentDialogCardViewModel.PurchasePrice,
                PurchaseDate = _equipmentDialogCardViewModel.PurchasedDate,
                WarrantyExpiry = _equipmentDialogCardViewModel.WarrantyExpiry,
                Condition = _equipmentDialogCardViewModel.Condition ?? string.Empty,
                Status = _equipmentDialogCardViewModel.Status ?? "Active",
                LastMaintenance = _equipmentDialogCardViewModel.LastMaintenance,
                NextMaintenance = _equipmentDialogCardViewModel.NextMaintenance
            };

            var (success, message, equipmentId) = await _inventoryService.AddEquipmentAsync(newEquipmentModel);

            if (success)
            {
                // Reload data to reflect changes
                await LoadEquipmentDataAsync();
            }
            else
            {
                _toastManager.CreateToast("Error Adding Equipment")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Adding Equipment")
                .WithContent($"Failed to add equipment: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private void ApplyEquipmentFilter()
    {
        Console.WriteLine($"[ApplyEquipmentFilter] Starting... Original: {OriginalEquipmentData.Count}");

        if (OriginalEquipmentData.Count == 0) return;

        List<Equipment> filteredList = OriginalEquipmentData.ToList();

        // Apply Equipment Category filter
        if (SelectedEquipmentFilterItem != "All")
        {
            filteredList = filteredList
                .Where(equipment => equipment.Category == SelectedEquipmentFilterItem)
                .ToList();
        }

        // Apply Condition filter
        if (SelectedConditionFilterItem != "All")
        {
            filteredList = filteredList
                .Where(equipment => equipment.Condition != null &&
                                    equipment.Condition.Equals(SelectedConditionFilterItem, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Apply Status filter
        if (SelectedStatusFilterItem != "All")
        {
            filteredList = filteredList
                .Where(equipment => equipment.Status != null &&
                                    equipment.Status.Equals(SelectedStatusFilterItem, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        Console.WriteLine($"[ApplyEquipmentFilter] After filters: {filteredList.Count}");

        CurrentFilteredEquipmentData = filteredList;

        // ✅ Unsubscribe from old items
        foreach (var item in EquipmentItems)
        {
            item.PropertyChanged -= OnEquipmentPropertyChanged;
        }

        EquipmentItems.Clear();

        foreach (var equipment in filteredList)
        {
            equipment.PropertyChanged += OnEquipmentPropertyChanged;
            EquipmentItems.Add(equipment);
        }

        UpdateEquipmentCounts();

        // ✅ FIX: Force UI refresh
        OnPropertyChanged(nameof(EquipmentItems));

        Console.WriteLine($"[ApplyEquipmentFilter] ✅ Complete - {EquipmentItems.Count} items in view");
    }

    [RelayCommand]
    private void ToggleSelection(bool? isChecked)
    {
        var shouldSelect = isChecked ?? false;

        foreach (var equipmentItem in EquipmentItems)
        {
            equipmentItem.IsSelected = shouldSelect;
        }
        UpdateEquipmentCounts();
    }

    [RelayCommand]
    private async Task SearchEquipment()
    {
        if (string.IsNullOrWhiteSpace(SearchStringResult))
        {
            // ✅ Unsubscribe before clearing
            foreach (var item in EquipmentItems)
            {
                item.PropertyChanged -= OnEquipmentPropertyChanged;
            }

            EquipmentItems.Clear();
            foreach (var equipment in CurrentFilteredEquipmentData)
            {
                equipment.PropertyChanged += OnEquipmentPropertyChanged;
                EquipmentItems.Add(equipment);
            }
            UpdateEquipmentCounts();
            return;
        }

        IsSearchingEquipment = true;

        try
        {
            await Task.Delay(500);

            var filteredEquipments = CurrentFilteredEquipmentData.Where(equipment =>
                equipment is { BrandName: not null, Category: not null, Condition: not null, Status: not null } &&
                (equipment.BrandName.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 equipment.Category.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 equipment.Condition.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 equipment.Status.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 (equipment.SupplierName?.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ?? false)))
                .ToList();

            // ✅ Unsubscribe before clearing
            foreach (var item in EquipmentItems)
            {
                item.PropertyChanged -= OnEquipmentPropertyChanged;
            }

            EquipmentItems.Clear();
            foreach (var equipment in filteredEquipments)
            {
                equipment.PropertyChanged += OnEquipmentPropertyChanged;
                EquipmentItems.Add(equipment);
            }
            UpdateEquipmentCounts();
        }
        finally
        {
            IsSearchingEquipment = false;
        }
    }

    [RelayCommand]
    private async Task ExportEquipmentList()
    {
        try
        {
            // Check if there are any equipment list to export
            if (EquipmentItems.Count == 0)
            {
                _toastManager.CreateToast("No equipment list to export")
                    .WithContent("There are no equipment list available for the selected filter.")
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

            var fileName = $"Equipment_List_{DateTime.Today:yyyy-MM-dd}.pdf";
            var pdfFile = await toplevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Download Equipment List",
                SuggestedStartLocation = startLocation,
                FileTypeChoices = [FilePickerFileTypes.Pdf],
                SuggestedFileName = fileName,
                ShowOverwritePrompt = true
            });

            if (pdfFile == null) return;

            var equipmentModel = new EquipmentDocumentModel
            {
                GeneratedDate = DateTime.Today,
                GymName = "AHON Victory Fitness Gym",
                GymAddress = "2nd Flr. Event Hub, Victory Central Mall, Brgy. Balibago, Sta. Rosa City, Laguna",
                GymPhone = "+63 123 456 7890",
                GymEmail = "info@ahonfitness.com",
                Items = EquipmentItems.Select(equipment => new EquipmentItem
                {
                    ID = equipment.ID,
                    BrandName = equipment.BrandName,
                    Category = equipment.Category,
                    Supplier = equipment.SupplierName,
                    Quantity = equipment.Quantity ?? 0,
                    PurchasedPrice = (int?)equipment.PurchasedPrice ?? 0,
                    PurchasedDate = equipment.PurchasedDate,
                    Warranty = equipment.Warranty,
                    Condition = equipment.Condition,
                    LastMaintenance = equipment.LastMaintenance,
                    NextMaintenance = equipment.NextMaintenance
                }).ToList()
            };

            var document = new EquipmentDocument(equipmentModel);

            await using var stream = await pdfFile.OpenWriteAsync();

            // Both cannot be enabled at the same time. Disable one of them 
            document.GeneratePdf(stream); // Generate the PDF
                                          // await document.ShowInCompanionAsync(); // For Hot-Reload Debugging

            _toastManager.CreateToast("Equipment list exported successfully")
                .WithContent($"Equipment list has been saved to {pdfFile.Name}")
                .DismissOnClick()
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Export failed")
                .WithContent($"Failed to export equipment list: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private void ShowEditEquipmentDialog(Equipment? equipment)
    {
        if (equipment == null) return;

        _equipmentDialogCardViewModel.InitializeForEditMode(equipment);
        _dialogManager.CreateDialog(_equipmentDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                await UpdateEquipmentInDatabase(equipment);
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Modifying Equipment Details Cancelled")
                    .WithContent("Click the three-dots if you want to modify equipment details")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(650)
            .Dismissible()
            .Show();
    }

    private async Task UpdateEquipmentInDatabase(Equipment equipment)
    {
        try
        {
            // ✅ Validate maintenance dates before updating
            if (_equipmentDialogCardViewModel.LastMaintenance.HasValue &&
                _equipmentDialogCardViewModel.NextMaintenance.HasValue &&
                _equipmentDialogCardViewModel.NextMaintenance <= _equipmentDialogCardViewModel.LastMaintenance)
            {
                _toastManager.CreateToast("Invalid Maintenance Dates")
                    .WithContent("Next maintenance date must be after the last maintenance date.")
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            if (_equipmentDialogCardViewModel.LastMaintenance > DateTime.Today)
            {
                _toastManager.CreateToast("Invalid Date")
                    .WithContent("Last maintenance date cannot be in the future.")
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            // Update the equipment object with values from dialog
            equipment.BrandName = _equipmentDialogCardViewModel.BrandName;
            equipment.Category = _equipmentDialogCardViewModel.Category;
            equipment.Quantity = _equipmentDialogCardViewModel.Quantity;
            equipment.SupplierID = _equipmentDialogCardViewModel.SupplierID;  // Update SupplierID
            equipment.SupplierName = _equipmentDialogCardViewModel.Supplier;  // Use Supplier instead of SelectedSupplier
            equipment.PurchasedPrice = _equipmentDialogCardViewModel.PurchasePrice;
            equipment.PurchasedDate = _equipmentDialogCardViewModel.PurchasedDate;
            equipment.Warranty = _equipmentDialogCardViewModel.WarrantyExpiry;
            equipment.Condition = _equipmentDialogCardViewModel.Condition;
            equipment.Status = _equipmentDialogCardViewModel.Status;
            equipment.LastMaintenance = _equipmentDialogCardViewModel.LastMaintenance;
            equipment.NextMaintenance = _equipmentDialogCardViewModel.NextMaintenance;

            var equipmentModel = MapToEquipmentModel(equipment);
            var (success, message) = await _inventoryService.UpdateEquipmentAsync(equipmentModel);

            if (success)
            {
                _ = LoadEquipmentDataAsync();
                _toastManager.CreateToast("Equipment Updated")
                    .WithContent($"Successfully modified {equipment.BrandName}!")
                    .DismissOnClick()
                    .ShowSuccess();

                // Refresh the UI
                OnPropertyChanged(nameof(EquipmentItems));
            }
            else
            {
                _toastManager.CreateToast("Update Failed")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Updating Equipment")
                .WithContent($"Failed to update equipment: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private void ShowSingleItemDeletionDialog(Equipment? equipment)
    {
        if (equipment == null) return;

        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete {equipment.BrandName} and remove the data from your database.")
            .WithPrimaryButton("Continue", () => OnSubmitDeleteSingleItem(equipment), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ShowMultipleItemDeletionDialog(Equipment? equipment)
    {
        if (equipment == null) return;

        var selectedCount = EquipmentItems.Count(x => x.IsSelected);

        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete {selectedCount} equipment item(s) and remove their data from your database.")
            .WithPrimaryButton("Continue", OnSubmitDeleteMultipleItems, DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    private async Task LoadSettingsAsync() => _currentSettings = await _settingsService.LoadSettingsAsync();

    private async Task OnSubmitDeleteSingleItem(Equipment equipment)
    {
        try
        {
            var (success, message) = await _inventoryService.DeleteEquipmentAsync(equipment.ID);

            if (success)
            {
                equipment.PropertyChanged -= OnEquipmentPropertyChanged;
                EquipmentItems.Remove(equipment);
                OriginalEquipmentData.Remove(equipment);
                CurrentFilteredEquipmentData.Remove(equipment);
                UpdateEquipmentCounts();

                _toastManager.CreateToast("Equipment Deleted")
                    .WithContent($"{equipment.BrandName} has been deleted successfully!")
                    .DismissOnClick()
                    .WithDelay(6)
                    .ShowSuccess();
            }
            else
            {
                _toastManager.CreateToast("Delete Failed")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Deleting Equipment")
                .WithContent($"Failed to delete equipment: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private async Task OnSubmitDeleteMultipleItems()
    {
        var selectedEquipments = EquipmentItems.Where(item => item.IsSelected).ToList();
        if (selectedEquipments.Count == 0)
        {
            _toastManager.CreateToast("No Selection")
                .WithContent("Please select equipment items to delete.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        try
        {
            var equipmentIds = selectedEquipments.Select(e => e.ID).ToList();
            var (success, message, deletedCount) = await _inventoryService.DeleteMultipleEquipmentAsync(equipmentIds);

            if (success)
            {
                foreach (var equipmentItem in selectedEquipments)
                {
                    equipmentItem.PropertyChanged -= OnEquipmentPropertyChanged;
                    EquipmentItems.Remove(equipmentItem);
                    OriginalEquipmentData.Remove(equipmentItem);
                    CurrentFilteredEquipmentData.Remove(equipmentItem);
                }
                UpdateEquipmentCounts();

                _toastManager.CreateToast("Equipment Deleted")
                    .WithContent($"Successfully deleted {deletedCount} equipment item(s)!")
                    .DismissOnClick()
                    .WithDelay(6)
                    .ShowSuccess();
            }
            else
            {
                _toastManager.CreateToast("Delete Failed")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowError();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Deleting Equipment")
                .WithContent($"Failed to delete equipment: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private void UpdateFilteredStatusItems()
    {
        if (SelectedConditionFilterItem == "All")
        {
            FilteredStatusFilterItems = StatusFilterItems;
        }
        else
        {
            var allowedStatuses = SelectedConditionFilterItem switch
            {
                "Excellent" => new[] { "All", "Available" },
                "Repairing" => new[] { "All", "Not Available" },
                "Broken" => new[] { "All", "Not Available" },
                _ => StatusFilterItems
            };

            FilteredStatusFilterItems = allowedStatuses;

            // Reset status if current selection is not in the filtered list
            if (!FilteredStatusFilterItems.Contains(SelectedStatusFilterItem))
            {
                SelectedStatusFilterItem = "All";
            }
        }
    }

    public bool CanDeleteSelectedEquipments
    {
        get
        {
            var selectedEquipments = EquipmentItems.Where(item => item.IsSelected).ToList();
            if (selectedEquipments.Count == 0) return false;

            if (SelectedEquipment?.Status != null &&
                !SelectedEquipment.Status.Equals("Not Available", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return selectedEquipments.All(equipment
                => equipment.Status != null &&
                   equipment.Status.Equals("Not Available", StringComparison.OrdinalIgnoreCase));
        }
    }

    private void UpdateEquipmentCounts()
    {
        SelectedCount = EquipmentItems.Count(x => x.IsSelected);
        TotalCount = EquipmentItems.Count;

        SelectAll = EquipmentItems.Count > 0 && EquipmentItems.All(x => x.IsSelected);
    }

    private void OnEquipmentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Equipment.IsSelected))
        {
            UpdateEquipmentCounts();
        }
    }

    partial void OnSearchStringResultChanged(string value)
    {
        SearchEquipmentCommand.Execute(null);
    }

    partial void OnSelectedEquipmentFilterItemChanged(string value)
    {
        ApplyEquipmentFilter();
    }

    partial void OnSelectedEquipmentChanged(Equipment? oldValue, Equipment? newValue)
    {
        OnPropertyChanged(nameof(CanDeleteSelectedEquipments));
    }

    partial void OnSelectedConditionFilterItemChanged(string value)
    {
        UpdateFilteredStatusItems();
        ApplyEquipmentFilter();
    }

    partial void OnSelectedStatusFilterItemChanged(string value)
    {
        ApplyEquipmentFilter();
    }

    private async void OnEquipmentDataChanged(object? sender, EventArgs e)
    {
        Console.WriteLine($"[OnEquipmentDataChanged] Event received! IsLoadingDataFlag: {_isLoadingDataFlag}");

        if (_isLoadingDataFlag)
        {
            Console.WriteLine("[OnEquipmentDataChanged] Already loading, skipping...");
            return;
        }

        try
        {
            Console.WriteLine("[OnEquipmentDataChanged] Starting reload...");
            _isLoadingDataFlag = true;

            // Small delay to debounce rapid events
            await Task.Delay(100);

            // ✅ FIX: Ensure we're on UI thread
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await LoadEquipmentDataAsync();
            });

            Console.WriteLine("[OnEquipmentDataChanged] ✅ Reload complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnEquipmentDataChanged] ❌ Error: {ex.Message}");

            // ✅ Show toast on UI thread
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _toastManager?.CreateToast("Error Reloading Equipment")
                    .WithContent($"Failed to reload: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
            });
        }
        finally
        {
            _isLoadingDataFlag = false;
        }
    }

    // Add to EquipmentInventoryViewModel class

    protected override void DisposeManagedResources()
    {
        Console.WriteLine("[DisposeManagedResources] Cleaning up...");

        var eventService = DashboardEventService.Instance;
        eventService.EquipmentAdded -= OnEquipmentDataChanged;
        eventService.EquipmentUpdated -= OnEquipmentDataChanged;
        eventService.EquipmentDeleted -= OnEquipmentDataChanged;

        // Unsubscribe from property change handlers
        foreach (var equipment in EquipmentItems)
        {
            equipment.PropertyChanged -= OnEquipmentPropertyChanged;
        }

        // Clear collections
        EquipmentItems?.Clear();
        OriginalEquipmentData?.Clear();
        CurrentFilteredEquipmentData?.Clear();

        base.DisposeManagedResources();
    }
}

public partial class Equipment : ObservableObject
{
    [ObservableProperty]
    private int _iD;

    [ObservableProperty]
    private string? _brandName;

    [ObservableProperty]
    private string? _category;

    [ObservableProperty]
    private int? _supplierID;  // Store the ID

    [ObservableProperty]
    private string? _supplierName;  // Display name from JOIN

    [ObservableProperty]
    private int? _quantity;

    [ObservableProperty]
    private decimal? _purchasedPrice;

    [ObservableProperty]
    private DateTime? _purchasedDate;

    [ObservableProperty]
    private DateTime? _warranty;

    [ObservableProperty]
    private string? _condition;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private DateTime? _lastMaintenance;

    [ObservableProperty]
    private DateTime? _nextMaintenance;

    [ObservableProperty]
    private bool _isSelected;

    // Formatted properties for display
    public string FormattedWarranty => Warranty.HasValue ? $"{Warranty.Value:MM/dd/yyyy}" : string.Empty;
    public string FormattedPurchasedDate => PurchasedDate.HasValue ? $"{PurchasedDate.Value:MM/dd/yyyy}" : string.Empty;
    public string FormattedLastMaintenance => LastMaintenance.HasValue ? $"{LastMaintenance.Value:MM/dd/yyyy}" : string.Empty;
    public string FormattedNextMaintenance => NextMaintenance.HasValue ? $"{NextMaintenance.Value:MM/dd/yyyy}" : string.Empty;
    public string FormattedPurchasedPrice => PurchasedPrice.HasValue ? $"₱{PurchasedPrice.Value:N2}" : "₱0.00";

    // Display supplier name, fallback to "N/A" if null
    public string DisplaySupplierName => SupplierName ?? "N/A";

    // Condition styling
    public IBrush ConditionForeground => Condition?.ToLowerInvariant() switch
    {
        "excellent" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        "repairing" => new SolidColorBrush(Color.FromRgb(251, 146, 60)),
        "broken" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
    };

    public IBrush ConditionBackground => Condition?.ToLowerInvariant() switch
    {
        "excellent" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),
        "repairing" => new SolidColorBrush(Color.FromArgb(25, 251, 146, 60)),
        "broken" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))
    };

    public string? ConditionDisplayText => Condition?.ToLowerInvariant() switch
    {
        "excellent" => "● Excellent",
        "repairing" => "● Repairing",
        "broken" => "● Broken",
        _ => Condition
    };

    // Status styling
    public IBrush StatusForeground => Status?.ToLowerInvariant() switch
    {
        "available" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        "not available" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
    };

    public IBrush StatusBackground => Status?.ToLowerInvariant() switch
    {
        "available" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),
        "not available" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))
    };

    public string? StatusDisplayText => Status?.ToLowerInvariant() switch
    {
        "available" => "● Available",
        "not available" => "● Not Available",
        _ => Status
    };

    partial void OnConditionChanged(string? value)
    {
        OnPropertyChanged(nameof(ConditionForeground));
        OnPropertyChanged(nameof(ConditionBackground));
        OnPropertyChanged(nameof(ConditionDisplayText));
    }

    partial void OnStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusDisplayText));
    }

    partial void OnSupplierNameChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplaySupplierName));
    }
}
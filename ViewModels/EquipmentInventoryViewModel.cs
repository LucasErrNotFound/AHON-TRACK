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

namespace AHON_TRACK.ViewModels;

[Page("equipment-inventory")]
public sealed partial class EquipmentInventoryViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _equipmentFilterItems = ["All", "Strength", "Cardio", "Machines", "Accessories"];

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

        _ = LoadEquipmentDataAsync();
        UpdateEquipmentCounts();
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        if (IsInitialized) return;
        await LoadSettingsAsync();
        _ = LoadEquipmentDataAsync();
        IsInitialized = true;
    }


    private async Task LoadEquipmentDataAsync()
    {
        if (_inventoryService == null) return;

        IsLoadingData = true;
        try
        {
            var (success, message, equipmentModels) = await _inventoryService.GetEquipmentAsync();

            if (!success || equipmentModels == null)
            {
                _toastManager.CreateToast("Error Loading Equipment")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            var equipmentList = equipmentModels.Select(MapToEquipment).ToList();

            OriginalEquipmentData = equipmentList;
            CurrentFilteredEquipmentData = [.. equipmentList];

            EquipmentItems.Clear();
            foreach (var equipment in equipmentList)
            {
                equipment.PropertyChanged += OnEquipmentPropertyChanged;
                EquipmentItems.Add(equipment);
            }
            TotalCount = EquipmentItems.Count;

            if (EquipmentItems.Count > 0)
            {
                SelectedEquipment = EquipmentItems[0];
            }

            ApplyEquipmentFilter();
            UpdateEquipmentCounts();
            await _inventoryService.ShowEquipmentAlertsAsync();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Loading Equipment")
                .WithContent($"Failed to load equipment data: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoadingData = false;
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
            CurrentStock = model.CurrentStock,
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
            CurrentStock = equipment.CurrentStock ?? 0,
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
                CurrentStock = _equipmentDialogCardViewModel.CurrentStock ?? 0,
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
        if (OriginalEquipmentData.Count == 0) return;
        List<Equipment> filteredList;

        if (SelectedEquipmentFilterItem == "All")
        {
            filteredList = OriginalEquipmentData.ToList();
        }
        else
        {
            filteredList = OriginalEquipmentData
                .Where(equipment => equipment.Category == SelectedEquipmentFilterItem)
                .ToList();
        }
        CurrentFilteredEquipmentData = filteredList;

        EquipmentItems.Clear();
        foreach (var equipment in filteredList)
        {
            equipment.PropertyChanged += OnEquipmentPropertyChanged;
            EquipmentItems.Add(equipment);
        }
        UpdateEquipmentCounts();
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
                equipment is { BrandName: not null, Category: not null, Condition: not null } &&
                (equipment.BrandName.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 equipment.Category.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 equipment.Condition.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 (equipment.SupplierName?.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ?? false)))
                .ToList();

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
                    CurrentStock = equipment.CurrentStock ?? 0,
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
            equipment.CurrentStock = _equipmentDialogCardViewModel.CurrentStock;
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
    
    // Add to EquipmentInventoryViewModel class

    protected override void DisposeManagedResources()
    {
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
    private int? _currentStock;

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
        "active" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        "inactive" => new SolidColorBrush(Color.FromRgb(100, 116, 139)),
        "under maintenance" => new SolidColorBrush(Color.FromRgb(251, 146, 60)),
        "retired" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        "on loan" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
    };

    public IBrush StatusBackground => Status?.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),
        "inactive" => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139)),
        "under maintenance" => new SolidColorBrush(Color.FromArgb(25, 251, 146, 60)),
        "retired" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)),
        "on loan" => new SolidColorBrush(Color.FromArgb(25, 59, 130, 246)),
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))
    };

    public string? StatusDisplayText => Status?.ToLowerInvariant() switch
    {
        "active" => "● Active",
        "inactive" => "● Inactive",
        "under maintenance" => "● Under Maintenance",
        "retired" => "● Retired",
        "on loan" => "● On Loan",
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
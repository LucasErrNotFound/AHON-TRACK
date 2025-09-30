using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;


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

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly EquipmentDialogCardViewModel _equipmentDialogCardViewModel;
    private readonly ISystemService _systemService;

    public EquipmentInventoryViewModel(
        DialogManager dialogManager,
        ToastManager toastManager,
        PageManager pageManager,
        EquipmentDialogCardViewModel equipmentDialogCardViewModel,
        ISystemService systemService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _equipmentDialogCardViewModel = equipmentDialogCardViewModel;
        _systemService = systemService;

        LoadEquipmentData();
        UpdateEquipmentCounts();
    }

    public EquipmentInventoryViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _equipmentDialogCardViewModel = new EquipmentDialogCardViewModel();
        _systemService = null;

        LoadEquipmentData();
        UpdateEquipmentCounts();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        if (IsInitialized) return;
        LoadEquipmentData();
        UpdateEquipmentCounts();
        IsInitialized = true;
    }

    private async void LoadEquipmentData()
    {
        try
        {
            var equipmentModels = await _systemService.GetEquipmentAsync();
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
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Loading Equipment")
                .WithContent($"Failed to load equipment data: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private Equipment MapToEquipment(EquipmentModel model)
    {
        return new Equipment
        {
            ID = model.EquipmentID,
            BrandName = model.EquipmentName,
            Category = model.Category,
            CurrentStock = model.CurrentStock,
            Supplier = model.Supplier,
            PurchasedPrice = (int?)(model.PurchasePrice ?? 0),
            PurchasedDate = model.PurchaseDate,
            Warranty = model.WarrantyExpiry,
            Condition = model.Condition,
            LastMaintenance = model.LastMaintenance,
            NextMaintenance = model.NextMaintenance
        };
    }

    private EquipmentModel MapToEquipmentModel(Equipment equipment)
    {
        return new EquipmentModel
        {
            EquipmentID = equipment.ID,
            EquipmentName = equipment.BrandName,
            Category = equipment.Category,
            CurrentStock = equipment.CurrentStock ?? 0,
            Supplier = equipment.Supplier,
            PurchasePrice = equipment.PurchasedPrice,
            PurchaseDate = equipment.PurchasedDate,
            WarrantyExpiry = equipment.Warranty,
            Condition = equipment.Condition,
            Status = "Active", // Default status
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
                _toastManager.CreateToast("Added a new equipment")
                    .WithContent($"You just added a new equipment to the database!")
                    .DismissOnClick()
                    .ShowSuccess();
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
                EquipmentName = _equipmentDialogCardViewModel.BrandName,
                Category = _equipmentDialogCardViewModel.Category,
                CurrentStock = _equipmentDialogCardViewModel.CurrentStock ?? 0,
                Supplier = _equipmentDialogCardViewModel.Supplier,
                PurchasePrice = _equipmentDialogCardViewModel.PurchasePrice,
                PurchaseDate = _equipmentDialogCardViewModel.PurchasedDate,
                WarrantyExpiry = _equipmentDialogCardViewModel.WarrantyExpiry,
                Condition = _equipmentDialogCardViewModel.Condition,
                Status = "Active",
                LastMaintenance = _equipmentDialogCardViewModel.LastMaintenance,
                NextMaintenance = _equipmentDialogCardViewModel.NextMaintenance
            };

            await _systemService.AddEquipmentAsync(newEquipmentModel);
            LoadEquipmentData(); // Reload to get updated data with new ID
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
                 equipment.Condition.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase)))
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
    private void ShowEditEquipmentDialog(Equipment? equipment)
    {
        if (equipment == null) return;

        _equipmentDialogCardViewModel.InitializeForEditMode(equipment);
        _dialogManager.CreateDialog(_equipmentDialogCardViewModel)
            .WithSuccessCallback(async _ =>
            {
                await UpdateEquipmentInDatabase(equipment);
                _toastManager.CreateToast("Modified equipment details")
                    .WithContent($"You have successfully modified {equipment.BrandName}!")
                    .DismissOnClick()
                    .ShowSuccess();
                LoadEquipmentData();
            })
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Modifying Equipment Details Cancelled")
                    .WithContent("Click the three-dots if you want to modify equipment details")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(950)
            .Show();
    }

    private async Task UpdateEquipmentInDatabase(Equipment equipment)
    {
        try
        {
            // Update the equipment object with values from dialog
            equipment.BrandName = _equipmentDialogCardViewModel.BrandName;
            equipment.Category = _equipmentDialogCardViewModel.Category;
            equipment.CurrentStock = _equipmentDialogCardViewModel.CurrentStock;
            equipment.Supplier = _equipmentDialogCardViewModel.Supplier;
            equipment.PurchasedPrice = _equipmentDialogCardViewModel.PurchasePrice;
            equipment.PurchasedDate = _equipmentDialogCardViewModel.PurchasedDate;
            equipment.Warranty = _equipmentDialogCardViewModel.WarrantyExpiry;
            equipment.Condition = _equipmentDialogCardViewModel.Condition;
            equipment.LastMaintenance = _equipmentDialogCardViewModel.LastMaintenance;
            equipment.NextMaintenance = _equipmentDialogCardViewModel.NextMaintenance;

            var equipmentModel = MapToEquipmentModel(equipment);
            await _systemService.UpdateEquipmentAsync(equipmentModel);

            // Refresh the UI
            OnPropertyChanged(nameof(EquipmentItems));
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

        _dialogManager.CreateDialog(
            "Are you absolutely sure?",
            $"This action cannot be undone. This will permanently delete multiple equipments and remove their data from your database.")
            .WithPrimaryButton("Continue", () => OnSubmitDeleteMultipleItems(equipment), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }

    private async Task OnSubmitDeleteSingleItem(Equipment equipment)
    {
        try
        {
            await _systemService.DeleteEquipmentAsync(equipment.ID);
            equipment.PropertyChanged -= OnEquipmentPropertyChanged;
            EquipmentItems.Remove(equipment);
            OriginalEquipmentData.Remove(equipment);
            CurrentFilteredEquipmentData.Remove(equipment);
            UpdateEquipmentCounts();

            _toastManager.CreateToast("Delete Equipment")
                .WithContent($"{equipment.BrandName} has been deleted successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Error Deleting Equipment")
                .WithContent($"Failed to delete equipment: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private async Task OnSubmitDeleteMultipleItems(Equipment equipment)
    {
        var selectedEquipments = EquipmentItems.Where(item => item.IsSelected).ToList();
        if (selectedEquipments.Count == 0) return;

        try
        {
            foreach (var equipmentItem in selectedEquipments)
            {
                await _systemService.DeleteEquipmentAsync(equipmentItem.ID);
                equipmentItem.PropertyChanged -= OnEquipmentPropertyChanged;
                EquipmentItems.Remove(equipmentItem);
                OriginalEquipmentData.Remove(equipmentItem);
                CurrentFilteredEquipmentData.Remove(equipmentItem);
            }
            UpdateEquipmentCounts();

            _toastManager.CreateToast($"Delete Selected Equipments")
                .WithContent($"Multiple equipments deleted successfully!")
                .DismissOnClick()
                .WithDelay(6)
                .ShowSuccess();
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
    private string? _supplier;

    [ObservableProperty]
    private int? _currentStock;

    [ObservableProperty]
    private int? _purchasedPrice;

    [ObservableProperty]
    private DateTime? _purchasedDate;

    [ObservableProperty]
    private DateTime? _warranty;

    [ObservableProperty]
    private string? _condition;

    [ObservableProperty]
    private DateTime? _lastMaintenance;

    [ObservableProperty]
    private DateTime? _nextMaintenance;

    [ObservableProperty]
    private bool _isSelected;

    public string FormattedWarranty => Warranty.HasValue ? $"{Warranty.Value:MM/dd/yyyy}" : string.Empty;
    public string FormattedPurchasedDate => PurchasedDate.HasValue ? $"{PurchasedDate.Value:MM/dd/yyyy}" : string.Empty;
    public string FormattedLastMaintenance => LastMaintenance.HasValue ? $"{LastMaintenance.Value:MM/dd/yyyy}" : string.Empty;
    public string FormattedNextMaintenance => NextMaintenance.HasValue ? $"{NextMaintenance.Value:MM/dd/yyyy}" : string.Empty;
    public string FormattedPurchasedPrice => $"₱{PurchasedPrice:N2}";

    public IBrush ConditionForeground => Condition?.ToLowerInvariant() switch
    {
        "excellent" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        "repairing" => new SolidColorBrush(Color.FromRgb(100, 116, 139)),
        "broken" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
    };

    public IBrush ConditionBackground => Condition?.ToLowerInvariant() switch
    {
        "excellent" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),
        "repairing" => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139)),
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

    partial void OnConditionChanged(string value)
    {
        OnPropertyChanged(nameof(ConditionForeground));
        OnPropertyChanged(nameof(ConditionBackground));
        OnPropertyChanged(nameof(ConditionDisplayText));
    }
}
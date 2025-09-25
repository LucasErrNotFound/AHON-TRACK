using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Components.ViewModels;
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
    
    public EquipmentInventoryViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, EquipmentDialogCardViewModel equipmentDialogCardViewModel)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _equipmentDialogCardViewModel = equipmentDialogCardViewModel;
        
        LoadEquipmentData();
        UpdateEquipmentCounts();
    }
    
    public EquipmentInventoryViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _equipmentDialogCardViewModel = new EquipmentDialogCardViewModel();
        
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

    private void LoadEquipmentData()
    {
        var sampleEquipment = GetSampleEquipmentData();
        OriginalEquipmentData = sampleEquipment;
        CurrentFilteredEquipmentData = [..sampleEquipment];
        
        EquipmentItems.Clear();
        foreach (var equipment in sampleEquipment)
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

    private List<Equipment> GetSampleEquipmentData()
    {
        var today = DateTime.Today;
        return 
        [
            new Equipment
            {
                ID = 1001,
                BrandName = "Exterminator Smith Machine",
                Category = "Machines",
                CurrentStock = 3,
                PurchaseDate = DateOnly.FromDateTime(today),
                Warranty = DateOnly.FromDateTime(today.AddMonths(36)),
                Condition = "Excellent",
                Maintenance = DateOnly.FromDateTime(today.AddDays(24)),
                Status = "Active"
            },
            new Equipment
            {
                ID = 1002,
                BrandName = "Ricky Hatt Dumbells",
                Category = "Strength",
                CurrentStock = 20,
                PurchaseDate = DateOnly.FromDateTime(today),
                Warranty = DateOnly.FromDateTime(today.AddMonths(48)),
                Condition = "Excellent",
                Maintenance = DateOnly.FromDateTime(today.AddDays(24)),
                Status = "Inactive"
            },
            new Equipment
            {
                ID = 1003,
                BrandName = "Pacquiao Boxing Gloves",
                Category = "Accessories",
                CurrentStock = 4,
                PurchaseDate = DateOnly.FromDateTime(today),
                Warranty = DateOnly.FromDateTime(today.AddMonths(36)),
                Condition = "Excellent",
                Maintenance = DateOnly.FromDateTime(today.AddDays(24)),
                Status = "Active"
            },
        ];
    }

    [RelayCommand]
    private void ShowAddEquipmentDialog()
    {
        _equipmentDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_equipmentDialogCardViewModel)
            .WithSuccessCallback(_ =>
                _toastManager.CreateToast("Added a new equipment")
                    .WithContent($"You just added a new equipment to the database!")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new equipment cancelled")
                    .WithContent("If you want to add a new equipment, please try again.")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(650)
            .Dismissible()
            .Show();
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
                equipment is { BrandName: not null, Category: not null, Condition: not null, Status: not null } && 
                (equipment.BrandName.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) || 
                 equipment.Category.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 equipment.Condition.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase) ||
                 equipment.Status.Contains(SearchStringResult, StringComparison.OrdinalIgnoreCase)))
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
    private void ShowSingleItemDeletionDialog(Equipment? equipment)
    {
        if (equipment == null) return;
        
        _dialogManager.CreateDialog("" + 
            "Are you absolutely sure?", $"This action cannot be undone. This will permanently delete {equipment.BrandName} and remove the data from your database.")
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

        _dialogManager.CreateDialog("" + 
            "Are you absolutely sure?", $"This action cannot be undone. This will permanently delete multiple equipments and remove their data from your database.")
            .WithPrimaryButton("Continue", () => OnSubmitDeleteMultipleItems(equipment), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .WithMaxWidth(512)
            .Dismissible()
            .Show();
    }
    
    private async Task OnSubmitDeleteSingleItem(Equipment equipment)
    {
        await DeleteEquipmentFromDatabase(equipment);
        equipment.PropertyChanged -= OnEquipmentPropertyChanged;
        EquipmentItems.Remove(equipment);
        UpdateEquipmentCounts();

        _toastManager.CreateToast("Delete Equipment")
            .WithContent($"{equipment.BrandName} has been deleted successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowSuccess();
    }
    private async Task OnSubmitDeleteMultipleItems(Equipment equipment)
    {
        var selectedEquipments = EquipmentItems.Where(item => item.IsSelected).ToList();
        if (selectedEquipments.Count == 0) return;

        foreach (var equipments in selectedEquipments)
        {
            await DeleteEquipmentFromDatabase(equipment);
            equipments.PropertyChanged -= OnEquipmentPropertyChanged;
            EquipmentItems.Remove(equipments);
        }
        UpdateEquipmentCounts();

        _toastManager.CreateToast($"Delete Selected Equipments")
            .WithContent($"Multiple equipments deleted successfully!")
            .DismissOnClick()
            .WithDelay(6)
            .ShowSuccess();
    }
    
    private async Task DeleteEquipmentFromDatabase(Equipment equipment)
    {
        await Task.Delay(100); // Just an animation/simulation of async operation
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
    private int? _iD;

    [ObservableProperty] 
    private string? _brandName;
    
    [ObservableProperty] 
    private string? _category;
    
    [ObservableProperty] 
    private int? _currentStock;
    
    [ObservableProperty] 
    private DateOnly? _purchaseDate;
    
    [ObservableProperty] 
    private DateOnly? _warranty;
    
    [ObservableProperty] 
    private string? _condition;
    
    [ObservableProperty] 
    private DateOnly? _maintenance;
    
    [ObservableProperty] 
    private string? _status;
    
    [ObservableProperty] 
    private bool _isSelected;
    
    public IBrush StatusForeground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),     // Green-500
        "inactive" => new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Gray-500
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))           // Default Gray-500
    };

    public IBrush StatusBackground => Status.ToLowerInvariant() switch
    {
        "active" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),     // Green-500 with alpha
        "inactive" => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139)), // Gray-500 with alpha
        _ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))           // Default Gray-500 with alpha
    };

    public string StatusDisplayText => Status.ToLowerInvariant() switch
    {
        "active" => "● Active",
        "inactive" => "● Inactive",
        _ => Status
    };

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusDisplayText));
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public partial class SupplierEquipmentDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private ObservableCollection<EquipmentItems> _equipmentItems = [];
    
    private string? _supplierName = string.Empty;
    private string? _contactPerson = string.Empty;
    private string? _email = string.Empty;
    private string? _address = string.Empty;
    private string? _phoneNumber = string.Empty;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly ISupplierService _supplierService;

    public SupplierEquipmentDialogCardViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager,
        PageManager pageManager,
        ISupplierService supplierService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _supplierService = supplierService;
        
        AddInitialItem();
    }

    public SupplierEquipmentDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        
        AddInitialItem();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        ClearAllFields();
        ClearAllEquipmentItems();
        
        if (EquipmentItems.Count == 0)
        {
            AddInitialItem();
        }
    }
    
    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }

    [RelayCommand(CanExecute = nameof(CanSaveSupplier))]
    private async void AddSupplier() // Make it async
    {
        if (!ValidateSupplierEquipment())
        {
            return;
        }

        _dialogManager
            .CreateDialog(
                "Confirm Purchase",
                "Are you ready to proceed with purchasing these equipments?")
            .WithPrimaryButton("Yes, proceed with the purchase",
                async () => { // Make this async
                    try
                    {
                        // Create supplier model for database
                        var supplierModel = new SupplierManagementModel
                        {
                            SupplierName = this.SupplierName,
                            ContactPerson = this.ContactPerson,
                            Email = this.Email,
                            PhoneNumber = this.PhoneNumber,
                            Address = this.Address,
                            Products = string.Join(", ", this.EquipmentItems.Select(i => i.ItemName)),
                            Status = "Active"
                        };

                        // Add supplier to database and get the ID
                        var result = await _supplierService.AddSupplierAsync(supplierModel);
                        
                        if (!result.Success || !result.SupplierId.HasValue)
                        {
                            _toastManager.CreateToast("Error")
                                .WithContent($"Failed to add supplier: {result.Message}")
                                .DismissOnClick()
                                .ShowError();
                            return;
                        }

                        // Now create the supplier data with the ID from database
                        var supplierData = new SupplierEquipmentData
                        {
                            SupplierID = result.SupplierId.Value, // Set the ID from database
                            SupplierName = this.SupplierName,
                            ContactPerson = this.ContactPerson,
                            Email = this.Email,
                            PhoneNumber = this.PhoneNumber,
                            Address = this.Address,
                            EquipmentItems = this.EquipmentItems.ToList()
                        };

                        Debug.WriteLine($"[SupplierEquipmentDialogCardViewModel] Supplier added with ID: {result.SupplierId.Value}");
                        Debug.WriteLine($"[SupplierEquipmentDialogCardViewModel] Navigating with supplier: {supplierData.SupplierName}");
                        Debug.WriteLine($"[SupplierEquipmentDialogCardViewModel] Equipment items count: {supplierData.EquipmentItems.Count}");

                        // Navigate with parameters
                        var parameters = new Dictionary<string, object>
                        {
                            { "SupplierData", supplierData }
                        };

                        _pageManager.Navigate<PoEquipmentViewModel>(parameters);

                        _toastManager.CreateToast("Success")
                            .WithContent("Supplier added and equipment data loaded to Purchase Order!")
                            .DismissOnClick()
                            .ShowSuccess();
                    
                        _dialogManager.Close(this, new CloseDialogOptions{ Success = true });
                    }
                    catch (Exception ex)
                    {
                        _toastManager.CreateToast("Error")
                            .WithContent($"An error occurred: {ex.Message}")
                            .DismissOnClick()
                            .ShowError();
                        
                        Debug.WriteLine($"[AddSupplier] Error: {ex.Message}");
                    }
                })
            .WithCancelButton("No, I want to add more equipments",
                () => {
                    _toastManager.CreateToast("Action Cancelled")
                        .WithContent("Continue editing your equipment list")
                        .DismissOnClick()
                        .ShowInfo();
                })
            .WithMaxWidth(800)
            .Dismissible()
            .Show();
    }
    
    [RelayCommand]
    private void AddItem()
    {
        var newItem = new EquipmentItems();
        newItem.PropertyChanged += OnEquipmentItemPropertyChanged;
        EquipmentItems.Add(newItem);
        OnPropertyChanged(nameof(CanSaveSupplier));
    }
    
    [RelayCommand]
    private void RemoveItem(EquipmentItems item)
    {
        if (EquipmentItems.Count > 1)
        {
            item.PropertyChanged -= OnEquipmentItemPropertyChanged;
            EquipmentItems.Remove(item);
            OnPropertyChanged(nameof(CanSaveSupplier));
        }
        else
        {
            _toastManager.CreateToast("Error on last item")
                .WithContent("Cannot remove the last item")
                .DismissOnClick()
                .ShowError();
        }
    }
    
    private bool ValidateSupplierEquipment()
    {
        if (string.IsNullOrWhiteSpace(SupplierName))
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Supplier name is required")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        if (string.IsNullOrWhiteSpace(ContactPerson))
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Contact person is required")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Email is required")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        if (string.IsNullOrWhiteSpace(PhoneNumber))
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Phone number is required")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        if (string.IsNullOrWhiteSpace(Address))
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Supplier address is required")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        if (EquipmentItems.Count == 0 || EquipmentItems.All(i => i.Price == 0))
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Please add at least one equipment item with a valid price")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        var invalidItems = EquipmentItems.Where(i =>
            string.IsNullOrWhiteSpace(i.ItemId) ||
            string.IsNullOrWhiteSpace(i.ItemName) ||
            string.IsNullOrWhiteSpace(i.BatchCode) ||
            string.IsNullOrWhiteSpace(i.SelectedUnit) ||
            string.IsNullOrWhiteSpace(i.SelectedCategory) ||
            string.IsNullOrWhiteSpace(i.SelectedCondition) ||
            !i.Price.HasValue ||
            i.Price <= 0).ToList();

        if (invalidItems.Count != 0)
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("All supplier items must have complete information and valid warranty expiration dates")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        var itemsWithInvalidWarranty = EquipmentItems.Where(i => 
            i.WarrantExpiry.HasValue && i.WarrantExpiry.Value.Date <= DateTimeOffset.Now.Date).ToList();

        if (itemsWithInvalidWarranty.Any())
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Warranty expiry date must be in the future (tomorrow or later)")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        return true;
    }
    
    private void AddInitialItem()
    {
        var initialItem = new EquipmentItems();
        initialItem.PropertyChanged += OnEquipmentItemPropertyChanged;
        EquipmentItems.Add(initialItem);
    }
    
    public bool CanSaveSupplier => 
        !string.IsNullOrWhiteSpace(SupplierName) &&
        !string.IsNullOrWhiteSpace(ContactPerson) &&
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(PhoneNumber) &&
        !string.IsNullOrWhiteSpace(Address) &&
        EquipmentItems.Count > 0 &&
        EquipmentItems.Any(i => 
            !string.IsNullOrWhiteSpace(i.ItemId) && 
            !string.IsNullOrWhiteSpace(i.ItemName) && 
            !string.IsNullOrWhiteSpace(i.BatchCode) && 
            !string.IsNullOrWhiteSpace(i.SelectedUnit) && 
            !string.IsNullOrWhiteSpace(i.SelectedCategory) && 
            !string.IsNullOrWhiteSpace(i.SelectedCondition) && 
            i.Price > 0 && 
            (!i.WarrantExpiry.HasValue || i.WarrantExpiry.Value.Date > DateTimeOffset.Now.Date));
    
    [Required(ErrorMessage = "Supplier name is required")]
    [RegularExpression("^[a-zA-Z0-9 ]*$", ErrorMessage = "cannot contain special characters.")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string? SupplierName
    {
        get => _supplierName;
        set => SetProperty(ref _supplierName, value, true);
    }

    [Required(ErrorMessage = "Contact person is required")]
    [RegularExpression("^[a-zA-Z ]*$", ErrorMessage = "cannot contain special characters.")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string? ContactPerson
    {
        get => _contactPerson;
        set => SetProperty(ref _contactPerson, value, true);
    }

    [Required(ErrorMessage = "Email is required")]
    [EmailValidation]
    public string? Email
    {
        get => _email;
        set => SetProperty(ref _email, value, true);
    }

    [Required(ErrorMessage = "Phone number is required")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Contact number must start with 09 and be 11 digits long")]
    public string? PhoneNumber
    {
        get => _phoneNumber;
        set => SetProperty(ref _phoneNumber, value, true);
    }
    
    [Required(ErrorMessage = "Supplier address is required")]
    [MaxLength(255, ErrorMessage = "Must not exceed 255 characters")]
    public string? Address 
    {
        get => _address;
        set => SetProperty(ref _address, value, true);
    }
    
    private void ClearAllFields()
    {
        SupplierName = string.Empty;
        ContactPerson = string.Empty;
        Email = string.Empty;
        PhoneNumber = string.Empty;
        Address = string.Empty;

        ClearAllErrors();
    }
    
    private void ClearAllEquipmentItems()
    {
        foreach (var item in EquipmentItems)
        {
            item.PropertyChanged -= OnEquipmentItemPropertyChanged;
        }
        
        EquipmentItems.Clear();
        
        OnPropertyChanged(nameof(CanSaveSupplier));
    }
    
    partial void OnEquipmentItemsChanged(ObservableCollection<EquipmentItems> value)
    {
        foreach (var item in value)
        {
            item.PropertyChanged += OnEquipmentItemPropertyChanged;
        }
        
        OnPropertyChanged(nameof(CanSaveSupplier));
    }

    private void OnEquipmentItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "ItemId" ||
            e.PropertyName == "ItemName" ||
            e.PropertyName == "BatchCode" ||
            e.PropertyName == "SelectedUnit" ||
            e.PropertyName == "SelectedCategory" ||
            e.PropertyName == "SelectedCondition" ||
            e.PropertyName == "Price" ||
            e.PropertyName == "WarrantExpiry")
        {
            OnPropertyChanged(nameof(CanSaveSupplier));
            AddSupplierCommand.NotifyCanExecuteChanged();
        }
    }
    
    protected override void DisposeManagedResources()
    {
        foreach (var item in EquipmentItems)
        {
            item.PropertyChanged -= OnEquipmentItemPropertyChanged;
        }
        
        _supplierName = null;
        _contactPerson = null;
        _email = null;
        _phoneNumber = null;
        _address = null;

        base.DisposeManagedResources();
    }
}

public partial class EquipmentItems : ObservableValidator
{
    [ObservableProperty]
    [Required(ErrorMessage = "Item ID is required")]
    [MaxLength(50, ErrorMessage = "Item ID cannot exceed 50 characters")]
    private string? _itemId;

    [ObservableProperty]
    [Required(ErrorMessage = "Item name is required")]
    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters")]
    private string? _itemName;

    [ObservableProperty]
    [Required(ErrorMessage = "Batch code is required")]
    [MaxLength(50, ErrorMessage = "Batch code cannot exceed 50 characters")]
    private string? _batchCode;

    [ObservableProperty]
    [Required(ErrorMessage = "Unit is required")]
    private string? _selectedUnit;

    [ObservableProperty]
    [Required(ErrorMessage = "Category is required")]
    private string? _selectedCategory;

    [ObservableProperty]
    [Required(ErrorMessage = "Condition is required")]
    private string? _selectedCondition;

    [ObservableProperty]
    [Range(0.01, 1000000, ErrorMessage = "Price must be between 0.01 and 1,000,000")]
    private decimal? _price;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWarrantyValid))]
    private DateTimeOffset? _warrantExpiry;
    
    public bool IsWarrantyValid => !WarrantExpiry.HasValue || WarrantExpiry.Value.Date > DateTimeOffset.Now.Date;

    public string[] UnitList { get; } = ["Piece (pc)", "Pair (pr)", "Set (set)", "Kilogram (kg)", 
        "Box (box)", "Pack (pack)", "Roll (roll)", "Bottle (bt)"];
    public string[] Category { get; } = ["Strength", "Cardio", "Machines", "Accessories"];
    public string[] ConditionList { get; } = ["Excellent", "Repairing", "Broken"];

    public EquipmentItems()
    {
        SelectedUnit = "Piece (pc)";
        SelectedCategory = "Pair (pr)";
        SelectedCondition = "Excellent";
        Price = 0;
        WarrantExpiry = DateTimeOffset.Now.AddDays(1).Date;
    }
}

public class SupplierEquipmentData
{
    public int SupplierID { get; set; }  // ⭐ Add this
    public string? SupplierName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public List<EquipmentItems> EquipmentItems { get; set; } = new();
}
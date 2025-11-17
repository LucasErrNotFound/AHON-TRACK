using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public partial class SupplierDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private ObservableCollection<ProductItems> _supplierItems = [];
    
    private string? _supplierName = string.Empty;
    private string? _contactPerson = string.Empty;
    private string? _email = string.Empty;
    private string? _address = string.Empty;
    private string? _phoneNumber = string.Empty;
    
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public SupplierDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager,
        PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        
        AddInitialItem();
    }

    public SupplierDialogCardViewModel()
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
        ClearAllSupplierItems();
        
        if (SupplierItems.Count == 0)
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
    private void AddSupplier()
    {
        if (!ValidateSupplier())
        {
            return;
        }

        _dialogManager
            .CreateDialog(
                "Confirm Supplier",
                "Are you ready to proceed with adding this supplier?")
            .WithPrimaryButton("Yes, proceed with adding",
                () => {
                    _pageManager.Navigate<PoProductViewModel>();
                    _dialogManager.Close(this, new CloseDialogOptions{ Success = true });
                    // All Data will be sent to the Supplier Management Page
                    // No Supplier Management Page yet, that is why you kept going back to this dialog
                    /*
                    var supplierData = new SupplierData
                    {
                        SupplierName = this.SupplierName,
                        ContactPerson = this.ContactPerson,
                        Email = this.Email,
                        PhoneNumber = this.PhoneNumber,
                        Address = this.Address,
                        SupplierItems = this.SupplierItems.ToList()
                    };

                    _toastManager.CreateToast("Success")
                        .WithContent("Supplier added successfully!")
                        .DismissOnClick()
                        .ShowSuccess();
                
                    _dialogManager.Close(this, new CloseDialogOptions 
                    { 
                        Success = true,
                        Result = supplierData
                    });
                    */
                })
            .WithCancelButton("No, I want to add more items",
                () => {
                    _toastManager.CreateToast("Action Cancelled")
                        .WithContent("Continue editing your supplier information")
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
        var newItem = new ProductItems();
        newItem.PropertyChanged += OnSupplierItemPropertyChanged;
        SupplierItems.Add(newItem);
        OnPropertyChanged(nameof(CanSaveSupplier));
    }
    
    [RelayCommand]
    private void RemoveItem(ProductItems item)
    {
        if (SupplierItems.Count > 1)
        {
            item.PropertyChanged -= OnSupplierItemPropertyChanged;
            SupplierItems.Remove(item);
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
    
    private bool ValidateSupplier()
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

        if (SupplierItems.Count == 0)
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Please add at least one supplier item with valid data")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        var invalidItems = SupplierItems.Where(i =>
            string.IsNullOrWhiteSpace(i.ItemName) ||
            string.IsNullOrWhiteSpace(i.ItemId) ||
            string.IsNullOrWhiteSpace(i.Description) ||
            string.IsNullOrWhiteSpace(i.SelectedUnit) ||
            string.IsNullOrWhiteSpace(i.SelectedCategory) ||
            string.IsNullOrWhiteSpace(i.BatchCode) ||
            i.MarkupPrice <= 0 ||
            !i.SellingPrice.HasValue ||
            i.SupplierPrice <= 0 ||
            !i.Expiration.HasValue ||
            i.Expiration.Value.Date <= DateTimeOffset.Now.Date).ToList();

        if (invalidItems.Count != 0)
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("All supplier items must have complete information and valid expiration dates")
                .DismissOnClick()
                .ShowError();
            return false;
        }

        return true;
    }
    
    private void AddInitialItem()
    {
        var initialItem = new ProductItems();
        initialItem.PropertyChanged += OnSupplierItemPropertyChanged;
        SupplierItems.Add(initialItem);
    }
    
    public bool CanSaveSupplier => 
        !string.IsNullOrWhiteSpace(SupplierName) &&
        !string.IsNullOrWhiteSpace(ContactPerson) &&
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(PhoneNumber) &&
        !string.IsNullOrWhiteSpace(Address) &&
        SupplierItems.Count > 0 &&
        SupplierItems.Any(i => 
            !string.IsNullOrWhiteSpace(i.ItemName) && 
            !string.IsNullOrWhiteSpace(i.ItemId) && 
            !string.IsNullOrWhiteSpace(i.Description) && 
            !string.IsNullOrWhiteSpace(i.SelectedUnit) && 
            !string.IsNullOrWhiteSpace(i.SelectedCategory) && 
            !string.IsNullOrWhiteSpace(i.BatchCode) && 
            i.MarkupPrice > 0 && 
            i.SupplierPrice > 0 && 
            i.SellingPrice.HasValue && 
            i.Expiration.HasValue && i.Expiration.Value.Date > DateTimeOffset.Now.Date);
    
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
    
    private void ClearAllSupplierItems()
    {
        foreach (var item in SupplierItems)
        {
            item.PropertyChanged -= OnSupplierItemPropertyChanged;
        }
        
        SupplierItems.Clear();
        
        OnPropertyChanged(nameof(CanSaveSupplier));
    }
    
    partial void OnSupplierItemsChanged(ObservableCollection<ProductItems> value)
    {
        foreach (var item in value)
        {
            item.PropertyChanged += OnSupplierItemPropertyChanged;
        }
        
        OnPropertyChanged(nameof(CanSaveSupplier));
    }

    private void OnSupplierItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProductItems.ItemName) || 
            e.PropertyName == nameof(ProductItems.ItemId) || 
            e.PropertyName == nameof(ProductItems.SelectedCategory) || 
            e.PropertyName == nameof(ProductItems.Description) || 
            e.PropertyName == nameof(ProductItems.BatchCode) || 
            e.PropertyName == nameof(ProductItems.SelectedUnit) || 
            e.PropertyName == nameof(ProductItems.Expiration) || 
            e.PropertyName == nameof(ProductItems.SupplierPrice) || 
            e.PropertyName == nameof(ProductItems.MarkupPrice) ||
            e.PropertyName == nameof(ProductItems.SellingPrice))
        {
            OnPropertyChanged(nameof(CanSaveSupplier));
            AddSupplierCommand.NotifyCanExecuteChanged();
        }
    }
    
    protected override void DisposeManagedResources()
    {
        foreach (var item in SupplierItems)
        {
            item.PropertyChanged -= OnSupplierItemPropertyChanged;
        }
        
        _supplierName = null;
        _contactPerson = null;
        _email = null;
        _phoneNumber = null;
        _address = null;

        base.DisposeManagedResources();
    }
}

public partial class ProductItems : ObservableValidator
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
    [Required(ErrorMessage = "Description is required")]
    [MaxLength(50, ErrorMessage = "Description cannot exceed 50 characters")]
    private string? _description;
    
    [ObservableProperty]
    [Required(ErrorMessage = "Unit is required")]
    private string? _selectedUnit;

    [ObservableProperty]
    [Required(ErrorMessage = "Category is required")]
    private string? _selectedCategory;

    [ObservableProperty]
    [Range(1, 1000000, ErrorMessage = "Unit price must be between 1 and 1,000,000")]
    private decimal? _supplierPrice;
    
    [ObservableProperty]
    [Range(1, 1000000, ErrorMessage = "Markup price must be between 1 and 1,000,000")]
    private decimal? _markupPrice;
    
    [ObservableProperty]
    [Required(ErrorMessage = "Batch code is required")]
    [MaxLength(50, ErrorMessage = "Batch code cannot exceed 50 characters")]
    private string? _batchCode;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpirationValid))]
    private DateTimeOffset? _expiration;
    
    public decimal? SellingPrice
    {
        get
        {
            if (SupplierPrice.HasValue && MarkupPrice.HasValue)
            {
                return SupplierPrice.Value + MarkupPrice.Value;
            }
            return null;
        }
    }
    
    public string[] UnitList { get; } = ["Piece (pc)", "Pair (pr)", "Set (set)", "Kilogram (kg)", 
        "Box (box)", "Pack (pack)", "Roll (roll)", "Bottle (bt)"];
    
    public string[] Category { get; } = ["Drinks", "Supplements", "Apparel", "Products", "Merchandise"];
    
    public bool IsExpirationValid => !Expiration.HasValue || Expiration.Value.Date > DateTimeOffset.Now.Date;

    public ProductItems()
    {
        SelectedUnit = "Piece (pc)";
        SelectedCategory = "Drinks";
        ItemName = string.Empty;
        Description = string.Empty;
        Expiration = DateTimeOffset.Now.AddDays(1).Date;
    }
    
    partial void OnSupplierPriceChanged(decimal? value)
    {
        OnPropertyChanged(nameof(SellingPrice));
    }

    partial void OnMarkupPriceChanged(decimal? value)
    {
        OnPropertyChanged(nameof(SellingPrice));
    }

    partial void OnItemNameChanged(string? value)
    {
        OnPropertyChanged(nameof(ItemName));
    }
}

public class SupplierProductData
{
    public string? SupplierName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public List<ProductItems> SupplierItems { get; set; } = new();
}
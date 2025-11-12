using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public partial class SupplierDialogCardViewModel : ViewModelBase, INavigable
{
    [ObservableProperty]
    private string[] _statusFilterItems = ["Active", "Inactive", "Suspended"];

    [ObservableProperty] 
    private bool _isStatusEnabled;

    [ObservableProperty]
    private string _dialogTitle = "Add Supplier Contact";

    [ObservableProperty]
    private string _dialogDescription = "Register new supplier with their contact to maintain reliable supply management";
    
    [ObservableProperty]
    private bool _isEditMode = false;

    private string? _supplierName = string.Empty;
    private string? _contactPerson = string.Empty;
    private string? _email = string.Empty;
    private string? _address = string.Empty;
    private string? _phoneNumber = string.Empty;
    private string? _status = string.Empty; // Get its status from the Purchase Order

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public SupplierDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public SupplierDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        ClearAllFields();
        DialogTitle = "Add Supplier Contact";
        DialogDescription = "Register new supplier with their contact to maintain reliable supply management";
        StatusFilterItems = ["Active", "Inactive", "Suspended"];
        Status = "Active";
        IsStatusEnabled = false;
        IsEditMode = false;
    }

    public void InitializeForEditMode(Supplier? supplier)
    {
        IsEditMode = true;
        IsStatusEnabled = true;
        DialogTitle = "Edit Existing Supplier Contact";
        DialogDescription = "Edit existing supplier with their contact to maintain latest details";
        ClearAllFields();

        SupplierName = supplier?.Name;
        ContactPerson = supplier?.ContactPerson;
        Email = supplier?.Email;
        PhoneNumber = supplier?.PhoneNumber;
        Address = supplier?.Address;
        // Place address here backend + database developer :)
        
        StatusFilterItems = ["Active", "Suspended"];
        Status = string.Equals(supplier?.Status, "Inactive", StringComparison.OrdinalIgnoreCase) 
            ? "Active" 
            : supplier?.Status;
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }

    [RelayCommand]
    private void AddSupplier()
    {
        ValidateAllProperties();

        if (HasErrors) return;
        
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    private void ClearAllFields()
    {
        SupplierName = string.Empty;
        ContactPerson = string.Empty;
        Email = string.Empty;
        PhoneNumber = string.Empty;
        Address = string.Empty;
        Status = string.Empty;

        ClearAllErrors();
    }

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

    [Required(ErrorMessage = "Select a status")]
    public string? Status
    {
        get => _status;
        set
        {
            SetProperty(ref _status, value, true);
        }
    }
    
    protected override void DisposeManagedResources()
    {
        // Wipe out lightweight/static arrays
        StatusFilterItems = [];

        // Clear UI/dialog text
        DialogTitle = string.Empty;
        DialogDescription = string.Empty;

        // Clear selections/flags
        IsEditMode = false;

        // Aggressively drop field data
        _supplierName = null;
        _contactPerson = null;
        _email = null;
        _phoneNumber = null;
        _address = null;
        _status = null;

        // Let base do any additional cleanup
        base.DisposeManagedResources();
    }
}
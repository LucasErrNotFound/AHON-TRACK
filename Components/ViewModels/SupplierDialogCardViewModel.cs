using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
    private string[] _statusFilterItems = ["Active", "Inactive", "Suspended"];
    
    [ObservableProperty]
    private string _dialogTitle = "Add Supplier Contact";

    [ObservableProperty]
    private string _dialogDescription = "Register new supplier with their contact to maintain reliable supply management";
    
    [ObservableProperty] 
    private bool _isEditMode = false;
    
    private string? _supplierName = string.Empty;
    private string? _contactPerson = string.Empty;
    private string? _email = string.Empty;
    private string? _phoneNumber = string.Empty;
    private string? _products = string.Empty;
    private string? _status = string.Empty;
    
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
        DialogTitle = "Add Supplier Contact";
        DialogDescription = "Register new supplier with their contact to maintain reliable supply management";
        IsEditMode = false;
        ClearAllFields();
    }

    public void InitializeForEditMode(Supplier? supplier)
    {
        IsEditMode = true;
        DialogTitle = "Edit Existing Supplier Contact";
        DialogDescription = "Edit existing supplier with their contact to maintain latest details";
        ClearAllFields();

        SupplierName = supplier?.Name;
        ContactPerson = supplier?.ContactPerson;
        Email = supplier?.Email;
        PhoneNumber = supplier?.PhoneNumber;
        Products = supplier?.Products;
        Status = supplier?.Status;
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
        Products = string.Empty;
        Status = string.Empty;
        
        ClearAllErrors();
    }
    
    [Required(ErrorMessage = "Supplier name is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string? SupplierName
    {
        get => _supplierName;
        set => SetProperty(ref _supplierName, value, true);
    }
    
    [Required(ErrorMessage = "Contact person is required")]
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
    
    [Required(ErrorMessage = "Products is required")]
    [MinLength(4, ErrorMessage = "Must be at least 4 characters long")]
    [MaxLength(50, ErrorMessage = "Must not exceed 50 characters")]
    public string? Products 
    {
        get => _products;
        set => SetProperty(ref _products, value, true);
    }
    
    [Required(ErrorMessage = "Select a status")]
    public string? Status
    {
        get => _status;
        set => SetProperty(ref _status, value, true);
    }
}
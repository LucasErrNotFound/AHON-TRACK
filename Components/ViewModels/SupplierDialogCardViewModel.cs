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
    private string _dialogTitle = "Add Supplier Contact";

    [ObservableProperty]
    private string _dialogDescription = "Register new supplier with their contact to maintain reliable supply management";

    [ObservableProperty]
    private ObservableCollection<string> _deliveryScheduleItems = [];

    [ObservableProperty]
    private ObservableCollection<string> _contractTermsItems = [];

    [ObservableProperty]
    private string? _selectedDeliverySchedule;

    [ObservableProperty]
    private string? _selectedContractTerms;

    [ObservableProperty]
    private bool _isEditMode = false;

    public string? DeliveryPattern => SchedulePattern;

    private string? _supplierName = string.Empty;
    private string? _contactPerson = string.Empty;
    private string? _email = string.Empty;
    private string? _phoneNumber = string.Empty;
    private string? _products = string.Empty;
    private string? _schedulePattern = "Month";
    private string? _contractPattern = "Month";
    private string? _status = string.Empty;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public SupplierDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;

        PopulateDeliveryScheduleItems();
        PopulateContractTermsItems();
    }

    public SupplierDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());

        PopulateDeliveryScheduleItems();
        PopulateContractTermsItems();
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

        if (string.IsNullOrWhiteSpace(supplier?.DeliverySchedule)) return;

        if (supplier.DeliverySchedule.Contains("day", StringComparison.OrdinalIgnoreCase))
        {
            SchedulePattern = "Day";
        }
        else if (supplier.DeliverySchedule.Contains("month", StringComparison.OrdinalIgnoreCase))
        {
            SchedulePattern = "Month";
        }
        SelectedDeliverySchedule = supplier.DeliverySchedule;

        if (string.IsNullOrWhiteSpace(supplier?.ContractTerms)) return;

        if (supplier.ContractTerms.Contains("day", StringComparison.OrdinalIgnoreCase))
        {
            ContractPattern = "Day";
        }
        else if (supplier.ContractTerms.Contains("month", StringComparison.OrdinalIgnoreCase))
        {
            ContractPattern = "Month";
        }
        SelectedContractTerms = supplier.ContractTerms;
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
        SelectedDeliverySchedule = string.Empty;
        SelectedContractTerms = string.Empty;

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

    public string? SchedulePattern
    {
        get => _schedulePattern;
        set
        {
            if (_schedulePattern != value)
            {
                _schedulePattern = value;
                OnPropertyChanged(nameof(SchedulePattern));
                OnPropertyChanged(nameof(IsScheduleDeliveryByDay));
                OnPropertyChanged(nameof(IsScheduleDeliveryByMonth));

                // Repopulate items when pattern changes
                PopulateDeliveryScheduleItems();

                // Clear selection when switching patterns
                SelectedDeliverySchedule = null;
            }
        }
    }

    public string? ContractPattern
    {
        get => _contractPattern;
        set
        {
            if (_contractPattern != value)
            {
                _contractPattern = value;
                OnPropertyChanged(nameof(ContractPattern));
                OnPropertyChanged(nameof(IsContractTermsByDay));
                OnPropertyChanged(nameof(IsContractTermsByMonth));

                // Repopulate items when pattern changes
                PopulateContractTermsItems();

                // Clear selection when switching patterns
                SelectedContractTerms = null;
            }
        }
    }

    public bool IsScheduleDeliveryByDay
    {
        get => SchedulePattern == "Day";
        set { if (value) SchedulePattern = "Day"; }
    }

    public bool IsScheduleDeliveryByMonth
    {
        get => SchedulePattern == "Month";
        set { if (value) SchedulePattern = "Month"; }
    }

    public bool IsContractTermsByDay
    {
        get => ContractPattern == "Day";
        set { if (value) ContractPattern = "Day"; }
    }

    public bool IsContractTermsByMonth
    {
        get => ContractPattern == "Month";
        set { if (value) ContractPattern = "Month"; }
    }

    [Required(ErrorMessage = "Select a status")]
    public string? Status
    {
        get => _status;
        set => SetProperty(ref _status, value, true);
    }

    public string? DeliverySchedule => SelectedDeliverySchedule;
    public string? ContractTerms => SelectedContractTerms;

    private void PopulateDeliveryScheduleItems()
    {
        DeliveryScheduleItems.Clear();

        if (SchedulePattern == "Day")
        {
            DeliveryScheduleItems.Add("everyday");
            for (int i = 2; i <= 30; i++)
            {
                DeliveryScheduleItems.Add($"every {i} days");
            }
        }
        else
        {
            DeliveryScheduleItems.Add("every 1 month");
            for (int i = 2; i <= 12; i++)
            {
                DeliveryScheduleItems.Add($"every {i} months");
            }
        }
    }

    private void PopulateContractTermsItems()
    {
        ContractTermsItems.Clear();

        if (ContractPattern == "Day")
        {
            ContractTermsItems.Add("1 day");
            for (int i = 2; i <= 30; i++)
            {
                ContractTermsItems.Add($"{i} days");
            }
        }
        else
        {
            ContractTermsItems.Add("1 month");
            for (int i = 2; i <= 12; i++)
            {
                ContractTermsItems.Add($"{i} months");
            }
        }
    }
    
    protected override void DisposeManagedResources()
    {
        // Wipe out lightweight/static arrays
        StatusFilterItems = [];

        // Clear UI/dialog text
        DialogTitle = string.Empty;
        DialogDescription = string.Empty;

        // Clear and replace collections to drop references
        DeliveryScheduleItems?.Clear();
        ContractTermsItems?.Clear();
        DeliveryScheduleItems = [];
        ContractTermsItems = [];

        // Clear selections/flags
        SelectedDeliverySchedule = null;
        SelectedContractTerms = null;
        IsEditMode = false;

        // Aggressively drop field data
        _supplierName = null;
        _contactPerson = null;
        _email = null;
        _phoneNumber = null;
        _products = null;
        _schedulePattern = null;
        _contractPattern = null;
        _status = null;

        // Let base do any additional cleanup
        base.DisposeManagedResources();
    }
}
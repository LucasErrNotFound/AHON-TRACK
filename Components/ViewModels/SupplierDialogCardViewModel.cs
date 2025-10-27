using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.Validators;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using AHON_TRACK.Services.Interface;
using Microsoft.Extensions.Logging;

namespace AHON_TRACK.Components.ViewModels;

public partial class SupplierDialogCardViewModel : ViewModelBase, INavigable
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly ILogger _logger;

    [ObservableProperty]
    private string[] _statusFilterItems = ["Active", "Inactive", "Suspended"];

    [ObservableProperty]
    private string _dialogTitle = "Add Supplier Contact";

    [ObservableProperty]
    private string _dialogDescription = "Register new supplier with their contact to maintain reliable supply management";

    [ObservableProperty] private ObservableCollection<string> _deliveryScheduleItems = [];
    [ObservableProperty] private ObservableCollection<string> _contractTermsItems = [];
    [ObservableProperty] private string? _selectedDeliverySchedule;
    [ObservableProperty] private string? _selectedContractTerms;
    [ObservableProperty] private bool _isEditMode = false;
    [ObservableProperty] private bool _isInitialized;

    public string? DeliveryPattern => SchedulePattern;

    private string? _supplierName = string.Empty;
    private string? _contactPerson = string.Empty;
    private string? _email = string.Empty;
    private string? _phoneNumber = string.Empty;
    private string? _products = string.Empty;
    private string? _schedulePattern = "Month";
    private string? _contractPattern = "Month";
    private string? _status = string.Empty;

    public SupplierDialogCardViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager, 
        ILogger logger)
    {
        _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        PopulateDeliveryScheduleItems();
        PopulateContractTermsItems();
    }

    // Design-time constructor
    public SupplierDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _logger = null!;

        PopulateDeliveryScheduleItems();
        PopulateContractTermsItems();
    }

    #region INavigable Implementation

    [AvaloniaHotReload]
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (IsInitialized)
        {
            _logger.LogDebug("SupplierDialogCardViewModel already initialized");
            return ValueTask.CompletedTask;
        }

        _logger.LogInformation("Initializing SupplierDialogCardViewModel");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            DialogTitle = "Add Supplier Contact";
            DialogDescription = "Register new supplier with their contact to maintain reliable supply management";
            IsEditMode = false;
            ClearAllFields();

            IsInitialized = true;
            _logger.LogInformation("SupplierDialogCardViewModel initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SupplierDialogCardViewModel initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing SupplierDialogCardViewModel");
            _toastManager.CreateToast("Initialization Error")
                .WithContent("Failed to initialize supplier dialog")
                .DismissOnClick()
                .ShowError();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Navigating away from SupplierDialog");
        return ValueTask.CompletedTask;
    }

    #endregion

    /*
    #region HotAvalonia Support

    [AvaloniaHotReload]
    public void Initialize()
    {
        _ = InitializeAsync(LifecycleToken);
    }

    #endregion
    */

    public void InitializeForEditMode(Supplier? supplier)
    {
        ThrowIfDisposed();
        
        try
        {
            IsEditMode = true;
            DialogTitle = "Edit Existing Supplier Contact";
            DialogDescription = "Edit existing supplier with their contact to maintain latest details";
            ClearAllFields();

            if (supplier == null)
            {
                _logger.LogWarning("InitializeForEditMode called with null supplier");
                return;
            }

            SupplierName = supplier.Name;
            ContactPerson = supplier.ContactPerson;
            Email = supplier.Email;
            PhoneNumber = supplier.PhoneNumber;
            Products = supplier.Products;
            Status = supplier.Status;

            if (!string.IsNullOrWhiteSpace(supplier.DeliverySchedule))
            {
                if (supplier.DeliverySchedule.Contains("day", StringComparison.OrdinalIgnoreCase))
                {
                    SchedulePattern = "Day";
                }
                else if (supplier.DeliverySchedule.Contains("month", StringComparison.OrdinalIgnoreCase))
                {
                    SchedulePattern = "Month";
                }
                SelectedDeliverySchedule = supplier.DeliverySchedule;
            }

            if (!string.IsNullOrWhiteSpace(supplier.ContractTerms))
            {
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

            _logger.LogDebug("Initialized edit mode for supplier: {SupplierName}", supplier.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing edit mode for supplier");
            _toastManager.CreateToast("Error")
                .WithContent("Failed to load supplier data")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        ThrowIfDisposed();
        _logger.LogDebug("Supplier dialog cancelled");
        _dialogManager.Close(this);
    }

    [RelayCommand]
    private void AddSupplier()
    {
        ThrowIfDisposed();
        
        try
        {
            ValidateAllProperties();

            if (HasErrors)
            {
                _logger.LogDebug("Supplier validation failed");
                return;
            }

            _logger.LogInformation("Supplier {Mode}: {SupplierName}", 
                IsEditMode ? "updated" : "added", SupplierName);
            
            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing supplier");
            _toastManager.CreateToast("Error")
                .WithContent("Failed to save supplier")
                .DismissOnClick()
                .ShowError();
        }
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

    #region Validated Properties

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

                PopulateDeliveryScheduleItems();
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

                PopulateContractTermsItems();
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

    #endregion

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

    #region Disposal

    protected override async ValueTask DisposeAsyncCore()
    {
        _logger.LogInformation("Disposing SupplierDialogCardViewModel");

        // Clear collections
        DeliveryScheduleItems.Clear();
        ContractTermsItems.Clear();

        // Clear validation errors
        ClearAllErrors();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    #endregion
}
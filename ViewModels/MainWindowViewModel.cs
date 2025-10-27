using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AHON_TRACK.Converters;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace AHON_TRACK.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly ILogger _logger;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly SettingsDialogCardViewModel _settingsDialogCardViewModel;
    
    // Lazy-loaded ViewModels for memory efficiency
    private readonly Lazy<ManageEmployeesViewModel> _manageEmployeesViewModel;
    private readonly Lazy<CheckInOutViewModel> _checkInOutViewModel;
    private readonly Lazy<ManageMembershipViewModel> _manageMembershipViewModel;
    private readonly Lazy<TrainingSchedulesViewModel> _trainingSchedulesViewModel;
    private readonly Lazy<ManageBillingViewModel> _manageBillingViewModel;
    private readonly Lazy<ProductPurchaseViewModel> _productPurchaseViewModel;
    private readonly Lazy<EquipmentInventoryViewModel> _equipmentInventoryViewModel;
    private readonly Lazy<ProductStockViewModel> _productStockViewModel;
    private readonly Lazy<SupplierManagementViewModel> _supplierManagementViewModel;
    private readonly Lazy<FinancialReportsViewModel> _financialReportsViewModel;
    private readonly Lazy<GymDemographicsViewModel> _gymDemographicsViewModel;
    private readonly Lazy<GymAttendanceViewModel> _gymAttendanceViewModel;
    private readonly Lazy<AuditLogsViewModel> _auditLogsViewModel;
    private readonly Lazy<EmployeeProfileInformationViewModel> _employeeProfileInformationViewModel;

    [ObservableProperty] private DialogManager _dialogManager;
    [ObservableProperty] private ToastManager _toastManager;
    [ObservableProperty] private object? _selectedPage;
    [ObservableProperty] private string _currentRoute = "dashboard";
    [ObservableProperty] private Bitmap? _avatarSource;
    [ObservableProperty] private string? _role = CurrentUserModel.Role;
    [ObservableProperty] private string? _username = CurrentUserModel.Username;
    [ObservableProperty] private bool _canManageEmployees;
    [ObservableProperty] private bool _canAccessCheckInOut;
    [ObservableProperty] private bool _canAccessMemberManagement;
    [ObservableProperty] private bool _canAccessBilling;
    [ObservableProperty] private bool _canAccessProductPurchase;
    [ObservableProperty] private bool _canViewFinancialReports;
    [ObservableProperty] private bool _canViewAnalytics;
    [ObservableProperty] private bool _canViewAuditLogs;
    [ObservableProperty] private bool _canAccessTraining;

    private bool _shouldShowSuccessLogInToast;

    public MainWindowViewModel(
        INavigationService navigationService,
        ILogger logger,
        DialogManager dialogManager,
        ToastManager toastManager,
        DashboardViewModel dashboardViewModel,
        SettingsDialogCardViewModel settingsDialogCardViewModel,
        ServiceProvider serviceProvider)
    {
        _navigationService = navigationService;
        _logger = logger;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _dashboardViewModel = dashboardViewModel;
        _settingsDialogCardViewModel = settingsDialogCardViewModel;

        // Lazy initialization for better startup performance
        _manageEmployeesViewModel = new Lazy<ManageEmployeesViewModel>(serviceProvider.GetService<ManageEmployeesViewModel>);
        _checkInOutViewModel = new Lazy<CheckInOutViewModel>(serviceProvider.GetService<CheckInOutViewModel>);
        _manageMembershipViewModel = new Lazy<ManageMembershipViewModel>(serviceProvider.GetService<ManageMembershipViewModel>);
        _trainingSchedulesViewModel = new Lazy<TrainingSchedulesViewModel>(serviceProvider.GetService<TrainingSchedulesViewModel>);
        _manageBillingViewModel = new Lazy<ManageBillingViewModel>(serviceProvider.GetService<ManageBillingViewModel>);
        _productPurchaseViewModel = new Lazy<ProductPurchaseViewModel>(serviceProvider.GetService<ProductPurchaseViewModel>);
        _equipmentInventoryViewModel = new Lazy<EquipmentInventoryViewModel>(serviceProvider.GetService<EquipmentInventoryViewModel>);
        _productStockViewModel = new Lazy<ProductStockViewModel>(serviceProvider.GetService<ProductStockViewModel>);
        _supplierManagementViewModel = new Lazy<SupplierManagementViewModel>(serviceProvider.GetService<SupplierManagementViewModel>);
        _financialReportsViewModel = new Lazy<FinancialReportsViewModel>(serviceProvider.GetService<FinancialReportsViewModel>);
        _gymDemographicsViewModel = new Lazy<GymDemographicsViewModel>(serviceProvider.GetService<GymDemographicsViewModel>);
        _gymAttendanceViewModel = new Lazy<GymAttendanceViewModel>(serviceProvider.GetService<GymAttendanceViewModel>);
        _auditLogsViewModel = new Lazy<AuditLogsViewModel>(serviceProvider.GetService<AuditLogsViewModel>);
        _employeeProfileInformationViewModel = new Lazy<EmployeeProfileInformationViewModel>(serviceProvider.GetService<EmployeeProfileInformationViewModel>);

        // Subscribe to navigation events
        _navigationService.NavigationCompleted += OnNavigationCompleted;
        
        // Subscribe to profile updates
        UserProfileEventService.Instance.ProfilePictureUpdated += OnProfilePictureUpdated;
    }

    // Design-time constructor
    public MainWindowViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _logger = null!;
        _navigationService = null!;
        
        _dashboardViewModel = new DashboardViewModel();
        _settingsDialogCardViewModel = new SettingsDialogCardViewModel();
        _manageEmployeesViewModel = new Lazy<ManageEmployeesViewModel>(() => new ManageEmployeesViewModel());
        _checkInOutViewModel = new Lazy<CheckInOutViewModel>(() => new CheckInOutViewModel());
        _manageMembershipViewModel = new Lazy<ManageMembershipViewModel>(() => new ManageMembershipViewModel());
        _trainingSchedulesViewModel = new Lazy<TrainingSchedulesViewModel>(() => new TrainingSchedulesViewModel());
        _manageBillingViewModel = new Lazy<ManageBillingViewModel>(() => new ManageBillingViewModel());
        _productPurchaseViewModel = new Lazy<ProductPurchaseViewModel>(() => new ProductPurchaseViewModel());
        _equipmentInventoryViewModel = new Lazy<EquipmentInventoryViewModel>(() => new EquipmentInventoryViewModel());
        _productStockViewModel = new Lazy<ProductStockViewModel>(() => new ProductStockViewModel());
        _supplierManagementViewModel = new Lazy<SupplierManagementViewModel>(() => new SupplierManagementViewModel());
        _financialReportsViewModel = new Lazy<FinancialReportsViewModel>(() => new FinancialReportsViewModel());
        _gymDemographicsViewModel = new Lazy<GymDemographicsViewModel>(() => new GymDemographicsViewModel());
        _gymAttendanceViewModel = new Lazy<GymAttendanceViewModel>(() => new GymAttendanceViewModel());
        _auditLogsViewModel = new Lazy<AuditLogsViewModel>(() => new AuditLogsViewModel());
        _employeeProfileInformationViewModel = new Lazy<EmployeeProfileInformationViewModel>(() => new EmployeeProfileInformationViewModel());
    }

    private void OnNavigationCompleted(INavigable page, string route)
    {
        CurrentRoute = route;
        SelectedPage = page;
        _logger.LogDebug("Navigation completed to {Route}", route);
    }

    public async void Initialize()
    {
        ThrowIfDisposed();
        
        _shouldShowSuccessLogInToast = false;
        AvatarSource = ImageHelper.GetAvatarOrDefault(CurrentUserModel.AvatarBytes);
        
        UpdateRoleBasedPermissions();
        
        // Navigate to dashboard
        await _navigationService.NavigateAsync<DashboardViewModel>(cancellationToken: LifecycleToken)
            .ConfigureAwait(false);
    }

    private void UpdateRoleBasedPermissions()
    {
        var userRole = CurrentUserModel.Role ?? string.Empty;
        var isAdmin = userRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        var isStaff = userRole.Equals("Staff", StringComparison.OrdinalIgnoreCase);
        var isCoach = userRole.Equals("Coach", StringComparison.OrdinalIgnoreCase);

        CanManageEmployees = isAdmin;
        CanViewFinancialReports = isAdmin;
        CanViewAuditLogs = isAdmin;
        CanAccessCheckInOut = isAdmin || isStaff || isCoach;
        CanAccessMemberManagement = isAdmin || isStaff || isCoach;
        CanAccessBilling = isAdmin || isStaff || isCoach;
        CanAccessProductPurchase = isAdmin || isStaff || isCoach;
        CanViewAnalytics = isAdmin || isStaff || isCoach;
        CanAccessTraining = true;
    }

    [RelayCommand]
    private void TryClose()
    {
        DialogManager.CreateDialog("Close Application", 
            "Are you sure you want to exit the application \n rather than logging out?")
            .WithPrimaryButton("Yes", OnAcceptExit, DialogButtonStyle.Destructive)
            .WithCancelButton("No")
            .WithMinWidth(300)
            .Show();
    }

    [RelayCommand]
    private void TryLogout()
    {
        DialogManager.CreateDialog("Logout", "Do you really want to log out of your account?")
            .WithPrimaryButton("Yes, log me out", SwitchToLoginWindow, DialogButtonStyle.Destructive)
            .WithCancelButton("No")
            .WithMinWidth(300)
            .Show();
    }

    // Navigation Commands - now async
    [RelayCommand]
    private Task OpenDashboardAsync() => 
        _navigationService.NavigateAsync<DashboardViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenManageEmployeesAsync() => 
        _navigationService.NavigateAsync<ManageEmployeesViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenCheckInOutAsync() => 
        _navigationService.NavigateAsync<CheckInOutViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenManageMembershipAsync() => 
        _navigationService.NavigateAsync<ManageMembershipViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenTrainingSchedulesAsync() => 
        _navigationService.NavigateAsync<TrainingSchedulesViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenManageBillingAsync() => 
        _navigationService.NavigateAsync<ManageBillingViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenProductPurchaseAsync() => 
        _navigationService.NavigateAsync<ProductPurchaseViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenEquipmentInventoryAsync() => 
        _navigationService.NavigateAsync<EquipmentInventoryViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenProductStockViewModelAsync() => 
        _navigationService.NavigateAsync<ProductStockViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenSupplierManagementAsync() => 
        _navigationService.NavigateAsync<SupplierManagementViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenFinancialReportsAsync() => 
        _navigationService.NavigateAsync<FinancialReportsViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenGymDemographicsAsync() => 
        _navigationService.NavigateAsync<GymDemographicsViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenGymAttendanceReportsAsync() => 
        _navigationService.NavigateAsync<GymAttendanceViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenAuditLogsAsync() => 
        _navigationService.NavigateAsync<AuditLogsViewModel>(cancellationToken: LifecycleToken).AsTask();

    [RelayCommand]
    private Task OpenViewProfileAsync()
    {
        var parameters = new Dictionary<string, object> { { "IsCurrentUser", true } };
        return _navigationService.NavigateAsync<EmployeeProfileInformationViewModel>(
            parameters, LifecycleToken).AsTask();
    }

    [RelayCommand]
    private async Task OpenSettingsDialogAsync()
    {
        await _settingsDialogCardViewModel.InitializeAsync().ConfigureAwait(false);
        DialogManager.CreateDialog(_settingsDialogCardViewModel)
            .WithSuccessCallback(_ =>
                ToastManager.CreateToast("Settings Saved!")
                    .WithContent("Your changes have been applied successfully")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                ToastManager.CreateToast("Changes Discarded!")
                    .WithContent("No settings were modified")
                    .DismissOnClick()
                    .ShowWarning())
            .WithMaxWidth(670)
            .Show();
    }

    private async void OnAcceptExit()
    {
        // Flush logs before exit
        await Log.CloseAndFlushAsync().ConfigureAwait(false);
        Environment.Exit(0);
    }

    public void SetInitialLogInToastState(bool showLogInSuccess)
    {
        if (!showLogInSuccess) return;

        _ = Task.Delay(900, LifecycleToken).ContinueWith(_ =>
        {
            if (!LifecycleToken.IsCancellationRequested)
            {
                ToastManager.CreateToast("You have signed in! Welcome back!")
                    .WithContent($"{DateTime.Now:dddd, MMMM d 'at' h:mm tt}")
                    .WithDelay(8)
                    .DismissOnClick()
                    .ShowSuccess();
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private async void SwitchToLoginWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) 
            return;

        var currentWindow = desktop.MainWindow;
        _shouldShowSuccessLogInToast = true;

        // Dispose current MainWindow resources - stay on UI thread
        await DisposeAsync(); // ✅ Removed .ConfigureAwait(false)

        var provider = new ServiceProvider();
        var viewModel = provider.GetService<LoginViewModel>();
        viewModel.Initialize();

        var loginWindow = new Views.LoginView { DataContext = viewModel };
        viewModel.SetInitialLogOutToastState(_shouldShowSuccessLogInToast);
        desktop.MainWindow = loginWindow;
        loginWindow.Show();
        currentWindow?.Close();
    }

    private void OnProfilePictureUpdated()
    {
        AvatarSource = ImageHelper.GetAvatarOrDefault(CurrentUserModel.AvatarBytes);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _logger.LogInformation("Disposing MainWindowViewModel");
        
        // Unsubscribe from events
        UserProfileEventService.Instance.ProfilePictureUpdated -= OnProfilePictureUpdated;
        if (_navigationService != null)
        {
            _navigationService.NavigationCompleted -= OnNavigationCompleted;
        }

        // Dispose navigation service (will dispose current page)
        if (_navigationService is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }

        // Dispose any instantiated lazy ViewModels
        DisposeIfCreated(_manageEmployeesViewModel);
        DisposeIfCreated(_checkInOutViewModel);
        DisposeIfCreated(_manageMembershipViewModel);
        DisposeIfCreated(_trainingSchedulesViewModel);
        DisposeIfCreated(_manageBillingViewModel);
        DisposeIfCreated(_productPurchaseViewModel);
        DisposeIfCreated(_equipmentInventoryViewModel);
        DisposeIfCreated(_productStockViewModel);
        DisposeIfCreated(_supplierManagementViewModel);
        DisposeIfCreated(_financialReportsViewModel);
        DisposeIfCreated(_gymDemographicsViewModel);
        DisposeIfCreated(_gymAttendanceViewModel);
        DisposeIfCreated(_auditLogsViewModel);
        DisposeIfCreated(_employeeProfileInformationViewModel);

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    private static async void DisposeIfCreated<T>(Lazy<T> lazy) where T : class
    {
        if (!lazy.IsValueCreated) return;
        
        if (lazy.Value is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (lazy.Value is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
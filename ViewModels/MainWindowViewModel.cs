using AHON_TRACK.Components.ViewModels;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly PageManager _pageManager;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly ManageEmployeesViewModel _manageEmployeesViewModel;
	private readonly CheckInOutViewModel _checkInOutViewModel;
	private readonly ManageMembershipViewModel _manageMembershipViewModel;
    private readonly TrainingSchedulesViewModel _trainingSchedulesViewModel;
    private readonly ManageBillingViewModel _manageBillingViewModel;
    private readonly ProductPurchaseViewModel _productPurchaseViewModel;
    private readonly EquipmentInventoryViewModel _equipmentInventoryViewModel;
    private readonly ProductStockViewModel  _productStockViewModel;
    private readonly SupplierManagementViewModel _supplierManagementViewModel;
    private readonly FinancialReportsViewModel _financialReportsViewModel;
    private readonly GymDemographicsViewModel  _gymDemographicsViewModel;
    private readonly GymAttendanceViewModel  _gymAttendanceViewModel;
    private readonly AuditLogsViewModel _auditLogsViewModel;

    private readonly EmployeeProfileInformationViewModel _employeeProfileInformationViewModel;
    private readonly SettingsDialogCardViewModel _settingsDialogCardViewModel;

    // Primary constructor for DI
    public MainWindowViewModel(
        PageManager pageManager,
        DialogManager dialogManager,
        ToastManager toastManager,
        DashboardViewModel dashboardViewModel,
        ManageEmployeesViewModel manageEmployeesViewModel,
		CheckInOutViewModel checkInOutViewModel,
		ManageMembershipViewModel manageMembershipViewModel,
        TrainingSchedulesViewModel trainingSchedulesViewModel,
        ManageBillingViewModel billingViewModel,
        ProductPurchaseViewModel productPurchaseViewModel,
        EquipmentInventoryViewModel equipmentInventoryViewModel,
        ProductStockViewModel productStockViewModel,
        SupplierManagementViewModel supplierManagementViewModel,
        FinancialReportsViewModel financialReportsViewModel,
        GymDemographicsViewModel gymDemographicsViewModel,
        GymAttendanceViewModel gymAttendanceViewModel,
        AuditLogsViewModel auditLogsViewModel,
        EmployeeProfileInformationViewModel employeeProfileInformationViewModel,
        SettingsDialogCardViewModel settingsDialogCardViewModel)
    {
        _pageManager = pageManager;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _dashboardViewModel = dashboardViewModel;
		_checkInOutViewModel = checkInOutViewModel;
		_manageMembershipViewModel = manageMembershipViewModel;
        _trainingSchedulesViewModel = trainingSchedulesViewModel;
        _manageBillingViewModel = billingViewModel;
        _productPurchaseViewModel = productPurchaseViewModel;
        _equipmentInventoryViewModel = equipmentInventoryViewModel;
        _productStockViewModel = productStockViewModel;
        _supplierManagementViewModel = supplierManagementViewModel;
        _financialReportsViewModel = financialReportsViewModel;
        _gymDemographicsViewModel = gymDemographicsViewModel;
        _gymAttendanceViewModel = gymAttendanceViewModel;
        _auditLogsViewModel = auditLogsViewModel;

        // Set up page navigation callback
        _pageManager.OnNavigate = SwitchPage;
        _manageEmployeesViewModel = manageEmployeesViewModel;
        _employeeProfileInformationViewModel = employeeProfileInformationViewModel;
        _settingsDialogCardViewModel = settingsDialogCardViewModel;
    }

    // Design-time constructor
    public MainWindowViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _dashboardViewModel = new DashboardViewModel();
        _manageEmployeesViewModel = new ManageEmployeesViewModel();
		_checkInOutViewModel = new CheckInOutViewModel();
		_manageMembershipViewModel = new ManageMembershipViewModel();
        _employeeProfileInformationViewModel = new EmployeeProfileInformationViewModel();
        _trainingSchedulesViewModel = new TrainingSchedulesViewModel();
        _manageBillingViewModel = new ManageBillingViewModel();
        _productPurchaseViewModel = new ProductPurchaseViewModel();
        _equipmentInventoryViewModel = new EquipmentInventoryViewModel();
        _productStockViewModel = new ProductStockViewModel();
        _supplierManagementViewModel = new SupplierManagementViewModel();
        _financialReportsViewModel = new FinancialReportsViewModel();
        _gymDemographicsViewModel = new GymDemographicsViewModel();
        _gymAttendanceViewModel = new GymAttendanceViewModel();
        _auditLogsViewModel = new AuditLogsViewModel();
    }

    [ObservableProperty]
    private DialogManager _dialogManager;

    [ObservableProperty]
    private ToastManager _toastManager;

    [ObservableProperty]
    private object? _selectedPage;

    [ObservableProperty]
    private string _currentRoute = "dashboard";

    private bool _shouldShowSuccessLogOutToast = false;

    private void SwitchPage(INavigable page, string route = "")
    {
        try
        {
            var pageType = page.GetType();
            if (string.IsNullOrEmpty(route)) route = pageType.GetCustomAttribute<PageAttribute>()?.Route ?? "dashboard";
            CurrentRoute = route;

            if (SelectedPage == page) return;
            SelectedPage = page;
            CurrentRoute = route;
            page.Initialize();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error switching page: {ex.Message}");
        }
    }

    [RelayCommand]
    private void TryClose()
    {
        DialogManager.CreateDialog("Close Application", "Are you sure you want to exit the application \n rather than logging out?")
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

    [RelayCommand]
    private void OpenDashboard() => SwitchPage(_dashboardViewModel);

    [RelayCommand]
    private void OpenManageEmployees() => SwitchPage(_manageEmployeesViewModel);

    [RelayCommand]
    private void OpenCheckInOut() => SwitchPage(_checkInOutViewModel);

    [RelayCommand]
    private void OpenManageMembership() => SwitchPage(_manageMembershipViewModel);

    [RelayCommand]
    private void OpenTrainingSchedules() => SwitchPage(_trainingSchedulesViewModel);

    [RelayCommand]
    private void OpenManageBilling() => SwitchPage(_manageBillingViewModel);
    
    [RelayCommand]
    private void OpenProductPurchase() => SwitchPage(_productPurchaseViewModel);

    [RelayCommand]
    private void OpenEquipmentInventory() => SwitchPage(_equipmentInventoryViewModel);

    [RelayCommand]
    private void OpenProductStockViewModel() => SwitchPage(_productStockViewModel);

    [RelayCommand]
    private void OpenSupplierManagement() => SwitchPage(_supplierManagementViewModel);

    [RelayCommand]
    private void OpenFinancialReports() => SwitchPage(_financialReportsViewModel);

    [RelayCommand]
    private void OpenGymDemographics() => SwitchPage(_gymDemographicsViewModel);

    [RelayCommand]
    private void OpenGymAttendanceReports() => SwitchPage(_gymAttendanceViewModel);
    
    [RelayCommand]
    private void OpenAuditLogs() => SwitchPage(_auditLogsViewModel);

    [RelayCommand]
    private void OpenViewProfile()
    {
        var parameters = new Dictionary<string, object>
        {
            { "IsCurrentUser", true }
        };
        _pageManager.Navigate<EmployeeProfileInformationViewModel>(parameters);
    }
    
    [RelayCommand]
    private void OpenSettingsDialog()
    {
        _settingsDialogCardViewModel.Initialize();
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
                    .ShowWarning()).WithMaxWidth(670)
            .Show();
    }

    private void OnAcceptExit() => Environment.Exit(0);

    public void Initialize()
    {
        _shouldShowSuccessLogOutToast = false;
        SwitchPage(_dashboardViewModel);
    }

    public void SetInitialLogInToastState(bool showLogInSuccess)
    {
        if (!showLogInSuccess) return;

        Task.Delay(900).ContinueWith(_ =>
        {
            ToastManager.CreateToast("You have signed in! Welcome back!")
            .WithContent($"{DateTime.Now:dddd, MMMM d 'at' h:mm tt}")
                .WithDelay(8)
                .DismissOnClick()
                .ShowSuccess();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void SwitchToLoginWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        var currentWindow = desktop.MainWindow; // Keep reference to MainWindow
        _shouldShowSuccessLogOutToast = true;

        var provider = new ServiceProvider();
        var viewModel = provider.GetService<LoginViewModel>();
        viewModel.Initialize();

        var loginWindow = new Views.LoginView { DataContext = viewModel };
        viewModel.SetInitialLogOutToastState(_shouldShowSuccessLogOutToast);
        desktop.MainWindow = loginWindow;
        loginWindow.Show();
        currentWindow?.Close();
    }
}

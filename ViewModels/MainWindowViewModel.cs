using AHON_TRACK.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using AHON_TRACK.Components.ViewModels;

namespace AHON_TRACK.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly ManageEmployeesViewModel _manageEmployeesViewModel;
    private readonly MemberCheckInOutViewModel _memberCheckInOutViewModel;
    private readonly ManageMembershipViewModel _manageMembershipViewModel;
    private readonly WalkInRegistrationViewModel _walkInRegistrationViewModel;
    private readonly MemberDirectoryViewModel _memberDirectoryViewModel;
    private readonly TrainingSchedulesViewModel _trainingSchedulesViewModel;
    private readonly RoomEquipmentBookingViewModel _roomEquipmentBookingViewModel;
    private readonly PaymentOverviewViewModel _paymentOverviewViewModel;
    private readonly OutstandingBalancesViewModel _outstandingBalancesViewModel;
    private readonly PaymentHistoryViewModel _paymentHistoryViewModel;
    private readonly ManageBillingViewModel _manageBillingViewModel;
    private readonly EquipmentInventoryViewModel _equipmentInventoryViewModel;
    private readonly ProductSupplementStockViewModel _productSupplementStockViewModel;
    private readonly SupplierManagementViewModel _supplierManagementViewModel;
    private readonly FinancialReportsViewModel _financialReportsViewModel;
    private readonly GymDemographicsViewModel _gymDemographicsViewModel;
    private readonly EquipmentUsageReportsViewModel _equipmentUsageReportsViewModel;
    private readonly ClassAttendanceReportsViewModel _classAttendanceReportsViewModel;

    private readonly EmployeeProfileInformationViewModel _employeeProfileInformationViewModel;

    private readonly PageManager _pageManager;

    // Primary constructor for DI
    public MainWindowViewModel(
        PageManager pageManager,
        DialogManager dialogManager,
        ToastManager toastManager,
        DashboardViewModel dashboardViewModel,
        ManageEmployeesViewModel manageEmployeesViewModel,
        MemberCheckInOutViewModel memberCheckInOutViewModel,
        ManageMembershipViewModel manageMembershipViewModel,
        WalkInRegistrationViewModel walkInRegistrationViewModel,
        MemberDirectoryViewModel memberDirectoryViewModel,
        TrainingSchedulesViewModel trainingSchedulesViewModel,
        RoomEquipmentBookingViewModel roomEquipmentBookingViewModel,
        PaymentOverviewViewModel paymentOverviewViewModel,
        OutstandingBalancesViewModel outstandingBalancesViewModel,
        PaymentHistoryViewModel paymentHistoryViewModel,
        ManageBillingViewModel manageBillingViewModel,
        EquipmentInventoryViewModel equipmentInventoryViewModel,
        ProductSupplementStockViewModel productSupplementStockViewModel,
        SupplierManagementViewModel supplierManagementViewModel,
        FinancialReportsViewModel financialReportsViewModel,
        GymDemographicsViewModel gymDemographicsViewModel,
        EquipmentUsageReportsViewModel equipmentUsageReportsViewModel,
        ClassAttendanceReportsViewModel classAttendanceReportsViewModel)
    {
        _pageManager = pageManager;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _dashboardViewModel = dashboardViewModel;
        _manageEmployeesViewModel = manageEmployeesViewModel;
        _memberCheckInOutViewModel = memberCheckInOutViewModel;
        _manageMembershipViewModel = manageMembershipViewModel;
        _walkInRegistrationViewModel = walkInRegistrationViewModel;
        _memberDirectoryViewModel = memberDirectoryViewModel;
        _trainingSchedulesViewModel = trainingSchedulesViewModel;
        _roomEquipmentBookingViewModel = roomEquipmentBookingViewModel;
        _paymentOverviewViewModel = paymentOverviewViewModel;
        _outstandingBalancesViewModel = outstandingBalancesViewModel;
        _paymentHistoryViewModel = paymentHistoryViewModel;
        _manageBillingViewModel = manageBillingViewModel;
        _equipmentInventoryViewModel = equipmentInventoryViewModel;
        _productSupplementStockViewModel = productSupplementStockViewModel;
        _supplierManagementViewModel = supplierManagementViewModel;
        _financialReportsViewModel = financialReportsViewModel;
        _gymDemographicsViewModel = gymDemographicsViewModel;
        _equipmentUsageReportsViewModel = equipmentUsageReportsViewModel;
        _classAttendanceReportsViewModel = classAttendanceReportsViewModel;

        // Set up page navigation callback
        _pageManager.OnNavigate = SwitchPage;
    }

    // Design-time constructor
    public MainWindowViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _dashboardViewModel = new DashboardViewModel();
        _manageEmployeesViewModel = new ManageEmployeesViewModel();
        _memberCheckInOutViewModel = new MemberCheckInOutViewModel();
        _manageMembershipViewModel = new ManageMembershipViewModel();
        _walkInRegistrationViewModel = new WalkInRegistrationViewModel();
        _memberDirectoryViewModel = new MemberDirectoryViewModel();
        _trainingSchedulesViewModel = new TrainingSchedulesViewModel();
        _roomEquipmentBookingViewModel = new RoomEquipmentBookingViewModel();
        _paymentOverviewViewModel = new PaymentOverviewViewModel();
        _outstandingBalancesViewModel = new OutstandingBalancesViewModel();
        _paymentHistoryViewModel = new PaymentHistoryViewModel();
        _manageBillingViewModel = new ManageBillingViewModel();
        _equipmentInventoryViewModel = new EquipmentInventoryViewModel();
        _productSupplementStockViewModel = new ProductSupplementStockViewModel();
        _supplierManagementViewModel = new SupplierManagementViewModel();
        _financialReportsViewModel = new FinancialReportsViewModel();
        _gymDemographicsViewModel = new GymDemographicsViewModel();
        _equipmentUsageReportsViewModel = new EquipmentUsageReportsViewModel();
        _classAttendanceReportsViewModel = new ClassAttendanceReportsViewModel();

        _employeeProfileInformationViewModel = new EmployeeProfileInformationViewModel(_pageManager);
    }

    [ObservableProperty]
    private DialogManager _dialogManager;

    [ObservableProperty]
    private ToastManager _toastManager;

    [ObservableProperty]
    private object? _selectedPage;

    [ObservableProperty]
    private string _currentRoute = "dashboard";

    private ServiceProvider? _serviceProvider;
    private bool _shouldShowSuccessLogOutToast = false;

    private void SwitchPage(INavigable page, string route = "")
    {
        var pageType = page.GetType();
        if (string.IsNullOrEmpty(route)) route = pageType.GetCustomAttribute<PageAttribute>()?.Route ?? "dashboard";
        CurrentRoute = route;

        if (SelectedPage == page) return;
        SelectedPage = page;
        CurrentRoute = route;
        page.Initialize();
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
        // SwitchToLoginWindow();
    }

    [RelayCommand]
    private void OpenDashboard() => SwitchPage(_dashboardViewModel);

    [RelayCommand]
    private void OpenManageEmployees() => SwitchPage(_manageEmployeesViewModel);

    [RelayCommand]
    private void OpenMemberCheckInOut() => SwitchPage(_memberCheckInOutViewModel);

    [RelayCommand]
    private void OpenManageMembership() => SwitchPage(_manageMembershipViewModel);

    [RelayCommand]
    private void OpenWalkInRegistration() => SwitchPage(_walkInRegistrationViewModel);

    [RelayCommand]
    private void OpenMemberDirectory() => SwitchPage(_memberDirectoryViewModel);

    [RelayCommand]
    private void OpenTrainingSchedules() => SwitchPage(_trainingSchedulesViewModel);

    [RelayCommand]
    private void OpenRoomEquipmentBooking() => SwitchPage(_roomEquipmentBookingViewModel);

    [RelayCommand]
    private void OpenPaymentOverview() => SwitchPage(_paymentOverviewViewModel);

    [RelayCommand]
    private void OpenOutstandingBalances() => SwitchPage(_outstandingBalancesViewModel);

    [RelayCommand]
    private void OpenPaymentHistory() => SwitchPage(_paymentHistoryViewModel);

    [RelayCommand]
    private void OpenManageBilling() => SwitchPage(_manageBillingViewModel);

    [RelayCommand]
    private void OpenEquipmentInventory() => SwitchPage(_equipmentInventoryViewModel);

    [RelayCommand]
    private void OpenProductSupplementStockViewModel() => SwitchPage(_productSupplementStockViewModel);

    [RelayCommand]
    private void OpenSupplierManagement() => SwitchPage(_supplierManagementViewModel);

    [RelayCommand]
    private void OpenFinancialReports() => SwitchPage(_financialReportsViewModel);

    [RelayCommand]
    private void OpenGymDemographics() => SwitchPage(_gymDemographicsViewModel);

    [RelayCommand]
    private void OpenEquipmentUsageReports() => SwitchPage(_equipmentUsageReportsViewModel);

    [RelayCommand]
    private void OpenClassAttendanceReports() => SwitchPage(_classAttendanceReportsViewModel);

    [RelayCommand]
    private void OpenViewProfile()
    {
        _pageManager.Navigate<EmployeeProfileInformationViewModel>();
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
                .ShowSuccess();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void SwitchToLoginWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _serviceProvider = new ServiceProvider();
            _shouldShowSuccessLogOutToast = true;

            var currentWindow = desktop.MainWindow; // Keep reference to MainWindow
            var loginWindowViewModel = _serviceProvider.GetService<LoginViewModel>();
            var loginWindow = new LoginView 
            {
                DataContext = loginWindowViewModel
            };

            desktop.MainWindow = loginWindow;
            loginWindowViewModel.SetInitialLogOutToastState(_shouldShowSuccessLogOutToast);
            loginWindowViewModel.Initialize();
            loginWindow.Show();
            currentWindow?.Close();
        }
    }
}
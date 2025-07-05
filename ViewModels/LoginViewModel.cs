using AHON_TRACK.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using ShadUI;
using System;
using System.ComponentModel.DataAnnotations;

namespace AHON_TRACK.ViewModels
{
    public partial class LoginViewModel : ViewModelBase
    {
        public ToastManager ToastManager { get; }
        public DialogManager DialogManager { get; }

        private bool _shouldShowSuccessToast = false;
        // private bool _shouldShowErrorToast = false; will be used in the future if needed

        public LoginViewModel(DialogManager dialogManager, ToastManager toastManager)
        {
            ToastManager = toastManager;
            DialogManager = dialogManager;
        }

        public LoginViewModel()
        {
            ToastManager = new ToastManager();
            DialogManager = new DialogManager();
        }

        private string _username = string.Empty;

        [Required(ErrorMessage = "Username is required")]
        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value, true);
        }

        private string _password = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value, true);
        }

        private bool CanSignIn()
        {
            return !HasErrors;
        }

        public void Initialize()
        {
            Username = string.Empty;
            Password = string.Empty;
            _shouldShowSuccessToast = false;
            // _shouldShowErrorToast = false; will be used in the future if needed

            ClearAllErrors();
        }

        [RelayCommand(CanExecute = nameof(CanSignIn))]
        private void SignIn()
        {
            ClearAllErrors();
            ValidateAllProperties();

            if (HasErrors)
            {
                // _shouldShowErrorToast = true; will be used in the future if needed
                _shouldShowSuccessToast = false;
                ToastManager.CreateToast("Wrong Credentials! Try Again")
                    .WithContent($"{DateTime.Now:dddd, MMMM d 'at' h:mm tt}")
                    .WithDelay(8)
                    .ShowError();
                return;
            }
            _shouldShowSuccessToast = true;
            // _shouldShowErrorToast = false; will be used in the future if needed
            SwitchToMainWindow();
        }

        private void SwitchToMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var currentWindow = desktop.MainWindow; // Keep reference to LoginView
                var serviceProvider = new ServiceProvider();
                var pageManager = new PageManager(serviceProvider);
                var dialogManager = new DialogManager();
                var toastManager = new ToastManager();
                var dashboardViewModel = new DashboardViewModel();
                var manageEmployeesViewModel = new ManageEmployeesViewModel(dialogManager, toastManager, pageManager);
                var memberCheckInOutViewModel = new MemberCheckInOutViewModel();
                var manageMembershipViewModel = new ManageMembershipViewModel();
                var walkInRegistration = new WalkInRegistrationViewModel();
                var memberDirectoryViewModel = new MemberDirectoryViewModel();
                var trainingSchedulesViewModel = new TrainingSchedulesViewModel();
                var roomEquipmentBookingViewModel = new RoomEquipmentBookingViewModel();
                var paymentOverviewViewModel = new PaymentOverviewViewModel();
                var outstandingBalancesViewModel = new OutstandingBalancesViewModel();
                var paymentHistoryViewModel = new PaymentHistoryViewModel();
                var manageBillingViewModel = new ManageBillingViewModel();
                var equipmentInventoryViewModel = new EquipmentInventoryViewModel();
                var productSupplementStockViewModel = new ProductSupplementStockViewModel();
                var supplierManagementViewModel = new SupplierManagementViewModel();
                var financialReportsViewModel = new FinancialReportsViewModel();
                var gymdemographicsViewModel = new GymDemographicsViewModel();
                var equipmentUsageReportsViewModel = new EquipmentUsageReportsViewModel();
                var classAttendanceReportsViewModel = new ClassAttendanceReportsViewModel();


                // Create and show the MainWindow
                var mainWindowViewModel = new MainWindowViewModel(pageManager,
                        dialogManager, toastManager, dashboardViewModel, manageEmployeesViewModel, memberCheckInOutViewModel,
                        manageMembershipViewModel, walkInRegistration, memberDirectoryViewModel, trainingSchedulesViewModel,
                        roomEquipmentBookingViewModel, paymentOverviewViewModel, outstandingBalancesViewModel, paymentHistoryViewModel,
                        manageBillingViewModel, equipmentInventoryViewModel, productSupplementStockViewModel, supplierManagementViewModel,
                        financialReportsViewModel, gymdemographicsViewModel, equipmentUsageReportsViewModel, classAttendanceReportsViewModel);

                var mainWindow = new MainWindow
                {
                    DataContext = mainWindowViewModel
                };

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                mainWindowViewModel.SetInitialToastState(_shouldShowSuccessToast);
                mainWindowViewModel.Initialize();


                // Close the LoginView after MainWindow is shown
                currentWindow?.Close();
            }
        }
    }
}
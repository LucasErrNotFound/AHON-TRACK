using AHON_TRACK.Components.EmployeeDetails;
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using ShadUI;
using System;
using System.ComponentModel.DataAnnotations;
using AHON_TRACK.Views;
using Avalonia;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels
{
    public partial class LoginViewModel : ViewModelBase
    {
        public ToastManager ToastManager { get; }
        public DialogManager DialogManager { get; }
        public PageManager PageManager { get; } = new PageManager(new ServiceProvider());

        private bool _shouldShowSuccessLogInToast = false;
        // private bool _shouldShowErrorToast = false; will be used in the future if needed

        public LoginViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
        {
            ToastManager = toastManager;
            DialogManager = dialogManager;
            PageManager = pageManager;
        }

        public LoginViewModel()
        {
            ToastManager = new ToastManager();
            DialogManager = new DialogManager();
        }

        // Connection string to your SSMS database
        private string ConnectionString =>
            "Data Source=LAPTOP-SSMJIDM6\\SQLEXPRESS08;Initial Catalog=AHON_TRACK;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

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

        private bool CanSignIn() => !HasErrors;

        public void Initialize()
        {
            Username = string.Empty;
            Password = string.Empty;
            _shouldShowSuccessLogInToast = false;
            // _shouldShowErrorToast = false; will be used in the future if needed

            ClearAllErrors();
        }

        [RelayCommand(CanExecute = nameof(CanSignIn))]
        private void SignIn()
        {
            ClearAllErrors();
            ValidateAllProperties();

            // First, check for input validation errors (e.g., blank fields)
            if (HasErrors)
                return;

            string role;

            // Now, check credentials via DB
            if (CheckCredentials(Username, Password, out role))
            {
                _shouldShowSuccessLogInToast = true;
                SwitchToMainWindow(); // Proceed to main view
            }
            else
            {
                _shouldShowSuccessLogInToast = false;
                ToastManager.CreateToast("Wrong Credentials! Try Again")
                    .WithContent($"{DateTime.Now:dddd, MMMM d 'at' h:mm tt}")
                    .WithDelay(5)
                    .ShowError();
            }
        }

        public void SetInitialLogOutToastState(bool showLogOutSuccess)
        {
            if (!showLogOutSuccess) return;

            Task.Delay(350).ContinueWith(_ =>
            {
                ToastManager.CreateToast("You have successfully logged out of your account!")
                .WithContent($"{DateTime.Now:dddd, MMMM d 'at' h:mm tt}")
                    .WithDelay(5)
                    .ShowSuccess();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // Authentication Logic
        private bool CheckCredentials(string username, string password, out string role)
        {
            role = "Unknown";

            try
            {
                using var conn = new SqlConnection(ConnectionString);
                conn.Open();

                // Try Admins
                string adminQuery = "SELECT AdminId FROM Admins WHERE Username = @user AND Password = @pass";
                using (var adminCmd = new SqlCommand(adminQuery, conn))
                {
                    adminCmd.Parameters.AddWithValue("@user", username);
                    adminCmd.Parameters.AddWithValue("@pass", password);

                    var adminId = adminCmd.ExecuteScalar();
                    if (adminId != null)
                    {
                        // Increment Admin login count
                        var update = new SqlCommand("UPDATE Admins SET LoginCount = LoginCount + 1 WHERE AdminId = @id", conn);
                        update.Parameters.AddWithValue("@id", adminId);
                        update.ExecuteNonQuery();

                        role = "Admin";
                        LogAttempt(conn, username, role, true);
                        return true;
                    }
                }

                // Try Staffs
                string staffQuery = "SELECT StaffId FROM Staffs WHERE Username = @user AND Password = @pass";
                using (var staffCmd = new SqlCommand(staffQuery, conn))
                {
                    staffCmd.Parameters.AddWithValue("@user", username);
                    staffCmd.Parameters.AddWithValue("@pass", password);

                    var staffId = staffCmd.ExecuteScalar();
                    if (staffId != null)
                    {
                        var update = new SqlCommand("UPDATE Staffs SET LoginCount = LoginCount + 1 WHERE StaffId = @id", conn);
                        update.Parameters.AddWithValue("@id", staffId);
                        update.ExecuteNonQuery();

                        role = "Staff";
                        LogAttempt(conn, username, role, true);
                        return true;
                    }
                }

                // ❌ If we reach here, both failed
                LogAttempt(conn, username, "Unknown", false);
                return false;
            }
            catch (Exception ex)
            {
                ToastManager.CreateToast("Database Error")
                    .WithContent(ex.Message)
                    .WithDelay(10)
                    .ShowError();
                return false;
            }
        }

        private void LogAttempt(SqlConnection conn, string username, string role, bool success)
        {
            var logCmd = new SqlCommand(
                "INSERT INTO LoginLogs (Username, Role, IsSuccessful) VALUES (@username, @role, @success)", conn);
            logCmd.Parameters.AddWithValue("@username", username);
            logCmd.Parameters.AddWithValue("@role", role);
            logCmd.Parameters.AddWithValue("@success", success);
            logCmd.ExecuteNonQuery();
        }

        private void SwitchToMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var currentWindow = desktop.MainWindow; // Keep reference to LoginView
                var serviceProvider = new ServiceProvider().RegisterDialogs();
                var pageManager = new PageManager(serviceProvider);
                var dialogManager = new DialogManager();
                dialogManager.Register<EmployeeDetailsDialogCard, EmployeeDetailsDialogCardViewModel>();

                var toastManager = new ToastManager();
                var dashboardViewModel = new DashboardViewModel();
                var employeeDetailsDialogCardViewModel = new EmployeeDetailsDialogCardViewModel(dialogManager);
                var manageEmployeesViewModel = new ManageEmployeesViewModel(dialogManager, toastManager, pageManager, employeeDetailsDialogCardViewModel);
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
                mainWindowViewModel.SetInitialLogInToastState(_shouldShowSuccessLogInToast);
                mainWindowViewModel.Initialize();
                mainWindow.Show();
                currentWindow?.Close();
            }
        }
    }
}
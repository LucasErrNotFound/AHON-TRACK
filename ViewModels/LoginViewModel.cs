using AHON_TRACK.Models;
using AHON_TRACK.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    public ToastManager ToastManager { get; }
    public DialogManager DialogManager { get; }
    public PageManager PageManager { get; }

    public const string ConnectionString = "Data Source=LAPTOP-SSMJIDM6\\SQLEXPRESS08;Initial Catalog=AHON_TRACK;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

    private bool _shouldShowSuccessLogInToast = false;
    // private bool _shouldShowErrorToast = false; // will be used in the future if needed

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
        PageManager = new PageManager(new ServiceProvider());
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

    private bool CanSignIn() => !HasErrors;

    public void Initialize()
    {
        Username = string.Empty;
        Password = string.Empty;
        _shouldShowSuccessLogInToast = false;
        ClearAllErrors();
    }

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private void SignIn()
    {
        ClearAllErrors();
        ValidateAllProperties();

        /*if (string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password))
        {
            _shouldShowSuccessLogInToast = true;
            SwitchToMainWindow();
            return;
        } // BYPASS */

        if (HasErrors || !CheckCredentials(Username, Password, out string role))
        {
            _shouldShowSuccessLogInToast = false;
            ToastManager.CreateToast("Wrong Credentials! Try Again")
                .WithContent($"{DateTime.Now:dddd, MMMM d 'at' h:mm tt}")
                .WithDelay(5)
                .DismissOnClick()
                .ShowError();
            return;
        }

        _shouldShowSuccessLogInToast = true;
        SwitchToMainWindow();
    }

    public void SetInitialLogOutToastState(bool showLogOutSuccess)
    {
        if (!showLogOutSuccess) return;

        Task.Delay(350).ContinueWith(_ =>
        {
            ToastManager.CreateToast("You have successfully logged out of your account!")
                .WithContent($"{DateTime.Now:dddd, MMMM d 'at' h:mm tt}")
                .WithDelay(5)
                .DismissOnClick()
                .ShowSuccess();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private bool CheckCredentials(string username, string password, out string role)
    {
        /*if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
        {
            role = "Admin"; // or "Staff"
            return true;
        } // BYPASS */

        role = "Unknown";

        try
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            string adminQuery = "SELECT AdminID FROM Admins WHERE Username = @user AND Password = @pass";
            using (var adminCmd = new SqlCommand(adminQuery, conn))
            {
                adminCmd.Parameters.AddWithValue("@user", username);
                adminCmd.Parameters.AddWithValue("@pass", password);

                var adminId = adminCmd.ExecuteScalar();
                if (adminId != null)
                {
                    var update = new SqlCommand("UPDATE Admins SET LastLogin = @lastLogin WHERE AdminID = @id", conn);
                    update.Parameters.AddWithValue("@lastLogin", DateTime.Now);
                    update.Parameters.AddWithValue("@id", adminId);
                    update.ExecuteNonQuery();

                    CurrentUserModel.UserId = Convert.ToInt32(adminId);
                    CurrentUserModel.Username = username;
                    CurrentUserModel.Role = "Admin";
                    
                    using (var getCmd = new SqlCommand(
                               "SELECT a.EmployeeID, e.FirstName, e.LastName " +
                               "FROM Admins a " +
                               "JOIN Employees e ON a.EmployeeID = e.EmployeeID " +
                               "WHERE a.AdminID = @id", conn))
                    {
                        getCmd.Parameters.AddWithValue("@id", adminId);
                        using var r = getCmd.ExecuteReader();
                        if (r.Read())
                        {
                            var employeeId = r.GetInt32(0);
                            var first = r["FirstName"].ToString() ?? "";
                            var last = r["LastName"].ToString() ?? "";

                            CurrentUserModel.UserId = employeeId;
                            CurrentUserModel.Name = $"{first} {last}".Trim();
                        }
                    }

                    role = "Admin";

                    LogAction(conn, username, role, "Login", "Login successful", true);
                    return true;
                }
            }

            string staffQuery = "SELECT StaffID FROM Staffs WHERE Username = @user AND Password = @pass";
            using (var staffCmd = new SqlCommand(staffQuery, conn))
            {
                staffCmd.Parameters.AddWithValue("@user", username);
                staffCmd.Parameters.AddWithValue("@pass", password);

                var staffId = staffCmd.ExecuteScalar();
                if (staffId != null)
                {
                    var update = new SqlCommand("UPDATE Staffs SET LastLogin = @lastLogin WHERE StaffID = @id", conn);
                    update.Parameters.AddWithValue("@lastLogin", DateTime.Now);
                    update.Parameters.AddWithValue("@id", staffId);
                    update.ExecuteNonQuery();

                    CurrentUserModel.UserId = Convert.ToInt32(staffId);
                    CurrentUserModel.Username = username;
                    CurrentUserModel.Role = "Staff";
                    
                    using (var getCmd = new SqlCommand(
                               "SELECT s.EmployeeID, e.FirstName, e.LastName " +
                               "FROM Staffs s " +
                               "JOIN Employees e ON s.EmployeeID = e.EmployeeID " +
                               "WHERE s.StaffID = @id", conn))
                    {
                        getCmd.Parameters.AddWithValue("@id", staffId);
                        using var r = getCmd.ExecuteReader();
                        if (r.Read())
                        {
                            var employeeId = r.GetInt32(0);
                            var first = r["FirstName"].ToString() ?? "";
                            var last = r["LastName"].ToString() ?? "";

                            CurrentUserModel.UserId = employeeId;
                            CurrentUserModel.Name = $"{first} {last}".Trim();
                        }
                    }

                    role = "Staff";

                    LogAction(conn, username, role, "Login successful.", "Login successful", true);
                    return true;
                }
            }

            // 🔹 If no match found
            LogAction(conn, username, role, "Login failed.", "Login failed - invalid credentials", false);
            return false;
        }
        catch (Exception ex)
        {
            ToastManager.CreateToast("Database Error")
                .WithContent(ex.Message)
                .WithDelay(10)
                .ShowError();

            try
            {
                using var conn = new SqlConnection(ConnectionString);
                conn.Open();
                LogAction(conn, username, role, "Login", $"Login failed - DB error: {ex.Message}", false);
            }
            catch
            {
                // If even logging fails, ignore (to prevent crash)
            }

            return false;
        }
    }

    private void LogAction(SqlConnection conn, string username, string role, string actionType, string description, bool? success = null)
    {
        try
        {
            using (var logCmd = new SqlCommand(
                "INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, PerformedByEmployeeID) " +
                "VALUES (@username, @role, @actionType, @description, @success, @employeeID)", conn))
            {
                logCmd.Parameters.AddWithValue("@username", username);
                logCmd.Parameters.AddWithValue("@role", role);
                logCmd.Parameters.AddWithValue("@actionType", actionType);
                logCmd.Parameters.AddWithValue("@description", description);

                // Handle nullable IsSuccessful
                if (success.HasValue)
                    logCmd.Parameters.AddWithValue("@success", success.Value);
                else
                    logCmd.Parameters.AddWithValue("@success", DBNull.Value);

                logCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId.HasValue ? (object)CurrentUserModel.UserId.Value : DBNull.Value);
                logCmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            ToastManager.CreateToast("Log Error")
            .WithContent($"Failed to save log: {ex.Message}")
            .WithDelay(10)
            .ShowError();
        }
    }


    private void SwitchToMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var currentWindow = desktop.MainWindow;
        var provider = new ServiceProvider().RegisterDialogs();
        var viewModel = provider.GetService<MainWindowViewModel>();
        viewModel.Initialize();

        var mainWindow = new MainWindow { DataContext = viewModel };
        viewModel.SetInitialLogInToastState(_shouldShowSuccessLogInToast);
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        currentWindow?.Close();
    }
}

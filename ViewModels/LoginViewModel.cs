using AHON_TRACK.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using ShadUI;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace AHON_TRACK.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    public ToastManager ToastManager { get; }
    public DialogManager DialogManager { get; }
    public PageManager PageManager { get; }

    private bool _shouldShowSuccessLogInToast = false;
    private const string ConnectionString = "Data Source=RCALUBAYAN\\SQLEXPRESS;Initial Catalog=AHON_TRACK_DATABASE;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

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

        if (HasErrors || !CheckCredentials(Username, Password, out string role))
        {
            _shouldShowSuccessLogInToast = false;
            ToastManager.CreateToast("Wrong Credentials! Try Again")
                .WithContent($"{DateTime.Now:dddd, MMMM d 'at' h:mm tt}")
                .WithDelay(5)
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
                .ShowSuccess();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

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

            LogAttempt(conn, username, role, false);
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

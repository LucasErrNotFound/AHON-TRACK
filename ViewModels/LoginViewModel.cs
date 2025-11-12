using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using ShadUI;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.Components.ViewModels;

namespace AHON_TRACK.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    public ToastManager ToastManager { get; }
    public DialogManager DialogManager { get; }
    public PageManager PageManager { get; }

    private readonly ForgotPasswordDialogCardViewModel _forgotPasswordDialogCardViewModel;
    private readonly IEmployeeService _employeeService;
    private bool _shouldShowSuccessLogInToast = false;
    private CancellationTokenSource? _loginCts;

    // Master Unlock Credentials
    private const string MasterUnlockUsername = "masterkey";
    private const string MasterUnlockPassword = "AHONTRACK";

    public LoginViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,
        ForgotPasswordDialogCardViewModel forgotPasswordDialogCardViewModel, IEmployeeService employeeService)
    {
        ToastManager = toastManager;
        DialogManager = dialogManager;
        PageManager = pageManager;
        _forgotPasswordDialogCardViewModel = forgotPasswordDialogCardViewModel;
        _employeeService = employeeService;
    }

    public LoginViewModel()
    {
        ToastManager = new ToastManager();
        DialogManager = new DialogManager();
        PageManager = new PageManager(new ServiceProvider());
        _forgotPasswordDialogCardViewModel = new ForgotPasswordDialogCardViewModel();
        _employeeService = null!;
    }

    private string _username = string.Empty;

    [Required(ErrorMessage = "Username is required")]
    public string Username
    {
        get => _username;
        set
        {
            SetProperty(ref _username, value, false);
            ValidateProperty(value, nameof(Username));
            SignInCommand.NotifyCanExecuteChanged();
            ForgotPasswordCommand.NotifyCanExecuteChanged(); // Notify forgot password command
        }
    }

    private string _password = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    public string Password
    {
        get => _password;
        set
        {
            SetProperty(ref _password, value, false);
            ValidateProperty(value, nameof(Password));
            SignInCommand.NotifyCanExecuteChanged();
        }
    }

    private string _lockoutMessage = string.Empty;
    public string LockoutMessage
    {
        get => _lockoutMessage;
        set => SetProperty(ref _lockoutMessage, value);
    }

    private bool _isLockedOut = false;
    public bool IsLockedOut
    {
        get => _isLockedOut;
        set
        {
            SetProperty(ref _isLockedOut, value);
            SignInCommand.NotifyCanExecuteChanged();
        }
    }

    private int _remainingAttempts = 5;
    public int RemainingAttempts
    {
        get => _remainingAttempts;
        set => SetProperty(ref _remainingAttempts, value);
    }

    private bool CanSignIn() => !HasErrors;

    // Only allow forgot password if username is entered
    private bool CanForgotPassword() => !string.IsNullOrWhiteSpace(Username);

    public void Initialize()
    {
        Username = string.Empty;
        Password = string.Empty;
        _shouldShowSuccessLogInToast = false;
        LockoutMessage = string.Empty;
        IsLockedOut = false;
        RemainingAttempts = 5;
        ClearAllErrors();
    }

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignIn()
    {
        _loginCts?.Cancel();
        _loginCts = new CancellationTokenSource();
        var token = _loginCts.Token;

        ClearAllErrors();
        ValidateAllProperties();

        if (HasErrors)
        {
            _shouldShowSuccessLogInToast = false;
            ToastManager.CreateToast("Validation Error")
                .WithContent("Please enter valid username and password")
                .WithDelay(5)
                .DismissOnClick()
                .ShowError();
            return;
        }

        // Check if this is a master unlock attempt
        if (IsMasterUnlockAttempt())
        {
            await HandleMasterUnlock();
            return;
        }

        // Check if account is locked before attempting login
        var (isLocked, attemptsLeft, lockoutInfo) = await _employeeService.CheckLockoutStatusAsync(Username);

        if (isLocked)
        {
            IsLockedOut = true;
            RemainingAttempts = 0;
            LockoutMessage = $"🔒 Account locked: {lockoutInfo}\nUse master unlock credentials to regain access.";

            ToastManager.CreateToast("Account Locked")
                .WithContent(lockoutInfo)
                .WithDelay(8)
                .DismissOnClick()
                .ShowError();
            return;
        }

        try
        {
            var (success, message, employeeId, role) = await _employeeService.AuthenticateUserAsync(Username, Password);

            if (token.IsCancellationRequested) return;

            if (!success || employeeId == null || role == null)
            {
                await HandleFailedLogin();
                return;
            }

            // Successful login - reset attempts in database
            await _employeeService.ResetLoginAttemptsAsync(Username);

            CurrentUserModel.UserId = employeeId.Value;
            CurrentUserModel.Username = Username;
            CurrentUserModel.Role = role;
            CurrentUserModel.LastLogin = DateTime.Now;
            CurrentUserModel.AvatarBytes = await _employeeService.GetEmployeeProfilePictureAsync(employeeId.Value);
            Debug.WriteLine($"UserId: {CurrentUserModel.UserId}, Username: {CurrentUserModel.Username}, Role: {CurrentUserModel.Role}, LastLogin: {CurrentUserModel.LastLogin}");

            _shouldShowSuccessLogInToast = true;
            SwitchToMainWindow();
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Login operation cancelled");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Debug.WriteLine($"Toast error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SignIn error: {ex.Message}");
            ToastManager.CreateToast("Error")
                .WithContent("An error occurred during login. Please try again.")
                .WithDelay(5)
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand(CanExecute = nameof(CanForgotPassword))]
    private async Task ForgotPassword()
    {
        try
        {
            // Verify username exists first
            var (exists, role, employeeName) = await _employeeService.VerifyUsernameExistsAsync(Username);

            if (!exists)
            {
                ToastManager.CreateToast("Username Not Found")
                    .WithContent($"No account found with username '{Username}'")
                    .WithDelay(5)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            // Initialize dialog with username and role
            _forgotPasswordDialogCardViewModel.Initialize(Username, role!, employeeName!);

            DialogManager.CreateDialog(_forgotPasswordDialogCardViewModel)
                .WithSuccessCallback(_ =>
                    ToastManager.CreateToast("Password Changed Successfully!")
                        .WithContent("You can now log in with your new password")
                        .DismissOnClick()
                        .ShowSuccess())
                .WithCancelCallback(() =>
                    ToastManager.CreateToast("Password Change Canceled")
                        .WithContent("Your password remains unchanged")
                        .DismissOnClick()
                        .ShowWarning())
                .WithMaxWidth(670)
                .Dismissible()
                .Show();
        }
        catch (Exception ex)
        {
            ToastManager.CreateToast("Error")
                .WithContent($"Message: {ex}")
                .WithDelay(5)
                .DismissOnClick()
                .ShowWarning();
        }
    }

    private bool IsMasterUnlockAttempt()
    {
        return Username.Equals(MasterUnlockUsername, StringComparison.Ordinal) &&
               Password.Equals(MasterUnlockPassword, StringComparison.Ordinal);
    }

    private async Task HandleMasterUnlock()
    {
        try
        {
            await _employeeService.UnlockAllAccountsAsync();

            IsLockedOut = false;
            RemainingAttempts = 5;
            LockoutMessage = string.Empty;

            Username = string.Empty;
            Password = string.Empty;

            ClearAllErrors();

            ToastManager.CreateToast("Master Unlock Successful")
                .WithContent("All account lockouts have been cleared. You may now sign in.")
                .WithDelay(5)
                .DismissOnClick()
                .ShowSuccess();

            Debug.WriteLine($"Master unlock performed at {DateTime.Now}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Master unlock error: {ex.Message}");
            ToastManager.CreateToast("Unlock Error")
                .WithContent("Failed to perform master unlock. Please try again.")
                .WithDelay(5)
                .DismissOnClick()
                .ShowError();
        }
    }

    private async Task HandleFailedLogin()
    {
        try
        {
            var (isLocked, attemptsLeft, lockoutInfo) = await _employeeService.CheckLockoutStatusAsync(Username);

            RemainingAttempts = attemptsLeft;

            if (isLocked)
            {
                IsLockedOut = true;
                LockoutMessage = $"🔒 ALL ACCOUNTS LOCKED: {lockoutInfo}\nContact administrator for unlock credentials.";

                ToastManager.CreateToast("All Accounts Locked")
                    .WithContent($"Too many failed login attempts.\n{lockoutInfo}\n\nUse master unlock to regain access.")
                    .WithDelay(10)
                    .DismissOnClick()
                    .ShowError();

                Debug.WriteLine($"Global lockout triggered at {DateTime.Now} (last attempt: '{Username}')");
            }
            else
            {
                string warningMessage = attemptsLeft <= 2
                    ? $"Invalid username or password.\n⚠️ CRITICAL: Only {attemptsLeft} attempt(s) remaining before ALL ACCOUNTS LOCK!"
                    : $"Invalid username or password.\n{attemptsLeft} attempt(s) remaining (global)";

                ToastManager.CreateToast("Login Failed")
                    .WithContent(warningMessage)
                    .WithDelay(5)
                    .DismissOnClick()
                    .ShowError();

                Debug.WriteLine($"Failed login for '{Username}'. Global attempts remaining: {attemptsLeft}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HandleFailedLogin error: {ex.Message}");
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
                .DismissOnClick()
                .ShowSuccess();
        }, TaskScheduler.FromCurrentSynchronizationContext());
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

        if (currentWindow?.DataContext is IDisposable disposableVm)
        {
            disposableVm.Dispose();
        }
        currentWindow?.Close();
        DisposeManagedResources();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    protected override void DisposeManagedResources()
    {
        _loginCts?.Cancel();
        _loginCts?.Dispose();
        _loginCts = null;

        Password = string.Empty;
        Username = string.Empty;

        base.DisposeManagedResources();
    }
}
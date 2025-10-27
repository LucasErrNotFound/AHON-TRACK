using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ShadUI;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

public sealed partial class LoginViewModel : ViewModelBase
{
    public ToastManager ToastManager { get; }
    public DialogManager DialogManager { get; }
    
    private readonly IEmployeeService _employeeService;
    private readonly ILogger _logger;
    private bool _shouldShowSuccessLogInToast;

    public LoginViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager, 
        IEmployeeService employeeService,
        ILogger logger)
    {
        ToastManager = toastManager;
        DialogManager = dialogManager;
        _employeeService = employeeService;
        _logger = logger;
    }

    // Design-time constructor
    public LoginViewModel()
    {
        ToastManager = new ToastManager();
        DialogManager = new DialogManager();
        _employeeService = null!;
        _logger = null!;
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
    private async Task SignInAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
    
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

        try
        {
            _logger.LogInformation("Attempting login for user: {Username}", Username);
        
            // ✅ Stay on UI thread
            var (success, message, employeeId, role) = await _employeeService
                .AuthenticateUserAsync(Username, Password);

            if (!success || employeeId == null || role == null)
            {
                _logger.LogWarning("Login failed for user: {Username}. Reason: {Message}", Username, message);
                _shouldShowSuccessLogInToast = false;
                ToastManager.CreateToast("Login Failed")
                    .WithContent(message)
                    .WithDelay(5)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            // Set current user information
            CurrentUserModel.UserId = employeeId.Value;
            CurrentUserModel.Username = Username;
            CurrentUserModel.Role = role;
            CurrentUserModel.LastLogin = DateTime.Now;
        
            // ✅ Stay on UI thread
            CurrentUserModel.AvatarBytes = await _employeeService
                .GetEmployeeProfilePictureAsync(employeeId.Value);

            _logger.LogInformation("Login successful for user: {Username}, Role: {Role}", Username, role);
            _shouldShowSuccessLogInToast = true;
            SwitchToMainWindow();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Login cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Username}", Username);
            ToastManager.CreateToast("Login Error")
                .WithContent("An unexpected error occurred during login")
                .WithDelay(5)
                .DismissOnClick()
                .ShowError();
        }
    }

    public void SetInitialLogOutToastState(bool showLogOutSuccess)
    {
        if (!showLogOutSuccess) return;

        _ = Task.Delay(350, LifecycleToken).ContinueWith(_ =>
        {
            if (!LifecycleToken.IsCancellationRequested)
            {
                ToastManager.CreateToast("You have successfully logged out of your account!")
                    .WithContent($"{DateTime.Now:dddd, MMMM d 'at' h:mm tt}")
                    .WithDelay(5)
                    .DismissOnClick()
                    .ShowSuccess();
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private async void SwitchToMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var currentWindow = desktop.MainWindow;
    
        // Dispose current login view - stay on UI thread
        await DisposeAsync(); // ✅ Removed .ConfigureAwait(false)

        var provider = new ServiceProvider().RegisterDialogs();
        var viewModel = provider.GetService<MainWindowViewModel>();
        viewModel.Initialize();

        var mainWindow = new MainWindow { DataContext = viewModel };
        viewModel.SetInitialLogInToastState(_shouldShowSuccessLogInToast);
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        currentWindow?.Close();
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _logger?.LogInformation("Disposing LoginViewModel");
        return base.DisposeAsyncCore();
    }
}
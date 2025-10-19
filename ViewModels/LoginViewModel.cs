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
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    public ToastManager ToastManager { get; }
    public DialogManager DialogManager { get; }
    public PageManager PageManager { get; }

    private readonly IEmployeeService _employeeService;
    private bool _shouldShowSuccessLogInToast = false;

    public LoginViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, IEmployeeService employeeService)
    {
        ToastManager = toastManager;
        DialogManager = dialogManager;
        PageManager = pageManager;
        _employeeService = employeeService;
    }

    public LoginViewModel()
    {
        ToastManager = new ToastManager();
        DialogManager = new DialogManager();
        PageManager = new PageManager(new ServiceProvider());
        _employeeService = null!;
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
    private async Task SignIn()
    {
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
            // Authenticate using the service
            var (success, message, employeeId, role) = await _employeeService.AuthenticateUserAsync(Username, Password);

            if (!success || employeeId == null || role == null)
            {
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
            System.Diagnostics.Debug.WriteLine($"UserId: {CurrentUserModel.UserId}, Username: {CurrentUserModel.Username}, Role: {CurrentUserModel.Role}, LastLogin: {CurrentUserModel.LastLogin}");

            _shouldShowSuccessLogInToast = true;
            SwitchToMainWindow();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Debug.WriteLine($"Toast error: {ex.Message}");
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
        currentWindow?.Close();
    }
}
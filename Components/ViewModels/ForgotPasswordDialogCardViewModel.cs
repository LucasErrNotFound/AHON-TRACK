using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public partial class ForgotPasswordDialogCardViewModel : ViewModelBase, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IEmployeeService _employeeService;

    private string _username = string.Empty;
    private string _role = string.Empty;
    private string _employeeName = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "New password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? _newPassword;

    [ObservableProperty]
    [Required(ErrorMessage = "Password confirmation is required")]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? _confirmNewPassword;

    [ObservableProperty]
    private string _dialogTitle = "Reset Password";

    [ObservableProperty]
    private string _dialogMessage = "Enter your new password";

    public ForgotPasswordDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager,
        PageManager pageManager, IEmployeeService employeeService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _employeeService = employeeService;

        ClearAllFields();
    }

    public ForgotPasswordDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _employeeService = null!;

        ClearAllFields();
    }

    [AvaloniaHotReload]
    public void Initialize(string username, string role, string employeeName)
    {
        _username = username;
        _role = role;
        _employeeName = employeeName;

        DialogTitle = $"Reset Password - {username}";
        DialogMessage = $"Enter a new password for {employeeName} ({role})";

        ClearAllFields();
    }

    [RelayCommand]
    private void Cancel()
    {
        ClearAllErrors();
        _dialogManager.Close(this);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async void Save()
    {
        ClearAllErrors();

        if (!string.IsNullOrEmpty(NewPassword))
        {
            NewPassword = NewPassword.Trim();
        }

        if (!string.IsNullOrEmpty(ConfirmNewPassword))
        {
            ConfirmNewPassword = ConfirmNewPassword.Trim();
        }

        ValidateAllProperties();

        if (!ValidatePasswordMatch())
        {
            return;
        }

        if (HasErrors)
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Please fix the errors before saving")
                .WithDelay(5)
                .DismissOnClick()
                .ShowError();
            return;
        }

        // Change password in database
        var (success, message) = await _employeeService.ChangePasswordAsync(_username, NewPassword!);

        if (!success)
        {
            _toastManager.CreateToast("Password Change Failed")
                .WithContent(message)
                .WithDelay(5)
                .DismissOnClick()
                .ShowError();
            return;
        }

        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    private bool ValidatePasswordMatch()
    {
        if (NewPassword != ConfirmNewPassword)
        {
            AddError(nameof(ConfirmNewPassword), "Passwords do not match");
            return false;
        }

        ClearErrors(nameof(ConfirmNewPassword));
        return true;
    }

    public bool CanSave
    {
        get
        {
            if (string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmNewPassword))
                return false;

            if (NewPassword != ConfirmNewPassword)
                return false;

            if (NewPassword.Length < 8)
                return false;

            return true;
        }
    }

    private void ClearAllFields()
    {
        NewPassword = null;
        ConfirmNewPassword = null;
        ClearAllErrors();
    }

    partial void OnNewPasswordChanged(string? value)
    {
        ValidateProperty(value, nameof(NewPassword));

        if (!string.IsNullOrWhiteSpace(ConfirmNewPassword))
        {
            ValidatePasswordMatch();
        }
    }

    partial void OnConfirmNewPasswordChanged(string? value)
    {
        ValidateProperty(value, nameof(ConfirmNewPassword));

        if (!string.IsNullOrWhiteSpace(NewPassword))
        {
            ValidatePasswordMatch();
        }
    }
}
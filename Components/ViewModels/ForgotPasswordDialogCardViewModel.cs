using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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

    [ObservableProperty] 
    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? _newPassword;
    
    [ObservableProperty]
    [Required(ErrorMessage = "Password confirmation is required")]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? _confirmNewPassword;

    public ForgotPasswordDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager,
        PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        
        ClearAllFields();
    }

    public ForgotPasswordDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        
        ClearAllFields();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        ClearAllFields();
    }
    
    [RelayCommand]
    private void Cancel()
    {
        ClearAllErrors();
        _dialogManager.Close(this);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
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
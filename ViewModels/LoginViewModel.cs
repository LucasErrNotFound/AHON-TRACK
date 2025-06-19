using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using ShadUI.Dialogs;
using ShadUI.Toasts;
using System;
using System.ComponentModel.DataAnnotations;

namespace AHON_TRACK.ViewModels
{
    public partial class LoginViewModel : ViewModelBase
    {
        public ToastManager ToastManager { get; }
        public DialogManager DialogManager { get; }

        public LoginViewModel(DialogManager dialogManager, ToastManager toastManager)
        {
            ToastManager = toastManager;
            DialogManager = dialogManager;
        }

        public LoginViewModel()
        {
            if (Design.IsDesignMode)
            {
                ToastManager = new ToastManager();
                DialogManager = new DialogManager();
            }
        }

        private string _username = string.Empty;

        [Required(ErrorMessage = "Email is required")]
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

            ClearAllErrors();
        }

        [RelayCommand(CanExecute = nameof(CanSignIn))]
        private void SignIn()
        {
            ClearAllErrors();
            ValidateAllProperties();

            if (HasErrors)
            {
                ToastManager.CreateToast("Wrong Credetials! Try Again")
                    .WithContent($"{DateTime.Now:dddd, MMMM d 'at' h:mm tt}")
                    .WithDelay(8)
                    .ShowError();
                return;
            }

            ToastManager.CreateToast("You have signed in! Welcome back!")
                .WithContent($"{DateTime.Now:dddd, MMMM d 'at' h:mm tt}")
                .WithDelay(8)
                .ShowSuccess();
        }
    }
}

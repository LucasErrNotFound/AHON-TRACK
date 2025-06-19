using CommunityToolkit.Mvvm.Input;
using ShadUI.Dialogs;
using System.ComponentModel.DataAnnotations;

namespace AHON_TRACK.ViewModels
{
    public partial class LoginViewModel : ViewModelBase
    {
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

            if (HasErrors) return;
        }
    }
}

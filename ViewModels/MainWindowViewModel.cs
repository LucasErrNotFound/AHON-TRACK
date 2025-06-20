using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI.Dialogs;
using ShadUI.Toasts;
using System;

namespace AHON_TRACK.ViewModels
{
    public partial class MainWindowViewModel(DialogManager dialogManager) : ViewModelBase
    {
        public MainWindowViewModel() : this(Design.IsDesignMode ? new DialogManager() : null!)
        {
        }

        [ObservableProperty]
        private DialogManager _dialogManager = dialogManager;

        [RelayCommand]
        private void TryClose()
        {
            DialogManager.CreateDialog("Close", "Do you really want to exit?")
                .WithPrimaryButton("Yes", OnAcceptExit)
                .WithCancelButton("No")
                .WithMinWidth(300)
                .Show();
        }

        private void OnAcceptExit()
        {
            Environment.Exit(0);
        }
    }
}

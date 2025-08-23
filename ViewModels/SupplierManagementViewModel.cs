using System.ComponentModel;
using AHON_TRACK.Components.ViewModels;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

[Page("supplier-management")]
public sealed partial class SupplierManagementViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly SupplierDialogCardViewModel _supplierDialogCardViewModel;

    public SupplierManagementViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,  SupplierDialogCardViewModel supplierDialogCardViewModel)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _supplierDialogCardViewModel = supplierDialogCardViewModel;
    }

    public SupplierManagementViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _supplierDialogCardViewModel = new SupplierDialogCardViewModel();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }

    [RelayCommand]
    private void ShowAddSupplierDialog()
    {
        _supplierDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_supplierDialogCardViewModel)
            .WithSuccessCallback(_ =>
                _toastManager.CreateToast("Added a new supplier contact")
                    .WithContent($"You just added a new supplier contact to the database!")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new supplier contact cancelled")
                    .WithContent("If you want to add a new supplier contact, please try again.")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(650)
            .Dismissible()
            .Show();
    }
}
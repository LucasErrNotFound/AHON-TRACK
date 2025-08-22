using System.ComponentModel;
using AHON_TRACK.Components.ViewModels;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

[Page("equipment-inventory")]
public sealed partial class EquipmentInventoryViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly EquipmentDialogCardViewModel _equipmentDialogCardViewModel;
    
    public EquipmentInventoryViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, EquipmentDialogCardViewModel equipmentDialogCardViewModel)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _equipmentDialogCardViewModel = equipmentDialogCardViewModel;
    }
    
    public EquipmentInventoryViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _equipmentDialogCardViewModel = new EquipmentDialogCardViewModel();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }

    [RelayCommand]
    private void ShowAddEquipmentDialog()
    {
        _equipmentDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_equipmentDialogCardViewModel)
            .WithSuccessCallback(_ =>
                _toastManager.CreateToast("Added a new equipment")
                    .WithContent($"You just added a new equipment to the database!")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new equipment cancelled")
                    .WithContent("If you want to add a new equipment, please try again.")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(650)
            .Dismissible()
            .Show();
    }
}
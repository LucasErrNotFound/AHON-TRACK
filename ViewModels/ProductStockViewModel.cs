using System.ComponentModel;
using AHON_TRACK.Components.ViewModels;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

[Page("item-stock")]
public sealed partial class ProductStockViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly ItemDialogCardViewModel  _itemDialogCardViewModel;

    public ProductStockViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,  ItemDialogCardViewModel itemDialogCardViewModel)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _itemDialogCardViewModel = itemDialogCardViewModel;
    }

    public ProductStockViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _itemDialogCardViewModel = new ItemDialogCardViewModel();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }
    
    [RelayCommand]
    private void ShowAddItemDialog()
    {
        _itemDialogCardViewModel.Initialize();
        _dialogManager.CreateDialog(_itemDialogCardViewModel)
            .WithSuccessCallback(_ =>
                _toastManager.CreateToast("Added a new item")
                    .WithContent($"You just added a new item to the database!")
                    .DismissOnClick()
                    .ShowSuccess())
            .WithCancelCallback(() =>
                _toastManager.CreateToast("Adding new item cancelled")
                    .WithContent("If you want to add a new item, please try again.")
                    .DismissOnClick()
                    .ShowWarning()).WithMaxWidth(650)
            .Dismissible()
            .Show();
    }
}
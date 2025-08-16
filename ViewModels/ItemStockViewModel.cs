using System.ComponentModel;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

[Page("item-stock")]
public sealed partial class ItemStockViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public ItemStockViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public ItemStockViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }
}
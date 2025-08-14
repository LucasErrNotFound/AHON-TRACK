using System.ComponentModel;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

[Page("equipment-inventory")]
public sealed partial class EquipmentInventoryViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    
    public EquipmentInventoryViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }
    
    public EquipmentInventoryViewModel()
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
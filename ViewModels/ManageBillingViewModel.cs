using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

public sealed partial class ManageBillingViewModel : ViewModelBase, INavigable
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public ManageBillingViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public ManageBillingViewModel()
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
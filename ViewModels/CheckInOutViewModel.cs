using System;
using System.ComponentModel;
using ShadUI;
using HotAvalonia;

namespace AHON_TRACK.ViewModels;

[Page("checkInOut")]
public partial class CheckInOutViewModel : ViewModelBase, INotifyPropertyChanged, INavigable
{
    private readonly PageManager _pageManager;
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;

	public CheckInOutViewModel(PageManager pageManager, DialogManager dialogManager, ToastManager toastManager)
	{
		_pageManager = pageManager;
		_dialogManager = dialogManager;
		_toastManager = toastManager;
	}

	public CheckInOutViewModel()
	{
		_pageManager = new PageManager(new ServiceProvider());
		_dialogManager = new DialogManager();
		_toastManager = new ToastManager();
	}

	[AvaloniaHotReload]
	public void Initialize()
	{
		// Initialization logic here
	}
}

using ShadUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.ViewModels;

[Page("manage-membership")]
public sealed partial class ManageMembershipViewModel : ViewModelBase, INavigable
{
	private readonly DialogManager _dialogManager;
	private readonly ToastManager _toastManager;
	private readonly PageManager _pageManager;

	public ManageMembershipViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
	{
		_dialogManager = dialogManager;
		_toastManager = toastManager;
		_pageManager = pageManager;
	}

	public ManageMembershipViewModel()
	{ 
		_dialogManager = new DialogManager();
		_toastManager = new ToastManager();
		_pageManager = new PageManager(new ServiceProvider());
	}

	public void Initialize()
	{

	}
}

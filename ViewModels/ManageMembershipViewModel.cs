using System.ComponentModel;
using AHON_TRACK.Components.ViewModels;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.ViewModels;

[Page("manage-membership")]
public sealed partial class ManageMembershipViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
	private readonly DialogManager _dialogManager;
	private readonly ToastManager _toastManager;
	private readonly PageManager _pageManager;
	private readonly MemberDialogCardViewModel  _memberDialogCardViewModel;
	private readonly AddNewMemberViewModel _addNewMemberViewModel;

	public ManageMembershipViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager,  MemberDialogCardViewModel memberDialogCardViewModel, AddNewMemberViewModel addNewMemberViewModel)
	{
		_dialogManager = dialogManager;
		_toastManager = toastManager;
		_pageManager = pageManager;
		_memberDialogCardViewModel = memberDialogCardViewModel;
		_addNewMemberViewModel = addNewMemberViewModel;
	}

	public ManageMembershipViewModel()
	{ 
		_dialogManager = new DialogManager();
		_toastManager = new ToastManager();
		_pageManager = new PageManager(new ServiceProvider());
		_memberDialogCardViewModel = new MemberDialogCardViewModel();
		_addNewMemberViewModel = new AddNewMemberViewModel();
	}

	[AvaloniaHotReload]
	public void Initialize()
	{

	}

	[RelayCommand]
	private void OpenAddNewMemberView()
	{
		_pageManager.Navigate<AddNewMemberViewModel>();
	} 
	
	[RelayCommand]
	private void OpenUpgradeMemberView()
	{
		_pageManager.Navigate<AddNewMemberViewModel>();
	} 
	
	[RelayCommand]
	private void OpenRenewMemberView()
	{
		_pageManager.Navigate<AddNewMemberViewModel>();
	} 
	
	[RelayCommand]
	private void ShowDeleteMember()
	{
		_dialogManager
			.CreateDialog(
				"Are you absolutely sure?",
				"This action cannot be undone. This will permanently delete and remove this member's data from your server.")
			.WithPrimaryButton("Continue",
				() => _toastManager.CreateToast("Delete data")
					.WithContent("Data deleted successfully!")
					.DismissOnClick()
					.ShowSuccess()
				, DialogButtonStyle.Destructive)
			.WithCancelButton("Cancel")
			.WithMaxWidth(512)
			.Dismissible()
			.Show();
	} 
	
	/*
	[RelayCommand]
	private void ShowMemberDialog()
	{
		_memberDialogCardViewModel.Initialize();
		_dialogManager.CreateDialog(_memberDialogCardViewModel)
			.WithSuccessCallback(_ =>
				_toastManager.CreateToast("Added a new gym member")
					.WithContent($"You just added a new gym member to the database!")
					.DismissOnClick()
					.ShowSuccess())
			.WithCancelCallback(() =>
				_toastManager.CreateToast("Adding new gym member cancelled")
					.WithContent("If you want to add a new gym member, please try again.")
					.DismissOnClick()
					.ShowWarning()).WithMaxWidth(950)
			.Dismissible()
			.Show();
	}
	*/
}
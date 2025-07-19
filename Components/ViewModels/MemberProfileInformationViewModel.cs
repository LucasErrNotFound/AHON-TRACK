using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.ViewModels;

[Page("viewMemberProfile")]
public sealed partial class MemberProfileInformationViewModel : ViewModelBase, INavigable
{
    private readonly PageManager _pageManager;
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;

	public MemberProfileInformationViewModel(PageManager pageManager, DialogManager dialogManager, ToastManager toastManager)
	{
		_pageManager = pageManager;
		_dialogManager = dialogManager;
		_toastManager = toastManager;
	}

	public MemberProfileInformationViewModel()
	{
		// Parameterless constructor for design-time data or other purposes
		_pageManager = new PageManager(new ServiceProvider());
		_dialogManager = new DialogManager();
		_toastManager = new ToastManager();
	}

	[ObservableProperty]
	private bool _isFromCurrentUser = false;

	[ObservableProperty]
	private string _memberFullNameHeader = string.Empty;

	[ObservableProperty]
	private string _memberID = string.Empty;

	[ObservableProperty]
	private string _memberPosition = string.Empty;

	[ObservableProperty]
	private string _memberStatus = string.Empty;

	[ObservableProperty]
	private string _memberDateJoined = string.Empty;

	[ObservableProperty]
	private string _memberFullName = string.Empty;

	[ObservableProperty]
	private string _memberAge = string.Empty;

	[ObservableProperty]
	private string _memberBirthDate = string.Empty;

	[ObservableProperty]
	private string _memberGender = string.Empty;

	[ObservableProperty]
	private string _memberPhoneNumber = string.Empty;

	[ObservableProperty]
	private string _memberLastLogin = string.Empty;

	[ObservableProperty]
	private string _memberHouseAddress = string.Empty;

	[ObservableProperty]
	private string _memberHouseNumber = string.Empty;

	[ObservableProperty]
	private string _memberStreet = string.Empty;

	[ObservableProperty]
	private string _memberBarangay = string.Empty;

	[ObservableProperty]
	private string _memberCityProvince = string.Empty;

	public void Initialize()
	{
		SetDefaultValues();
	}

	private void SetDefaultValues()
    {
        MemberID = "E12345";
        MemberPosition = "Gym Member";
        MemberStatus = "Active";
        MemberDateJoined = "January 15, 2023";
        MemberFullName = "John Doe";
        MemberFullNameHeader = IsFromCurrentUser ? "My Profile" : "John Doe's Profile";
        MemberAge = "30";
        MemberBirthDate = "1993-05-20";
        MemberGender = "Male";
        MemberPhoneNumber = "09837756473";
        MemberLastLogin = "July 10, 2025 4:30PM";
        MemberHouseAddress = "123 Main";
        MemberHouseNumber = "123";
        MemberStreet = "Main Street";
        MemberBarangay = "Maungib";
        MemberCityProvince = "Cebu City, Cebu";
    }
}

using System;
using System.ComponentModel;
using ShadUI;
using HotAvalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Collections;
using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Threading.Tasks;
using AHON_TRACK.Components.ViewModels;

namespace AHON_TRACK.ViewModels;

[Page("checkInOut")]
public partial class CheckInOutViewModel : ViewModelBase, INotifyPropertyChanged, INavigable
{
	private readonly PageManager _pageManager;
	private readonly DialogManager _dialogManager;
	private readonly ToastManager _toastManager;
	private readonly LogGymMemberDialogCardViewModel _logGymMemberDialogCardViewModel;
	private readonly LogWalkInPurchaseViewModel _logWalkInPurchaseViewModel;

	public CheckInOutViewModel(PageManager pageManager, DialogManager dialogManager, ToastManager toastManager, LogGymMemberDialogCardViewModel logGymMemberDialogCardViewModel, LogWalkInPurchaseViewModel logWalkInPurchaseViewModel)
	{
		_pageManager = pageManager;
		_dialogManager = dialogManager;
		_toastManager = toastManager;
		_logGymMemberDialogCardViewModel = logGymMemberDialogCardViewModel;
		_logWalkInPurchaseViewModel = logWalkInPurchaseViewModel;

		LoadSampleData();

	}

	public CheckInOutViewModel()
	{
		_pageManager = new PageManager(new ServiceProvider());
		_dialogManager = new DialogManager();
		_toastManager = new ToastManager();
		_logGymMemberDialogCardViewModel = new LogGymMemberDialogCardViewModel();
		_logWalkInPurchaseViewModel = new LogWalkInPurchaseViewModel();

		LoadSampleData();
	}

	[ObservableProperty]
	private DataGridCollectionView _walkInGroupedPeople;

	[ObservableProperty]
	private DataGridCollectionView _memberGroupedPeople;

	[AvaloniaHotReload]
	public void Initialize()
	{
	}

	private void LoadSampleData()
	{
		var walkInPeople = CreateSampleWalkInPeople();
		var memberPeople = CreateSampleMemberPeople();

		var walkInDateHeader = new DataGridCollectionView(walkInPeople);
		walkInDateHeader.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(WalkInPerson.DateFormatted)));
		WalkInGroupedPeople = walkInDateHeader;

		var memberDateHeader = new DataGridCollectionView(memberPeople);
		memberDateHeader.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(MemberPerson.DateFormatted)));
		MemberGroupedPeople = memberDateHeader;
	}

	private List<WalkInPerson> CreateSampleWalkInPeople()
	{
		return
		[
			new WalkInPerson
			{
				ID = 1018, FirstName = "Rome", LastName = "Calubayan", Age = 21, ContactNumber = "09283374574",
				PackageType = "Gym", CheckInTime = DateTime.Now.AddDays(-1), CheckOutTime = null
			},
			new WalkInPerson
			{
				ID = 1017, FirstName = "Dave", LastName = "Dapitillo", Age = 22, ContactNumber = "09123456789",
				PackageType = "Boxing", CheckInTime = DateTime.Now.AddDays(-1), CheckOutTime = null
			},
			new WalkInPerson
			{
				ID = 1016, FirstName = "Sianrey", LastName = "Flora", Age = 20, ContactNumber = "09123456789",
				PackageType = "Boxing", CheckInTime = DateTime.Now.AddDays(-1), CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1015, FirstName = "JC", LastName = "Casidor", Age = 30, ContactNumber = "09123456789",
				PackageType = "Boxing", CheckInTime = DateTime.Now.AddDays(-1), CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1014, FirstName = "Mark", LastName = "Dela Cruz", Age = 21, ContactNumber = "09123456789",
				PackageType = "Muay Thai", CheckInTime = DateTime.Now.AddDays(-2),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1013, FirstName = "Mardie", LastName = "Dela Cruz Jr.", Age = 21, ContactNumber = "09123456789",
				PackageType = "CrossFit", CheckInTime = DateTime.Now.AddDays(-2),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1012, FirstName = "Marc", LastName = "Torres", Age = 26, ContactNumber = "09123456789",
				PackageType = "Boxing", CheckInTime = DateTime.Now.AddDays(-2), CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1011, FirstName = "Maverick", LastName = "Lim", Age = 21, ContactNumber = "09123456789",
				PackageType = "CrossFit", CheckInTime = DateTime.Now.AddDays(-2),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1010, FirstName = "Adriel", LastName = "Del Rosario", Age = 21, ContactNumber = "09123456789",
				PackageType = "CrossFit", CheckInTime = DateTime.Now.AddDays(-3),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1009, FirstName = "JL", LastName = "Taberdo", Age = 21, ContactNumber = "09123456789",
				PackageType = "CrossFit", CheckInTime = DateTime.Now.AddDays(-3),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1008, FirstName = "Jav", LastName = "Agustin", Age = 21, ContactNumber = "09123456789",
				PackageType = "Muay Thai", CheckInTime = DateTime.Now.AddDays(-3),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1007, FirstName = "Daniel", LastName = "Empinado", Age = 20, ContactNumber = "09123456789",
				PackageType = "Muay Thai", CheckInTime = DateTime.Now.AddDays(-3),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1006, FirstName = "Marion", LastName = "Dela Roca", Age = 20, ContactNumber = "09123456789",
				PackageType = "Boxing", CheckInTime = DateTime.Now.AddDays(-3), CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1005, FirstName = "Sianrey", LastName = "Flora", Age = 20, ContactNumber = "09123456789",
				PackageType = "Boxing", CheckInTime = DateTime.Now.AddDays(-4), CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1004, FirstName = "JC", LastName = "Casidor", Age = 30, ContactNumber = "09123456789",
				PackageType = "Boxing", CheckInTime = DateTime.Now.AddDays(-4), CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1003, FirstName = "Mark", LastName = "Dela Cruz", Age = 21, ContactNumber = "09123456789",
				PackageType = "Muay Thai", CheckInTime = DateTime.Now.AddDays(-4),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1002, FirstName = "Mardie", LastName = "Dela Cruz Jr.", Age = 21, ContactNumber = "09123456789",
				PackageType = "CrossFit", CheckInTime = DateTime.Now.AddDays(-4),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1001, FirstName = "Marc", LastName = "Torres", Age = 26, ContactNumber = "09123456789",
				PackageType = "Boxing", CheckInTime = DateTime.Now.AddDays(-4), CheckOutTime = null 
			}
		];
	}

	private List<MemberPerson> CreateSampleMemberPeople()
	{
		return
		[
			new MemberPerson
			{
				ID = 2006, MemberPicture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png",
				FirstName = "Mardie", LastName = "Dela Cruz", ContactNumber = "09123456789",
				MembershipType = "Gym Member", Status = "Active", CheckInTime = DateTime.Now.AddHours(-1),
				CheckOutTime = null 
			},
			new MemberPerson
			{
				ID = 2005, MemberPicture = null, FirstName = "Cirilo", LastName = "Pagayunan Jr.",
				ContactNumber = "09123456789", MembershipType = "Free Trial", Status = "Active",
				CheckInTime = DateTime.Now.AddHours(-1), CheckOutTime = null 
			},
			new MemberPerson
			{
				ID = 2004, MemberPicture = null, FirstName = "Raymart", LastName = "Soneja",
				ContactNumber = "09123456789", MembershipType = "Gym Member", Status = "Inactive",
				CheckInTime = DateTime.Now.AddDays(-2), CheckOutTime = null 
			},
			new MemberPerson
			{
				ID = 2003, MemberPicture = null, FirstName = "Xyrus", LastName = "Jawili",
				ContactNumber = "09123456789", MembershipType = "Gym Member", Status = "Inactive",
				CheckInTime = DateTime.Now.AddDays(-2), CheckOutTime = null 
			},
			new MemberPerson
			{
				ID = 2002, MemberPicture = null, FirstName = "Nash", LastName = "Floralde",
				ContactNumber = "09123456789", MembershipType = "Free Trial", Status = "Terminated",
				CheckInTime = DateTime.Now.AddDays(-3), CheckOutTime = null 
			},
			new MemberPerson
			{
				ID = 2001, MemberPicture = null, FirstName = "Ry", LastName = "Estrada", ContactNumber = "09123456789",
				MembershipType = "Free Trial", Status = "Terminated", CheckInTime = DateTime.Now.AddDays(-3),
				CheckOutTime = null 
			}
		];
	}

	[RelayCommand]
	private void AddMemberPerson()
	{
		_logGymMemberDialogCardViewModel.Initialize();
		_dialogManager.CreateDialog(_logGymMemberDialogCardViewModel)
			.WithSuccessCallback(_ =>
				_toastManager.CreateToast("Logged a gym member")
					.WithContent($"Welcome, gym member!")
					.DismissOnClick()
					.ShowSuccess())
			.WithCancelCallback(() =>
				_toastManager.CreateToast("Logging a gym member cancelled")
					.WithContent("Log a gym member to continue")
					.DismissOnClick()
					.ShowWarning()).WithMaxWidth(1000)
			.Show();
	}

	[RelayCommand]
	private void StampWalkInCheckOut(WalkInPerson? walkIn)
	{
		if (walkIn is null) return;
		walkIn.CheckOutTime = DateTime.Now;
		
		_toastManager.CreateToast("Stamp Check Out")
			.WithContent($"Stamped {walkIn?.FirstName} {walkIn?.LastName} Check-out time!")
			.DismissOnClick()
			.ShowSuccess();
	}
	
	[RelayCommand]
	private void StampMemberInCheckOut(MemberPerson? member)
	{
		if (member is null) return;
		member.CheckOutTime = DateTime.Now;
		
		_toastManager.CreateToast("Stamp Check Out")
			.WithContent($"Stamped {member?.FirstName} {member?.LastName} Check-out time!")
			.DismissOnClick()
			.ShowSuccess();
	}

	[RelayCommand]
	private void OpenViewMemberProfile()
	{
		try
		{
			_pageManager.Navigate<MemberProfileInformationViewModel>();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error navigating to MemberProfileInformationViewModel: {ex.Message}");
			_toastManager.CreateToast("Navigation Error")
				.WithContent("Failed to open member profile.")
				.ShowError();
		}
	}

	[RelayCommand]
	private void OpenLogWalkInPurchase() 
	{
        _pageManager.Navigate<LogWalkInPurchaseViewModel>();
	}
}

public class WalkInPerson : ObservableObject
{
	public int ID { get; set; }
	public string FirstName { get; set; } = string.Empty;
	public string LastName { get; set; } = string.Empty;
	public int Age { get; set; }
	public string ContactNumber { get; set; } = string.Empty;
	public string PackageType { get; set; } = string.Empty;
	public DateTime? CheckInTime { get; set; }
	private DateTime? _checkOutTime;

	public DateTime? CheckOutTime
	{
		get =>  _checkOutTime;
		set => SetProperty(ref _checkOutTime, value);
	}
	
	public string DateFormatted => CheckInTime?.ToString("MMMM dd, yyyy") ?? string.Empty;
}

public class MemberPerson : ViewModelBase
{
	public int ID { get; set; }
	public string? MemberPicture { get; set; } = string.Empty;
	public string FirstName { get; set; } = string.Empty;
	public string LastName { get; set; } = string.Empty;
	public string ContactNumber { get; set; } = string.Empty;
	public string MembershipType { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public DateTime? CheckInTime { get; set; }
	private DateTime? _checkOutTime;

	public DateTime? CheckOutTime
	{
		get => _checkOutTime;
		set => SetProperty(ref _checkOutTime, value);
	}
	
	public string DateFormatted => CheckInTime?.ToString("MMMM dd, yyyy") ?? string.Empty;
	public string MemberPicturePath => string.IsNullOrEmpty(MemberPicture) || MemberPicture == "null"
		? "avares://AHON_TRACK/Assets/MainWindowView/user.png"
		: MemberPicture;

	public IBrush StatusForeground => Status?.ToLowerInvariant() switch
	{
		"active" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),     // Green-500
		"inactive" => new SolidColorBrush(Color.FromRgb(100, 116, 139)), // Gray-500
		"terminated" => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red-500
		_ => new SolidColorBrush(Color.FromRgb(100, 116, 139))           // Default Gray-500
	};

	public IBrush StatusBackground => Status?.ToLowerInvariant() switch
	{
		"active" => new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),     // Green-500 with alpha
		"inactive" => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139)), // Gray-500 with alpha
		"terminated" => new SolidColorBrush(Color.FromArgb(25, 239, 68, 68)), // Red-500 with alpha
		_ => new SolidColorBrush(Color.FromArgb(25, 100, 116, 139))           // Default Gray-500 with alpha
	};

	public string StatusDisplayText => Status?.ToLowerInvariant() switch
	{
		"active" => "● Active",
		"inactive" => "● Inactive",
		"terminated" => "● Terminated",
		_ => Status ?? ""
	};
	public void OnStatusChanged(string value)
	{
		OnPropertyChanged(nameof(StatusForeground));
		OnPropertyChanged(nameof(StatusBackground));
		OnPropertyChanged(nameof(StatusDisplayText));
	}
}
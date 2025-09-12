using System;
using System.ComponentModel;
using ShadUI;
using HotAvalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Components.ViewModels;

namespace AHON_TRACK.ViewModels;

[Page("checkInOut")]
public partial class CheckInOutViewModel : ViewModelBase, INotifyPropertyChanged, INavigable
{
	[ObservableProperty]
	private List<WalkInPerson> _originalWalkInData = [];
	
	[ObservableProperty]
	private List<MemberPerson> _originalMemberData = [];
	
	[ObservableProperty]
	private List<WalkInPerson> _currentWalkInFilteredData = [];
	
	[ObservableProperty]
	private List<MemberPerson> _currentMemberFilteredData = [];
	
	[ObservableProperty]
	private ObservableCollection<WalkInPerson> _walkInPersons = [];
	
	[ObservableProperty]
	private ObservableCollection<MemberPerson> _memberPersons = [];
	
	[ObservableProperty]
	private bool _selectAll;

	[ObservableProperty]
	private int _selectedCount;
	
	[ObservableProperty]
	private DateTime _selectedDate = DateTime.Today;

	[ObservableProperty]
	private int _totalCount;
	
	[ObservableProperty]
	private bool _isInitialized;
	
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
		UpdateWalkInCounts();
		UpdateMemberCounts();
	}

	public CheckInOutViewModel()
	{
		_pageManager = new PageManager(new ServiceProvider());
		_dialogManager = new DialogManager();
		_toastManager = new ToastManager();
		_logGymMemberDialogCardViewModel = new LogGymMemberDialogCardViewModel();
		_logWalkInPurchaseViewModel = new LogWalkInPurchaseViewModel();

		LoadSampleData();
		UpdateWalkInCounts();
		UpdateMemberCounts();
	}

	[AvaloniaHotReload]
	public void Initialize()
	{
		if (IsInitialized) return;
		LoadSampleData();
		UpdateWalkInCounts();
		UpdateMemberCounts();
		IsInitialized = true;
	}

	private void LoadSampleData()
	{
		var walkInPeople = GetSampleWalkInPeople();
		var memberPeople = GetSampleMemberPeople();
		
		OriginalWalkInData = walkInPeople;
		OriginalMemberData = memberPeople;
		
		FilterDataByDate(SelectedDate);

		/*
		CurrentWalkInFilteredData = [..walkInPeople];
		CurrentMemberFilteredData = [..memberPeople];
		
		WalkInPersons.Clear();
		MemberPersons.Clear();

		foreach (var walkIn in walkInPeople)
		{
			walkIn.PropertyChanged += OnWalkInPropertyChanged;
			WalkInPersons.Add(walkIn);
		}
		
		foreach (var member in memberPeople)
		{
			member.PropertyChanged += OnMemberPropertyChanged;
			MemberPersons.Add(member);
		}
		
		TotalCount = WalkInPersons.Count;
		TotalCount = MemberPersons.Count;
		*/
	}

	private List<WalkInPerson> GetSampleWalkInPeople()
	{
		var today = DateTime.Today;
		return
		[
			// Today's data
			new WalkInPerson
			{
				ID = 1018, FirstName = "Rome", LastName = "Calubayan", Age = 21, ContactNumber = "09283374574",
				PackageType = "Gym", DateAttendance = today, CheckInTime = today.AddHours(8), CheckOutTime = null
			},
			new WalkInPerson
			{
				ID = 1017, FirstName = "Dave", LastName = "Dapitillo", Age = 22, ContactNumber = "09123456789",
				PackageType = "Boxing", DateAttendance = today, CheckInTime = today.AddHours(9), CheckOutTime = null
			},
			new WalkInPerson
			{
				ID = 1016, FirstName = "Sianrey", LastName = "Flora", Age = 20, ContactNumber = "09123456789",
				PackageType = "Boxing", DateAttendance = today, CheckInTime = today.AddHours(10), CheckOutTime = null 
			},
        
			// Yesterday's data
			new WalkInPerson
			{
				ID = 1015, FirstName = "JC", LastName = "Casidor", Age = 30, ContactNumber = "09123456789",
				PackageType = "Boxing", DateAttendance = today.AddDays(-1), CheckInTime = today.AddHours(8), CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1014, FirstName = "Mark", LastName = "Dela Cruz", Age = 21, ContactNumber = "09123456789",
				PackageType = "Muay Thai", DateAttendance = today.AddDays(-1), CheckInTime = today.AddHours(9),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1013, FirstName = "Mardie", LastName = "Dela Cruz Jr.", Age = 21, ContactNumber = "09123456789",
				PackageType = "CrossFit", DateAttendance = today.AddDays(-1), CheckInTime = today.AddHours(10),
				CheckOutTime = null 
			},
        
			// 2 days ago
			new WalkInPerson
			{
				ID = 1012, FirstName = "Marc", LastName = "Torres", Age = 26, ContactNumber = "09123456789",
				PackageType = "Boxing", DateAttendance = today.AddDays(-2), CheckInTime = today.AddHours(8), CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1011, FirstName = "Maverick", LastName = "Lim", Age = 21, ContactNumber = "09123456789",
				PackageType = "CrossFit", DateAttendance = today.AddDays(-2), CheckInTime = today.AddHours(9),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1010, FirstName = "Adriel", LastName = "Del Rosario", Age = 21, ContactNumber = "09123456789",
				PackageType = "CrossFit", DateAttendance = today.AddDays(-2), CheckInTime = today.AddHours(10),
				CheckOutTime = null 
			},
        
			// 3 days ago
			new WalkInPerson
			{
				ID = 1009, FirstName = "JL", LastName = "Taberdo", Age = 21, ContactNumber = "09123456789",
				PackageType = "CrossFit", DateAttendance = today.AddDays(-3), CheckInTime = today.AddHours(8),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1008, FirstName = "Jav", LastName = "Agustin", Age = 21, ContactNumber = "09123456789",
				PackageType = "Muay Thai", DateAttendance = today.AddDays(-3), CheckInTime = today.AddHours(9),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1007, FirstName = "Daniel", LastName = "Empinado", Age = 20, ContactNumber = "09123456789",
				PackageType = "Muay Thai", DateAttendance = today.AddDays(-3), CheckInTime = today.AddHours(10),
				CheckOutTime = null 
			},
        
			// 4 days ago
			new WalkInPerson
			{
				ID = 1006, FirstName = "Marion", LastName = "Dela Roca", Age = 20, ContactNumber = "09123456789",
				PackageType = "Boxing", DateAttendance = today.AddDays(-4), CheckInTime = today.AddHours(8), CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1005, FirstName = "Sianrey", LastName = "Flora", Age = 20, ContactNumber = "09123456789",
				PackageType = "Boxing", DateAttendance = today.AddDays(-4), CheckInTime = today.AddHours(9), CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1004, FirstName = "JC", LastName = "Casidor", Age = 30, ContactNumber = "09123456789",
				PackageType = "Boxing", DateAttendance = today.AddDays(-4), CheckInTime = today.AddHours(10), CheckOutTime = null 
			},
        
			// 5 days ago
			new WalkInPerson
			{
				ID = 1003, FirstName = "Mark", LastName = "Dela Cruz", Age = 21, ContactNumber = "09123456789",
				PackageType = "Muay Thai", DateAttendance = today.AddDays(-5), CheckInTime = today.AddHours(8),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1002, FirstName = "Mardie", LastName = "Dela Cruz Jr.", Age = 21, ContactNumber = "09123456789",
				PackageType = "CrossFit", DateAttendance = today.AddDays(-5), CheckInTime = today.AddHours(9),
				CheckOutTime = null 
			},
			new WalkInPerson
			{
				ID = 1001, FirstName = "Marc", LastName = "Torres", Age = 26, ContactNumber = "09123456789",
				PackageType = "Boxing", DateAttendance = today.AddDays(-5), CheckInTime = today.AddHours(10), CheckOutTime = null 
			}
		];
	}

	private List<MemberPerson> GetSampleMemberPeople()
	{
		var today = DateTime.Today;
		return
		[
			// Today's data
			new MemberPerson
			{
				ID = 2006, MemberPicture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png",
				FirstName = "Mardie", LastName = "Dela Cruz", ContactNumber = "09123456789",
				MembershipType = "Gym Member", Status = "Active", DateAttendance = today, CheckInTime = today.AddHours(8),
				CheckOutTime = null 
			},
			new MemberPerson
			{
				ID = 2005, MemberPicture = null, FirstName = "Cirilo", LastName = "Pagayunan Jr.",
				ContactNumber = "09123456789", MembershipType = "Free Trial", Status = "Active",
				CheckInTime = today.AddHours(9), DateAttendance = today, CheckOutTime = null 
			},
        
			// Yesterday's data
			new MemberPerson
			{
				ID = 2004, MemberPicture = null, FirstName = "Raymart", LastName = "Soneja",
				ContactNumber = "09123456789", MembershipType = "Gym Member", Status = "Inactive", 
				DateAttendance = today.AddDays(-1), CheckInTime = today.AddHours(8), CheckOutTime = null 
			},
        
			// 2 days ago
			new MemberPerson
			{
				ID = 2003, MemberPicture = null, FirstName = "Xyrus", LastName = "Jawili",
				ContactNumber = "09123456789", MembershipType = "Gym Member", Status = "Inactive",
				DateAttendance = today.AddDays(-2), CheckInTime = today.AddHours(8), CheckOutTime = null 
			},
        
			// 3 days ago
			new MemberPerson
			{
				ID = 2002, MemberPicture = null, FirstName = "Nash", LastName = "Floralde",
				ContactNumber = "09123456789", MembershipType = "Free Trial", Status = "Terminated",
				DateAttendance = today.AddDays(-3), CheckInTime = today.AddHours(8), CheckOutTime = null 
			},
			new MemberPerson
			{
				ID = 2001, MemberPicture = null, FirstName = "Ry", LastName = "Estrada", ContactNumber = "09123456789",
				MembershipType = "Free Trial", Status = "Terminated", DateAttendance = today.AddDays(-3), 
				CheckInTime = today.AddHours(9), CheckOutTime = null 
			}
		];
	}

	[RelayCommand]
	private void AddMemberPerson()
	{
		_logGymMemberDialogCardViewModel.Initialize();
		_dialogManager.CreateDialog(_logGymMemberDialogCardViewModel)
			.WithSuccessCallback(_ =>
			{
				var selectedMember	= _logGymMemberDialogCardViewModel.LastSelectedMember;
				if (selectedMember is not null)
				{
					_toastManager.CreateToast($"Logged {selectedMember.FirstName} {selectedMember.LastName}")
						.WithContent($"Welcome, gym member!")
						.DismissOnClick()
						.ShowSuccess();
				}
				else
				{
					_toastManager.CreateToast("Failed to log a gym member")
						.WithContent($"Did not log a gym member, please try again.")
						.DismissOnClick()
						.ShowError();
				}
			})
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
	private void ShowWalkInItemDeletionDialog(WalkInPerson? walkInPerson)
	{
		if (walkInPerson is null) return;
		
		_dialogManager.CreateDialog("" + "Are you absolutely sure?", 
				$"This action cannot be undone. This will permanently delete {walkInPerson.FirstName} {walkInPerson.LastName} and remove the data from your database.")
			.WithPrimaryButton("Continue", () => OnSubmitDeleteSingleDataItem(walkInPerson), DialogButtonStyle.Destructive)
			.WithCancelButton("Cancel")
			.WithMaxWidth(512)
			.Dismissible()
			.Show();
	}
	
	[RelayCommand]
	private void ShowMemberItemDeletionDialog(MemberPerson? memberPerson)
	{
		if (memberPerson is null) return;
		
		_dialogManager.CreateDialog("" + "Are you absolutely sure?",
				$"This action cannot be undone. This will permanently delete {memberPerson.FirstName} {memberPerson.LastName} and remove the data from your database.")
			.WithPrimaryButton("Continue", () => OnSubmitDeleteSingleDataItem(memberPerson), DialogButtonStyle.Destructive)
			.WithCancelButton("Cancel")
			.WithMaxWidth(512)
			.Dismissible()
			.Show();
	}
	
	private async Task OnSubmitDeleteSingleDataItem(WalkInPerson walkInPerson)
	{
		await DeleteItemFromDatabase(walkInPerson);
		WalkInPersons.Remove(walkInPerson);
		
		_toastManager.CreateToast($"Delete {walkInPerson.FirstName} {walkInPerson.LastName} data")
			.WithContent($"{walkInPerson.FirstName} {walkInPerson.LastName}'s data deleted successfully!")
			.DismissOnClick()
			.WithDelay(6)
			.ShowSuccess();
	}
	
	private async Task OnSubmitDeleteSingleDataItem(MemberPerson memberPerson)
	{
		await DeleteItemFromDatabase(memberPerson);
		MemberPersons.Remove(memberPerson);

		_toastManager.CreateToast($"Delete {memberPerson.FirstName} {memberPerson.LastName} data")
			.WithContent($"{memberPerson.FirstName} {memberPerson.LastName}'s data deleted successfully!")
			.DismissOnClick()
			.WithDelay(6)
			.ShowSuccess();
	}
	
	private async Task DeleteItemFromDatabase(WalkInPerson walkInPerson)
	{
		await Task.Delay(100);
	}
	
	private async Task DeleteItemFromDatabase(MemberPerson memberPerson)
	{
		await Task.Delay(100);
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
	
	private void OnWalkInPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(WalkInPerson.IsSelected))
		{
			UpdateWalkInCounts();
		}
	}
	
	private void OnMemberPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(MemberPerson.IsSelected))
		{
			UpdateMemberCounts();
		}
	}
	
	partial void OnSelectedDateChanged(DateTime value)
	{
		FilterDataByDate(value);
	}
	
	private void FilterDataByDate(DateTime selectedDate)
	{
		var filteredWalkInData = OriginalWalkInData
			.Where(w => w.DateAttendance?.Date == selectedDate.Date)
			.ToList();
    
		CurrentWalkInFilteredData = filteredWalkInData;
    
		WalkInPersons.Clear();
		foreach (var walkIn in filteredWalkInData)
		{
			walkIn.PropertyChanged -= OnWalkInPropertyChanged;
			walkIn.PropertyChanged += OnWalkInPropertyChanged;
			WalkInPersons.Add(walkIn);
		}
    
		var filteredMemberData = OriginalMemberData
			.Where(m => m.DateAttendance?.Date == selectedDate.Date)
			.ToList();
    
		CurrentMemberFilteredData = filteredMemberData;
    
		MemberPersons.Clear();
		foreach (var member in filteredMemberData)
		{
			member.PropertyChanged -= OnMemberPropertyChanged;
			member.PropertyChanged += OnMemberPropertyChanged;
			MemberPersons.Add(member);
		}
    
		UpdateWalkInCounts();
		UpdateMemberCounts();
	}
	
	private void UpdateWalkInCounts()
	{
		SelectedCount = WalkInPersons.Count(x => x.IsSelected);
		TotalCount = WalkInPersons.Count;

		SelectAll = WalkInPersons.Count > 0 && WalkInPersons.All(x => x.IsSelected);
	}
	
	private void UpdateMemberCounts()
	{
		SelectedCount = MemberPersons.Count(x => x.IsSelected);
		TotalCount = MemberPersons.Count;

		SelectAll = MemberPersons.Count > 0 && MemberPersons.All(x => x.IsSelected);
	}
}

public partial class WalkInPerson : ObservableObject
{
	[ObservableProperty]
	private bool _isSelected;
	
	public int ID { get; set; }
	public string FirstName { get; set; } = string.Empty;
	public string LastName { get; set; } = string.Empty;
	public int Age { get; set; }
	public string ContactNumber { get; set; } = string.Empty;
	public string PackageType { get; set; } = string.Empty;
	
	public DateTime? DateAttendance { get; set; }
	public DateTime? CheckInTime { get; set; }
	private DateTime? _checkOutTime;

	public DateTime? CheckOutTime
	{
		get =>  _checkOutTime;
		set => SetProperty(ref _checkOutTime, value);
	}
	
	public string DateFormatted => DateAttendance?.ToString("MMMM dd, yyyy") ?? string.Empty;
}

public partial class MemberPerson : ViewModelBase
{
	[ObservableProperty]
	private bool _isSelected;
	
	public int ID { get; set; }
	public string? MemberPicture { get; set; } = string.Empty;
	public string FirstName { get; set; } = string.Empty;
	public string LastName { get; set; } = string.Empty;
	public string ContactNumber { get; set; } = string.Empty;
	public string MembershipType { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	
	public DateTime? DateAttendance { get; set; }
	public DateTime? CheckInTime { get; set; }
	private DateTime? _checkOutTime;

	public DateTime? CheckOutTime
	{
		get => _checkOutTime;
		set => SetProperty(ref _checkOutTime, value);
	}
	
	public string DateFormatted => DateAttendance?.ToString("MMMM dd, yyyy") ?? string.Empty;
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
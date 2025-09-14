using AHON_TRACK.ViewModels;
using HotAvalonia;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public sealed partial class LogGymMemberDialogCardViewModel : ViewModelBase
{
	[ObservableProperty]
	private ObservableCollection<MemberPerson> _allMembers = [];

	[ObservableProperty]
	private ObservableCollection<MemberPerson> _filteredMembers = [];

	[ObservableProperty]
	private ObservableCollection<string> _memberSuggestions = [];

	[ObservableProperty]
	private string _searchText = string.Empty;

	[ObservableProperty]
	private MemberPerson? _selectedMember;

	[ObservableProperty]
	private bool _isSearching;

	private readonly DialogManager _dialogManager;

	public LogGymMemberDialogCardViewModel(DialogManager dialogManager, List<MemberPerson>? members = null)
	{
		_dialogManager = dialogManager;
		if (members != null)
		{
			LoadMembers(members);
		}
	}

	public LogGymMemberDialogCardViewModel(DialogManager dialogManager)
	{
		_dialogManager = dialogManager;
		LoadSampleMembers();
		UpdateSuggestions();
	}

	public LogGymMemberDialogCardViewModel()
	{
		_dialogManager = new DialogManager();
		LoadSampleMembers();
		UpdateSuggestions();
	}

	[AvaloniaHotReload]
	public void Initialize()
	{
		ClearAllErrors();

		// Load sample data if no data provided
		if (AllMembers.Count == 0)
		{
			LoadSampleMembers();
		}

		UpdateSuggestions();
	}

	private void LoadMembers(List<MemberPerson> members)
	{
		AllMembers.Clear();
		FilteredMembers.Clear();

		foreach (var member in members)
		{
			AllMembers.Add(member);
			FilteredMembers.Add(member);
		}

		UpdateSuggestions();
	}

	private void LoadSampleMembers()
	{
		var sampleMembers = new List<MemberPerson>
		{
			new() { ID = 2006, MemberPicture = "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png", FirstName = "Mardie", LastName = "Dela Cruz", ContactNumber = "09123456789", MembershipType = "Gym Member", Status = "Active", CheckInTime = DateTime.Now.AddHours(-1), CheckOutTime = DateTime.Now.AddHours(-1) },
			new() { ID = 2005, MemberPicture = null, FirstName = "Cirilo", LastName = "Pagayunan Jr.", ContactNumber = "09123456789", MembershipType = "Free Trial", Status = "Active", CheckInTime = DateTime.Now.AddHours(-1), CheckOutTime = DateTime.Now.AddHours(-1) },
			new() { ID = 2004, MemberPicture = null, FirstName = "Raymart", LastName = "Soneja", ContactNumber = "09123456789", MembershipType = "Gym Member", Status = "Inactive", CheckInTime = DateTime.Now.AddDays(-2), CheckOutTime = DateTime.Now.AddDays(-2) },
			new() { ID = 2003, MemberPicture = null, FirstName = "Xyrus", LastName = "Jawili", ContactNumber = "09123456789", MembershipType = "Gym Member", Status = "Inactive", CheckInTime = DateTime.Now.AddDays(-2), CheckOutTime = DateTime.Now.AddDays(-2) },
			new() { ID = 2002, MemberPicture = null, FirstName = "Nash", LastName = "Floralde", ContactNumber = "09123456789", MembershipType = "Free Trial", Status = "Terminated", CheckInTime = DateTime.Now.AddDays(-3), CheckOutTime = DateTime.Now.AddDays(-3) },
			new() { ID = 2001, MemberPicture = null, FirstName = "Ry", LastName = "Estrada", ContactNumber = "09123456789", MembershipType = "Free Trial", Status = "Terminated", CheckInTime = DateTime.Now.AddDays(-3), CheckOutTime = DateTime.Now.AddDays(-3) },
			new() { ID = 2007, MemberPicture = null, FirstName = "John", LastName = "Doe", ContactNumber = "09123456789", MembershipType = "Gym Member", Status = "Active", CheckInTime = DateTime.Now, CheckOutTime = null },
			new() { ID = 2008, MemberPicture = null, FirstName = "Jane", LastName = "Smith", ContactNumber = "09123456789", MembershipType = "Premium", Status = "Active", CheckInTime = DateTime.Now, CheckOutTime = null },
			new() { ID = 2009, MemberPicture = null, FirstName = "Mike", LastName = "Johnson", ContactNumber = "09123456789", MembershipType = "Gym Member", Status = "Active", CheckInTime = DateTime.Now, CheckOutTime = null },
			new() { ID = 2010, MemberPicture = null, FirstName = "Sarah", LastName = "Williams", ContactNumber = "09123456789", MembershipType = "Free Trial", Status = "Active", CheckInTime = DateTime.Now, CheckOutTime = null }
		};

		LoadMembers(sampleMembers);
	}

	private void UpdateSuggestions()
	{
		var suggestions = AllMembers
			.Select(m => $"{m.FirstName} {m.LastName}")
			.Distinct()
			.OrderBy(s => s)
			.ToList();

		MemberSuggestions.Clear();
		foreach (var suggestion in suggestions)
		{
			MemberSuggestions.Add(suggestion);
		}
	}

	[RelayCommand]
	private async Task SearchMembers()
	{
		if (string.IsNullOrWhiteSpace(SearchText))
		{
			// Reset to show all members
			FilteredMembers.Clear();
			foreach (var member in AllMembers)
			{
				FilteredMembers.Add(member);
			}
			SelectedMember = null;
			return;
		}

		IsSearching = true;

		try
		{
			await Task.Delay(200); // Small delay to simulate search

			var searchTerm = SearchText.ToLowerInvariant();
			var filteredResults = AllMembers.Where(member =>
				member.FirstName.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
				member.LastName.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                $"{member.FirstName} {member.LastName}".Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
				member.ID.ToString().Contains(searchTerm) ||
				member.MembershipType.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
				member.Status.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase)
            ).ToList();

			FilteredMembers.Clear();
			foreach (var member in filteredResults)
			{
				FilteredMembers.Add(member);
			}

			// Auto-select if exact match found
			var exactMatch = filteredResults.FirstOrDefault(m =>
				$"{m.FirstName} {m.LastName}".Equals(SearchText, StringComparison.OrdinalIgnoreCase));

			if (exactMatch != null)
			{
				SelectedMember = exactMatch;
			}
		}
		finally
		{
			IsSearching = false;
		}
	}

	// This method is called when SearchText changes
	partial void OnSearchTextChanged(string value)
	{
		SearchMembersCommand.Execute(null);
	}

	// This method is called when a member is selected from the suggestion
	partial void OnSelectedMemberChanged(MemberPerson? value)
	{
		if (value == null) return;
		// Update the search text to match the selected member
		SearchText = $"{value.FirstName} {value.LastName}";

		// Show only the selected member in the grid
		FilteredMembers.Clear();
		FilteredMembers.Add(value);
	}

	[RelayCommand]
	private void SelectMember(MemberPerson member)
	{
		SelectedMember = member;
	}

	[RelayCommand]
	private void ClearSearch()
	{
		SearchText = string.Empty;
		SelectedMember = null;
		FilteredMembers.Clear();
		foreach (var member in AllMembers)
		{
			FilteredMembers.Add(member);
		}
	}

	[RelayCommand]
	private void Cancel()
	{
		ClearSearch();
		_dialogManager.Close(this);
	}
	
	public MemberPerson? LastSelectedMember { get; private set; } // I think this line makes it so that it remembers what you selected (and I know it's stupid but hey it works)

	[RelayCommand]
	private void Submit()
	{
		if (SelectedMember == null) return;
		// Process the selected member for check-in/out
		LastSelectedMember = SelectedMember;
		
		ClearSearch();
		_dialogManager.Close(this, new CloseDialogOptions { Success = true });
	}
}
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using HotAvalonia;
using ShadUI;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using System.Collections.ObjectModel;

namespace AHON_TRACK.ViewModels;

[Page("manage-membership")]
public sealed partial class ManageMembershipViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IMemberService _memberService;

    public ObservableCollection<MemberModel> Members { get; private set; } = new ObservableCollection<MemberModel>();

    public ManageMembershipViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, IMemberService memberService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _memberService = memberService;
    }

    public ManageMembershipViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public async void Initialize()
    {
        try
        {
            var memberList = await _memberService.GetMemberAsync();

            Members.Clear();
            if (memberList.Count == 0)
            {
                LoadSampleMembers();
                _toastManager.CreateToast("No members found in DB. Loaded sample data instead.");
            }
            else
            {
                foreach (var member in memberList)
                    Members.Add(member);

                _toastManager.CreateToast($"Loaded {Members.Count} members from DB.");
            }
        }
        catch
        {
            LoadSampleMembers();
            _toastManager.CreateToast("Error loading DB members. Showing sample data.");
        }
    }

    public void LoadSampleMembers()
    {
        Members.Clear();

        Members.Add(new MemberModel
        {
            ID = 1,
            Name = "John Doe",
            ContactNumber = "09171234567",
            MembershipType = "Premium",
            Status = "Active",
            Validity = "2025-12-31"
        });
        Members.Add(new MemberModel
        {
            ID = 2,
            Name = "Jane Smith",
            ContactNumber = "09281234567",
            MembershipType = "Basic",
            Status = "Expired",
            Validity = "2024-12-31"
        });
        Members.Add(new MemberModel
        {
            ID = 3,
            Name = "Mark Johnson",
            ContactNumber = "09391234567",
            MembershipType = "VIP",
            Status = "Active",
            Validity = "2026-01-15"
        });

        _toastManager.CreateToast($"Loaded {Members.Count} test members.");
    }
}
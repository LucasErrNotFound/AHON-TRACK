using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.ViewModels;

[Page("viewMemberProfile")]
public sealed partial class MemberProfileInformationViewModel : ViewModelBase, INavigable, INavigableWithParameters
{
    private readonly PageManager _pageManager;
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly IMemberService _memberService;

    [ObservableProperty]
    private bool _isFromCurrentUser = false;

    [ObservableProperty]
    private int _currentMemberId = 0;

    [ObservableProperty]
    private string _memberFullNameHeader = string.Empty;

    [ObservableProperty]
    private string _memberID = string.Empty;

    [ObservableProperty]
    private string _memberPosition = string.Empty;

    [ObservableProperty]
    private string _memberStatus = string.Empty;

    [ObservableProperty]
    private DateTime? _memberDateJoined;

    [ObservableProperty]
    private string _memberValidUntil = string.Empty;

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
    private string _memberPackage = string.Empty;

    [ObservableProperty]
    private string _memberPaymentMethod = string.Empty;

    [ObservableProperty]
    private Bitmap? _memberProfileImage;

    [ObservableProperty]
    private bool _isLoading = false;

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

    public MemberProfileInformationViewModel(
        IMemberService memberService,
        PageManager pageManager,
        DialogManager dialogManager,
        ToastManager toastManager)
    {
        _memberService = memberService;
        _pageManager = pageManager;
        _dialogManager = dialogManager;
        _toastManager = toastManager;
    }

    public MemberProfileInformationViewModel()
    {
        _pageManager = new PageManager(new ServiceProvider());
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _memberService = null!;
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        SetDefaultValues();
    }

    public void SetNavigationParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("MemberId", out var memberIdObj))
        {
            if (memberIdObj is int memberId)
            {
                CurrentMemberId = memberId;
                _ = LoadMemberDataAsync(memberId);
            }
            else if (memberIdObj is string memberIdStr && int.TryParse(memberIdStr, out var parsedId))
            {
                CurrentMemberId = parsedId;
                _ = LoadMemberDataAsync(parsedId);
            }
        }
        else if (parameters.TryGetValue("Member", out var memberObj) && memberObj is ManageMembersItem member)
        {
            if (int.TryParse(member.ID, out var parsedId))
            {
                CurrentMemberId = parsedId;
                _ = LoadMemberDataAsync(parsedId);
            }
        }

        if (parameters.TryGetValue("IsCurrentUser", out var isCurrentUserObj) && isCurrentUserObj is bool isCurrentUser)
        {
            IsFromCurrentUser = isCurrentUser;
        }
    }

    private async Task LoadMemberDataAsync(int memberId)
    {
        if (_memberService == null)
        {
            Debug.WriteLine("[MemberProfile] MemberService is null");
            SetDefaultValues();
            return;
        }

        IsLoading = true;

        try
        {
            var result = await _memberService.GetMemberByIdAsync(memberId);

            if (!result.Success || result.Member == null)
            {
                Debug.WriteLine($"[MemberProfile] Failed to load member: {result.Message}");
                _toastManager?.CreateToast("Load Error")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowError();

                SetDefaultValues();
                return;
            }

            var member = result.Member;

            // Populate member data
            MemberID = member.MemberID.ToString();
            MemberPosition = member.MembershipType ?? "Gym Member";
            MemberStatus = member.Status ?? "Active";
            MemberValidUntil = member.ValidUntil ?? "N/A";

            // Note: If you need DateJoined, you'll need to add it to your Members table
            // For now, using a placeholder or leaving empty
            MemberDateJoined = member.DateJoined; // Or fetch from database if available

            MemberFullName = member.Name ?? $"{member.FirstName} {member.LastName}";
            MemberFullNameHeader = IsFromCurrentUser ? "My Profile" : $"{MemberFullName}'s Profile";

            MemberAge = member.Age?.ToString() ?? "N/A";
            MemberBirthDate = member.BirthYear?.ToString("yyyy") ?? "N/A";
            MemberGender = member.Gender ?? "N/A";
            MemberPhoneNumber = member.ContactNumber ?? "N/A";
            MemberPackage = member.MembershipType ?? "None";
            MemberPaymentMethod = member.PaymentMethod ?? "N/A";

            // Load profile image
            if (member.ProfileImageSource != null)
            {
                MemberProfileImage = member.ProfileImageSource;
            }

            // Address fields - these need to be added to your database schema if needed
            MemberHouseAddress = "N/A";
            MemberHouseNumber = "N/A";
            MemberStreet = "N/A";
            MemberBarangay = "N/A";
            MemberCityProvince = "N/A";
            MemberLastLogin = "N/A";

            Debug.WriteLine($"[MemberProfile] Successfully loaded member: {MemberFullName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MemberProfile] Error loading member data: {ex.Message}");
            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to load member profile: {ex.Message}")
                .DismissOnClick()
                .ShowError();

            SetDefaultValues();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SetDefaultValues()
    {
        MemberID = "Unknown";
        MemberPosition = "Gym Member";
        MemberStatus = "Active";
        MemberDateJoined = null;
        MemberValidUntil = "N/A";
        MemberFullName = "Unknown Member";
        MemberFullNameHeader = IsFromCurrentUser ? "My Profile" : "Member Profile";
        MemberAge = "N/A";
        MemberBirthDate = "N/A";
        MemberGender = "N/A";
        MemberPhoneNumber = "N/A";
        MemberPackage = "None";
        MemberPaymentMethod = "N/A";
        MemberProfileImage = null;
        MemberLastLogin = "N/A";
        MemberHouseAddress = "N/A";
        MemberHouseNumber = "N/A";
        MemberStreet = "N/A";
        MemberBarangay = "N/A";
        MemberCityProvince = "N/A";
    }

    [RelayCommand]
    private void GoBack()
    {
        _pageManager.Navigate<ManageMembershipViewModel>();
    }

    [RelayCommand]
    private async Task EditProfile()
    {
        if (CurrentMemberId == 0)
        {
            _toastManager?.CreateToast("Error")
                .WithContent("Cannot edit profile - invalid member ID")
                .DismissOnClick()
                .ShowError();
            return;
        }

        // Navigate to edit view or show edit dialog
        _toastManager?.CreateToast("Edit Profile")
            .WithContent("Edit functionality coming soon")
            .DismissOnClick()
            .ShowInfo();
    }

    [RelayCommand]
    private async Task RefreshProfile()
    {
        if (CurrentMemberId > 0)
        {
            await LoadMemberDataAsync(CurrentMemberId);
            _toastManager?.CreateToast("Profile Refreshed")
                .WithContent("Member profile has been refreshed")
                .DismissOnClick()
                .ShowSuccess();
        }
    }
    
    protected override void DisposeManagedResources()
    {
        // Dispose image if necessary and drop reference
        if (MemberProfileImage is IDisposable imgDisposable) imgDisposable.Dispose();
        MemberProfileImage = null;

        // Reset identity / flags
        IsFromCurrentUser = false;
        CurrentMemberId = 0;

        // Clear all displayed strings
        MemberFullNameHeader = string.Empty;
        MemberID = string.Empty;
        MemberPosition = string.Empty;
        MemberStatus = string.Empty;
        MemberValidUntil = string.Empty;
        MemberFullName = string.Empty;
        MemberAge = string.Empty;
        MemberBirthDate = string.Empty;
        MemberGender = string.Empty;
        MemberPhoneNumber = string.Empty;
        MemberPackage = string.Empty;
        MemberPaymentMethod = string.Empty;
        MemberLastLogin = string.Empty;
        MemberHouseAddress = string.Empty;
        MemberHouseNumber = string.Empty;
        MemberStreet = string.Empty;
        MemberBarangay = string.Empty;
        MemberCityProvince = string.Empty;

        // Reset nullable/date fields and loading state
        MemberDateJoined = null;
        IsLoading = false;

        // Do not reassign injected readonly services — we only clear what we own.

        base.DisposeManagedResources();
    }
}
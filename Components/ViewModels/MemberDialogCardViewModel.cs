using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AHON_TRACK.Converters;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using AHON_TRACK.Services.Events;

namespace AHON_TRACK.Components.ViewModels;

public partial class MemberDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _middleInitialItems = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"];

    [ObservableProperty]
    private string _dialogTitle = "Edit Gym Member Details";

    [ObservableProperty]
    private string _dialogDescription = "Please fill out the form to edit this gym member's information";

    [ObservableProperty]
    private bool _isEditMode = false;

    [ObservableProperty]
    private int _currentMemberId = 0;

    [ObservableProperty]
    private Image? _memberProfileImageControl;

    [ObservableProperty]
    private Bitmap? _profileImageSource;

    // Personal Details Section
    private string _memberFirstName = string.Empty;
    private string _selectedMiddleInitialItem;
    private string _memberLastName = string.Empty;
    private string _memberGender = string.Empty;
    private string _memberContactNumber = string.Empty;
    private string _memberPackages = string.Empty;
    private int? _memberAge;
    private DateTime? _memberBirthDate;
    private DateTime? _memberDateJoined;

    // Account Section
    private DateTime? _memberValidUntil;
    private string _memberStatus = string.Empty;

    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly IMemberService _memberService;

    private int? _memberPackageId;

    private List<PackageModel> _memberPackageModels = new();

    public DateTime? MemberDateJoined
    {
        get => _memberDateJoined;
        set => SetProperty(ref _memberDateJoined, value, true);
    }

    [Required(ErrorMessage = "First name is required")]
    [RegularExpression(@"^[a-zA-Z ]*$", ErrorMessage = "Alphabets only.")]
    [MinLength(2, ErrorMessage = "Must be at least 2 characters long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string MemberFirstName
    {
        get => _memberFirstName;
        set => SetProperty(ref _memberFirstName, value, true);
    }

    // [Required(ErrorMessage = "Select your MI")] -> I disabled this because apparently there are people who do not have middle name/initial
    public string SelectedMiddleInitialItem
    {
        get => _selectedMiddleInitialItem;
        set => SetProperty(ref _selectedMiddleInitialItem, value, true);
    }

    [Required(ErrorMessage = "Last name is required")]
    [RegularExpression(@"^[a-zA-Z ]*$", ErrorMessage = "Alphabets only.")]
    [MinLength(2, ErrorMessage = "Must be at least 2 characters long")]
    [MaxLength(15, ErrorMessage = "Must not exceed 15 characters")]
    public string MemberLastName
    {
        get => _memberLastName;
        set => SetProperty(ref _memberLastName, value, true);
    }

    [Required(ErrorMessage = "Select your gender")]
    public string? MemberGender
    {
        get => _memberGender;
        set
        {
            if (_memberGender == value) return;
            _memberGender = value ?? string.Empty;
            OnPropertyChanged(nameof(MemberGender));
            OnPropertyChanged(nameof(IsMale));
            OnPropertyChanged(nameof(IsFemale));
        }
    }

    public bool IsMale
    {
        get => MemberGender == "Male";
        set { if (value) MemberGender = "Male"; }
    }

    public bool IsFemale
    {
        get => MemberGender == "Female";
        set { if (value) MemberGender = "Female"; }
    }

    [Required(ErrorMessage = "Contact number is required")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Contact number must start with 09 and be 11 digits long")]
    public string MemberContactNumber
    {
        get => _memberContactNumber;
        set => SetProperty(ref _memberContactNumber, value, true);
    }

    [Required(ErrorMessage = "Package is required")]
    public string MemberPackages
    {
        get => _memberPackages;
        set => SetProperty(ref _memberPackages, value, true);
    }

    [Required(ErrorMessage = "Age is required")]
    [Range(3, 100, ErrorMessage = "Age must be between 3 and 100")]
    public int? MemberAge
    {
        get => _memberAge;
        set => SetProperty(ref _memberAge, value, true);
    }

    [Required(ErrorMessage = "Birth date is required")]
    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? MemberBirthDate
    {
        get => _memberBirthDate;
        set
        {
            SetProperty(ref _memberBirthDate, value, true);
            if (value.HasValue)
            {
                MemberAge = CalculateAge(value.Value);
            }
            else
            {
                _memberAge = null;
                OnPropertyChanged(nameof(MemberAge));
            }
        }
    }

    [Required(ErrorMessage = "Valid until date is required")]
    [DataType(DataType.Date, ErrorMessage = "Invalid date format")]
    public DateTime? MemberValidUntil
    {
        get => _memberValidUntil;
        set => SetProperty(ref _memberValidUntil, value, true);
    }

    [Required(ErrorMessage = "Status is required")]
    public string MemberStatus
    {
        get => _memberStatus;
        set => SetProperty(ref _memberStatus, value, true);
    }

    private byte[]? _profileImage;
    public byte[]? ProfileImage
    {
        get => _profileImage;
        set
        {
            SetProperty(ref _profileImage, value);
            Debug.WriteLine($"ProfileImage updated: {(value != null ? $"{value.Length} bytes" : "null")}");
        }
    }

    public MemberDialogCardViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager, IMemberService memberService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _memberService = memberService;
    }

    public MemberDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
        _memberService = null!;
    }

    [AvaloniaHotReload]
    public async Task Initialize()
    {
        IsEditMode = false;
        DialogTitle = "Edit Gym Member Details";
    }

    // Overload to accept string (for backward compatibility)
    public async Task PopulateWithMemberDataAsync(string memberId)
    {
        if (int.TryParse(memberId, out int id))
        {
            await PopulateWithMemberDataAsync(id);
        }
        else
        {
            Debug.WriteLine($"[PopulateWithMemberData] Invalid member ID: {memberId}");
            _toastManager.CreateToast("Invalid ID")
                .WithContent("The member ID is not valid.")
                .DismissOnClick()
                .ShowError();
        }
    }

    // Primary method accepting int
    public async Task PopulateWithMemberDataAsync(int memberId)
    {
        CurrentMemberId = memberId;

        if (_memberService == null)
        {
            Debug.WriteLine("[PopulateWithMemberData] MemberService is null");
            return;
        }

        try
        {
            var result = await _memberService.GetMemberByIdAsync(memberId);

            if (!result.Success || result.Member == null)
            {
                Debug.WriteLine($"[PopulateWithMemberData] Failed to load member: {result.Message}");
                _toastManager?.CreateToast("Load Error")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            var memberData = result.Member;

            MemberFirstName = memberData.FirstName ?? string.Empty;
            SelectedMiddleInitialItem = memberData.MiddleInitial ?? string.Empty;
            MemberLastName = memberData.LastName ?? string.Empty;
            MemberGender = memberData.Gender ?? string.Empty;
            MemberContactNumber = memberData.ContactNumber?.Replace(" ", "") ?? string.Empty;

            // Set the package name for display
            MemberPackages = memberData.MembershipType ?? string.Empty;
            _memberPackageId = memberData.PackageID;

            MemberAge = memberData.Age;
            MemberBirthDate = memberData.DateOfBirth;
            MemberStatus = memberData.Status ?? "Active";

            // Parse ValidUntil date
            if (!string.IsNullOrEmpty(memberData.ValidUntil))
            {
                if (DateTime.TryParse(memberData.ValidUntil, out var validUntilDate))
                {
                    MemberValidUntil = validUntilDate;
                }
            }
            if (memberData.AvatarSource != null)
            {
                ProfileImageSource = memberData.AvatarSource;

                if (memberData.AvatarSource != ManageMemberModel.DefaultAvatarSource)
                {
                    ProfileImage = ImageHelper.BitmapToBytes(memberData.AvatarSource);
                }
            }

            IsEditMode = true;

            Debug.WriteLine($"[PopulateWithMemberData] Loaded member: {memberData.MembershipType}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PopulateWithMemberData] Error: {ex.Message}");
            _toastManager.CreateToast("Load Error")
                .WithContent($"Failed to load member data: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    private int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;

        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }

    [RelayCommand]
    private async Task ChooseFile()
    {
        try
        {
            var toplevel = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (toplevel == null) return;

            var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Image File",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = ["*.png", "*.jpg"]
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = ["*.*"]
                    }
                ]
            });

            if (files.Count > 0)
            {
                var selectedFile = files[0];
                _toastManager.CreateToast("Image file selected")
                    .WithContent($"{selectedFile.Name}")
                    .DismissOnClick()
                    .ShowInfo();

                var file = files[0];
                await using var stream = await file.OpenReadAsync();

                var bitmap = new Bitmap(stream);

                // Update UI control if present
                if (MemberProfileImageControl != null)
                {
                    MemberProfileImageControl.Source = bitmap;
                    MemberProfileImageControl.IsVisible = true;
                }

                // Convert to bytes for database storage
                stream.Position = 0;
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                ProfileImage = memoryStream.ToArray();
                ProfileImageSource = bitmap;

                Debug.WriteLine($"✅ Profile image loaded: {ProfileImage.Length} bytes");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error from uploading Picture: {ex.Message}");
            _toastManager.CreateToast("Image Error")
                .WithContent($"Failed to load image: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task SaveDetails()
    {
        ClearAllErrors();
        ValidateAllProperties();

        if (HasErrors)
        {
            _toastManager?.CreateToast("Validation Error")
                .WithContent("Please fix all validation errors before saving.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        try
        {
            // Get PackageID from the selected package name
            int? packageId = null;
            if (!string.IsNullOrEmpty(MemberPackages))
            {
                var package = _memberPackageModels.FirstOrDefault(p =>
                    p.packageName.Equals(MemberPackages, StringComparison.OrdinalIgnoreCase));
                packageId = package?.packageID;
            }

            var memberModel = new ManageMemberModel
            {
                MemberID = CurrentMemberId,
                FirstName = MemberFirstName,
                MiddleInitial = string.IsNullOrWhiteSpace(SelectedMiddleInitialItem) ? null : SelectedMiddleInitialItem,
                LastName = MemberLastName,
                Gender = MemberGender,
                ContactNumber = MemberContactNumber,
                Age = MemberAge,
                DateOfBirth = MemberBirthDate,
                ValidUntil = MemberValidUntil?.ToString("MMM dd, yyyy"),
                MembershipType = MemberPackages,
                PackageID = _memberPackageId,
                Status = MemberStatus
            };

            if (_memberService != null)
            {
                var result = await _memberService.UpdateMemberAsync(memberModel);

                if (!result.Success)
                {
                    _toastManager?.CreateToast("Update Failed")
                        .WithContent(result.Message)
                        .DismissOnClick()
                        .ShowError();
                    return;
                }
            }

            DashboardEventService.Instance.NotifyMemberUpdated();

            Debug.WriteLine($"[SaveDetails] Updated member with Package ID: {packageId}");

            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveDetails] Error: {ex.Message}");
            _toastManager?.CreateToast("Update Failed")
                .WithContent($"Failed to update member: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }

    private void ClearAllFields()
    {
        MemberFirstName = string.Empty;
        SelectedMiddleInitialItem = string.Empty;
        MemberLastName = string.Empty;
        MemberGender = null;
        MemberContactNumber = string.Empty;
        MemberPackages = string.Empty;
        MemberAge = null;
        MemberBirthDate = null;
        MemberValidUntil = null;
        MemberStatus = string.Empty;
        ProfileImage = null;
        _memberPackageId = null;
        ProfileImageSource = ImageHelper.GetDefaultAvatar();
        ClearAllErrors();
    }

    protected override void DisposeManagedResources()
    {
        // Dispose and drop image resources
        if (ProfileImageSource is IDisposable bmp) bmp.Dispose();
        ProfileImageSource = null;
        if (MemberProfileImageControl != null)
        {
            MemberProfileImageControl.Source = null;
            MemberProfileImageControl.IsVisible = false;
        }
        MemberProfileImageControl = null;

        // Clear binary blob
        ProfileImage = null;

        // Reset identity and flags
        IsEditMode = false;
        CurrentMemberId = 0;

        // Clear text / date fields
        MemberFirstName = string.Empty;
        SelectedMiddleInitialItem = string.Empty;
        MemberLastName = string.Empty;
        MemberGender = string.Empty;
        MemberContactNumber = string.Empty;
        MemberPackages = string.Empty;
        MemberAge = null;
        MemberBirthDate = null;
        MemberDateJoined = null;
        MemberValidUntil = null;
        MemberStatus = string.Empty;

        // Aggressively clear package models
        _memberPackageModels?.Clear();
        _memberPackageModels = new List<PackageModel>();
        _memberPackageId = null;

        // Clear dialog metadata
        DialogTitle = string.Empty;
        DialogDescription = string.Empty;

        // Let base handle any remaining managed cleanup
        base.DisposeManagedResources();
    }
}
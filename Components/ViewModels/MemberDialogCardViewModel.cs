using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.Converters;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using AHON_TRACK.Services.Events;
using HotAvalonia;
using Microsoft.Extensions.Logging;

namespace AHON_TRACK.Components.ViewModels;

public partial class MemberDialogCardViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    [ObservableProperty]
    private string[] _middleInitialItems = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"];

    [ObservableProperty]
    private string _dialogTitle = "Edit Gym Member Details";

    [ObservableProperty]
    private string _dialogDescription = "Please fill out the form to edit this gym member's information";
    
    [ObservableProperty] private bool _isEditMode = false;
    [ObservableProperty] private int _currentMemberId = 0;
    [ObservableProperty] private Image? _memberProfileImageControl;
    [ObservableProperty] private Bitmap? _profileImageSource;
    
    [ObservableProperty] private bool _isInitialized;
    [ObservableProperty] private bool _isLoading;

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
    private readonly IMemberService _memberService;
    private readonly ILogger _logger;

    private List<PackageModel> _memberPackageModels = [];

    public DateTime? MemberDateJoined
    {
        get => _memberDateJoined;
        set => SetProperty(ref _memberDateJoined, value, true);
    }

    [Required(ErrorMessage = "First name is required")]
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

    public MemberDialogCardViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager, 
        IMemberService memberService,
        ILogger logger)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _memberService = memberService;
        _logger = logger;
    }

    public MemberDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _memberService = null!;
        _logger = null!;
    }

    /*
    [AvaloniaHotReload]
    public async Task Initialize()
    {
        IsEditMode = false;
        DialogTitle = "Edit Gym Member Details";
        DialogDescription = "Please fill out the form to edit this gym member's information";
    }
    */
    
    [AvaloniaHotReload]
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
    
        if (IsInitialized)
        {
            _logger?.LogDebug("MemberDialogCardViewModel already initialized");
            return;
        }

        _logger?.LogInformation("Initializing MemberDialogCardViewModel");

        try
        {
            await Task.Yield(); // Ensure async context
        
            IsEditMode = false;
            DialogTitle = "Edit Gym Member Details";
            DialogDescription = "Please fill out the form to edit this gym member's information";
        
            IsInitialized = true;
            _logger?.LogInformation("MemberDialogCardViewModel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing MemberDialogCardViewModel");
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Navigating away from MemberDialog");
        return ValueTask.CompletedTask;
    }

    // Overload to accept string (for backward compatibility)
    public async Task PopulateWithMemberDataAsync(string memberId, CancellationToken cancellationToken = default)
    {
        if (int.TryParse(memberId, out int id))
        {
            await PopulateWithMemberDataAsync(id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger?.LogWarning("Invalid member ID: {MemberId}", memberId);
            _toastManager?.CreateToast("Invalid ID")
                .WithContent("The member ID is not valid.")
                .DismissOnClick()
                .ShowError();
        }
    }

    // Primary method accepting int
    public async Task PopulateWithMemberDataAsync(int memberId, CancellationToken cancellationToken = default)
    {
        CurrentMemberId = memberId;

        if (_memberService == null)
        {
            _logger?.LogWarning("MemberService is null");
            return;
        }

        IsLoading = true;

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            var result = await _memberService.GetMemberByIdAsync(memberId)
                .ConfigureAwait(false);

            linkedCts.Token.ThrowIfCancellationRequested();

            if (!result.Success || result.Member == null)
            {
                _logger?.LogWarning("Failed to load member {MemberId}: {Message}", 
                    memberId, result.Message);
            
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
            MemberPackages = memberData.MembershipType ?? string.Empty;
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

            _logger?.LogInformation("Loaded member {MemberId}: {Name}, Package: {Package}", 
                memberId, $"{MemberFirstName} {MemberLastName}", MemberPackages);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("PopulateWithMemberDataAsync cancelled for member {MemberId}", memberId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading member {MemberId}", memberId);
            _toastManager?.CreateToast("Load Error")
                .WithContent($"Failed to load member data: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
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
            LifecycleToken.ThrowIfCancellationRequested();
        
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
                        Patterns = ["*.png", "*.jpg", "*.jpeg"]
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
                _toastManager?.CreateToast("Image file selected")
                    .WithContent($"{selectedFile.Name}")
                    .DismissOnClick()
                    .ShowInfo();

                await using var stream = await selectedFile.OpenReadAsync();
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

                _logger?.LogDebug("Profile image loaded: {Size} bytes", ProfileImage.Length);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("File selection cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error uploading picture");
            _toastManager?.CreateToast("Image Error")
                .WithContent($"Failed to load image: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task SaveDetails()
    {
        try
        {
            LifecycleToken.ThrowIfCancellationRequested();
        
            ClearAllErrors();
            ValidateAllProperties();

            if (HasErrors)
            {
                _logger?.LogWarning("Member dialog validation failed");
                _toastManager?.CreateToast("Validation Error")
                    .WithContent("Please fix all validation errors before saving.")
                    .DismissOnClick()
                    .ShowWarning();
                return;
            }

            if (_memberService == null)
            {
                _logger?.LogWarning("Member service is not available");
                _toastManager?.CreateToast("Service Error")
                    .WithContent("Member service is not available")
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            IsLoading = true;

            // Get PackageID from the selected package name
            int? packageId = null;
            if (!string.IsNullOrEmpty(MemberPackages))
            {
                var package = _memberPackageModels.FirstOrDefault(p =>
                    p.packageName.Equals(MemberPackages, StringComparison.OrdinalIgnoreCase));
                packageId = package?.packageID;
            
                if (packageId == null)
                {
                    _logger?.LogWarning("Package '{Package}' not found in loaded packages", MemberPackages);
                }
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
                Status = MemberStatus,
                ProfilePicture = ProfileImage
            };

            var result = await _memberService.UpdateMemberAsync(memberModel)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                _logger?.LogWarning("Failed to update member {MemberId}: {Message}", 
                    CurrentMemberId, result.Message);
            
                _toastManager?.CreateToast("Update Failed")
                    .WithContent(result.Message)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            DashboardEventService.Instance.NotifyMemberUpdated();

            _logger?.LogInformation("Updated member {MemberId}: {Name}, Package: {Package} (ID: {PackageId})", 
                CurrentMemberId, $"{MemberFirstName} {MemberLastName}", MemberPackages, packageId);

            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Member save cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating member {MemberId}", CurrentMemberId);
            _toastManager?.CreateToast("Update Failed")
                .WithContent($"Failed to update member: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        try
        {
            _logger?.LogDebug("Member dialog cancelled");
            _dialogManager.Close(this);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during cancel");
        }
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
        ProfileImageSource = ImageHelper.GetDefaultAvatar();
        ClearAllErrors();
    }
    
    protected override async ValueTask DisposeAsyncCore()
    {
        _logger?.LogInformation("Disposing MemberDialogCardViewModel");

        // Clear collections and references
        _memberPackageModels.Clear();
        ProfileImage = null;
        ProfileImageSource = null;

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}
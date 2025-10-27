using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AHON_TRACK.Converters;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Events;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace AHON_TRACK.Components.ViewModels;

[Page("viewEmployeeProfile")]
public sealed partial class EmployeeProfileInformationViewModel : ViewModelBase, INavigableWithParameters
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly AddNewEmployeeDialogCardViewModel _addNewEmployeeDialogCardViewModel;
    private readonly IEmployeeService _employeeService;
    private readonly INavigationService _navigationService;
    private readonly ILogger _logger;

    [ObservableProperty] private bool _isFromCurrentUser = false;
    [ObservableProperty] private bool _isInitialized;
    [ObservableProperty] private bool _isLoading;
    
    [ObservableProperty] private ManageEmployeesItem? _selectedEmployeeData;
    [ObservableProperty] private Bitmap? _employeeAvatarSource;
    [ObservableProperty] private string _employeeFullNameHeader = string.Empty;
    [ObservableProperty] private string _employeePosition = string.Empty;
    [ObservableProperty] private string _employeeStatus = string.Empty;
    [ObservableProperty] private string _employeeDateJoined = string.Empty;
    [ObservableProperty] private string _employeeFullName = string.Empty;
    [ObservableProperty] private string _employeeBirthDate = string.Empty;
    [ObservableProperty] private string _employeeGender = string.Empty;
    [ObservableProperty] private string _employeePhoneNumber = string.Empty;
    [ObservableProperty] private string _employeeLastLogin = string.Empty;
    [ObservableProperty] private string _employeeHouseAddress = string.Empty;
    [ObservableProperty] private string _employeeHouseNumber = string.Empty;
    [ObservableProperty] private string _employeeStreet = string.Empty;
    [ObservableProperty] private string _employeeBarangay = string.Empty;
    [ObservableProperty] private string _employeeCityProvince = string.Empty;
    [ObservableProperty] private string _employeeZipCode = string.Empty;
    [ObservableProperty] private int _employeeID;
    [ObservableProperty] private int _employeeAge;
    
    public Bitmap? DisplayAvatarSource => IsFromCurrentUser 
        ? ImageHelper.GetAvatarOrDefault(CurrentUserModel.AvatarBytes)
        : _selectedEmployeeData?.AvatarSource ?? ImageHelper.GetDefaultAvatar();

    public EmployeeProfileInformationViewModel(
        DialogManager dialogManager, 
        ToastManager toastManager, 
        AddNewEmployeeDialogCardViewModel addNewEmployeeDialogCardViewModel, 
        IEmployeeService employeeService,
        ILogger logger,
        INavigationService navigationService)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _addNewEmployeeDialogCardViewModel = addNewEmployeeDialogCardViewModel;
        _employeeService = employeeService;
        _logger = logger;
        _navigationService = navigationService;

        UserProfileEventService.Instance.ProfilePictureUpdated += OnProfilePictureUpdated;
    }

    public EmployeeProfileInformationViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _addNewEmployeeDialogCardViewModel = new AddNewEmployeeDialogCardViewModel();
        _employeeService = null!;
        _logger = null!;
        _navigationService = null!;
    }

    public void SetNavigationParameters(Dictionary<string, object> parameters)
    {
        // Check if this is current user profile
        if (parameters.TryGetValue("IsCurrentUser", out var isCurrentUserValue) && isCurrentUserValue is bool isCurrentUser)
        {
            IsFromCurrentUser = isCurrentUser;
        }

        // Check if employee data is passed
        if (parameters.TryGetValue("EmployeeData", out var employeeDataValue) && employeeDataValue is ManageEmployeesItem employeeData)
        {
            _selectedEmployeeData = employeeData;
        }
    }

    /*
    [AvaloniaHotReload]
    public void Initialize()
    {
        _ = InitializeAsync(LifecycleToken);
    }
    */
    
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
    
        if (IsInitialized)
        {
            _logger?.LogDebug("EmployeeProfileInformationViewModel already initialized");
            return;
        }

        _logger?.LogInformation("Initializing EmployeeProfileInformationViewModel");

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                LifecycleToken, cancellationToken);

            if (IsFromCurrentUser)
            {
                await InitializeCurrentUserProfileAsync(linkedCts.Token).ConfigureAwait(false);
            }
            else if (_selectedEmployeeData != null)
            {
                await InitializeEmployeeProfileAsync(linkedCts.Token).ConfigureAwait(false);
            }
            else
            {
                _logger?.LogWarning("No navigation parameters set, using default values");
                SetDefaultValues();
            }

            IsInitialized = true;
            _logger?.LogInformation("EmployeeProfileInformationViewModel initialized successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("EmployeeProfileInformationViewModel initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing EmployeeProfileInformationViewModel");
            SetDefaultValues(); // Fallback
        }
    }

    public ValueTask OnNavigatingFromAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Navigating away from EmployeeProfile");
        return ValueTask.CompletedTask;
    }

    private async Task InitializeEmployeeProfileAsync(CancellationToken cancellationToken = default)
    {
        if (_selectedEmployeeData == null)
        {
            _logger?.LogWarning("No employee data to initialize");
            return;
        }

        IsLoading = true;

        try
        {
            var (success, message, fullEmployee) = await _employeeService
                .ViewEmployeeProfileAsync(_selectedEmployeeData.ID)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (!success || fullEmployee == null)
            {
                _logger?.LogWarning("Failed to load employee {EmployeeId}: {Message}", 
                    _selectedEmployeeData.ID, message);
            
                _toastManager?.CreateToast("Error")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowError();
                return;
            }

            EmployeeID = fullEmployee.ID;
            EmployeePosition = fullEmployee.Position;
            EmployeeStatus = fullEmployee.Status;
            EmployeeDateJoined = fullEmployee.DateJoined == DateTime.MinValue
                ? "N/A"
                : fullEmployee.DateJoined.ToString("MMMM d, yyyy");
            EmployeeFullName = fullEmployee.Name;
            EmployeeFullNameHeader = $"{fullEmployee.Name}'s Profile";
            EmployeePhoneNumber = fullEmployee.ContactNumber;
            EmployeeAge = fullEmployee.Age;
            EmployeeBirthDate = fullEmployee.Birthdate;
            EmployeeGender = fullEmployee.Gender;
            EmployeeLastLogin = fullEmployee.LastLogin;
            EmployeeHouseAddress = fullEmployee.HouseAddress;
            EmployeeHouseNumber = fullEmployee.HouseNumber;
            EmployeeStreet = fullEmployee.Street;
            EmployeeBarangay = fullEmployee.Barangay;
            EmployeeCityProvince = fullEmployee.CityProvince;
            EmployeeZipCode = fullEmployee.ZipCode;
            EmployeeAvatarSource = fullEmployee.AvatarSource;
        
            OnPropertyChanged(nameof(DisplayAvatarSource));
        
            _logger?.LogInformation("Loaded profile for employee {EmployeeId}: {Name}", 
                fullEmployee.ID, fullEmployee.Name);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("InitializeEmployeeProfileAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading employee profile");
            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to load employee profile: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task InitializeCurrentUserProfileAsync(CancellationToken cancellationToken = default)
    {
        IsFromCurrentUser = true;
        IsLoading = true;

        try
        {
            int? currentUserId = CurrentUserModel.UserId;

            if (!currentUserId.HasValue)
            {
                _logger?.LogWarning("No user logged in, using default values");
                SetDefaultValues();
                EmployeeFullNameHeader = "My Profile";
                OnPropertyChanged(nameof(DisplayAvatarSource));
                return;
            }

            var (success, message, fullEmployee) = await _employeeService
                .ViewEmployeeProfileAsync(currentUserId.Value)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (!success || fullEmployee == null)
            {
                _logger?.LogWarning("Failed to load current user profile: {Message}", message);
            
                _toastManager?.CreateToast("Error")
                    .WithContent(message)
                    .DismissOnClick()
                    .ShowError();
            
                SetDefaultValues();
                return;
            }

            EmployeeID = fullEmployee.ID;
            EmployeePosition = fullEmployee.Position;
            EmployeeStatus = fullEmployee.Status;
            EmployeeDateJoined = fullEmployee.DateJoined == DateTime.MinValue
                ? "N/A"
                : fullEmployee.DateJoined.ToString("MMMM d, yyyy");
            EmployeeFullName = fullEmployee.Name;
            EmployeeFullNameHeader = "My Profile";
            EmployeePhoneNumber = fullEmployee.ContactNumber;
            EmployeeAge = fullEmployee.Age;
            EmployeeBirthDate = fullEmployee.Birthdate;
            EmployeeGender = fullEmployee.Gender;
            EmployeeLastLogin = fullEmployee.LastLogin;
            EmployeeHouseAddress = fullEmployee.HouseAddress;
            EmployeeHouseNumber = fullEmployee.HouseNumber;
            EmployeeStreet = fullEmployee.Street;
            EmployeeBarangay = fullEmployee.Barangay;
            EmployeeCityProvince = fullEmployee.CityProvince;
            EmployeeZipCode = fullEmployee.ZipCode;
        
            if (fullEmployee.AvatarBytes != null)
            {
                CurrentUserModel.AvatarBytes = fullEmployee.AvatarBytes;
            }

            OnPropertyChanged(nameof(DisplayAvatarSource));
        
            _logger?.LogInformation("Loaded current user profile: {Name}", fullEmployee.Name);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("InitializeCurrentUserProfileAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading current user profile");
            _toastManager?.CreateToast("Error")
                .WithContent($"Failed to load profile: {ex.Message}")
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
        EmployeeID = 13203;
        EmployeePosition = "Software Engineer";
        EmployeeStatus = "Active";
        EmployeeDateJoined = "January 15, 2023";
        EmployeeFullName = "John Doe";
        EmployeeFullNameHeader = IsFromCurrentUser ? "My Profile" : "John Doe's Profile";
        EmployeeAge = 31;
        EmployeeBirthDate = "1993-05-20";
        EmployeeGender = "Male";
        EmployeePhoneNumber = "09837756473";
        EmployeeLastLogin = "July 10, 2025 4:30PM";
        EmployeeHouseAddress = "123 Main";
        EmployeeHouseNumber = "123";
        EmployeeStreet = "Main Street";
        EmployeeBarangay = "Maungib";
        EmployeeCityProvince = "Cebu City, Cebu";
        EmployeeZipCode = "6000";
    }

    [RelayCommand]
    private async Task BackPage()
    {
        try
        {
            await _navigationService.NavigateAsync<DashboardViewModel>(cancellationToken: LifecycleToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error navigating back to dashboard");
        }
    }

    [RelayCommand]
    private async Task ShowEditProfileDialog()
    {
        try
        {
            // Create a ManageEmployeesItem from current profile data for editing
            var currentEmployeeData = new ManageEmployeesItem
            {
                ID = EmployeeID,
                Name = EmployeeFullName,
                Position = EmployeePosition,
                Status = EmployeeStatus,
                ContactNumber = EmployeePhoneNumber,
                DateJoined = DateTime.TryParse(EmployeeDateJoined, out var dateJoined) 
                    ? dateJoined 
                    : DateTime.MinValue,
                Username = CurrentUserModel.Username ?? "currentuser"
            };

            _logger?.LogDebug("Opening edit profile dialog for employee {EmployeeId}", EmployeeID);
        
            await _addNewEmployeeDialogCardViewModel.InitializeForEditMode(currentEmployeeData)
                .ConfigureAwait(false);
        
            _dialogManager.CreateDialog(_addNewEmployeeDialogCardViewModel)
                .WithSuccessCallback(async vm =>
                {
                    _toastManager?.CreateToast("Modified Employee Details")
                        .WithContent($"You have successfully modified details")
                        .DismissOnClick()
                        .ShowSuccess();
                
                    // Refresh the profile data
                    if (IsFromCurrentUser)
                    {
                        await InitializeCurrentUserProfileAsync(LifecycleToken).ConfigureAwait(false);
                    }
                    else if (_selectedEmployeeData != null)
                    {
                        await InitializeEmployeeProfileAsync(LifecycleToken).ConfigureAwait(false);
                    }
                })
                .WithCancelCallback(() =>
                    _toastManager?.CreateToast("Modifying Employee Details Cancelled")
                        .WithContent("Click the three-dots if you want to modify your employees' details")
                        .DismissOnClick()
                        .ShowWarning())
                .WithMaxWidth(950)
                .Show();
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error showing edit profile dialog");
            _toastManager?.CreateToast("Error")
                .WithContent("Failed to open edit dialog")
                .DismissOnClick()
                .ShowError();
        }
    }
    
    private async void OnProfilePictureUpdated()
    {
        try
        {
            if (IsFromCurrentUser)
            {
                _logger?.LogDebug("Profile picture updated, refreshing display");
                OnPropertyChanged(nameof(DisplayAvatarSource));
            
                // Optionally reload full profile to ensure consistency
                await InitializeCurrentUserProfileAsync(LifecycleToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during disposal
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling profile picture update");
        }
    }
    
    protected override async ValueTask DisposeAsyncCore()
    {
        _logger?.LogInformation("Disposing EmployeeProfileInformationViewModel");

        // Unsubscribe from events
        UserProfileEventService.Instance.ProfilePictureUpdated -= OnProfilePictureUpdated;

        // Clear references
        _selectedEmployeeData = null;
        EmployeeAvatarSource = null;

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AHON_TRACK.ViewModels;
using AHON_TRACK.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.ViewModels;

public partial class NotifyDialogCardViewmodel : ViewModelBase, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    private readonly ISmsService? _smsService;

    [ObservableProperty]
    private string? _memberName;

    [ObservableProperty]
    private string? _memberStatus;

    [ObservableProperty]
    private string? _memberValidity;

    [ObservableProperty]
    private string? _memberContactNumber;

    [ObservableProperty]
    [Required(ErrorMessage = "Message content is required")]
    [NotifyCanExecuteChangedFor(nameof(NotifyCommand))]
    private string? _textDescription;

    [ObservableProperty]
    private bool _isSending;

    private bool CanNotify() => !string.IsNullOrWhiteSpace(TextDescription) && !IsSending;

    public NotifyDialogCardViewmodel(
        DialogManager dialogManager, 
        ToastManager toastManager, 
        PageManager pageManager,
        ISmsService? smsService = null)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
        _smsService = smsService;
    }

    public NotifyDialogCardViewmodel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        IsSending = false;
        TextDescription = null;
    }

    public void SetMemberInfo(ManageMembersItem member)
    {
        MemberName = member.Name;
        MemberStatus = member.Status;
        MemberValidity = member.Validity.ToString("MMMM dd, yyyy");
        MemberContactNumber = member.ContactNumber;

        // Auto-generate appropriate message based on status
        GenerateDefaultMessage(member);
    }

    private void GenerateDefaultMessage(ManageMembersItem member)
    {
        if (_smsService == null)
        {
            TextDescription = $"Dear {member.Name}, your membership status requires attention. Please visit our gym to renew.";
            return;
        }

        string statusLower = member.Status?.ToLower() ?? "";

        if (statusLower == "near expiry")
        {
            int daysRemaining = (member.Validity.Date - DateTime.Now.Date).Days;
        
            Debug.WriteLine($"[GenerateDefaultMessage] Member: {member.Name}");
            Debug.WriteLine($"[GenerateDefaultMessage] Valid Until: {member.Validity:yyyy-MM-dd}");
            Debug.WriteLine($"[GenerateDefaultMessage] Today: {DateTime.Now:yyyy-MM-dd}");
            Debug.WriteLine($"[GenerateDefaultMessage] Days Remaining: {daysRemaining}");
        
            TextDescription = _smsService.GenerateNearExpiryMessage(
                member.Name, 
                member.Validity.ToString("MMMM dd, yyyy"),
                daysRemaining
            );
        }
        else if (statusLower == "expired")
        {
            TextDescription = _smsService.GenerateExpiredMessage(
                member.Name,
                member.Validity.ToString("MMMM dd, yyyy")
            );
        }
        else
        {
            TextDescription = $"Dear {member.Name}, this is a notification regarding your membership status.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanNotify))]
    private async Task NotifyAsync()
    {
        ValidateAllProperties();

        if (HasErrors)
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Please fix all validation errors before sending.")
                .DismissOnClick()
                .ShowError();
            return;
        }

        if (string.IsNullOrWhiteSpace(MemberContactNumber))
        {
            _toastManager.CreateToast("No Contact Number")
                .WithContent($"{MemberName} does not have a contact number on file.")
                .DismissOnClick()
                .ShowWarning();
            return;
        }

        // Show confirmation before sending SMS
        bool shouldProceed = await ShowSendConfirmationAsync();
        if (!shouldProceed)
        {
            return;
        }

        IsSending = true;

        try
        {
            if (_smsService != null)
            {
                Debug.WriteLine($"[NotifyAsync] Sending SMS to {MemberName} at {MemberContactNumber}");
                
                var result = await _smsService.SendSmsAsync(
                    MemberContactNumber,
                    TextDescription ?? ""
                );

                if (result.Success)
                {
                    _toastManager.CreateToast("SMS Sent Successfully")
                        .WithContent($"Notification sent to {MemberName} at {_smsService.FormatPhilippineNumber(MemberContactNumber)}")
                        .DismissOnClick()
                        .ShowSuccess();

                    _dialogManager.Close(this, new CloseDialogOptions { Success = true });
                }
                else
                {
                    _toastManager.CreateToast("SMS Failed")
                        .WithContent($"Failed to send SMS: {result.Message}")
                        .DismissOnClick()
                        .ShowError();
                }
            }
            else
            {
                // Fallback if SMS service is not configured
                _toastManager.CreateToast("SMS Service Unavailable")
                    .WithContent("SMS service is not configured. Notification recorded without sending SMS.")
                    .DismissOnClick()
                    .ShowWarning();

                _dialogManager.Close(this, new CloseDialogOptions { Success = true });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotifyAsync] Error: {ex.Message}");
            _toastManager.CreateToast("Error")
                .WithContent($"An error occurred: {ex.Message}")
                .DismissOnClick()
                .ShowError();
        }
        finally
        {
            IsSending = false;
        }
    }

    private async Task<bool> ShowSendConfirmationAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        string formattedNumber = _smsService?.FormatPhilippineNumber(MemberContactNumber ?? "") ?? MemberContactNumber;

        _dialogManager.CreateDialog(
            "Confirm SMS Notification",
            $"Send SMS notification to {MemberName} at {formattedNumber}?\n\nThis action will send a text message to the member's phone.")
            .WithPrimaryButton("Send SMS", () => tcs.SetResult(true), DialogButtonStyle.Primary)
            .WithCancelButton("Cancel", () => tcs.SetResult(false))
            .WithMaxWidth(512)
            .Dismissible()
            .Show();

        return await tcs.Task;
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }
}
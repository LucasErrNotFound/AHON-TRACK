using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public partial class NotifyDialogCardViewmodel : ViewModelBase, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    [ObservableProperty] 
    [Required(ErrorMessage = "Description is required")]
    [NotifyCanExecuteChangedFor(nameof(NotifyCommand))]
    private string? _textDescription;
    
    private bool CanNotify() => !string.IsNullOrWhiteSpace(TextDescription);

    public NotifyDialogCardViewmodel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
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
    }
    
    [RelayCommand(CanExecute = nameof(CanNotify))]
    private void Notify()
    {
        ValidateAllProperties();
        
        if (HasErrors)
        {
            _toastManager.CreateToast("Validation Error")
                .WithContent("Please fix all validation errors before saving.")
                .DismissOnClick()
                .ShowError();
            return;
        }
        
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }
    
    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }
}
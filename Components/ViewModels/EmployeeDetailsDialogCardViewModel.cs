using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.Input;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

public sealed partial class EmployeeDetailsDialogCardViewModel : ViewModelBase
{
    private readonly DialogManager _dialogManager;
    public EmployeeDetailsDialogCardViewModel(DialogManager dialogManager)
    {
        _dialogManager = dialogManager;
    }

    public EmployeeDetailsDialogCardViewModel()
    {
        _dialogManager = new DialogManager();
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
        ClearAllErrors();
    }

    [RelayCommand]
    private void SaveDetails()
    {
        ClearAllErrors();
        ValidateAllProperties();

        if (HasErrors) return;
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }
}

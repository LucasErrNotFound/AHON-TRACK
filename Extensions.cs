using AHON_TRACK;
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Components.AddNewEmployeeDialog;
using ShadUI;

namespace AHON_TRACK;

public static class Extensions
{
    public static ServiceProvider RegisterDialogs(this ServiceProvider service)
    {
        var dialogService = service.GetService<DialogManager>();
        dialogService.Register<AddNewEmployeeDialogCard, AddNewEmployeeDialogCardViewModel>();

        return service;
    }
}
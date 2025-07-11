using AHON_TRACK;
using AHON_TRACK.Components.EmployeeDetails;
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.ViewModels;
using ShadUI;

namespace AHON_TRACK;

public static class Extensions
{
    public static ServiceProvider RegisterDialogs(this ServiceProvider service)
    {
        var dialogService = service.GetService<DialogManager>();
        dialogService.Register<EmployeeDetailsDialogCard, EmployeeDetailsDialogCardViewModel>();

        return service;
    }
}
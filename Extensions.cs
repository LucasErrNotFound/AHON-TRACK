using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Components.AddNewEmployeeDialog;
using AHON_TRACK.Components.AddTrainingScheduleDialog;
using AHON_TRACK.Components.LogGymMemberDialog;
using ShadUI;

namespace AHON_TRACK;

public static class Extensions
{
    public static ServiceProvider RegisterDialogs(this ServiceProvider service)
    {
        var dialogService = service.GetService<DialogManager>();
        dialogService.Register<AddNewEmployeeDialogCard, AddNewEmployeeDialogCardViewModel>();
		dialogService.Register<LogGymMemberDialogCard, LogGymMemberDialogCardViewModel>();
        dialogService.Register<AddTrainingScheduleDialogCard, AddTrainingScheduleDialogCardViewModel>();

        return service;
    }
}
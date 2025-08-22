using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Components.AddNewEmployeeDialog;
using AHON_TRACK.Components.AddNewPackageDialog;
using AHON_TRACK.Components.AddTrainingScheduleDialog;
using AHON_TRACK.Components.EditPackageDialog;
using AHON_TRACK.Components.EquipmentDialog;
using AHON_TRACK.Components.ItemDialog;
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
        dialogService.Register<AddNewPackageDialogCard, AddNewPackageDialogCardViewModel>();
        dialogService.Register<EditPackageDialogCard, EditPackageDialogCardViewModel>();
        dialogService.Register<EquipmentDialogCard, EquipmentDialogCardViewModel>();
        dialogService.Register<ItemDialogCard, ItemDialogCardViewModel>();

        return service;
    }
}
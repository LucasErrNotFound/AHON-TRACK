using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Components.AddNewEmployeeDialog;
using AHON_TRACK.Components.AddNewPackageDialog;
using AHON_TRACK.Components.AddTrainingScheduleDialog;
using AHON_TRACK.Components.ChangeScheduleDialog;
using AHON_TRACK.Components.EditPackageDialog;
using AHON_TRACK.Components.EquipmentDialog;
using AHON_TRACK.Components.ForgotPasswordDialog;
using AHON_TRACK.Components.LogGymMemberDialog;
using AHON_TRACK.Components.MemberDialog;
using AHON_TRACK.Components.NotifyDialog;
using AHON_TRACK.Components.SettingsDialog;
using AHON_TRACK.Components.SupplierDialog;
using AHON_TRACK.Components.SupplierEquipmentDialog;
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
        dialogService.Register<SupplierDialogCard, SupplierDialogCardViewModel>();
        dialogService.Register<MemberDialogCard, MemberDialogCardViewModel>();
        dialogService.Register<ChangeScheduleDialogCard, ChangeScheduleDialogCardViewModel>();
        dialogService.Register<SettingsDialogCard, SettingsDialogCardViewModel>();
        dialogService.Register<ForgotPasswordDialogCard, ForgotPasswordDialogCardViewModel>();
        dialogService.Register<NotifyDialogCard, NotifyDialogCardViewmodel>();
        dialogService.Register<SupplierEquipmentDialogCard, SupplierEquipmentDialogCardViewModel>();

        return service;
    }
}
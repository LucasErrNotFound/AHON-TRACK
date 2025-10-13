using AHON_TRACK.ViewModels;
using AHON_TRACK.Components.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.IO;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using ShadUI;
using Jab;
using Serilog;

namespace AHON_TRACK;

[ServiceProvider]
[Transient<LoginViewModel>]
[Transient<MainWindowViewModel>]
[Transient<DashboardViewModel>]
[Transient<ManageEmployeesViewModel>]
[Transient<CheckInOutViewModel>]
[Transient<ManageMembershipViewModel>]
[Transient<TrainingSchedulesViewModel>]
[Transient<ManageBillingViewModel>]
[Transient<ProductPurchaseViewModel>]
[Transient<EquipmentInventoryViewModel>]
[Transient<ProductStockViewModel>]
[Transient<SupplierManagementViewModel>]
[Transient<AddNewEmployeeDialogCardViewModel>]
[Transient<EmployeeProfileInformationViewModel>]
[Transient<MemberProfileInformationViewModel>]
[Transient<LogGymMemberDialogCardViewModel>]
[Transient<LogWalkInPurchaseViewModel>]
[Transient<AddTrainingScheduleDialogCardViewModel>]
[Transient<AddNewPackageDialogCardViewModel>]
[Transient<EditPackageDialogCardViewModel>]
[Transient<AddEditProductViewModel>]
[Transient<EquipmentDialogCardViewModel>]
[Transient<SupplierDialogCardViewModel>]
[Transient<FinancialReportsViewModel>]
[Transient<GymDemographicsViewModel>]
[Transient<GymAttendanceViewModel>]
[Transient<AuditLogsViewModel>]
[Transient<MemberDialogCardViewModel>]
[Transient<AddNewMemberViewModel>]
[Transient<ChangeScheduleDialogCardViewModel>]
[Singleton<DialogManager>]
[Singleton<ToastManager>]
[Singleton<IMessenger, WeakReferenceMessenger>]
[Singleton<IPackageService, PackageService>]
[Singleton(typeof(ILogger), Factory = nameof(LoggerFactory))]
[Singleton(typeof(PageManager), Factory = nameof(PageManagerFactory))]
public partial class ServiceProvider
{
    public static ILogger LoggerFactory()
    {
        var currentFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AHON_TRACK\\logs");

        Directory.CreateDirectory(currentFolder);

        var file = Path.Combine(currentFolder, "log.txt");

        var config = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(file, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Logger = config;

        return config;
    }

    public PageManager PageManagerFactory()
    {
        return new PageManager(this);
    }
}
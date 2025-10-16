using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.IO;
using ShadUI;
using Jab;
using Serilog;
using ShadUI;
using System;
using System.IO;

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
[Transient<SettingsDialogCardViewModel>]
[Singleton<DialogManager>]
[Singleton<ToastManager>]
[Singleton<IMessenger, WeakReferenceMessenger>]
[Singleton(typeof(ILogger), Factory = nameof(LoggerFactory))]
[Singleton(typeof(PageManager), Factory = nameof(PageManagerFactory))]
[Singleton<string>(Factory = nameof(ConnectionStringFactory))]
[Singleton<IEmployeeService, EmployeeService>]
[Singleton<IDashboardService, DashboardService>]
[Singleton<IMemberService, MemberService>]
[Singleton<ICheckInOutService, CheckInOutService>]
[Singleton<ITrainingService, TrainingService>]
[Singleton<IProductService, ProductService>]
[Singleton<IInventoryService, InventoryService>]
[Singleton<IPackageService, PackageService>]
[Singleton<ISupplierService, SupplierService>]
[Singleton<IWalkInService, WalkInService>]
[Singleton<IProductPurchaseService, ProductPurchaseService>]

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

    private string ConnectionStringFactory()
    {
        return "Data Source=LAPTOP-SSMJIDM6\\SQLEXPRESS08;Initial Catalog=AHON_TRACK;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";
    }


}
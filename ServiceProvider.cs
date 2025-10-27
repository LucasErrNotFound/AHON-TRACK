using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Jab;
using Microsoft.Extensions.Logging;
using Serilog;
using ShadUI;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace AHON_TRACK;

[ServiceProvider]
[Singleton<ServiceProvider>] 
[Transient<LoginViewModel>]
[Transient<MainWindowViewModel>]
[Transient<DashboardViewModel>]
[Transient<DashboardModel>]
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
[Singleton<SettingsService>]
[Singleton<IMessenger, WeakReferenceMessenger>]
[Singleton(typeof(INavigationService), Factory = nameof(NavigationServiceFactory))]
[Singleton<ILoggerFactory>(Factory = nameof(LoggerFactoryFactory))]
[Singleton<ILogger>(Factory = nameof(LoggerFactoryLogger))]
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
[Singleton<DataCountingService>]
public partial class ServiceProvider
{
    public INavigationService NavigationServiceFactory()
    {
        ILogger logger = GetService<ILogger>();
        return new NavigationService(this, logger);
    }
    
    private ILoggerFactory LoggerFactoryFactory()
    {
        // Configure Serilog once
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                "Logs\\AHON_TRACK-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10,
                shared: true)
            .CreateLogger();

        // Create Microsoft logger factory bridged to Serilog
        var factory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        return factory;
    }

    private ILogger LoggerFactoryLogger()
    {
        var factory = GetService<ILoggerFactory>();
        return factory.CreateLogger("AHON_TRACK");
    }
    
    private string ConnectionStringFactory()
    {
        // TODO: Move to configuration file
        return "Data Source=LAPTOP-SSMJIDM6\\SQLEXPRESS08;Initial Catalog=AHON_TRACK;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";
    }
}
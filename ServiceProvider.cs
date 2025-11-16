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
[Transient<ServiceProvider>]
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
[Transient<PurchaseOrderViewModel>]
[Transient<ForgotPasswordDialogCardViewModel>]
[Transient<NotifyDialogCardViewmodel>]
[Transient<SupplierEquipmentDialogCardViewModel>]
[Singleton<DialogManager>]
[Singleton<ToastManager>]
[Singleton<SettingsService>]
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
[Singleton<DataCountingService, DataCountingService>]

[Singleton(typeof(ISmsService), Factory = nameof(SmsServiceFactory))]
[Singleton(typeof(IPurchaseOrderService), Factory = nameof(PurchaseOrderServiceFactory))]
[Singleton(typeof(BackupDatabaseService), Factory = nameof(BackupDatabaseServiceFactory))]
[Singleton(typeof(BackupSchedulerService), Factory = nameof(BackupSchedulerServiceFactory))]

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
    
    private ISmsService SmsServiceFactory()
    {
        // ⚠️ IMPORTANT: Replace these with your actual HttpSMS credentials
        // Get your API key from https://httpsms.com/settings
        const string apiKey = "uk_a0sh3JfokZPdEA3tSGD1UGizW3JCetUHIAEKAcaHLLrMRC_NDnQnbtbacOhh_Mx6";
        
        // Your sending phone number (must be registered with HttpSMS)
        // Format: +639XXXXXXXXX (Philippine number)
        const string fromPhoneNumber = "+639157055726";

        return new SmsService(apiKey, fromPhoneNumber);
    }

    private IPurchaseOrderService PurchaseOrderServiceFactory()
    {
        var connectionString = ConnectionStringFactory();
        var toastManager = GetService<ToastManager>();
        var productService = GetService<IProductService>();
        var supplierService = GetService<ISupplierService>();
        var inventoryService = GetService<IInventoryService>();

        return new PurchaseOrderService(connectionString, toastManager, productService);
    }

    private BackupDatabaseService BackupDatabaseServiceFactory()
    {
        var connectionString = ConnectionStringFactory();
        return new BackupDatabaseService(connectionString);
    }

    private BackupSchedulerService BackupSchedulerServiceFactory()
    {
        var settingsService = GetService<SettingsService>();
        var backupDatabaseService = GetService<BackupDatabaseService>();
        var toastManager = GetService<ToastManager>();
        return new BackupSchedulerService(settingsService, backupDatabaseService, toastManager);
    }

    private string ConnectionStringFactory()
    {
        return "Data Source=LAPTOP-SSMJIDM6\\SQLEXPRESS08;Initial Catalog=AHON_TRACK;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";
    }
}
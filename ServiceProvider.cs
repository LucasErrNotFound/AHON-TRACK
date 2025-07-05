using AHON_TRACK;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
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
[Transient<MemberCheckInOutViewModel>]
[Transient<ManageMembershipViewModel>]
[Transient<WalkInRegistrationViewModel>]
[Transient<MemberDirectoryViewModel>]
[Transient<TrainingSchedulesViewModel>]
[Transient<RoomEquipmentBookingViewModel>]
[Transient<PaymentOverviewViewModel>]
[Transient<OutstandingBalancesViewModel>]
[Transient<PaymentHistoryViewModel>]
[Transient<ManageBillingViewModel>]
[Transient<EquipmentInventoryViewModel>]
[Transient<ProductSupplementStockViewModel>]
[Transient<SupplierManagementViewModel>]
[Transient<FinancialReportsViewModel>]
[Transient<GymDemographicsViewModel>]
[Transient<EquipmentUsageReportsViewModel>]
[Transient<ClassAttendanceReportsViewModel>]
[Singleton<ToastManager>]
[Singleton<DialogManager>]
[Singleton<IMessenger, WeakReferenceMessenger>]
[Singleton(typeof(ILogger), Factory = nameof(LoggerFactory))]
[Singleton(typeof(PageManager), Factory = nameof(PageManagerFactory))]
public partial class ServiceProvider
{
    public ILogger LoggerFactory()
    {
        var currentFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AHON_TRACK\\logs");

        Directory.CreateDirectory(currentFolder); //ensure the directory exists

        var file = Path.Combine(currentFolder, "log.txt");

        var config = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(file, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Logger = config; //set the global logger

        return config;
    }

    public PageManager PageManagerFactory()
    {
        return new PageManager(this);
    }
}
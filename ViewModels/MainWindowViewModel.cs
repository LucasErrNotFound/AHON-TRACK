using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI.Dialogs;
using ShadUI.Toasts;
using System;
using System.Threading.Tasks;
using AHON_TRACK.Views;
using AHON_TRACK.ViewModels;
using System.ComponentModel.Design;
using ShadUI.Themes;
using Avalonia;

namespace AHON_TRACK.ViewModels;

public sealed partial class MainWindowViewModel(
    DialogManager dialogManager,
    ToastManager toastManager,
    DashboardViewModel dashboardViewModel,
    ManageEmployeesViewModel manageEmployeesViewModel,
    MemberCheckInOutViewModel memberCheckInOutViewModel,
    ManageMembershipViewModel manageMembershipViewModel,
    WalkInRegistrationViewModel walkInRegistrationViewModel,
    MemberDirectoryViewModel memberDirectoryViewModel,
    TrainingSchedulesViewModel trainingSchedulesViewModel,
    RoomEquipmentBookingViewModel roomEquipmentBookingViewModel,
    PaymentOverviewViewModel paymentOverviewViewModel,
    OutstandingBalancesViewModel outstandingBalancesViewModel,
    PaymentHistoryViewModel paymentHistoryViewModel,
    ManageBillingViewModel manageBillingViewModel,
    EquipmentInventoryViewModel equipmentInventoryViewModel,
    ProductSupplementStockViewModel productSupplementStockViewModel,
    SupplierManagementViewModel supplierManagementViewModel,
    FinancialReportsViewModel financialReportsViewModel,
    GymDemographicsViewModel gymDemographicsViewModel,
    EquipmentUsageReportsViewModel equipmentUsageReportsViewModel,
    ClassAttendanceReportsViewModel classAttendanceReportsViewModel) : ViewModelBase
{
    public MainWindowViewModel() : 
        this(Design.IsDesignMode? new DialogManager() : null!,
            Design.IsDesignMode? new ToastManager() : null!,
            Design.IsDesignMode? new DashboardViewModel() : null!,
            Design.IsDesignMode? new ManageEmployeesViewModel() : null!,
            Design.IsDesignMode? new MemberCheckInOutViewModel() : null!,
            Design.IsDesignMode? new ManageMembershipViewModel() : null!,
            Design.IsDesignMode? new WalkInRegistrationViewModel() : null!,
            Design.IsDesignMode? new MemberDirectoryViewModel() : null!,
            Design.IsDesignMode? new TrainingSchedulesViewModel() : null!,
            Design.IsDesignMode? new RoomEquipmentBookingViewModel() : null!,
            Design.IsDesignMode? new PaymentOverviewViewModel() : null!,
            Design.IsDesignMode? new OutstandingBalancesViewModel() : null!,
            Design.IsDesignMode? new PaymentHistoryViewModel() : null!,
            Design.IsDesignMode? new ManageBillingViewModel() : null!,
            Design.IsDesignMode? new EquipmentInventoryViewModel() : null!,
            Design.IsDesignMode? new ProductSupplementStockViewModel() : null!,
            Design.IsDesignMode? new SupplierManagementViewModel() : null!,
            Design.IsDesignMode? new FinancialReportsViewModel() : null!,
            Design.IsDesignMode? new GymDemographicsViewModel() : null!,
            Design.IsDesignMode? new EquipmentUsageReportsViewModel() : null!,
            Design.IsDesignMode? new ClassAttendanceReportsViewModel() : null!
        
        ) { }

    [ObservableProperty]
    private DialogManager dialogManager = dialogManager;

    [ObservableProperty]
    private ToastManager toastManager = toastManager;

    [ObservableProperty]
    private object? _selectedPage;

    private async Task<bool> SwitchPageAsync(object page)
    {
        if (SelectedPage == page) return false;

        await Task.Delay(200);
        SelectedPage = page;
        return true;
    }

    [RelayCommand]
    private void TryClose()
    {
        DialogManager.CreateDialog("Close", "Do you really want to exit?")
            .WithPrimaryButton("Yes", OnAcceptExit)
            .WithCancelButton("No")
            .WithMinWidth(300)
            .Show();
    }

    // I'll probably refactor this to make it shorter
    [RelayCommand]
    private async Task OpenDashboard()
    {
        if (await SwitchPageAsync(dashboardViewModel))
        {
            dashboardViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenManageEmployees()
    {
        if (await SwitchPageAsync(manageEmployeesViewModel))
        {
            manageEmployeesViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenMemberCheckInOut()
    {
        if (await SwitchPageAsync(memberCheckInOutViewModel))
        {
            memberCheckInOutViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenManageMembership()
    {
        if (await SwitchPageAsync(manageMembershipViewModel))
        {
            manageMembershipViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenWalkInRegistration()
    {
        if (await SwitchPageAsync(walkInRegistrationViewModel))
        {
            walkInRegistrationViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenMemberDirectory()
    {
        if (await SwitchPageAsync(memberDirectoryViewModel))
        {
            memberDirectoryViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenTrainingSchedules()
    {
        if (await SwitchPageAsync(trainingSchedulesViewModel))
        {
            trainingSchedulesViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenRoomEquipmentBooking()
    {
        if (await SwitchPageAsync(roomEquipmentBookingViewModel))
        {
            roomEquipmentBookingViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenPaymentOverview()
    {
        if (await SwitchPageAsync(paymentOverviewViewModel))
        {
            paymentOverviewViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenOutstandingBalances()
    {
        if (await SwitchPageAsync(outstandingBalancesViewModel))
        {
            outstandingBalancesViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenPaymentHistory()
    {
        if (await SwitchPageAsync(paymentHistoryViewModel))
        {
            paymentHistoryViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenManageBilling()
    {
        if (await SwitchPageAsync(manageBillingViewModel))
        {
            manageBillingViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenEquipmentInventory()
    {
        if (await SwitchPageAsync(equipmentInventoryViewModel))
        {
            equipmentInventoryViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenProductSupplementStockViewModel()
    {
        if (await SwitchPageAsync(productSupplementStockViewModel))
        {
            productSupplementStockViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenSupplierManagement()
    {
        if (await SwitchPageAsync(supplierManagementViewModel))
        {
            supplierManagementViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenFinancialReports()
    {
        if (await SwitchPageAsync(financialReportsViewModel))
        {
            financialReportsViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenGymDemographics()
    {
        if (await SwitchPageAsync(gymDemographicsViewModel))
        {
            gymDemographicsViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenEquipmentUsageReports()
    {
        if (await SwitchPageAsync(equipmentUsageReportsViewModel))
        {
            equipmentUsageReportsViewModel.Initialize();
        }
    }

    [RelayCommand]
    private async Task OpenClassAttendanceReports()
    {
        if (await SwitchPageAsync(classAttendanceReportsViewModel))
        {
            classAttendanceReportsViewModel.Initialize();
        }
    }

    private void OnAcceptExit()
    {
        Environment.Exit(0);
    }

    public void Initialize()
    {
        SelectedPage = dashboardViewModel;
        dashboardViewModel.Initialize();
    }
}

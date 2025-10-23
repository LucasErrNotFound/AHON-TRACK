using System;

namespace AHON_TRACK.Services.Events
{
    public class DashboardEventService
    {
        private static DashboardEventService? _instance;
        public static DashboardEventService Instance => _instance ??= new DashboardEventService();

        // Existing events
        public event EventHandler? RecentLogsUpdated;
        public event EventHandler? SalesUpdated;
        public event EventHandler? TrainingSessionsUpdated;
        public event EventHandler? ChartDataUpdated;

        // New events for population & members
        public event Action? OnPopulationDataChanged;
        public event EventHandler? MemberAdded;
        public event EventHandler? MemberDeleted;
        public event EventHandler? MemberUpdated;

        public event EventHandler? WalkInAdded;
        public event EventHandler? WalkInDeleted;
        public event EventHandler? WalkInUpdated;

        // New events for employees
        public event EventHandler? EmployeeAdded;
        public event EventHandler? EmployeeDeleted;
        public event EventHandler? EmployeeUpdated;

        // New events for products
        public event EventHandler? ProductAdded;
        public event EventHandler? ProductDeleted;
        public event EventHandler? ProductUpdated;

        // New events for equipments
        public event EventHandler? EquipmentAdded;
        public event EventHandler? EquipmentDeleted;
        public event EventHandler? EquipmentUpdated;

        // New Events for Purchases
        public event EventHandler? ProductPurchased;

        // New Events for Scheduling
        public event EventHandler? ScheduleAdded;
        public event EventHandler? ScheduleUpdated;

        // New Events for checkin/out
        public event EventHandler? CheckinAdded;
        public event EventHandler? CheckoutAdded;
        public event EventHandler? CheckInOutDeleted;

        // New Events for Packages
        public event EventHandler? PackageAdded;
        public event EventHandler? PackageDeleted;
        public event EventHandler? PackageUpdated;

        private DashboardEventService() { }

        // --- Dashboard events ---
        public void NotifyRecentLogsUpdated() => RecentLogsUpdated?.Invoke(this, EventArgs.Empty);
        public void NotifySalesUpdated() => SalesUpdated?.Invoke(this, EventArgs.Empty);
        public void NotifyTrainingSessionsUpdated() => TrainingSessionsUpdated?.Invoke(this, EventArgs.Empty);
        public void NotifyChartDataUpdated() => ChartDataUpdated?.Invoke(this, EventArgs.Empty);
        public void NotifyPopulationDataChanged() => OnPopulationDataChanged?.Invoke();

        // --- Member events ---
        public void NotifyMemberAdded() => MemberAdded?.Invoke(this, EventArgs.Empty);
        public void NotifyMemberDeleted() => MemberDeleted?.Invoke(this, EventArgs.Empty);
        public void NotifyMemberUpdated() => MemberUpdated?.Invoke(this, EventArgs.Empty);

        // --- Walk-in events ---
        public void NotifyWalkInAdded()
        {
            WalkInAdded?.Invoke(this, EventArgs.Empty);
            NotifyPopulationDataChanged();
        }
        public void NotifyWalkInDeleted()
        {
            WalkInDeleted?.Invoke(this, EventArgs.Empty);
            NotifyPopulationDataChanged();
        }
        public void NotifyWalkInUpdated()
        {
            WalkInUpdated?.Invoke(this, EventArgs.Empty);
            NotifyPopulationDataChanged();
        }

        // --- Employee events ---
        public void NotifyEmployeeAdded() => EmployeeAdded?.Invoke(this, EventArgs.Empty);
        public void NotifyEmployeeDeleted() => EmployeeDeleted?.Invoke(this, EventArgs.Empty);
        public void NotifyEmployeeUpdated() => EmployeeUpdated?.Invoke(this, EventArgs.Empty);

        // --- Product events ---
        public void NotifyProductAdded() => ProductAdded?.Invoke(this, EventArgs.Empty);
        public void NotifyProductDeleted() => ProductDeleted?.Invoke(this, EventArgs.Empty);
        public void NotifyProductUpdated() => ProductUpdated?.Invoke(this, EventArgs.Empty);

        // --- Equipment events ---
        public void NotifyEquipmentAdded() => EquipmentAdded?.Invoke(this, EventArgs.Empty);
        public void NotifyEquipmentDeleted() => EquipmentDeleted?.Invoke(this, EventArgs.Empty);
        public void NotifyEquipmentUpdated() => EquipmentUpdated?.Invoke(this, EventArgs.Empty);

        // --- Purchase events ---
        public void NotifyProductPurchased() => ProductPurchased?.Invoke(this, EventArgs.Empty);

        // --- Schedule events ---
        public void NotifyScheduleAdded() => ScheduleAdded?.Invoke(this, EventArgs.Empty);
        public void NotifyScheduleUpdated() => ScheduleUpdated?.Invoke(this, EventArgs.Empty);

        // --- Check In - Out ---
        public void NotifyCheckinAdded() => CheckinAdded?.Invoke(this, EventArgs.Empty);
        public void NotifyCheckoutAdded() => CheckoutAdded?.Invoke(this, EventArgs.Empty);
        public void NotifyCheckInOutDeleted() => CheckInOutDeleted?.Invoke(this, EventArgs.Empty);

        // ---Packages event ---
        public void NotifyPackageAdded() => PackageAdded?.Invoke(this, EventArgs.Empty);
        public void NotifyPackageUpdated() => PackageUpdated?.Invoke(this, EventArgs.Empty);
        public void NotifyPackageDeleted() => PackageDeleted?.Invoke(this, EventArgs.Empty);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        private DashboardEventService() { }

        // 🔹 When a new product is added:
        public void NotifyProductAdded() => ProductAdded?.Invoke(this, EventArgs.Empty);
        public void NotifyProductDeleted() => ProductDeleted?.Invoke(this, EventArgs.Empty);
        public void NotifyProductUpdated() => ProductUpdated?.Invoke(this, EventArgs.Empty);

        // 🔹 When a new employee is added:
        public void NotifyEmployeeAdded() => EmployeeAdded?.Invoke(this, EventArgs.Empty);
        public void NotifyEmployeeDeleted() => EmployeeDeleted?.Invoke(this, EventArgs.Empty);
        public void NotifyEmployeeUpdated() => EmployeeUpdated?.Invoke(this, EventArgs.Empty);

        // --- Notify methods ---
        public void NotifyRecentLogsUpdated() => RecentLogsUpdated?.Invoke(this, EventArgs.Empty);
        public void NotifySalesUpdated() => SalesUpdated?.Invoke(this, EventArgs.Empty);
        public void NotifyTrainingSessionsUpdated() => TrainingSessionsUpdated?.Invoke(this, EventArgs.Empty);
        public void NotifyChartDataUpdated() => ChartDataUpdated?.Invoke(this, EventArgs.Empty);

        public void NotifyPopulationDataChanged() => OnPopulationDataChanged?.Invoke();

        // 🔹 When a new member is added:
        public void NotifyMemberAdded() => MemberAdded?.Invoke(this, EventArgs.Empty);
        public void NotifyMemberDeleted() => MemberDeleted?.Invoke(this, EventArgs.Empty);
        public void NotifyMemberUpdated() => MemberUpdated?.Invoke(this, EventArgs.Empty);

        // 🔹 When a new walk-in is added:
        public void NotifyWalkInAdded()
        {
            WalkInAdded?.Invoke(this, EventArgs.Empty);
            NotifyPopulationDataChanged();
        }
    }
}

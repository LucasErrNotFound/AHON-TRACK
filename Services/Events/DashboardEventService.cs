using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Events
{
    public class DashboardEventService
    {
        private static DashboardEventService _instance;
        public static DashboardEventService Instance => _instance ??= new DashboardEventService();

        public event EventHandler RecentLogsUpdated;
        public event EventHandler SalesUpdated;
        public event EventHandler TrainingSessionsUpdated;
        public event Action? OnPopulationDataChanged;

        public void NotifyPopulationDataChanged()
        {
            OnPopulationDataChanged?.Invoke();
        }
        public event EventHandler? ChartDataUpdated;
        public void NotifyChartDataUpdated() => ChartDataUpdated?.Invoke(this, EventArgs.Empty);

        public void NotifyRecentLogsUpdated() => RecentLogsUpdated?.Invoke(this, EventArgs.Empty);
        public void NotifySalesUpdated() => SalesUpdated?.Invoke(this, EventArgs.Empty);
        public void NotifyTrainingSessionsUpdated() => TrainingSessionsUpdated?.Invoke(this, EventArgs.Empty);
    }
}

using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface IDashboardService
    {
        Task<IEnumerable<int>> GetAvailableYearsAsync();
        Task<IEnumerable<SalesItem>> GetSalesAsync(int topN = 5);
        Task<string> GenerateSalesSummaryAsync(int topN = 5);
        Task<IEnumerable<TrainingSession>> GetTrainingSessionsAsync(int topN = 5);
        Task<string> GenerateTrainingSessionsSummaryAsync(int topN = 5);
        Task<IEnumerable<RecentLog>> GetRecentLogsAsync(int topN = 5);
        Task<string> GenerateRecentLogSummaryAsync(int topN = 5);
        Task<int[]> GetSalesDataForYearAsync(int year);
    }



}

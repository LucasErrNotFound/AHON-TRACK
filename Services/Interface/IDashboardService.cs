using AHON_TRACK.Models;
using AHON_TRACK.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface IDashboardService
    {
        // SALES
        Task<IEnumerable<int>> GetAvailableYearsAsync();
        Task<IEnumerable<SalesItem>> GetSalesAsync(int topN = 5);
        Task<string> GenerateSalesSummaryAsync(int topN = 5);
        Task<int[]> GetSalesDataForYearAsync(int year);

        // RECENt LOGS
        Task<IEnumerable<RecentLog>> GetRecentLogsAsync(int topN = 5);
        Task<string> GenerateRecentLogSummaryAsync(int topN = 5);

        // TRAINING SESSION SUMMARY
        Task<IEnumerable<TrainingSession>> GetTrainingSessionsAsync(int topN = 5);
        Task<string> GenerateTrainingSessionsSummaryAsync(int topN = 5);
        string GenerateTrainingSessionsSummary(IEnumerable<TrainingSession> sessions);

        // AUDIT LOGS
        Task<IEnumerable<AuditLogItems>> GetAuditLogsAsync(int topN = 0, DateTime? selectedDate = null, string? position = null);
        Task<IEnumerable<DateTime>> GetAuditLogDatesAsync();
        Task<int> GetAuditLogCountByDateAsync(DateTime date);
        Task<string> GenerateAuditLogsSummaryAsync();
    }



}

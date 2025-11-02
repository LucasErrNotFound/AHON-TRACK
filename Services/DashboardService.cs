using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Avalonia.Media.Imaging;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AHON_TRACK.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly string _connectionString;

        public DashboardService(string connectionString)
        {
            _connectionString = connectionString
                ?? throw new ArgumentNullException(nameof(connectionString));
        }

        #region DASHBOARD SUMMARY CARDS

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(DateTime fromDate, DateTime toDate)
        {
            var summary = new DashboardSummaryDto();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                // Total Revenue for current period
                string revenueQuery = @"
    SELECT ISNULL(SUM(Amount), 0) AS TotalRevenue
    FROM Sales
    WHERE IsDeleted = 0 
      AND CAST(SaleDate AS DATE) BETWEEN @From AND @To";

                using (var cmd = new SqlCommand(revenueQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDate);
                    cmd.Parameters.AddWithValue("@To", toDate);
                    summary.TotalRevenue = Convert.ToDouble(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
                }

                // Member Subscriptions (new members in period)
                string subscriptionsQuery = @"
    SELECT COUNT(*)
    FROM Members
    WHERE IsDeleted = 0
      AND CAST(DateJoined AS DATE) BETWEEN @From AND @To";

                using (var cmd = new SqlCommand(subscriptionsQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDate);
                    cmd.Parameters.AddWithValue("@To", toDate);
                    summary.MemberSubscriptions = Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
                }

                // Sales Count
                string salesQuery = @"
    SELECT COUNT(DISTINCT SaleID)
    FROM Sales
    WHERE IsDeleted = 0 
      AND CAST(SaleDate AS DATE) BETWEEN @From AND @To";

                using (var cmd = new SqlCommand(salesQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDate);
                    cmd.Parameters.AddWithValue("@To", toDate);
                    summary.SalesCount = Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
                }

                // Active Now - Count currently checked in (CheckIn NOT NULL and CheckOut IS NULL)
                string activeQuery = @"
    SELECT 
        (SELECT COUNT(DISTINCT MemberID)
         FROM MemberCheckIns
         WHERE IsDeleted = 0
           AND CheckIn IS NOT NULL
           AND CheckOut IS NULL)
        +
        (SELECT COUNT(DISTINCT CustomerID)
         FROM WalkInRecords
         WHERE IsDeleted = 0
           AND CheckIn IS NOT NULL
           AND CheckOut IS NULL)";

                using (var cmd = new SqlCommand(activeQuery, conn))
                {
                    summary.ActiveNow = Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetDashboardSummaryAsync] Error: {ex.Message}");
            }

            return summary;
        }

        /// <summary>
        /// Calculates growth percentage between two periods
        /// </summary>
        public async Task<DashboardGrowthDto> GetDashboardGrowthAsync(DateTime currentFrom, DateTime currentTo)
        {
            var growth = new DashboardGrowthDto();

            try
            {
                // Calculate previous period (same duration, shifted back)
                var duration = (currentTo - currentFrom).Days;
                var previousFrom = currentFrom.AddDays(-duration - 1);
                var previousTo = currentFrom.AddDays(-1);

                var currentSummary = await GetDashboardSummaryAsync(currentFrom, currentTo).ConfigureAwait(false);
                var previousSummary = await GetDashboardSummaryAsync(previousFrom, previousTo).ConfigureAwait(false);

                // Calculate growth percentages
                growth.RevenueGrowth = CalculateGrowthPercentage(
                    currentSummary.TotalRevenue,
                    previousSummary.TotalRevenue);

                growth.SubscriptionsGrowth = CalculateGrowthPercentage(
                    currentSummary.MemberSubscriptions,
                    previousSummary.MemberSubscriptions);

                growth.SalesGrowth = CalculateGrowthPercentage(
                    currentSummary.SalesCount,
                    previousSummary.SalesCount);

                growth.ActiveNowGrowth = CalculateGrowthPercentage(
                    currentSummary.ActiveNow,
                    previousSummary.ActiveNow);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetDashboardGrowthAsync] Error: {ex.Message}");
            }

            return growth;
        }

        private static double CalculateGrowthPercentage(double current, double previous)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            return Math.Round(((current - previous) / previous) * 100, 2);
        }

        /// <summary>
        /// Gets current active count and hourly growth percentage
        /// Compares current active users with those active 1 hour ago
        /// </summary>
        public async Task<(int CurrentActive, double GrowthPercent)> GetActiveNowWithGrowthAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                // Current active - Currently checked in (CheckOut IS NULL)
                string currentQuery = @"
    SELECT 
        (SELECT COUNT(DISTINCT MemberID)
         FROM MemberCheckIns
         WHERE IsDeleted = 0
           AND CheckIn IS NOT NULL
           AND CheckOut IS NULL)
        +
        (SELECT COUNT(DISTINCT CustomerID)
         FROM WalkInRecords
         WHERE IsDeleted = 0
           AND CheckIn IS NOT NULL
           AND CheckOut IS NULL)";

                int currentActive;
                using (var cmd = new SqlCommand(currentQuery, conn))
                {
                    currentActive = Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
                }

                // Active count from 1 hour ago
                // Counts people who: checked in before/at 1hr ago AND (haven't checked out OR checked out after 1hr ago)
                string oneHourAgoQuery = @"
    SELECT 
        (SELECT COUNT(DISTINCT MemberID)
         FROM MemberCheckIns
         WHERE IsDeleted = 0
           AND CheckIn <= DATEADD(hour, -1, GETDATE())
           AND (CheckOut IS NULL OR CheckOut >= DATEADD(hour, -1, GETDATE())))
        +
        (SELECT COUNT(DISTINCT CustomerID)
         FROM WalkInRecords
         WHERE IsDeleted = 0
           AND CheckIn <= DATEADD(hour, -1, GETDATE())
           AND (CheckOut IS NULL OR CheckOut >= DATEADD(hour, -1, GETDATE())))";

                int oneHourAgoActive;
                using (var cmd = new SqlCommand(oneHourAgoQuery, conn))
                {
                    oneHourAgoActive = Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
                }

                // Calculate hourly growth percentage
                double growth = oneHourAgoActive == 0
                    ? (currentActive > 0 ? 100 : 0)
                    : Math.Round(((double)(currentActive - oneHourAgoActive) / oneHourAgoActive) * 100, 0);

                return (currentActive, growth);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetActiveNowWithGrowthAsync] Error: {ex.Message}");
                return (0, 0);
            }
        }

        #endregion

        #region SUPPORTING CLASS
        public class DashboardSummaryDto
        {
            public double TotalRevenue { get; set; }
            public int MemberSubscriptions { get; set; }
            public int SalesCount { get; set; }
            public int ActiveNow { get; set; }
        }

        public class DashboardGrowthDto
        {
            public double RevenueGrowth { get; set; }
            public double SubscriptionsGrowth { get; set; }
            public double SalesGrowth { get; set; }
            public double ActiveNowGrowth { get; set; }
        }
        #endregion

        #region SALES

        public async Task<IEnumerable<SalesItem>> GetSalesAsync(int topN = 5)
        {
            var sales = new List<SalesItem>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                string query = @"
            SELECT TOP (@TopN)
                CASE 
                    WHEN s.MemberID IS NOT NULL THEN CONCAT(m.FirstName, ' ', m.LastName)
                    WHEN s.CustomerID IS NOT NULL THEN CONCAT(wc.FirstName, ' ', wc.LastName)
                    ELSE 'Unknown Customer'
                END AS CustomerName,
                CASE 
                    WHEN s.ProductID IS NOT NULL THEN p.ProductName
                    WHEN s.PackageID IS NOT NULL THEN pkg.PackageName
                    ELSE 'Unknown Item'
                END AS ProductName,
                s.Amount,
                s.SaleDate,
                m.ProfilePicture,
                CASE 
                    WHEN s.MemberID IS NOT NULL THEN 'Gym Member'
                    WHEN s.CustomerID IS NOT NULL THEN 'Walk-in'
                    ELSE 'Unknown'
                END AS CustomerType
            FROM Sales s
            LEFT JOIN Members m ON s.MemberID = m.MemberID
            LEFT JOIN WalkInCustomers wc ON s.CustomerID = wc.CustomerID
            LEFT JOIN Products p ON s.ProductID = p.ProductID
            LEFT JOIN Packages pkg ON s.PackageID = pkg.PackageID
            ORDER BY s.SaleDate DESC;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@TopN", topN);

                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    Bitmap avatar;
                    string customerType = reader["CustomerType"]?.ToString() ?? "Unknown";

                    // Get profile picture from Members or use default for WalkIns
                    if (customerType == "Gym Member" && reader["ProfilePicture"] != DBNull.Value)
                    {
                        try
                        {
                            var bytes = (byte[])reader["ProfilePicture"];
                            avatar = ImageHelper.BytesToBitmap(bytes);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to convert profile picture: {ex.Message}");
                            avatar = ImageHelper.GetDefaultAvatarSafe() ?? ImageHelper.CreateFallbackBitmap();
                        }
                    }
                    else
                    {
                        // WalkIn customers or Members without profile picture get default avatar
                        avatar = ImageHelper.GetDefaultAvatarSafe() ?? ImageHelper.CreateFallbackBitmap();
                    }

                    sales.Add(new SalesItem
                    {
                        CustomerName = reader["CustomerName"]?.ToString() ?? "Unknown",
                        ProductName = reader["ProductName"]?.ToString() ?? "Unknown",
                        Amount = reader["Amount"] == DBNull.Value ? 0 : (decimal)reader["Amount"],
                        CustomerType = customerType,
                        AvatarSource = avatar
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetSalesAsync] Error: {ex.Message}");
            }

            return sales;
        }

        public async Task<string> GenerateSalesSummaryAsync(int topN = 5)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                // Get current month's total sales
                string query = @"
            SELECT 
                COUNT(*) AS OrderCount,
                COALESCE(SUM(Amount), 0) AS TotalAmount
            FROM Sales
            WHERE MONTH(SaleDate) = MONTH(GETDATE())
              AND YEAR(SaleDate) = YEAR(GETDATE());";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                if (await reader.ReadAsync().ConfigureAwait(false))
                {
                    int count = Convert.ToInt32(reader["OrderCount"]);
                    decimal total = Convert.ToDecimal(reader["TotalAmount"]);
                    return $"You made {count} sales this month totaling ₱{total:N2}";
                }

                return "No sales data available";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GenerateSalesSummaryAsync] Error: {ex.Message}");
                return "Sales data unavailable";
            }
        }

        public async Task<IEnumerable<int>> GetAvailableYearsAsync()
        {
            var years = new List<int>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                string query = @"
            SELECT DISTINCT YEAR(SaleDate) AS Year 
            FROM Sales 
            WHERE SaleDate IS NOT NULL
            ORDER BY Year DESC;";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    years.Add(Convert.ToInt32(reader["Year"]));
                }

                // If no years found, add current year
                if (years.Count == 0)
                {
                    years.Add(DateTime.Now.Year);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAvailableYearsAsync] Error: {ex.Message}");
                // Fallback to current year
                years.Add(DateTime.Now.Year);
            }

            return years;
        }

        public async Task<int[]> GetSalesDataForYearAsync(int year)
        {
            var salesData = new int[12]; // 12 months

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                string query = @"
            SELECT 
                MONTH(SaleDate) AS Month, 
                COALESCE(SUM(Amount), 0) AS TotalSales
            FROM Sales
            WHERE YEAR(SaleDate) = @Year
            GROUP BY MONTH(SaleDate)
            ORDER BY Month;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Year", year);

                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    int month = Convert.ToInt32(reader["Month"]); // 1-12
                    int total = reader["TotalSales"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["TotalSales"]);

                    // Store using 0-based index (month 1 = index 0)
                    salesData[month - 1] = total;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetSalesDataForYearAsync] Error: {ex.Message}");
            }

            return salesData;
        }

        #endregion

        #region TRAINING SESSIONS

        /// <summary>
        /// Gets upcoming training sessions for dashboard
        /// </summary>
        public async Task<IEnumerable<TrainingSession>> GetTrainingSessionsAsync(int topN = 5)
        {
            var sessions = new List<TrainingSession>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                string query = @"
SELECT TOP (@TopN)
    t.TrainingID,
    CONCAT(t.FirstName, ' ', t.LastName) AS ClientName,
    t.CustomerType AS MembershipType,
    t.PackageType AS TrainingType,
    t.AssignedCoach,
    t.ScheduledDate AS Date,
    FORMAT(t.ScheduledTimeStart, 'hh:mm tt') + ' - ' + FORMAT(t.ScheduledTimeEnd, 'hh:mm tt') AS TimeSlot,
    t.Attendance,
    t.ProfilePicture
FROM Trainings t
WHERE t.ScheduledDate >= CAST(GETDATE() AS DATE)
  AND (t.Attendance = 'Pending' OR t.Attendance IS NULL)
ORDER BY t.ScheduledDate ASC, t.ScheduledTimeStart ASC;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@TopN", topN);

                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    // Default avatar
                    string avatarSource = "avares://AHON_TRACK/Assets/MainWindowView/user.png";

                    sessions.Add(new TrainingSession
                    {
                        ClientName = reader["ClientName"]?.ToString() ?? string.Empty,
                        MembershipType = reader["MembershipType"]?.ToString() ?? "Member",
                        TrainingType = reader["TrainingType"]?.ToString() ?? string.Empty,
                        Location = reader["AssignedCoach"]?.ToString() ?? string.Empty,
                        TimeSlot = reader["TimeSlot"]?.ToString() ?? string.Empty,
                        Date = reader["Date"] == DBNull.Value ? DateTime.MinValue : (DateTime)reader["Date"],
                        AvatarSource = avatarSource
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetTrainingSessionsAsync] Error: {ex.Message}");
            }

            return sessions;
        }

        /// <summary>
        /// Generates a summary of upcoming training sessions
        /// </summary>
        public async Task<string> GenerateTrainingSessionsSummaryAsync(int topN = 5)
        {
            try
            {
                // Get the actual count of upcoming sessions
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                string countQuery = @"
SELECT COUNT(*) 
FROM Trainings 
WHERE ScheduledDate >= CAST(GETDATE() AS DATE)
  AND (Attendance = 'Pending' OR Attendance IS NULL);";

                using var cmd = new SqlCommand(countQuery, conn);
                var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                int count = Convert.ToInt32(result);

                return count switch
                {
                    0 => "You have 0 upcoming training schedules this week",
                    1 => "You have 1 upcoming training schedule",
                    _ => $"You have {count} upcoming training schedules"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GenerateTrainingSessionsSummaryAsync] Error: {ex.Message}");
                return "Training sessions data unavailable";
            }
        }

        /// <summary>
        /// Generates a summary from existing training sessions collection
        /// </summary>
        public string GenerateTrainingSessionsSummary(IEnumerable<TrainingSession> sessions)
        {
            try
            {
                int count = sessions?.Count() ?? 0;

                return count switch
                {
                    0 => "You have 0 upcoming training schedules this week",
                    1 => "You have 1 upcoming training schedule",
                    _ => $"You have {count} upcoming training schedules"
                };
            }
            catch
            {
                return "Training sessions summary unavailable";
            }
        }

        #endregion

        #region RECENT LOGS

        public async Task<IEnumerable<RecentLog>> GetRecentLogsAsync(int topN = 5)
        {
            var logs = new List<RecentLog>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                string query = @"
SELECT TOP (@TopN)
    sl.Username,
    sl.Role AS UserType,
    sl.ActionDescription AS ActionLogName,
    sl.LogDateTime,
    e.ProfilePicture
FROM SystemLogs sl
LEFT JOIN Employees e ON sl.PerformedByEmployeeID = e.EmployeeID
ORDER BY sl.LogDateTime DESC;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@TopN", topN);

                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    DateTime logDateTime = (DateTime)reader["LogDateTime"];
                    string formattedDate = logDateTime.ToString("MM/dd/yyyy hh:mm tt");

                    Bitmap avatar;

                    if (reader["ProfilePicture"] != DBNull.Value)
                    {
                        try
                        {
                            var bytes = (byte[])reader["ProfilePicture"];
                            avatar = ImageHelper.BytesToBitmap(bytes);
                        }
                        catch
                        {
                            avatar = ImageHelper.GetDefaultAvatarSafe() ?? ImageHelper.CreateFallbackBitmap();
                        }
                    }
                    else
                    {
                        avatar = ImageHelper.GetDefaultAvatarSafe() ?? ImageHelper.CreateFallbackBitmap();
                    }

                    logs.Add(new RecentLog
                    {
                        Username = reader["Username"].ToString(),
                        UserType = reader["UserType"].ToString(),
                        ActionLogName = reader["ActionLogName"].ToString(),
                        LogDateTime = logDateTime,
                        FormattedDateTime = formattedDate,
                        AvatarSource = avatar
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetRecentLogsAsync] Error: {ex.Message}");
            }

            return logs;
        }

        public async Task<string> GenerateRecentLogSummaryAsync(int topN = 5)
        {
            try
            {
                var logs = await GetRecentLogsAsync(topN).ConfigureAwait(false);
                return $"You have {logs.Count()} recent action logs today";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GenerateRecentLogSummaryAsync] Error: {ex.Message}");
                return "Logs unavailable";
            }
        }

        #endregion

        // Add these methods to your DashboardService class (in the #region Audit Logs section)
        // Make sure to add this using statement at the top of your DashboardService.cs file:
        // using AHON_TRACK.ViewModels;

        #region AUDIT LOGS

        /// <summary>
        /// Gets audit logs with optional filtering
        /// </summary>
        /// <param name="topN">Number of logs to retrieve (0 for all)</param>
        /// <param name="selectedDate">Optional date filter</param>
        /// <param name="position">Optional position filter (Admin, Staff, or null for all)</param>
        /// <returns>Collection of audit logs as AuditLogItems</returns>
        public async Task<IEnumerable<AuditLogItems>> GetAuditLogsAsync(
    int topN = 0,
    DateTime? selectedDate = null,
    string? position = null)
        {
            var logs = new List<AuditLogItems>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                var queryBuilder = new System.Text.StringBuilder(@"
            SELECT ");

                if (topN > 0)
                    queryBuilder.Append("TOP (@TopN) ");

                queryBuilder.Append(@"
            ID,
            ProfilePicture,
            Name,
            Username,
            Position,
            DateAndTime,
            Action,
            Role,
            ActionType,
            IsSuccessful
            FROM vw_AuditLogs
            WHERE 1=1");

                if (selectedDate.HasValue)
                    queryBuilder.Append(" AND CAST(DateAndTime AS DATE) = CAST(@SelectedDate AS DATE)");

                if (!string.IsNullOrEmpty(position) && position != "All")
                    queryBuilder.Append(" AND Position = @Position");

                queryBuilder.Append(" ORDER BY DateAndTime DESC;");

                using var cmd = new SqlCommand(queryBuilder.ToString(), conn);

                if (topN > 0)
                    cmd.Parameters.AddWithValue("@TopN", topN);
                if (selectedDate.HasValue)
                    cmd.Parameters.AddWithValue("@SelectedDate", selectedDate.Value.Date);
                if (!string.IsNullOrEmpty(position) && position != "All")
                    cmd.Parameters.AddWithValue("@Position", position);

                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    Bitmap? profilePicture = null;

                    if (reader["ProfilePicture"] != DBNull.Value)
                    {
                        try
                        {
                            var bytes = (byte[])reader["ProfilePicture"];
                            profilePicture = ImageHelper.BytesToBitmap(bytes);
                        }
                        catch
                        {
                            profilePicture = ImageHelper.GetDefaultAvatarSafe() ?? ImageHelper.CreateFallbackBitmap();
                        }
                    }
                    else
                    {
                        profilePicture = ImageHelper.GetDefaultAvatarSafe() ?? ImageHelper.CreateFallbackBitmap();
                    }

                    logs.Add(new AuditLogItems
                    {
                        ID = reader["ID"]?.ToString() ?? "0",
                        AvatarSource = profilePicture,
                        Name = reader["Name"]?.ToString() ?? "Unknown",
                        Username = reader["Username"]?.ToString() ?? "Unknown",
                        Position = reader["Position"]?.ToString() ?? "Unknown",
                        DateAndTime = reader["DateAndTime"] == DBNull.Value
                            ? DateTime.MinValue
                            : (DateTime)reader["DateAndTime"],
                        Action = reader["Action"]?.ToString() ?? "No action recorded"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAuditLogsAsync] Error: {ex.Message}");
            }

            return logs;
        }

        /// <summary>
        /// Gets all available dates that have audit log entries
        /// </summary>
        public async Task<IEnumerable<DateTime>> GetAuditLogDatesAsync()
        {
            var dates = new List<DateTime>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                string query = @"
            SELECT DISTINCT CAST(LogDateTime AS DATE) AS LogDate
            FROM SystemLogs
            WHERE LogDateTime IS NOT NULL
            ORDER BY LogDate DESC;";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    dates.Add((DateTime)reader["LogDate"]);
                }

                // If no dates found, add today
                if (dates.Count == 0)
                {
                    dates.Add(DateTime.Today);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAuditLogDatesAsync] Error: {ex.Message}");
                dates.Add(DateTime.Today);
            }

            return dates;
        }

        /// <summary>
        /// Gets count of audit logs for a specific date
        /// </summary>
        public async Task<int> GetAuditLogCountByDateAsync(DateTime date)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                string query = @"
            SELECT COUNT(*) 
            FROM SystemLogs
            WHERE CAST(LogDateTime AS DATE) = CAST(@Date AS DATE);";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Date", date.Date);

                var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAuditLogCountByDateAsync] Error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Gets audit logs summary for display
        /// </summary>
        public async Task<string> GenerateAuditLogsSummaryAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                string query = @"
            SELECT COUNT(*) AS TodayCount
            FROM SystemLogs
            WHERE CAST(LogDateTime AS DATE) = CAST(GETDATE() AS DATE);";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                if (await reader.ReadAsync().ConfigureAwait(false))
                {
                    int count = Convert.ToInt32(reader["TodayCount"]);
                    return $"You have {count} audit log{(count != 1 ? "s" : "")} today";
                }

                return "No audit logs today";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GenerateAuditLogsSummaryAsync] Error: {ex.Message}");
                return "Audit logs unavailable";
            }
        }

        #endregion
    }
}
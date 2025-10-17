﻿using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Avalonia.Media.Imaging;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
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

        #region SALES

        public async Task<IEnumerable<SalesItem>> GetSalesAsync(int topN = 5)
        {
            var sales = new List<SalesItem>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

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

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
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
                await conn.OpenAsync();

                // Get current month's total sales
                string query = @"
            SELECT 
                COUNT(*) AS OrderCount,
                COALESCE(SUM(Amount), 0) AS TotalAmount
            FROM Sales
            WHERE MONTH(SaleDate) = MONTH(GETDATE())
              AND YEAR(SaleDate) = YEAR(GETDATE());";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
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
                await conn.OpenAsync();

                string query = @"
            SELECT DISTINCT YEAR(SaleDate) AS Year 
            FROM Sales 
            WHERE SaleDate IS NOT NULL
            ORDER BY Year DESC;";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
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
                await conn.OpenAsync();

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

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
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

        public async Task<IEnumerable<TrainingSession>> GetTrainingSessionsAsync(int topN = 5)
        {
            var sessions = new List<TrainingSession>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT TOP (@TopN)
                        CONCAT(c.FirstName, ' ', c.LastName) AS ClientName,
                        c.MembershipType,
                        t.TrainingType,
                        t.Location,
                        t.TimeSlot,
                        t.Date
                    FROM TrainingSessions t
                    INNER JOIN Clients c ON t.ClientId = c.ClientId
                    ORDER BY t.Date DESC;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@TopN", topN);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    sessions.Add(new TrainingSession
                    {
                        ClientName = reader["ClientName"]?.ToString() ?? string.Empty,
                        MembershipType = reader["MembershipType"]?.ToString() ?? "Gym Member",
                        TrainingType = reader["TrainingType"]?.ToString() ?? string.Empty,
                        Location = reader["Location"]?.ToString() ?? string.Empty,
                        TimeSlot = reader["TimeSlot"]?.ToString() ?? string.Empty,
                        Date = reader["Date"] == DBNull.Value ? DateTime.MinValue : (DateTime)reader["Date"],
                        AvatarSource = "avares://AHON_TRACK/Assets/MainWindowView/user.png"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetTrainingSessionsAsync] Error: {ex.Message}");
            }

            return sessions;
        }

        public async Task<string> GenerateTrainingSessionsSummaryAsync(int topN = 5)
        {
            try
            {
                var sessions = await GetTrainingSessionsAsync(topN);
                int count = sessions.Count();
                return $"Upcoming Training Sessions: {count}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GenerateTrainingSessionsSummaryAsync] Error: {ex.Message}");
                return "Training sessions data unavailable";
            }
        }

        public string GenerateTrainingSessionsSummary(IEnumerable<TrainingSession> sessions)
        {
            try
            {
                int count = sessions.Count();
                return $"Upcoming Training Sessions: {count}";
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
                await conn.OpenAsync();

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

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
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
                var logs = await GetRecentLogsAsync(topN);
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
                await conn.OpenAsync();

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

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
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
                await conn.OpenAsync();

                string query = @"
            SELECT DISTINCT CAST(LogDateTime AS DATE) AS LogDate
            FROM SystemLogs
            WHERE LogDateTime IS NOT NULL
            ORDER BY LogDate DESC;";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
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
                await conn.OpenAsync();

                string query = @"
            SELECT COUNT(*) 
            FROM SystemLogs
            WHERE CAST(LogDateTime AS DATE) = CAST(@Date AS DATE);";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Date", date.Date);

                var result = await cmd.ExecuteScalarAsync();
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
                await conn.OpenAsync();

                string query = @"
            SELECT COUNT(*) AS TodayCount
            FROM SystemLogs
            WHERE CAST(LogDateTime AS DATE) = CAST(GETDATE() AS DATE);";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
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
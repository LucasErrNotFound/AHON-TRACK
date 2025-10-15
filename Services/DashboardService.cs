using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
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
                    SELECT TOP (@TopN) CustomerName, ProductName, Amount
                    FROM Sales
                    ORDER BY SaleDate DESC;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@TopN", topN);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    sales.Add(new SalesItem
                    {
                        CustomerName = reader["CustomerName"]?.ToString() ?? string.Empty,
                        ProductName = reader["ProductName"]?.ToString() ?? string.Empty,
                        Amount = reader["Amount"] == DBNull.Value ? 0 : (decimal)reader["Amount"]
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
                var sales = await GetSalesAsync(topN);
                int count = sales.Count();
                decimal total = sales.Sum(s => s.Amount);

                return $"Total Sales: {count} orders, ₱{total:N2}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GenerateSalesSummaryAsync] Error: {ex.Message}");
                return "Sales data unavailable";
            }
        }

        public string GenerateSalesSummary(IEnumerable<SalesItem> sales)
        {
            try
            {
                int count = sales.Count();
                decimal total = sales.Sum(s => s.Amount);
                return $"Total Sales: {count} orders, ₱{total:N2}";
            }
            catch
            {
                return "Sales summary unavailable";
            }
        }

        public async Task<IEnumerable<int>> GetAvailableYearsAsync()
        {
            var years = new List<int>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = "SELECT DISTINCT YEAR(SaleDate) AS Year FROM Sales ORDER BY Year DESC;";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    years.Add(Convert.ToInt32(reader["Year"]));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAvailableYearsAsync] Error: {ex.Message}");
            }

            return years;
        }

        public async Task<int[]> GetSalesDataForYearAsync(int year)
        {
            var salesData = new int[12];

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT MONTH(SaleDate) AS Month, SUM(Amount) AS TotalSales
                    FROM Sales
                    WHERE YEAR(SaleDate) = @Year
                    GROUP BY MONTH(SaleDate)
                    ORDER BY Month;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Year", year);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int month = Convert.ToInt32(reader["Month"]);
                    salesData[month - 1] = reader["TotalSales"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["TotalSales"]);
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
    }
}
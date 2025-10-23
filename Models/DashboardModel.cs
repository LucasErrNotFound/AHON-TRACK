using AHON_TRACK.Converters;
using Avalonia.Media.Imaging;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AHON_TRACK.Models;

// Model for individual sales items
public class SalesItem
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerType { get; set; } = "Gym Member";
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; init; }
    public Bitmap? AvatarSource { get; set; }

    // Formatted currency for display
    public string FormattedAmount => $"+₱{Amount:F2}";
}

// Model for training sessions
public class TrainingSession
{
    public string ClientName { get; set; } = string.Empty;
    public string MembershipType { get; set; } = "Gym Member";
    public string TrainingType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string TimeSlot { get; set; } = string.Empty;
    public string AvatarSource { get; set; } = "avares://AHON_TRACK/Assets/MainWindowView/user.png";
    public DateTime Date { get; init; }

    // Formatted date for display
    public string FormattedDate => Date.ToString("MMM dd, yyyy");
}

// Model for Recent Logs
public class RecentLog
{
    public string Username { get; set; }
    public string UserType { get; set; }
    public string ActionLogName { get; set; }
    public DateTime LogDateTime { get; set; }

    // New property for display
    public string FormattedDateTime { get; set; }
    public Bitmap AvatarSource { get; set; } = ImageHelper.GetDefaultAvatar();
}

public class Notification
{
    public NotificationType Type { get; set; } = NotificationType.Info;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime DateAndTime { get; set; } = DateTime.Now;
    
    // Formatted date for display
    public string FormattedDateTime => DateAndTime.ToString("dd MMM yyyy 'at' h:mm tt");
    
    // Badge text based on type
    public string BadgeText => Type.ToString();
    
    // Badge color resource key based on type
    public string BadgeColor => Type switch
    {
        NotificationType.Success => "#FF2E7D32",
        NotificationType.Info => "#FF0288D1",
        NotificationType.Warning => "#FFED6C02",
        NotificationType.Error => "#FFE7000B",
        _ => "#FF2979FF"
    };
}

public enum NotificationType
{
    Success,
    Info,
    Warning,
    Error
}

// Main Dashboard Model - handles all data operations
public class DashboardModel
{
    // Constants for year range || JUST A PROTOTYPE CHILL OUT || I did this so that we can see the year items on the drop-menu
    private const int StartYear = 2025;
    private const int YearsToShow = 10;

    // Dictionary to store data for different years
    private readonly Dictionary<int, int[]> _yearlyData = new();

    public DashboardModel()
    {
        InitializeYearlyData();
    }

    #region Sales Data Operations

    public List<SalesItem> GetSampleSalesData()
    {
        return
        [
            new SalesItem { CustomerName = "Jedd Calubayan", ProductName = "Red Horse Mucho", Amount = 300.00m },
            new SalesItem { CustomerName = "Sianrey Flora", ProductName = "Membership Renewal", Amount = 500.00m },
            new SalesItem { CustomerName = "JC Casidor", ProductName = "Protein Milk Shake", Amount = 35.00m },
            new SalesItem { CustomerName = "Mardie Dela Cruz", ProductName = "AHON T-Shirt", Amount = 135.00m },
            new SalesItem { CustomerName = "JL Taberdo", ProductName = "Lifting Straps", Amount = 360.00m },
            new SalesItem { CustomerName = "Jav Agustin", ProductName = "AHON Tumbler", Amount = 235.00m },
            new SalesItem { CustomerName = "Marc Torres", ProductName = "Gym Membership", Amount = 499.00m },
            new SalesItem { CustomerName = "Maverick Lim", ProductName = "Cobra Berry", Amount = 40.00m }
        ];
    }

    public async Task<List<SalesItem>> GetSalesFromDatabaseAsync()
    {
        // Replace this with your actual SQL database call
        // Example:
        // using var connection = new SqlConnection(connectionString);
        // var sales = await connection.QueryAsync<SalesItem>("SELECT * FROM Sales ORDER BY CreatedDate DESC");
        // return sales.ToList();

        await Task.Delay(100); // Simulate async operation
        return [];
    }

    public string GenerateSalesSummary(int salesCount)
    {
        return $"You made {salesCount} sales this month.";
    }

    #endregion

    #region Training Sessions Data Operations

    public List<TrainingSession> GetSampleTrainingSessionsData()
    {
        return
        [
            new TrainingSession
            {
                ClientName = "Rome Jedd Calubayan",
                MembershipType = "Gym Member",
                TrainingType = "Boxing",
                Location = "Boxing Ring",
                TimeSlot = "8:00AM - 9:00AM",
                Date = new DateTime(2025, 6, 16)
            },

            new TrainingSession
            {
                ClientName = "JC Casidore",
                MembershipType = "Gym Member",
                TrainingType = "Boxing",
                Location = "Boxing Ring",
                TimeSlot = "8:00AM - 9:00AM",
                Date = new DateTime(2025, 6, 16)
            },

            new TrainingSession
            {
                ClientName = "Mardie Dela Cruz",
                MembershipType = "Walk-in",
                TrainingType = "Muay Thai",
                Location = "Gym Mat",
                TimeSlot = "1:00PM - 2:00PM",
                Date = new DateTime(2025, 6, 17)
            },

            new TrainingSession
            {
                ClientName = "JL Taberdo",
                MembershipType = "Gym Member",
                TrainingType = "Crossfit Training",
                Location = "Gym Mat",
                TimeSlot = "4:00PM - 5:00PM",
                Date = new DateTime(2025, 6, 19)
            },

            new TrainingSession
            {
                ClientName = "Jav Agustin",
                MembershipType = "Walk-in",
                TrainingType = "Muay Thai",
                Location = "Gym Mat",
                TimeSlot = "1:00PM - 2:00PM",
                Date = new DateTime(2025, 6, 21)
            },

            new TrainingSession
            {
                ClientName = "Sianrey Flora",
                MembershipType = "Gym Member",
                TrainingType = "Boxing",
                Location = "Boxing Ring",
                TimeSlot = "2:00PM - 5:00PM",
                Date = new DateTime(2025, 6, 26)
            },

            new TrainingSession
            {
                ClientName = "Marc Torres",
                MembershipType = "Gym Member",
                TrainingType = "Boxing",
                Location = "Boxing Ring",
                TimeSlot = "3:00PM - 6:00PM",
                Date = new DateTime(2025, 6, 27)
            },

            new TrainingSession
            {
                ClientName = "Maverick Lim",
                MembershipType = "Gym Member",
                TrainingType = "CrossFit Training",
                Location = "Gym Mat",
                TimeSlot = "11:00AM - 12:00PM",
                Date = new DateTime(2025, 6, 28)
            },

            new TrainingSession
            {
                ClientName = "Dave Dapitillo",
                MembershipType = "Walk-in",
                TrainingType = "Muay Thai",
                Location = "Gym Mat",
                TimeSlot = "12:00PM - 2:00PM",
                Date = new DateTime(2025, 6, 28)
            }

        ];
    }

    public async Task<List<TrainingSession>> GetTrainingSessionsFromDatabaseAsync()
    {
        // Replace this with your actual SQL database call
        // Example:
        // using var connection = new SqlConnection(connectionString);
        // var sessions = await connection.QueryAsync<TrainingSession>(
        //     "SELECT * FROM TrainingSessions WHERE Date >= @StartDate ORDER BY Date, TimeSlot", 
        //     new { StartDate = DateTime.Today });
        // return sessions.ToList();

        await Task.Delay(100); // Simulate async operation
        return [];
    }

    public string GenerateTrainingSessionsSummary(int sessionCount)
    {
        return $"You have {sessionCount} upcoming training schedules this week";
    }

    public int FindInsertionIndex(List<TrainingSession> sessions, TrainingSession newSession)
    {
        for (var i = 0; i < sessions.Count; i++)
        {
            if (sessions[i].Date > newSession.Date)
            {
                return i;
            }
        }
        return sessions.Count;
    }

    #endregion

    #region Recent Logs Data Operations
    public const string connectionString = "Data Source=LAPTOP-SSMJIDM6\\SQLEXPRESS08;Initial Catalog=AHON_TRACK;Integrated Security=True;Encrypt=True;Trust Server Certificate=True";

    public async Task<List<RecentLog>> GetRecentLogsFromDatabaseAsync(string connectionString)
    {
        var logs = new List<RecentLog>();

        try
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            string query = @"
        SELECT TOP 10 
        Username, 
        Role AS UserType, 
        ActionType AS ActionLogName, 
        LogDateTime
        FROM SystemLogs
        ORDER BY LogDateTime DESC;";

            using var cmd = new SqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                DateTime logDateTime = (DateTime)reader["LogDateTime"];
                string formattedDate = logDateTime.ToString("MM/dd/yyyy hh:mm tt"); // 12H format

                logs.Add(new RecentLog
                {
                    Username = reader["Username"].ToString(),
                    UserType = reader["UserType"].ToString(),           // comes from SQL alias
                    ActionLogName = reader["ActionLogName"].ToString(), // comes from SQL alias
                    LogDateTime = logDateTime,
                    FormattedDateTime = formattedDate,
                    AvatarSource = ImageHelper.GetDefaultAvatar()
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching logs: {ex.Message}");
        }

        return logs;
    }

    public string GenerateRecentLogsSummary(int logCount) => $"You have {logCount} recent action logs today";

    #endregion

    #region Chart Data Operations

    public List<int> GetAvailableYears()
    {
        var currentYear = DateTime.Now.Year;
        var endYear = Math.Max(currentYear + 5, StartYear + YearsToShow);

        var years = new List<int>();
        for (var year = StartYear; year <= endYear; year++)
        {
            years.Add(year);
        }

        return years;
    }

    public int[] GetDataForYear(int year)
    {
        return _yearlyData.TryGetValue(year, out var data) ? data : new int[12];
    }

    public void AddYearData(int year)
    {
        if (!_yearlyData.ContainsKey(year))
        {
            _yearlyData[year] = GenerateSampleDataForYear(year);
        }
    }

    private void InitializeYearlyData()
    {
        var years = GetAvailableYears();
        foreach (var year in years)
        {
            _yearlyData[year] = GenerateSampleDataForYear(year);
        }
    }

    private int[] GenerateSampleDataForYear(int year)
    {
        // Generate sample data based on the year
        // Replace this with actual data loading logic
        var baseValues = new[] { 12000, 15125, 14200, 20000, 14000, 13500, 15000, 19000, 10000, 19930, 13000, 26000 };
        var yearMultiplier = 1 + (year - StartYear) * 0.1; // 10% increase per year

        return [.. baseValues.Select(value => (int)(value * yearMultiplier))];
    }
    #endregion
}

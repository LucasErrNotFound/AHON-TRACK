using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    // Model for individual sales items
    public class SalesItem
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerType { get; set; } = "Gym Member";
        public string ProductName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string AvatarSource { get; set; } = "avares://AHON_TRACK/Assets/MainWindowView/user.png";

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
        public DateTime Date { get; set; }

        // Formatted date for display
        public string FormattedDate => Date.ToString("MMM dd, yyyy");
    }

    // Model for Recent Logs
    public class RecentLog
    {
        public string Username { get; set; } = string.Empty;
        public string UserType { get; set; } = "Gym Admin";
        public string ActionLogName { get; set; } = string.Empty;
        public string AvatarSource { get; set; } = "avares://AHON_TRACK/Assets/MainWindowView/user.png";
        public DateTime LogDateTime { get; set; }
    }

    // Main Dashboard Model - handles all data operations
    public class DashboardModel
    {
        // Constants for year range || JUST A PROTOTYPE CHILL OUT || I did this so that we can see the year items on the dropmenu
        private const int START_YEAR = 2025;
        private const int YEARS_TO_SHOW = 10;

        // Dictionary to store data for different years
        private readonly Dictionary<int, int[]> _yearlyData = new();

        public DashboardModel()
        {
            InitializeYearlyData();
        }

        #region Sales Data Operations

        public List<SalesItem> GetSampleSalesData()
        {
            return new List<SalesItem>
            {
                new SalesItem { CustomerName = "Jedd Calubayan", ProductName = "Red Horse Mucho", Amount = 300.00m },
                new SalesItem { CustomerName = "Sianrey Flora", ProductName = "Membership Renewal", Amount = 500.00m },
                new SalesItem { CustomerName = "JC Casidor", ProductName = "Protein Milk Shake", Amount = 35.00m },
                new SalesItem { CustomerName = "Mardie Dela Cruz", ProductName = "AHON T-Shirt", Amount = 135.00m },
                new SalesItem { CustomerName = "JL Taberdo", ProductName = "Lifting Straps", Amount = 360.00m },
                new SalesItem { CustomerName = "Jav Agustin", ProductName = "AHON Tumbler", Amount = 235.00m },
                new SalesItem { CustomerName = "Marc Torres", ProductName = "Gym Membership", Amount = 499.00m },
                new SalesItem { CustomerName = "Maverick Lim", ProductName = "Cobra Berry", Amount = 40.00m }
            };
        }

        public async Task<List<SalesItem>> GetSalesFromDatabaseAsync()
        {
            // Replace this with your actual SQL database call
            // Example:
            // using var connection = new SqlConnection(connectionString);
            // var sales = await connection.QueryAsync<SalesItem>("SELECT * FROM Sales ORDER BY CreatedDate DESC");
            // return sales.ToList();

            await Task.Delay(100); // Simulate async operation
            return new List<SalesItem>();
        }

        public string GenerateSalesSummary(int salesCount)
        {
            return $"You made {salesCount} sales this month.";
        }

        #endregion

        #region Training Sessions Data Operations

        public List<TrainingSession> GetSampleTrainingSessionsData()
        {
            return new List<TrainingSession>
            {
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
                    TrainingType = "Crossift Training",
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
                },
            };
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
            return new List<TrainingSession>();
        }

        public string GenerateTrainingSessionsSummary(int sessionCount)
        {
            return $"You have {sessionCount} upcoming training schedules this week";
        }

        public int FindInsertionIndex(List<TrainingSession> sessions, TrainingSession newSession)
        {
            for (int i = 0; i < sessions.Count; i++)
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

        public List<RecentLog> GetSampleRecentLogsData()
        {
            return new List<RecentLog>
            {
                new RecentLog
                {
                    Username = "Kuya Rome",
                    UserType = "Gym Admin",
                    ActionLogName = "Printed monthly reports",
                    LogDateTime = DateTime.Now.AddMinutes(-30)
                },
                new RecentLog
                {
                    Username = "Jaycee",
                    UserType = "Gym Admin",
                    ActionLogName = "Modified rates in boxing package",
                    LogDateTime = DateTime.Now.AddHours(-1)
                },
                new RecentLog
                {
                    Username = "Figora",
                    UserType = "Gym Staff",
                    ActionLogName = "Reported a broken gym equipment",
                    LogDateTime = DateTime.Now.AddHours(-2)
                },
                new RecentLog
                {
                    Username = "JeyEL",
                    UserType = "Gym Staff",
                    ActionLogName = "Scheduled a gym equipment maintenance",
                    LogDateTime = DateTime.Now.AddHours(-3)
                },
                new RecentLog
                {
                    Username = "Mr. Javitos",
                    UserType = "Gym Admin",
                    ActionLogName = "Printed month gym member summary",
                    LogDateTime = DateTime.Now.AddHours(-4)
                }
            };
        }

        public async Task<List<RecentLog>> GetRecentLogsFromDatabaseAsync()
        {
            // Replace this with your actual SQL database call
            // Example:
            // using var connection = new SqlConnection(connectionString);
            // var logs = await connection.QueryAsync<RecentLog>(
            //     "SELECT * FROM ActivityLogs WHERE DATE(LogDateTime) = CURDATE() ORDER BY LogDateTime DESC LIMIT 10");
            // return logs.ToList();

            await Task.Delay(100); // Simulate async operation
            return new List<RecentLog>();
        }

        public string GenerateRecentLogsSummary(int logCount)
        {
            return $"You have {logCount} recent action logs today";
        }

        #endregion

        #region Chart Data Operations

        public List<int> GetAvailableYears()
        {
            var currentYear = DateTime.Now.Year;
            var endYear = Math.Max(currentYear + 5, START_YEAR + YEARS_TO_SHOW);

            var years = new List<int>();
            for (int year = START_YEAR; year <= endYear; year++)
            {
                years.Add(year);
            }

            return years;
        }

        public int[] GetDataForYear(int year)
        {
            return _yearlyData.TryGetValue(year, out int[] data) ? data : new int[12];
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
            var yearMultiplier = 1 + (year - START_YEAR) * 0.1; // 10% increase per year

            return baseValues.Select(value => (int)(value * yearMultiplier)).ToArray();
        }
        #endregion
    }
}
using AHON_TRACK.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AHON_TRACK.Services
{
    public class DataCountingService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public DataCountingService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        #region Demographics
        public async Task<(Dictionary<string, int> AgeGroups, Dictionary<string, int> GenderCounts, Dictionary<DateTime, int> PopulationCounts)>
            GetGymDemographicsAsync(DateTime from, DateTime to)
        {
            var ageGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var genderCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var populationCounts = new Dictionary<DateTime, int>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // --- AGE GROUPS (FILTERED BY DATE RANGE) ---
                string ageGroupQuery = @"
            SELECT AgeGroup, COUNT(*) AS Count
            FROM (
                SELECT 
                    CASE 
                        WHEN Age BETWEEN 18 AND 29 THEN '18-29'
                        WHEN Age BETWEEN 30 AND 39 THEN '30-39'
                        WHEN Age BETWEEN 40 AND 54 THEN '40-54'
                        WHEN Age >= 55 THEN '55+'
                        ELSE 'Unknown'
                    END AS AgeGroup
                FROM Members
                WHERE Age IS NOT NULL 
                    AND IsDeleted = 0
                    AND CAST(DateJoined AS DATE) >= @From 
                    AND CAST(DateJoined AS DATE) < @To
                UNION ALL
                SELECT 
                    CASE 
                        WHEN Age BETWEEN 18 AND 29 THEN '18-29'
                        WHEN Age BETWEEN 30 AND 39 THEN '30-39'
                        WHEN Age BETWEEN 40 AND 54 THEN '40-54'
                        WHEN Age >= 55 THEN '55+'
                        ELSE 'Unknown'
                    END AS AgeGroup
                FROM WalkInCustomers
                WHERE Age IS NOT NULL
                    AND IsDeleted = 0
                    AND CAST(DateJoined AS DATE) >= @From 
                    AND CAST(DateJoined AS DATE) < @To
            ) AS Combined
            GROUP BY AgeGroup;";

                using (var cmd = new SqlCommand(ageGroupQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", from.Date);
                    cmd.Parameters.AddWithValue("@To", to.Date);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string group = reader.GetString(0);
                            int count = reader.GetInt32(1);
                            ageGroups[group] = count;
                        }
                    }
                }

                // --- GENDER COUNTS (FILTERED BY DATE RANGE) ---
                string genderQuery = @"
            SELECT Gender, COUNT(*) AS Count
            FROM (
                SELECT Gender 
                FROM Members 
                WHERE Gender IN ('Male', 'Female') 
                    AND IsDeleted = 0
                    AND CAST(DateJoined AS DATE) >= @From 
                    AND CAST(DateJoined AS DATE) < @To
                UNION ALL
                SELECT Gender 
                FROM WalkInCustomers 
                WHERE Gender IN ('Male', 'Female') 
                    AND IsDeleted = 0
                    AND CAST(DateJoined AS DATE) >= @From 
                    AND CAST(DateJoined AS DATE) < @To
            ) AS Combined
            GROUP BY Gender;";

                using (var cmd = new SqlCommand(genderQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", from.Date);
                    cmd.Parameters.AddWithValue("@To", to.Date);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string gender = reader.GetString(0);
                            int count = reader.GetInt32(1);
                            genderCounts[gender] = count;
                        }
                    }
                }

                string populationQuery = @"
    WITH InitialPopulation AS (
        SELECT COUNT(*) AS BaseCount
        FROM Members
        WHERE CAST(DateJoined AS DATE) < @From
        AND (ValidUntil IS NULL OR CAST(ValidUntil AS DATE) >= @From)
        AND IsDeleted = 0
    ),
    DailyMemberJoins AS (
        SELECT CAST(DateJoined AS DATE) AS [Date], COUNT(*) AS Change
        FROM Members 
        WHERE CAST(DateJoined AS DATE) >= @From AND CAST(DateJoined AS DATE) < @To
        AND IsDeleted = 0
        GROUP BY CAST(DateJoined AS DATE)
    ),
    DailyMemberRemovals AS (
        SELECT CAST(ValidUntil AS DATE) AS [Date], -COUNT(*) AS Change
        FROM Members
        WHERE CAST(ValidUntil AS DATE) >= @From AND CAST(ValidUntil AS DATE) < @To
        AND Status IN ('Expired', 'Terminated')
        AND IsDeleted = 0
        GROUP BY CAST(ValidUntil AS DATE)
    ),
    DailyWalkIns AS (
        SELECT CAST(DateJoined AS DATE) AS [Date], COUNT(*) AS Change
        FROM WalkInCustomers
        WHERE CAST(DateJoined AS DATE) >= @From AND CAST(DateJoined AS DATE) < @To
        AND IsDeleted = 0
        GROUP BY CAST(DateJoined AS DATE)
    ),
    AllChanges AS (
        SELECT [Date], Change FROM DailyMemberJoins
        UNION ALL
        SELECT [Date], Change FROM DailyMemberRemovals
        UNION ALL
        SELECT [Date], Change FROM DailyWalkIns
    ),
    DailyNetChange AS (
        SELECT [Date], SUM(Change) AS NetChange
        FROM AllChanges
        GROUP BY [Date]
    )
    SELECT 
        [Date],
        (SELECT BaseCount FROM InitialPopulation) + 
        SUM(NetChange) OVER (ORDER BY [Date] ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS Population
    FROM DailyNetChange
    ORDER BY [Date];";

                using (var cmd = new SqlCommand(populationQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", from.Date); // Ensure time is 00:00:00
                    cmd.Parameters.AddWithValue("@To", to.Date);


                    _toastManager.CreateToast($"Querying from {from:yyyy-MM-dd} to {to:yyyy-MM-dd}");

                    using var reader = await cmd.ExecuteReaderAsync();
                    int rowCount = 0;
                    while (await reader.ReadAsync())
                    {
                        DateTime date = reader.GetDateTime(0);
                        int cumulativeCount = reader.GetInt32(1);
                        populationCounts[date] = cumulativeCount;
                        rowCount++;

                        _toastManager.CreateToast($"Date: {date:yyyy-MM-dd}, Population: {cumulativeCount}");
                    }

                    _toastManager.CreateToast($"Total rows returned: {rowCount}");
                }

                return (ageGroups, genderCounts, populationCounts);
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast($"Error loading demographics: {ex.Message}");
                return (ageGroups, genderCounts, populationCounts);
            }
        }

        #endregion

        #region ATTENDANCE
        public async Task<IEnumerable<AttendanceDataDto>> GetAttendanceDataAsync(DateTime fromDate, DateTime toDate)
        {
            const string query = @"
            SELECT 
                AttendanceDate,
                ISNULL(SUM(CASE WHEN AttendanceType = 'Walk-In' THEN CheckInCount ELSE 0 END), 0) AS WalkIns,
                ISNULL(SUM(CASE WHEN AttendanceType = 'Member' THEN CheckInCount ELSE 0 END), 0) AS Members,
                ISNULL(SUM(CheckInCount), 0) AS TotalAttendance
            FROM (
                SELECT 
                    CAST(DateAttendance AS DATE) AS AttendanceDate,
                    'Member' AS AttendanceType,
                    COUNT(DISTINCT MemberID) AS CheckInCount
                FROM MemberCheckIns
                WHERE IsDeleted = 0
                    AND CAST(DateAttendance AS DATE) BETWEEN @FromDate AND @ToDate
                GROUP BY CAST(DateAttendance AS DATE)
                
                UNION ALL
                
                SELECT 
                    CAST(Attendance AS DATE) AS AttendanceDate,
                    'Walk-In' AS AttendanceType,
                    COUNT(DISTINCT CustomerID) AS CheckInCount
                FROM WalkInRecords
                WHERE IsDeleted = 0
                    AND CAST(Attendance AS DATE) BETWEEN @FromDate AND @ToDate
                GROUP BY CAST(Attendance AS DATE)
            ) AS CombinedData
            GROUP BY AttendanceDate
            ORDER BY AttendanceDate;";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<AttendanceDataDto>(
                query,
                new { FromDate = fromDate, ToDate = toDate }
            );
        }

        public async Task<CustomerTypePercentageDto> GetCustomerTypePercentagesAsync(DateTime fromDate, DateTime toDate)
        {
            const string query = @"
            SELECT 
                ISNULL(SUM(WalkIns), 0) AS TotalWalkIns,
                ISNULL(SUM(Members), 0) AS TotalMembers
            FROM (
                SELECT 
                    AttendanceDate,
                    ISNULL(SUM(CASE WHEN AttendanceType = 'Walk-In' THEN CheckInCount ELSE 0 END), 0) AS WalkIns,
                    ISNULL(SUM(CASE WHEN AttendanceType = 'Member' THEN CheckInCount ELSE 0 END), 0) AS Members
                FROM (
                    SELECT 
                        CAST(DateAttendance AS DATE) AS AttendanceDate,
                        'Member' AS AttendanceType,
                        COUNT(DISTINCT MemberID) AS CheckInCount
                    FROM MemberCheckIns
                    WHERE IsDeleted = 0
                        AND CAST(DateAttendance AS DATE) BETWEEN @FromDate AND @ToDate
                    GROUP BY CAST(DateAttendance AS DATE)
                    
                    UNION ALL
                    
                    SELECT 
                        CAST(Attendance AS DATE) AS AttendanceDate,
                        'Walk-In' AS AttendanceType,
                        COUNT(DISTINCT CustomerID) AS CheckInCount
                    FROM WalkInRecords
                    WHERE IsDeleted = 0
                        AND CAST(Attendance AS DATE) BETWEEN @FromDate AND @ToDate
                    GROUP BY CAST(Attendance AS DATE)
                ) AS CombinedData
                GROUP BY AttendanceDate
            ) AS DailyTotals;";

            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
                query,
                new { FromDate = fromDate, ToDate = toDate }
            );

            var totalWalkIns = (int)(result?.TotalWalkIns ?? 0);
            var totalMembers = (int)(result?.TotalMembers ?? 0);
            var total = totalWalkIns + totalMembers;

            return new CustomerTypePercentageDto
            {
                TotalWalkIns = totalWalkIns,
                TotalMembers = totalMembers,
                WalkInsPercentage = total > 0 ? (double)totalWalkIns / total * 100 : 0,
                MembersPercentage = total > 0 ? (double)totalMembers / total * 100 : 0
            };
        }

        #endregion

        #region FINANCIAL REPORTS

        public async Task<IEnumerable<PackageSalesDataDto>> GetPackageSalesDataAsync(DateTime fromDate, DateTime toDate)
        {
            const string query = @"
    SELECT 
        CAST(s.SaleDate AS DATE) AS SaleDate,
        p.PackageName AS PackageType,
        SUM(s.Amount) AS Revenue,
        SUM(s.Quantity) AS TotalSales
    FROM Sales s
    INNER JOIN Packages p ON s.PackageID = p.PackageID
    WHERE s.IsDeleted = 0
        AND s.PackageID IS NOT NULL
        AND CAST(s.SaleDate AS DATE) BETWEEN @FromDate AND @ToDate
    GROUP BY CAST(s.SaleDate AS DATE), p.PackageName
    ORDER BY SaleDate, PackageType;";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<PackageSalesDataDto>(
                query,
                new { FromDate = fromDate, ToDate = toDate }
            );
        }

        public async Task<Dictionary<string, double>> GetPackageSalesTotalsByTypeAsync(DateTime fromDate, DateTime toDate)
        {
            const string query = @"
    SELECT 
        p.PackageName AS PackageType,  -- Changed from p.Type
        SUM(s.Amount) AS TotalRevenue
    FROM Sales s
    INNER JOIN Packages p ON s.PackageID = p.PackageID
    WHERE s.IsDeleted = 0
        AND s.PackageID IS NOT NULL
        AND CAST(s.SaleDate AS DATE) BETWEEN @FromDate AND @ToDate
    GROUP BY p.PackageName;";

            using var connection = new SqlConnection(_connectionString);
            var results = await connection.QueryAsync<(string PackageType, double TotalRevenue)>(
                query,
                new { FromDate = fromDate, ToDate = toDate }
            );

            var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var (packageType, totalRevenue) in results)
            {
                totals[packageType] = totalRevenue;
            }

            return totals;
        }

        public async Task<PackageSalesSummaryDto> GetPackageSalesSummaryAsync(DateTime fromDate, DateTime toDate)
        {
            const string query = @"
    SELECT 
        p.PackageName AS PackageType,  -- Changed from p.Type
        SUM(s.Amount) AS TotalRevenue,
        SUM(s.Quantity) AS TotalQuantity,
        COUNT(DISTINCT s.SaleID) AS TransactionCount
    FROM Sales s
    INNER JOIN Packages p ON s.PackageID = p.PackageID
    WHERE s.IsDeleted = 0
        AND s.PackageID IS NOT NULL
        AND CAST(s.SaleDate AS DATE) BETWEEN @FromDate AND @ToDate
    GROUP BY p.PackageName;";

            using var connection = new SqlConnection(_connectionString);
            var results = await connection.QueryAsync<PackageTypeSummaryDto>(
                query,
                new { FromDate = fromDate, ToDate = toDate }
            );

            return new PackageSalesSummaryDto
            {
                PackageTypeSummaries = results.ToList(),
                TotalRevenue = results.Sum(r => r.TotalRevenue),
                TotalQuantity = results.Sum(r => r.TotalQuantity),
                TotalTransactions = results.Sum(r => r.TransactionCount)
            };
        }

        #endregion

        #region NONFUNCTIONAL REQUIREMENTS

        public async Task<(double TotalRevenue, int TotalSales, int TotalGymPackages, int TotalWalkInMembers)> GetFinancialSummaryAsync(DateTime fromDate, DateTime toDate)
        {
            double totalRevenue = 0;
            int totalSales = 0;
            int totalGymPackages = 0;
            int totalWalkInMembers = 0;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // TOTAL REVENUE
                const string revenueQuery = @"
            SELECT ISNULL(SUM(Amount), 0) 
            FROM Sales 
            WHERE IsDeleted = 0 
              AND CAST(SaleDate AS DATE) BETWEEN @From AND @To";

                totalRevenue = await conn.ExecuteScalarAsync<double>(revenueQuery, new { From = fromDate, To = toDate });

                // TOTAL SALES
                const string salesQuery = @"
            SELECT COUNT(DISTINCT SaleID)
            FROM Sales
            WHERE IsDeleted = 0 
              AND CAST(SaleDate AS DATE) BETWEEN @From AND @To";

                totalSales = await conn.ExecuteScalarAsync<int>(salesQuery, new { From = fromDate, To = toDate });

                // TOTAL GYM PACKAGES SOLD
                const string packageQuery = @"
            SELECT COUNT(*) 
            FROM Sales 
            WHERE PackageID IS NOT NULL AND IsDeleted = 0 
              AND CAST(SaleDate AS DATE) BETWEEN @From AND @To";

                totalGymPackages = await conn.ExecuteScalarAsync<int>(packageQuery, new { From = fromDate, To = toDate });

                // TOTAL WALK-IN / MEMBER TRANSACTIONS
                const string walkInQuery = @"
            SELECT COUNT(*) 
            FROM WalkInRecords
            WHERE IsDeleted = 0 
              AND CAST(Attendance AS DATE) BETWEEN @From AND @To";

                totalWalkInMembers = await conn.ExecuteScalarAsync<int>(walkInQuery, new { From = fromDate, To = toDate });

                return (totalRevenue, totalSales, totalGymPackages, totalWalkInMembers);
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast($"Error loading financial summary: {ex.Message}");
                return (totalRevenue, totalSales, totalGymPackages, totalWalkInMembers);
            }
        }

        #endregion

        #region SUPPORTING CLASS
        // DTOs
        public class AttendanceDataDto
        {
            public DateTime AttendanceDate { get; set; }
            public int WalkIns { get; set; }
            public int Members { get; set; }
            public int TotalAttendance { get; set; }
        }

        public class CustomerTypePercentageDto
        {
            public int TotalWalkIns { get; set; }
            public int TotalMembers { get; set; }
            public double WalkInsPercentage { get; set; }
            public double MembersPercentage { get; set; }
        }

        public class PackageSalesDataDto
        {
            public DateTime SaleDate { get; set; }
            public string PackageType { get; set; }
            public double Revenue { get; set; }
            public int TotalSales { get; set; }
        }

        public class PackageTypeSummaryDto
        {
            public string PackageType { get; set; }
            public double TotalRevenue { get; set; }
            public int TotalQuantity { get; set; }
            public int TransactionCount { get; set; }
        }

        public class PackageSalesSummaryDto
        {
            public List<PackageTypeSummaryDto> PackageTypeSummaries { get; set; }
            public double TotalRevenue { get; set; }
            public int TotalQuantity { get; set; }
            public int TotalTransactions { get; set; }
        }
        #endregion

    }
}

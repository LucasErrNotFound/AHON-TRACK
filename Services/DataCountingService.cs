using AHON_TRACK.Models;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
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

                // --- AGE GROUPS ---
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
                        WHERE Age IS NOT NULL AND IsDeleted = 0
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
                        WHERE Age IS NOT NULL AND IsDeleted = 0
                    ) AS Combined
                    GROUP BY AgeGroup;";

                using (var cmd = new SqlCommand(ageGroupQuery, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string group = reader.GetString(0);
                        int count = reader.GetInt32(1);
                        ageGroups[group] = count;
                    }
                }

                // --- GENDER COUNTS ---
                string genderQuery = @"
                    SELECT Gender, COUNT(*) AS Count
                    FROM (
                        SELECT Gender FROM Members WHERE Gender IN ('Male', 'Female') AND IsDeleted = 0
                        UNION ALL
                        SELECT Gender FROM WalkInCustomers WHERE Gender IN ('Male', 'Female') AND IsDeleted = 0
                    ) AS Combined
                    GROUP BY Gender;";

                using (var cmd = new SqlCommand(genderQuery, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string gender = reader.GetString(0);
                        int count = reader.GetInt32(1);
                        genderCounts[gender] = count;
                    }
                }

                // --- POPULATION TREND ---
                string populationQuery = @"
                    SELECT CAST(DateJoined AS DATE) AS [Date], COUNT(*) AS Count
                    FROM (
                        SELECT DateJoined FROM Members WHERE DateJoined BETWEEN @From AND @To AND IsDeleted = 0
                        UNION ALL
                        SELECT DateJoined FROM WalkInCustomers WHERE DateJoined BETWEEN @From AND @To AND IsDeleted = 0
                    ) AS Combined
                    GROUP BY CAST(DateJoined AS DATE)
                    ORDER BY [Date];";

                using (var cmd = new SqlCommand(populationQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", from);
                    cmd.Parameters.AddWithValue("@To", to);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        DateTime date = reader.GetDateTime(0);
                        int count = reader.GetInt32(1);
                        populationCounts[date] = count;
                    }
                }

                return (ageGroups, genderCounts, populationCounts);
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast($"Error loading demographics: {ex.Message}");
                return (ageGroups, genderCounts, populationCounts);
            }
        }
    }
}

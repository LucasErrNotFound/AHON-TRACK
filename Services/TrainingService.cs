using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services
{
    public class TrainingService : ITrainingService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public TrainingService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                using var logCmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                      VALUES (@username, @role, @actionType, @description, @success, GETDATE()), @employeeID", conn);

                logCmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@success", success);
                logCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                await logCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        public async Task<List<TrainingModel>> GetTrainingSchedulesAsync()
        {
            var trainings = new List<TrainingModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT trainingID, memberID, firstName, lastName, contactNumber, 
                       picture, packageType, assignedCoach, scheduledDate, 
                       scheduledTimeStart, scheduledTimeEnd, attendance, 
                       createdAt, updatedAt
                FROM Trainings 
                ORDER BY scheduledDate, scheduledTimeStart";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                trainings.Add(new TrainingModel
                                {
                                    trainingID = reader.GetInt32("trainingID"),
                                    memberID = reader.GetInt32("memberID"),
                                    firstName = reader["firstName"]?.ToString() ?? "",
                                    lastName = reader["lastName"]?.ToString() ?? "",
                                    contactNumber = reader["contactNumber"]?.ToString() ?? "",
                                    picture = reader["picture"]?.ToString() ?? "",
                                    packageType = reader["packageType"]?.ToString() ?? "",
                                    assignedCoach = reader["assignedCoach"]?.ToString() ?? "",
                                    scheduledDate = reader.GetDateTime("scheduledDate"),
                                    scheduledTimeStart = reader.GetDateTime("scheduledTimeStart"),
                                    scheduledTimeEnd = reader.GetDateTime("scheduledTimeEnd"),
                                    attendance = reader["attendance"]?.ToString(),
                                    createdAt = reader.GetDateTime("createdAt"),
                                    updatedAt = reader["updatedAt"] != DBNull.Value ? reader.GetDateTime("updatedAt") : DateTime.MinValue
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load training schedules: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }
            return trainings;
        }

        public async Task<List<TrainingModel>> GetTrainingSchedulesByDateAsync(DateTime date)
        {
            var trainings = new List<TrainingModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT trainingID, memberID, firstName, lastName, contactNumber, 
                       picture, packageType, assignedCoach, scheduledDate, 
                       scheduledTimeStart, scheduledTimeEnd, attendance, 
                       createdAt, updatedAt
                FROM Trainings 
                WHERE CAST(scheduledDate AS DATE) = @Date
                ORDER BY scheduledTimeStart";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Date", date.Date);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                trainings.Add(new TrainingModel
                                {
                                    trainingID = reader.GetInt32("trainingID"),
                                    memberID = reader.GetInt32("memberID"),
                                    firstName = reader["firstName"]?.ToString() ?? "",
                                    lastName = reader["lastName"]?.ToString() ?? "",
                                    contactNumber = reader["contactNumber"]?.ToString() ?? "",
                                    picture = reader["picture"]?.ToString() ?? "",
                                    packageType = reader["packageType"]?.ToString() ?? "",
                                    assignedCoach = reader["assignedCoach"]?.ToString() ?? "",
                                    scheduledDate = reader.GetDateTime("scheduledDate"),
                                    scheduledTimeStart = reader.GetDateTime("scheduledTimeStart"),
                                    scheduledTimeEnd = reader.GetDateTime("scheduledTimeEnd"),
                                    attendance = reader["attendance"]?.ToString(),
                                    createdAt = reader.GetDateTime("createdAt"),
                                    updatedAt = reader["updatedAt"] != DBNull.Value ? reader.GetDateTime("updatedAt") : DateTime.MinValue
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load training schedules by date: {ex.Message}")
                    .ShowError();
            }
            return trainings;
        }

        public async Task<List<TrainingModel>> GetTrainingSchedulesByPackageTypeAsync(string packageType)
        {
            var trainings = new List<TrainingModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT trainingID, memberID, firstName, lastName, contactNumber, 
                       picture, packageType, assignedCoach, scheduledDate, 
                       scheduledTimeStart, scheduledTimeEnd, attendance, 
                       createdAt, updatedAt
                FROM Trainings 
                WHERE packageType = @PackageType
                ORDER BY scheduledDate, scheduledTimeStart";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@PackageType", packageType);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                trainings.Add(new TrainingModel
                                {
                                    trainingID = reader.GetInt32("trainingID"),
                                    memberID = reader.GetInt32("memberID"),
                                    firstName = reader["firstName"]?.ToString() ?? "",
                                    lastName = reader["lastName"]?.ToString() ?? "",
                                    contactNumber = reader["contactNumber"]?.ToString() ?? "",
                                    picture = reader["picture"]?.ToString() ?? "",
                                    packageType = reader["packageType"]?.ToString() ?? "",
                                    assignedCoach = reader["assignedCoach"]?.ToString() ?? "",
                                    scheduledDate = reader.GetDateTime("scheduledDate"),
                                    scheduledTimeStart = reader.GetDateTime("scheduledTimeStart"),
                                    scheduledTimeEnd = reader.GetDateTime("scheduledTimeEnd"),
                                    attendance = reader["attendance"]?.ToString(),
                                    createdAt = reader.GetDateTime("createdAt"),
                                    updatedAt = reader["updatedAt"] != DBNull.Value ? reader.GetDateTime("updatedAt") : DateTime.MinValue
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load training schedules by package type: {ex.Message}")
                    .ShowError();
            }
            return trainings;
        }

        public async Task<List<TrainingModel>> GetTrainingSchedulesByCoachAsync(string coachName)
        {
            var trainings = new List<TrainingModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT trainingID, memberID, firstName, lastName, contactNumber, 
                       picture, packageType, assignedCoach, scheduledDate, 
                       scheduledTimeStart, scheduledTimeEnd, attendance, 
                       createdAt, updatedAt
                FROM Trainings 
                WHERE assignedCoach = @CoachName
                ORDER BY scheduledDate, scheduledTimeStart";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CoachName", coachName);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                trainings.Add(new TrainingModel
                                {
                                    trainingID = reader.GetInt32("trainingID"),
                                    memberID = reader.GetInt32("memberID"),
                                    firstName = reader["firstName"]?.ToString() ?? "",
                                    lastName = reader["lastName"]?.ToString() ?? "",
                                    contactNumber = reader["contactNumber"]?.ToString() ?? "",
                                    picture = reader["picture"]?.ToString() ?? "",
                                    packageType = reader["packageType"]?.ToString() ?? "",
                                    assignedCoach = reader["assignedCoach"]?.ToString() ?? "",
                                    scheduledDate = reader.GetDateTime("scheduledDate"),
                                    scheduledTimeStart = reader.GetDateTime("scheduledTimeStart"),
                                    scheduledTimeEnd = reader.GetDateTime("scheduledTimeEnd"),
                                    attendance = reader["attendance"]?.ToString(),
                                    createdAt = reader.GetDateTime("createdAt"),
                                    updatedAt = reader["updatedAt"] != DBNull.Value ? reader.GetDateTime("updatedAt") : DateTime.MinValue
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load training schedules by coach: {ex.Message}")
                    .ShowError();
            }
            return trainings;
        }

        public async Task<bool> AddTrainingScheduleAsync(TrainingModel training)
        {
            const string query = @"
        INSERT INTO Trainings (memberID, firstName, lastName, contactNumber, picture, 
                              packageType, assignedCoach, scheduledDate, scheduledTimeStart, 
                              scheduledTimeEnd, attendance)
        OUTPUT INSERTED.trainingID
        VALUES (@memberID, @firstName, @lastName, @contactNumber, @picture, 
                @packageType, @assignedCoach, @scheduledDate, @scheduledTimeStart, 
                @scheduledTimeEnd, @attendance)";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@memberID", training.memberID);
                        command.Parameters.AddWithValue("@firstName", training.firstName);
                        command.Parameters.AddWithValue("@lastName", training.lastName);
                        command.Parameters.AddWithValue("@contactNumber", (object)training.contactNumber ?? DBNull.Value);
                        command.Parameters.AddWithValue("@picture", (object)training.picture ?? DBNull.Value);
                        command.Parameters.AddWithValue("@packageType", training.packageType);
                        command.Parameters.AddWithValue("@assignedCoach", training.assignedCoach);
                        command.Parameters.AddWithValue("@scheduledDate", training.scheduledDate);
                        command.Parameters.AddWithValue("@scheduledTimeStart", training.scheduledTimeStart);
                        command.Parameters.AddWithValue("@scheduledTimeEnd", training.scheduledTimeEnd);
                        command.Parameters.AddWithValue("@attendance", (object)training.attendance ?? DBNull.Value);

                        var newId = await command.ExecuteScalarAsync();

                        if (newId != null && newId != DBNull.Value)
                        {
                            training.trainingID = Convert.ToInt32(newId);

                            await LogActionAsync(connection, "Add Training Schedule",
                                $"Added training schedule for {training.firstName} {training.lastName} - {training.packageType} with {training.assignedCoach} on {training.scheduledDate:MMM dd, yyyy}", true);

                            _toastManager?.CreateToast("Training Schedule Added")
                                .WithContent($"Training schedule for {training.firstName} {training.lastName} added successfully!")
                                .ShowSuccess();
                            return true;
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to add training schedule",
                        $"Failed to add training schedule for {training.firstName} {training.lastName} - SQL Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to add training schedule: {ex.Message}")
                    .ShowError();
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to add training schedule",
                        $"Failed to add training schedule for {training.firstName} {training.lastName} - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error adding training schedule: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> UpdateTrainingScheduleAsync(TrainingModel training)
        {
            const string query = @"
        UPDATE Trainings SET 
            memberID = @memberID,
            firstName = @firstName,
            lastName = @lastName,
            contactNumber = @contactNumber,
            picture = @picture,
            packageType = @packageType,
            assignedCoach = @assignedCoach,
            scheduledDate = @scheduledDate,
            scheduledTimeStart = @scheduledTimeStart,
            scheduledTimeEnd = @scheduledTimeEnd,
            attendance = @attendance,
            updatedAt = GETDATE()
        WHERE trainingID = @trainingID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@trainingID", training.trainingID);
                        command.Parameters.AddWithValue("@memberID", training.memberID);
                        command.Parameters.AddWithValue("@firstName", training.firstName);
                        command.Parameters.AddWithValue("@lastName", training.lastName);
                        command.Parameters.AddWithValue("@contactNumber", (object)training.contactNumber ?? DBNull.Value);
                        command.Parameters.AddWithValue("@picture", (object)training.picture ?? DBNull.Value);
                        command.Parameters.AddWithValue("@packageType", training.packageType);
                        command.Parameters.AddWithValue("@assignedCoach", training.assignedCoach);
                        command.Parameters.AddWithValue("@scheduledDate", training.scheduledDate);
                        command.Parameters.AddWithValue("@scheduledTimeStart", training.scheduledTimeStart);
                        command.Parameters.AddWithValue("@scheduledTimeEnd", training.scheduledTimeEnd);
                        command.Parameters.AddWithValue("@attendance", (object)training.attendance ?? DBNull.Value);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Update Training Schedule",
                                $"Updated training schedule for {training.firstName} {training.lastName} (ID: {training.trainingID})", true);

                            _toastManager?.CreateToast("Training Schedule Updated")
                                .WithContent($"Training schedule for {training.firstName} {training.lastName} updated successfully!")
                                .ShowSuccess();
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to update training schedule",
                        $"Failed to update training schedule for {training.firstName} {training.lastName} - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error updating training schedule: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> UpdateAttendanceAsync(int trainingID, string attendance)
        {
            const string query = @"
        UPDATE Trainings 
        SET attendance = @attendance, updatedAt = GETDATE()
        WHERE trainingID = @trainingID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@trainingID", trainingID);
                        command.Parameters.AddWithValue("@attendance", attendance);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Update Attendance",
                                $"Updated attendance to '{attendance}' for training ID {trainingID}", true);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to update attendance",
                        $"Failed to update attendance for training ID {trainingID} - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error updating attendance: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> DeleteTrainingScheduleAsync(int trainingID)
        {
            const string query = "DELETE FROM Trainings WHERE trainingID = @trainingID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var trainingInfo = await GetTrainingNameByIdAsync(connection, trainingID);

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@trainingID", trainingID);
                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Delete Training Schedule",
                                $"Deleted training schedule: {trainingInfo} (ID: {trainingID})", true);

                            _toastManager?.CreateToast("Training Schedule Deleted")
                                .WithContent($"Training schedule deleted successfully!")
                                .ShowSuccess();
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to delete training schedule",
                        $"Failed to delete training schedule ID {trainingID} - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error deleting training schedule: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<TrainingModel?> GetTrainingScheduleByIdAsync(int trainingID)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT trainingID, memberID, firstName, lastName, contactNumber, 
                       picture, packageType, assignedCoach, scheduledDate, 
                       scheduledTimeStart, scheduledTimeEnd, attendance, 
                       createdAt, updatedAt
                FROM Trainings 
                WHERE trainingID = @trainingID";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@trainingID", trainingID);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new TrainingModel
                                {
                                    trainingID = reader.GetInt32("trainingID"),
                                    memberID = reader.GetInt32("memberID"),
                                    firstName = reader["firstName"]?.ToString() ?? "",
                                    lastName = reader["lastName"]?.ToString() ?? "",
                                    contactNumber = reader["contactNumber"]?.ToString() ?? "",
                                    picture = reader["picture"]?.ToString() ?? "",
                                    packageType = reader["packageType"]?.ToString() ?? "",
                                    assignedCoach = reader["assignedCoach"]?.ToString() ?? "",
                                    scheduledDate = reader.GetDateTime("scheduledDate"),
                                    scheduledTimeStart = reader.GetDateTime("scheduledTimeStart"),
                                    scheduledTimeEnd = reader.GetDateTime("scheduledTimeEnd"),
                                    attendance = reader["attendance"]?.ToString(),
                                    createdAt = reader.GetDateTime("createdAt"),
                                    updatedAt = reader["updatedAt"] != DBNull.Value ? reader.GetDateTime("updatedAt") : DateTime.MinValue
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load training schedule: {ex.Message}")
                    .ShowError();
            }
            return null;
        }

        private async Task<string> GetTrainingNameByIdAsync(SqlConnection connection, int trainingID)
        {
            const string query = "SELECT firstName + ' ' + lastName AS FullName FROM Trainings WHERE trainingID = @trainingID";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@trainingID", trainingID);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? "Unknown Training";
            }
        }

        public async Task<List<TraineeModel>> GetAvailableTraineesAsync()
        {
            var trainees = new List<TraineeModel>();
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
            SELECT 
                m.MemberID,
                m.Firstname,
                m.Lastname,
                m.ContactNumber,
                m.ProfilePicture,
                pkg.packageName AS PackageType,
                mem.SessionsLeft
            FROM Members m
            INNER JOIN Members mem ON m.MemberID = mem.MemberID
            INNER JOIN Package pkg ON mem.PackageID = pkg.packageID
            WHERE m.Status = 'Active' 
            AND mem.SessionsLeft > 0
            ORDER BY m.Firstname, m.Lastname";

                await using var command = new SqlCommand(query, connection);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    byte[]? bytes = null;
                    if (!reader.IsDBNull(reader.GetOrdinal("ProfilePicture")))
                        bytes = (byte[])reader["ProfilePicture"];

                    trainees.Add(new TraineeModel
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("MemberID")),
                        FirstName = reader["Firstname"]?.ToString() ?? "",
                        LastName = reader["Lastname"]?.ToString() ?? "",
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                        PackageType = reader["PackageType"]?.ToString() ?? "",
                        SessionLeft = reader.GetInt32(reader.GetOrdinal("SessionsLeft")),
                        Picture = ImageHelper.BytesToBase64(bytes ?? Array.Empty<byte>()) ?? "avares://AHON_TRACK/Assets/MainWindowView/user.png"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAvailableTraineesAsync] {ex.Message}");
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load available trainees: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }
            return trainees;
        }
    }
}

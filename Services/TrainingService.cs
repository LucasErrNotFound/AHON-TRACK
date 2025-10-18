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
                                    customerID = reader.GetInt32("memberID"),
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
                                    customerID = reader.GetInt32("memberID"),
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
                                    customerID = reader.GetInt32("memberID"),
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
                                    customerID = reader.GetInt32("memberID"),
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
        INSERT INTO Trainings (
            CustomerID, CustomerType, FirstName, LastName, ContactNumber, 
            ProfilePicture, PackageID, PackageType, AssignedCoach, 
            ScheduledDate, ScheduledTimeStart, ScheduledTimeEnd, 
            Attendance, CreatedByEmployeeID
        )
        OUTPUT INSERTED.TrainingID
        VALUES (
            @CustomerID, @CustomerType, @FirstName, @LastName, @ContactNumber, 
            @ProfilePicture, @PackageID, @PackageType, @AssignedCoach, 
            @ScheduledDate, @ScheduledTimeStart, @ScheduledTimeEnd, 
            @Attendance, @EmployeeID
        )";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CustomerID", training.customerID);
                        command.Parameters.AddWithValue("@CustomerType", training.customerType ?? "Member");
                        command.Parameters.AddWithValue("@FirstName", training.firstName);
                        command.Parameters.AddWithValue("@LastName", training.lastName);
                        command.Parameters.AddWithValue("@ContactNumber", (object)training.contactNumber ?? DBNull.Value);

                        // Convert base64 picture string to bytes if needed
                        byte[]? pictureBytes = null;
                        if (!string.IsNullOrEmpty(training.picture) &&
                            !training.picture.StartsWith("avares://"))
                        {
                            try
                            {
                                pictureBytes = Convert.FromBase64String(training.picture);
                            }
                            catch
                            {
                                pictureBytes = null;
                            }
                        }
                        command.Parameters.AddWithValue("@ProfilePicture", (object)pictureBytes ?? DBNull.Value);

                        command.Parameters.AddWithValue("@PackageID", training.packageID);
                        command.Parameters.AddWithValue("@PackageType", training.packageType);
                        command.Parameters.AddWithValue("@AssignedCoach", training.assignedCoach);
                        command.Parameters.AddWithValue("@ScheduledDate", training.scheduledDate.Date);
                        command.Parameters.AddWithValue("@ScheduledTimeStart", training.scheduledTimeStart);
                        command.Parameters.AddWithValue("@ScheduledTimeEnd", training.scheduledTimeEnd);
                        command.Parameters.AddWithValue("@Attendance", (object)training.attendance ?? "Pending");
                        command.Parameters.AddWithValue("@EmployeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                        var newId = await command.ExecuteScalarAsync();

                        if (newId != null && newId != DBNull.Value)
                        {
                            training.trainingID = Convert.ToInt32(newId);

                            // Decrement session count
                            await DecrementSessionCountAsync(connection, training.customerID,
                                training.customerType, training.packageID);

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
                        command.Parameters.AddWithValue("@memberID", training.customerID);
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
                                    customerID = reader.GetInt32("memberID"),
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
            -- Get Members with sessions
            SELECT 
                m.MemberID AS CustomerID,
                'Member' AS CustomerType,
                m.Firstname AS FirstName,
                m.Lastname AS LastName,
                m.ContactNumber,
                m.ProfilePicture,
                p.PackageName AS PackageType,
                p.PackageID,
                COALESCE(ms.SessionsLeft, 0) AS SessionsLeft
            FROM Members m
            INNER JOIN Packages p ON m.PackageID = p.PackageID
            LEFT JOIN MemberSessions ms ON m.MemberID = ms.CustomerID AND p.PackageID = ms.PackageID
            WHERE m.Status = 'Active' 
                AND COALESCE(ms.SessionsLeft, 0) > 0

            UNION ALL

            -- Get Walk-In Customers with sessions
            SELECT 
                w.CustomerID,
                'WalkIn' AS CustomerType,
                w.FirstName,
                w.LastName,
                w.ContactNumber,
                NULL AS ProfilePicture,
                p.PackageName AS PackageType,
                p.PackageID,
                COALESCE(ws.SessionsLeft, 0) AS SessionsLeft
            FROM WalkInCustomers w
            INNER JOIN WalkInSessions ws ON w.CustomerID = ws.CustomerID
            INNER JOIN Packages p ON ws.PackageID = p.PackageID
            WHERE w.WalkinType = 'Regular'
                AND COALESCE(ws.SessionsLeft, 0) > 0

            ORDER BY FirstName, LastName";

                await using var command = new SqlCommand(query, connection);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    byte[]? pictureBytes = null;
                    if (!reader.IsDBNull(reader.GetOrdinal("ProfilePicture")))
                        pictureBytes = (byte[])reader["ProfilePicture"];

                    trainees.Add(new TraineeModel
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("CustomerID")),
                        CustomerType = reader["CustomerType"]?.ToString() ?? "",
                        FirstName = reader["FirstName"]?.ToString() ?? "",
                        LastName = reader["LastName"]?.ToString() ?? "",
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                        PackageType = reader["PackageType"]?.ToString() ?? "",
                        PackageID = reader.GetInt32(reader.GetOrdinal("PackageID")),
                        SessionLeft = reader.GetInt32(reader.GetOrdinal("SessionsLeft")),
                        Picture = ImageHelper.BytesToBase64(pictureBytes ?? Array.Empty<byte>())
                            ?? "avares://AHON_TRACK/Assets/MainWindowView/user.png"
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

        private async Task DecrementSessionCountAsync(SqlConnection connection, int customerID, string customerType, int packageID)
        {
            try
            {
                string table = customerType == "Member" ? "MemberSessions" : "WalkInSessions";
                string query = $@"
            UPDATE {table}
            SET SessionsLeft = SessionsLeft - 1
            WHERE CustomerID = @CustomerID 
                AND PackageID = @PackageID 
                AND SessionsLeft > 0";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CustomerID", customerID);
                command.Parameters.AddWithValue("@PackageID", packageID);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DecrementSessionCountAsync] {ex.Message}");
            }
        }

    }
}

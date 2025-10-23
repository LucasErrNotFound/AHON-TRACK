using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        #region Role-Based Access Control

        private bool CanCreate()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanUpdate()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanDelete()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanView()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;
        }

        #endregion

        #region CREATE

        /// <summary>
        /// Adds a new training schedule with full validation and capacity checking
        /// </summary>
        public async Task<bool> AddTrainingScheduleAsync(TrainingModel training)
        {
            if (!CanCreate())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to add training schedules.")
                    .ShowError();
                return false;
            }

            const string trainingInsertQuery = @"
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
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                // Step 1: Find CoachID
                var coachId = await GetCoachIdByNameAsync(connection, transaction, training.assignedCoach);
                if (coachId == null)
                {
                    _toastManager?.CreateToast("Coach Not Found")
                        .WithContent($"Coach '{training.assignedCoach}' does not exist.")
                        .ShowError();
                    return false;
                }

                // Step 2: Check daily capacity (8 sessions max per day)
                bool canTakeMore = await CheckDailyCapacityAsync(connection, transaction, coachId.Value, training.scheduledDate);
                if (!canTakeMore)
                {
                    _toastManager?.CreateToast("Coach Limit Reached")
                        .WithContent($"Coach {training.assignedCoach} has already reached 8 sessions for {training.scheduledDate:MMM dd}.")
                        .ShowWarning();
                    return false;
                }

                // Step 3: Check for existing or overlapping schedule
                var scheduleId = await GetCoachScheduleIdAsync(connection, transaction, coachId.Value,
                    training.scheduledDate, training.scheduledTimeStart, training.scheduledTimeEnd);

                if (scheduleId == -1)
                {
                    transaction.Rollback();
                    return false;
                }
                else if (scheduleId == null)
                {
                    scheduleId = await CreateCoachScheduleAsync(connection, transaction, coachId.Value,
                        training.scheduledDate, training.scheduledTimeStart, training.scheduledTimeEnd);

                    if (scheduleId == null)
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
                else
                {
                    bool hasSpace = await CheckScheduleCapacityAsync(connection, transaction, scheduleId.Value);
                    if (!hasSpace)
                    {
                        _toastManager?.CreateToast("Schedule Full")
                            .WithContent($"The selected time slot for {training.assignedCoach} is already full.")
                            .ShowWarning();
                        transaction.Rollback();
                        return false;
                    }

                    await IncrementScheduleCapacityAsync(connection, transaction, scheduleId.Value);
                }

                // Step 4: Insert training record
                using var trainingCmd = new SqlCommand(trainingInsertQuery, connection, transaction);
                trainingCmd.Parameters.AddWithValue("@CustomerID", training.customerID);
                trainingCmd.Parameters.AddWithValue("@CustomerType", training.customerType?.Trim() ?? "Member");
                trainingCmd.Parameters.AddWithValue("@FirstName", training.firstName ?? string.Empty);
                trainingCmd.Parameters.AddWithValue("@LastName", training.lastName ?? string.Empty);
                trainingCmd.Parameters.AddWithValue("@ContactNumber", (object?)training.contactNumber ?? DBNull.Value);
                trainingCmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary).Value = (object?)training.picture ?? DBNull.Value;
                trainingCmd.Parameters.AddWithValue("@PackageID", training.packageID);
                trainingCmd.Parameters.AddWithValue("@PackageType", training.packageType ?? string.Empty);
                trainingCmd.Parameters.AddWithValue("@AssignedCoach", training.assignedCoach ?? string.Empty);
                trainingCmd.Parameters.AddWithValue("@ScheduledDate", training.scheduledDate.Date);
                trainingCmd.Parameters.AddWithValue("@ScheduledTimeStart", training.scheduledTimeStart);
                trainingCmd.Parameters.AddWithValue("@ScheduledTimeEnd", training.scheduledTimeEnd);
                trainingCmd.Parameters.AddWithValue("@Attendance", training.attendance ?? "Pending");
                trainingCmd.Parameters.AddWithValue("@EmployeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                var newId = await trainingCmd.ExecuteScalarAsync();
                if (newId == null || newId == DBNull.Value)
                {
                    transaction.Rollback();
                    return false;
                }

                training.trainingID = Convert.ToInt32(newId);

                // Step 5: Decrement package session count
                await DecrementSessionCountAsync(connection, transaction, training.customerID,
                    training.customerType?.Trim() ?? "Member", training.packageID);

                // Step 6: Commit transaction
                transaction.Commit();
                DashboardEventService.Instance.NotifyScheduleAdded();
                DashboardEventService.Instance.NotifyMemberUpdated();
                DashboardEventService.Instance.NotifyTrainingSessionsUpdated();

                await LogActionAsync(connection, "CREATE",
                    $"Added training schedule for {training.firstName} {training.lastName} with {training.assignedCoach} on {training.scheduledDate:MMM dd, yyyy}.", true);

                /*   _toastManager?.CreateToast("Training Added")
                       .WithContent($"Training scheduled for {training.firstName} {training.lastName} successfully!")
                       .ShowSuccess(); */

                return true;
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error Adding Schedule")
                    .WithContent(ex.Message)
                    .ShowError();

                Debug.WriteLine($"AddTrainingScheduleAsync Error: {ex}");
                return false;
            }
        }

        #endregion

        #region READ

        /// <summary>
        /// Gets all training schedules ordered by date and time
        /// </summary>
        public async Task<List<TrainingModel>> GetTrainingSchedulesAsync()
        {
            if (!CanView())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to view training schedules.")
                    .ShowError();
                return new List<TrainingModel>();
            }

            var trainings = new List<TrainingModel>();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
SELECT TrainingID, CustomerID, FirstName, LastName, ContactNumber, 
       ProfilePicture, PackageType, AssignedCoach, ScheduledDate, 
       ScheduledTimeStart, ScheduledTimeEnd, Attendance, 
       CreatedAt, UpdatedAt
FROM Trainings 
ORDER BY ScheduledDate, ScheduledTimeStart";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    byte[]? pictureBytes = reader["ProfilePicture"] != DBNull.Value
                        ? (byte[])reader["ProfilePicture"]
                        : null;

                    trainings.Add(new TrainingModel
                    {
                        trainingID = reader.GetInt32(reader.GetOrdinal("TrainingID")),
                        customerID = reader.GetInt32(reader.GetOrdinal("CustomerID")),
                        firstName = reader["FirstName"]?.ToString() ?? "",
                        lastName = reader["LastName"]?.ToString() ?? "",
                        contactNumber = reader["ContactNumber"]?.ToString() ?? "",
                        picture = pictureBytes,
                        packageType = reader["PackageType"]?.ToString() ?? "",
                        assignedCoach = reader["AssignedCoach"]?.ToString() ?? "",
                        scheduledDate = reader.GetDateTime(reader.GetOrdinal("ScheduledDate")),
                        scheduledTimeStart = reader.GetDateTime(reader.GetOrdinal("ScheduledTimeStart")),
                        scheduledTimeEnd = reader.GetDateTime(reader.GetOrdinal("ScheduledTimeEnd")),
                        attendance = reader["Attendance"]?.ToString(),
                        createdAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        updatedAt = reader["UpdatedAt"] != DBNull.Value
                            ? reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                            : DateTime.MinValue
                    });
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load training schedules: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();

                Debug.WriteLine($"GetTrainingSchedulesAsync Error: {ex}");
            }

            return trainings;
        }

        /// <summary>
        /// Gets a single training schedule by ID
        /// </summary>
        public async Task<TrainingModel?> GetTrainingScheduleByIdAsync(int trainingID)
        {
            if (!CanView())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to view training schedules.")
                    .ShowError();
                return null;
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
SELECT TrainingID, CustomerID, CustomerType, FirstName, LastName, ContactNumber, 
       ProfilePicture, PackageID, PackageType, AssignedCoach, ScheduledDate, 
       ScheduledTimeStart, ScheduledTimeEnd, Attendance, 
       CreatedAt, UpdatedAt, CreatedByEmployeeID
FROM Trainings 
WHERE TrainingID = @TrainingID";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TrainingID", trainingID);

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new TrainingModel
                    {
                        trainingID = reader.GetInt32(reader.GetOrdinal("TrainingID")),
                        customerID = reader.GetInt32(reader.GetOrdinal("CustomerID")),
                        customerType = reader["CustomerType"]?.ToString() ?? "",
                        firstName = reader["FirstName"]?.ToString() ?? "",
                        lastName = reader["LastName"]?.ToString() ?? "",
                        contactNumber = reader["ContactNumber"]?.ToString() ?? "",
                        picture = !reader.IsDBNull(reader.GetOrdinal("ProfilePicture"))
                            ? (byte[])reader["ProfilePicture"]
                            : Array.Empty<byte>(),
                        packageID = reader.GetInt32(reader.GetOrdinal("PackageID")),
                        packageType = reader["PackageType"]?.ToString() ?? "",
                        assignedCoach = reader["AssignedCoach"]?.ToString() ?? "",
                        scheduledDate = reader.GetDateTime(reader.GetOrdinal("ScheduledDate")),
                        scheduledTimeStart = reader.GetDateTime(reader.GetOrdinal("ScheduledTimeStart")),
                        scheduledTimeEnd = reader.GetDateTime(reader.GetOrdinal("ScheduledTimeEnd")),
                        attendance = reader["Attendance"]?.ToString() ?? "Pending",
                        createdAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        updatedAt = reader["UpdatedAt"] != DBNull.Value
                            ? reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                            : DateTime.MinValue,
                        addedByEmployeeID = reader["CreatedByEmployeeID"] != DBNull.Value
                            ? reader.GetInt32(reader.GetOrdinal("CreatedByEmployeeID"))
                            : 0
                    };
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load training schedule: {ex.Message}")
                    .ShowError();

                Debug.WriteLine($"GetTrainingScheduleByIdAsync Error: {ex}");
            }

            return null;
        }

        /// <summary>
        /// Gets available trainees with remaining sessions
        /// </summary>
        public async Task<List<TraineeModel>> GetAvailableTraineesAsync()
        {
            if (!CanView())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to view trainees.")
                    .ShowError();
                return new List<TraineeModel>();
            }
            var trainees = new List<TraineeModel>();
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                const string query = @"
-- Get Members with sessions
SELECT 
    m.MemberID AS CustomerID,
    m.CustomerType AS CustomerType,
    m.Firstname AS FirstName,
    m.Lastname AS LastName,
    m.ContactNumber,
    m.ProfilePicture,
    p.PackageName AS PackageType,
    p.PackageID,
    COALESCE(ms.SessionsLeft, 0) AS SessionsLeft
FROM Members m
INNER JOIN MemberSessions ms ON m.MemberID = ms.CustomerID
INNER JOIN Packages p ON ms.PackageID = p.PackageID
WHERE COALESCE(ms.SessionsLeft, 0) > 0
ORDER BY FirstName, LastName;";
                await using var command = new SqlCommand(query, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Bitmap? picture = null;
                    if (!reader.IsDBNull(reader.GetOrdinal("ProfilePicture")))
                    {
                        var pictureBytes = (byte[])reader["ProfilePicture"];
                        if (pictureBytes.Length > 0)
                        {
                            using var ms = new MemoryStream(pictureBytes);
                            picture = new Bitmap(ms);
                        }
                    }
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
                        Picture = picture ?? new Bitmap(AssetLoader.Open(new Uri("avares://AHON_TRACK/Assets/MainWindowView/user.png")))
                    });
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load available trainees: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
                Debug.WriteLine($"GetAvailableTraineesAsync Error: {ex}");
            }
            return trainees;
        }

        /// <summary>
        /// Gets list of coaches for dropdown/combo box
        /// </summary>
        public async Task<List<(int CoachID, string FullName, string Username)>> GetCoachNamesAsync()
        {
            var coaches = new List<(int, string, string)>();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
SELECT 
    c.CoachID,
    (e.FirstName + ' ' + e.LastName) AS FullName,
    c.Username
FROM Coach c
INNER JOIN Employees e ON c.CoachID = e.EmployeeID
WHERE c.Username LIKE '%coach%'
  AND c.IsDeleted = 0
ORDER BY e.FirstName;";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int coachId = reader.GetInt32(0);
                    string fullName = reader.GetString(1);
                    string username = reader.GetString(2);
                    coaches.Add((coachId, fullName, username));
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load coaches: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();

                Debug.WriteLine($"GetCoachNamesAsync Error: {ex}");
            }

            return coaches;
        }

        #endregion

        #region UPDATE

        /// <summary>
        /// Updates an existing training schedule
        /// </summary>
        public async Task<bool> UpdateTrainingScheduleAsync(TrainingModel training)
        {
            if (!CanUpdate())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to update training schedules.")
                    .ShowError();
                return false;
            }

            const string query = @"
UPDATE Trainings SET 
    CustomerID = @CustomerID,
    FirstName = @FirstName,
    LastName = @LastName,
    ContactNumber = @ContactNumber,
    ProfilePicture = @ProfilePicture,
    PackageType = @PackageType,
    AssignedCoach = @AssignedCoach,
    ScheduledDate = @ScheduledDate,
    ScheduledTimeStart = @ScheduledTimeStart,
    ScheduledTimeEnd = @ScheduledTimeEnd,
    Attendance = @Attendance,
    UpdatedAt = GETDATE()
WHERE TrainingID = @TrainingID";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TrainingID", training.trainingID);
                command.Parameters.AddWithValue("@CustomerID", training.customerID);
                command.Parameters.AddWithValue("@FirstName", training.firstName);
                command.Parameters.AddWithValue("@LastName", training.lastName);
                command.Parameters.AddWithValue("@ContactNumber", (object)training.contactNumber ?? DBNull.Value);
                command.Parameters.AddWithValue("@ProfilePicture", (object)training.picture ?? DBNull.Value);
                command.Parameters.AddWithValue("@PackageType", training.packageType);
                command.Parameters.AddWithValue("@AssignedCoach", training.assignedCoach);
                command.Parameters.AddWithValue("@ScheduledDate", training.scheduledDate);
                command.Parameters.AddWithValue("@ScheduledTimeStart", training.scheduledTimeStart);
                command.Parameters.AddWithValue("@ScheduledTimeEnd", training.scheduledTimeEnd);
                command.Parameters.AddWithValue("@Attendance", (object)training.attendance ?? DBNull.Value);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    DashboardEventService.Instance.NotifyScheduleUpdated();
                    await LogActionAsync(connection, "UPDATE",
                        $"Updated training schedule for {training.firstName} {training.lastName} (ID: {training.trainingID})", true);

                    /*  _toastManager?.CreateToast("Training Schedule Updated")
                          .WithContent($"Training schedule for {training.firstName} {training.lastName} updated successfully!")
                          .ShowSuccess(); */

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                await LogActionAsync(connection, "UPDATE",
                    $"Failed to update training schedule for {training.firstName} {training.lastName} - Error: {ex.Message}", false);

                _toastManager?.CreateToast("Error")
                    .WithContent($"Error updating training schedule: {ex.Message}")
                    .ShowError();

                Debug.WriteLine($"UpdateTrainingScheduleAsync Error: {ex}");
            }

            return false;
        }

        /// <summary>
        /// Updates attendance status for a training session
        /// </summary>
        public async Task<bool> UpdateAttendanceAsync(int trainingID, string attendance)
        {
            if (!CanUpdate())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to update attendance.")
                    .ShowError();
                return false;
            }

            const string query = @"
UPDATE Trainings 
SET Attendance = @Attendance, UpdatedAt = GETDATE()
WHERE TrainingID = @TrainingID";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TrainingID", trainingID);
                command.Parameters.AddWithValue("@Attendance", attendance);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    DashboardEventService.Instance.NotifyScheduleUpdated();
                    await LogActionAsync(connection, "UPDATE",
                        $"Updated attendance to '{attendance}' for training ID {trainingID}", true);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                await LogActionAsync(connection, "UPDATE",
                    $"Failed to update attendance for training ID {trainingID} - Error: {ex.Message}", false);

                _toastManager?.CreateToast("Error")
                    .WithContent($"Error updating attendance: {ex.Message}")
                    .ShowError();

                Debug.WriteLine($"UpdateAttendanceAsync Error: {ex}");
            }

            return false;
        }

        #endregion

        #region DELETE

        /// <summary>
        /// Deletes a training schedule by ID
        /// </summary>
        public async Task<bool> DeleteTrainingScheduleAsync(int trainingID)
        {
            if (!CanDelete())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete training schedules.")
                    .ShowError();
                return false;
            }

            const string query = "DELETE FROM Trainings WHERE TrainingID = @TrainingID";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var trainingInfo = await GetTrainingNameByIdAsync(connection, trainingID);

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TrainingID", trainingID);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(connection, "DELETE",
                        $"Deleted training schedule: {trainingInfo} (ID: {trainingID})", true);

                    /*   _toastManager?.CreateToast("Training Schedule Deleted")
                        .WithContent("Training schedule deleted successfully!")
                        .ShowSuccess();*/

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                await LogActionAsync(connection, "DELETE",
                    $"Failed to delete training schedule ID {trainingID} - Error: {ex.Message}", false);

                _toastManager?.CreateToast("Error")
                    .WithContent($"Error deleting training schedule: {ex.Message}")
                    .ShowError();

                Debug.WriteLine($"DeleteTrainingScheduleAsync Error: {ex}");
            }

            return false;
        }

        #endregion

        #region UTILITY METHODS

        /// <summary>
        /// Logs system actions to database
        /// </summary>
        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                using var logCmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                      VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)", conn);

                logCmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@success", success);
                logCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                await logCmd.ExecuteNonQueryAsync();
                DashboardEventService.Instance.NotifyTrainingSessionsUpdated();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        /// <summary>
        /// Gets training participant's name by training ID
        /// </summary>
        private async Task<string> GetTrainingNameByIdAsync(SqlConnection connection, int trainingID)
        {
            const string query = "SELECT FirstName + ' ' + LastName AS FullName FROM Trainings WHERE TrainingID = @TrainingID";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@TrainingID", trainingID);

            var result = await command.ExecuteScalarAsync();
            return result?.ToString() ?? "Unknown Training";
        }

        #endregion

        #region HELPER METHODS - Coach & Schedule Management

        /// <summary>
        /// Gets coach ID by their full name
        /// </summary>
        private async Task<int?> GetCoachIdByNameAsync(SqlConnection connection, SqlTransaction transaction, string coachFullName)
        {
            const string query = @"
SELECT TOP 1 c.CoachID
FROM Coach c
INNER JOIN Employees e ON c.CoachID = e.EmployeeID
WHERE (e.FirstName + ' ' + e.LastName) = @CoachFullName
  AND c.IsDeleted = 0";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@CoachFullName", coachFullName.Trim());

            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : (int?)null;
        }

        /// <summary>
        /// Checks if coach has capacity for more sessions on a given date (max 8 per day)
        /// </summary>
        private async Task<bool> CheckDailyCapacityAsync(SqlConnection connection, SqlTransaction transaction, int coachId, DateTime date)
        {
            const string query = @"
SELECT SUM(CurrentCapacity)
FROM CoachSchedule
WHERE CoachID = @CoachID 
  AND ScheduledDate = @Date 
  AND IsDeleted = 0";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@CoachID", coachId);
            cmd.Parameters.AddWithValue("@Date", date.Date);

            var result = await cmd.ExecuteScalarAsync();
            int totalToday = result != DBNull.Value ? Convert.ToInt32(result) : 0;
            return totalToday < 8;
        }

        /// <summary>
        /// Gets or detects coach schedule ID for a given time slot
        /// Returns: scheduleId if found, null if none exists, -1 if overlap detected
        /// </summary>
        private async Task<int?> GetCoachScheduleIdAsync(SqlConnection connection, SqlTransaction transaction, int coachId, DateTime date, DateTime start, DateTime end)
        {
            // 1️⃣ Try to find exact match first
            const string exactQuery = @"
SELECT TOP 1 ScheduleID 
FROM CoachSchedule 
WHERE CoachID = @CoachID 
  AND ScheduledDate = @Date 
  AND ScheduledTimeStart = @Start 
  AND ScheduledTimeEnd = @End 
  AND IsDeleted = 0";

            using var exactCmd = new SqlCommand(exactQuery, connection, transaction);
            exactCmd.Parameters.AddWithValue("@CoachID", coachId);
            exactCmd.Parameters.AddWithValue("@Date", date.Date);
            exactCmd.Parameters.AddWithValue("@Start", start);
            exactCmd.Parameters.AddWithValue("@End", end);

            var exactResult = await exactCmd.ExecuteScalarAsync();
            if (exactResult != null && exactResult != DBNull.Value)
                return Convert.ToInt32(exactResult);

            // 2️⃣ If no exact match, check for overlapping schedule
            const string overlapQuery = @"
SELECT TOP 1 ScheduleID
FROM CoachSchedule
WHERE CoachID = @CoachID 
  AND ScheduledDate = @Date
  AND IsDeleted = 0
  AND (@Start < ScheduledTimeEnd AND @End > ScheduledTimeStart)";

            using var overlapCmd = new SqlCommand(overlapQuery, connection, transaction);
            overlapCmd.Parameters.AddWithValue("@CoachID", coachId);
            overlapCmd.Parameters.AddWithValue("@Date", date.Date);
            overlapCmd.Parameters.AddWithValue("@Start", start);
            overlapCmd.Parameters.AddWithValue("@End", end);

            var overlapResult = await overlapCmd.ExecuteScalarAsync();

            if (overlapResult != null && overlapResult != DBNull.Value)
            {
                // ⚠ Time conflict found
                _toastManager?.CreateToast("Conflict Detected")
                    .WithContent($"Coach already has a schedule overlapping this time slot ({start:hh\\:mm tt}–{end:hh\\:mm tt}).")
                    .ShowWarning();
                return -1;
            }

            // 3️⃣ Otherwise, no schedule at all for that time
            return null;
        }

        /// <summary>
        /// Creates a new coach schedule entry
        /// </summary>
        private async Task<int?> CreateCoachScheduleAsync(SqlConnection connection, SqlTransaction transaction, int coachId, DateTime date, DateTime start, DateTime end)
        {
            const string query = @"
INSERT INTO CoachSchedule (CoachID, ScheduledDate, ScheduledTimeStart, ScheduledTimeEnd, MaxCapacity, CurrentCapacity, CreatedAt)
OUTPUT INSERTED.ScheduleID
VALUES (@CoachID, @Date, @Start, @End, 8, 1, GETDATE())";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@CoachID", coachId);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            cmd.Parameters.AddWithValue("@Start", start);
            cmd.Parameters.AddWithValue("@End", end);

            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent("Failed to create coach schedule.")
                    .ShowError();
                return null;
            }

            return Convert.ToInt32(result);
        }

        /// <summary>
        /// Checks if a schedule has available capacity
        /// </summary>
        private async Task<bool> CheckScheduleCapacityAsync(SqlConnection connection, SqlTransaction transaction, int scheduleId)
        {
            const string query = @"
SELECT CASE WHEN CurrentCapacity < MaxCapacity THEN 1 ELSE 0 END 
FROM CoachSchedule 
WHERE ScheduleID = @ScheduleID";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@ScheduleID", scheduleId);

            var result = await cmd.ExecuteScalarAsync();
            return result != null && Convert.ToInt32(result) == 1;
        }

        /// <summary>
        /// Increments the current capacity of a schedule
        /// </summary>
        private async Task IncrementScheduleCapacityAsync(SqlConnection connection, SqlTransaction transaction, int scheduleId)
        {
            const string query = @"
UPDATE CoachSchedule 
SET CurrentCapacity = CurrentCapacity + 1, 
    UpdatedAt = GETDATE()
WHERE ScheduleID = @ScheduleID 
  AND CurrentCapacity < MaxCapacity";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@ScheduleID", scheduleId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Decrements session count for a customer's package
        /// </summary>
        private async Task DecrementSessionCountAsync(SqlConnection connection, SqlTransaction transaction, int customerID, string customerType, int packageID)
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

                using var command = new SqlCommand(query, connection, transaction);
                command.Parameters.AddWithValue("@CustomerID", customerID);
                command.Parameters.AddWithValue("@PackageID", packageID);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DecrementSessionCountAsync] {ex.Message}");
            }
        }

        #endregion
    }
}
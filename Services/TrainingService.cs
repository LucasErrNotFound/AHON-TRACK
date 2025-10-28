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

        // Constants
        private const int MAX_DAILY_SESSIONS = 8;
        private const int MAX_SCHEDULE_CAPACITY = 8;
        private const string DEFAULT_CUSTOMER_TYPE = "Member";
        private const string DEFAULT_ATTENDANCE = "Pending";

        public TrainingService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _toastManager = toastManager;
        }

        #region Role-Based Access Control

        private bool CanCreate() => IsInRole("Admin", "Staff", "Coach");
        private bool CanUpdate() => IsInRole("Admin", "Staff", "Coach");
        private bool CanDelete() => IsInRole("Admin");
        private bool CanView() => IsInRole("Admin", "Staff", "Coach");

        private bool IsInRole(params string[] roles)
        {
            return roles.Any(role => string.Equals(CurrentUserModel.Role, role, StringComparison.OrdinalIgnoreCase));
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
                ShowAccessDeniedToast("add training schedules");
                return false;
            }

            if (training == null)
                throw new ArgumentNullException(nameof(training));

            var duration = training.scheduledTimeEnd - training.scheduledTimeStart;
            const int MIN_DURATION_MINUTES = 30;

            if (duration.TotalMinutes < MIN_DURATION_MINUTES)
            {
                ShowToast("Invalid Schedule Duration",
                    $"Training schedule must be at least {MIN_DURATION_MINUTES} minutes. Current duration: {duration.TotalMinutes:F0} minutes.",
                    ToastType.Warning);
                return false;
            }

            if (duration.TotalMinutes <= 0)
            {
                ShowToast("Invalid Time Range",
                    "End time must be after start time.",
                    ToastType.Warning);
                return false;
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    // Validate and get coach ID
                    var coachId = await GetCoachIdByNameAsync(connection, transaction, training.assignedCoach);
                    if (!coachId.HasValue)
                    {
                        ShowToast("Coach Not Found", $"Coach '{training.assignedCoach}' does not exist.", ToastType.Error);
                        return false;
                    }

                    // Check daily capacity
                    if (!await CheckDailyCapacityAsync(connection, transaction, coachId.Value, training.scheduledDate))
                    {
                        ShowToast("Coach Limit Reached",
                            $"Coach {training.assignedCoach} has already reached {MAX_DAILY_SESSIONS} sessions for {training.scheduledDate:MMM dd}.",
                            ToastType.Warning);
                        return false;
                    }

                    // Handle schedule (get existing or create new)
                    var scheduleId = await GetOrCreateScheduleAsync(connection, transaction, coachId.Value,
                        training.scheduledDate, training.scheduledTimeStart, training.scheduledTimeEnd);

                    if (!scheduleId.HasValue)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    // Insert training record
                    training.trainingID = await InsertTrainingRecordAsync(connection, transaction, training);
                    if (training.trainingID == 0)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    // Decrement package session count
                    await DecrementSessionCountAsync(connection, transaction,
                        training.customerID,
                        training.customerType?.Trim() ?? DEFAULT_CUSTOMER_TYPE,
                        training.packageID);

                    // Commit and notify
                    transaction.Commit();
                    NotifyTrainingAdded();

                    await LogActionAsync(connection, "CREATE",
                        $"Added training schedule for {training.firstName} {training.lastName} with {training.assignedCoach} on {training.scheduledDate:MMM dd, yyyy}.",
                        true);

                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                ShowToast("Error Adding Schedule", ex.Message, ToastType.Error);
                Debug.WriteLine($"AddTrainingScheduleAsync Error: {ex}");
                return false;
            }
        }

        private async Task<int> InsertTrainingRecordAsync(SqlConnection connection, SqlTransaction transaction, TrainingModel training)
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

            using var cmd = new SqlCommand(query, connection, transaction);

            cmd.Parameters.AddWithValue("@CustomerID", training.customerID);
            cmd.Parameters.AddWithValue("@CustomerType", training.customerType?.Trim() ?? DEFAULT_CUSTOMER_TYPE);
            cmd.Parameters.AddWithValue("@FirstName", training.firstName ?? string.Empty);
            cmd.Parameters.AddWithValue("@LastName", training.lastName ?? string.Empty);
            cmd.Parameters.AddWithValue("@ContactNumber", ToDbValue(training.contactNumber));
            cmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary).Value = ToDbValue(training.picture);
            cmd.Parameters.AddWithValue("@PackageID", training.packageID);
            cmd.Parameters.AddWithValue("@PackageType", training.packageType ?? string.Empty);
            cmd.Parameters.AddWithValue("@AssignedCoach", training.assignedCoach ?? string.Empty);
            cmd.Parameters.AddWithValue("@ScheduledDate", training.scheduledDate.Date);
            cmd.Parameters.AddWithValue("@ScheduledTimeStart", training.scheduledTimeStart);
            cmd.Parameters.AddWithValue("@ScheduledTimeEnd", training.scheduledTimeEnd);
            cmd.Parameters.AddWithValue("@Attendance", training.attendance ?? DEFAULT_ATTENDANCE);
            cmd.Parameters.AddWithValue("@EmployeeID", ToDbValue(CurrentUserModel.UserId));

            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
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
                ShowAccessDeniedToast("view training schedules");
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
                    trainings.Add(MapTrainingFromReader(reader));
                }
            }
            catch (Exception ex)
            {
                ShowToast("Database Error", $"Failed to load training schedules: {ex.Message}", ToastType.Error, 5);
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
                ShowAccessDeniedToast("view training schedules");
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
                    return MapTrainingDetailsFromReader(reader);
                }
            }
            catch (Exception ex)
            {
                ShowToast("Database Error", $"Failed to load training schedule: {ex.Message}", ToastType.Error);
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
                ShowAccessDeniedToast("view trainees");
                return new List<TraineeModel>();
            }

            var trainees = new List<TraineeModel>();

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
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
ORDER BY FirstName, LastName";

                await using var command = new SqlCommand(query, connection);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    trainees.Add(MapTraineeFromReader(reader));
                }
            }
            catch (Exception ex)
            {
                ShowToast("Database Error", $"Failed to load available trainees: {ex.Message}", ToastType.Error, 5);
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
ORDER BY e.FirstName";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    coaches.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2)
                    ));
                }
            }
            catch (Exception ex)
            {
                ShowToast("Database Error", $"Failed to load coaches: {ex.Message}", ToastType.Error, 5);
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
                ShowAccessDeniedToast("update training schedules");
                return false;
            }

            if (training == null)
                throw new ArgumentNullException(nameof(training));

            var duration = training.scheduledTimeEnd - training.scheduledTimeStart;
            const int MIN_DURATION_MINUTES = 30;

            if (duration.TotalMinutes < MIN_DURATION_MINUTES)
            {
                ShowToast("Invalid Schedule Duration",
                    $"Training schedule must be at least {MIN_DURATION_MINUTES} minutes. Current duration: {duration.TotalMinutes:F0} minutes.",
                    ToastType.Warning);
                return false;
            }

            if (duration.TotalMinutes <= 0)
            {
                ShowToast("Invalid Time Range",
                    "End time must be after start time.",
                    ToastType.Warning);
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
                command.Parameters.AddWithValue("@ContactNumber", ToDbValue(training.contactNumber));
                command.Parameters.AddWithValue("@ProfilePicture", ToDbValue(training.picture));
                command.Parameters.AddWithValue("@PackageType", training.packageType);
                command.Parameters.AddWithValue("@AssignedCoach", training.assignedCoach);
                command.Parameters.AddWithValue("@ScheduledDate", training.scheduledDate);
                command.Parameters.AddWithValue("@ScheduledTimeStart", training.scheduledTimeStart);
                command.Parameters.AddWithValue("@ScheduledTimeEnd", training.scheduledTimeEnd);
                command.Parameters.AddWithValue("@Attendance", ToDbValue(training.attendance));

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    DashboardEventService.Instance.NotifyScheduleUpdated();
                    await LogActionAsync(connection, "UPDATE",
                        $"Updated training schedule for {training.firstName} {training.lastName} (ID: {training.trainingID})", true);

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

                ShowToast("Error", $"Error updating training schedule: {ex.Message}", ToastType.Error);
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
                ShowAccessDeniedToast("update attendance");
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

                ShowToast("Error", $"Error updating attendance: {ex.Message}", ToastType.Error);
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
                ShowToast("Access Denied", "Only administrators can delete training schedules.", ToastType.Error);
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

                ShowToast("Error", $"Error deleting training schedule: {ex.Message}", ToastType.Error);
                Debug.WriteLine($"DeleteTrainingScheduleAsync Error: {ex}");
            }

            return false;
        }

        #endregion

        #region HELPER METHODS - Coach & Schedule Management

        private async Task<int?> GetCoachIdByNameAsync(SqlConnection connection, SqlTransaction transaction, string coachFullName)
        {
            const string query = @"
            SELECT TOP 1 c.CoachID
            FROM Coach c
            INNER JOIN Employees e ON c.CoachID = e.EmployeeID
            WHERE (e.FirstName + ' ' + e.LastName) = @CoachFullName
             AND c.IsDeleted = 0";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@CoachFullName", coachFullName?.Trim() ?? string.Empty);

            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : null;
        }

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
            return totalToday < MAX_DAILY_SESSIONS;
        }

        private async Task<int?> GetOrCreateScheduleAsync(SqlConnection connection, SqlTransaction transaction,
            int coachId, DateTime date, DateTime start, DateTime end)
        {
            // Try exact match first
            var scheduleId = await GetExactScheduleAsync(connection, transaction, coachId, date, start, end);
            if (scheduleId.HasValue)
                return scheduleId;

            // Check for overlap
            if (await HasScheduleOverlapAsync(connection, transaction, coachId, date, start, end))
            {
                ShowToast("Conflict Detected",
                    $"Coach already has a schedule overlapping this time slot ({start:hh\\:mm tt}–{end:hh\\:mm tt}).",
                    ToastType.Warning);
                return null;
            }

            // Create new schedule
            return await CreateCoachScheduleAsync(connection, transaction, coachId, date, start, end);
        }

        private async Task<int?> GetExactScheduleAsync(SqlConnection connection, SqlTransaction transaction,
            int coachId, DateTime date, DateTime start, DateTime end)
        {
            const string query = @"
                SELECT TOP 1 ScheduleID 
                FROM CoachSchedule 
                WHERE CoachID = @CoachID 
              AND ScheduledDate = @Date 
              AND ScheduledTimeStart = @Start 
              AND ScheduledTimeEnd = @End 
             AND IsDeleted = 0";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@CoachID", coachId);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            cmd.Parameters.AddWithValue("@Start", start);
            cmd.Parameters.AddWithValue("@End", end);

            var result = await cmd.ExecuteScalarAsync();

            if (result != null && result != DBNull.Value)
            {
                int scheduleId = Convert.ToInt32(result);

                // Check if this schedule has space
                if (!await CheckScheduleCapacityAsync(connection, transaction, scheduleId))
                {
                    ShowToast("Schedule Full", "The selected time slot is already full.", ToastType.Warning);
                    return null;
                }

                // Increment capacity
                await IncrementScheduleCapacityAsync(connection, transaction, scheduleId);
                return scheduleId;
            }

            return null;
        }

        private async Task<bool> HasScheduleOverlapAsync(SqlConnection connection, SqlTransaction transaction,
            int coachId, DateTime date, DateTime start, DateTime end)
        {
            const string query = @"
                SELECT TOP 1 ScheduleID
                FROM CoachSchedule
                WHERE CoachID = @CoachID 
              AND ScheduledDate = @Date
              AND IsDeleted = 0
              AND (@Start < ScheduledTimeEnd AND @End > ScheduledTimeStart)";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@CoachID", coachId);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            cmd.Parameters.AddWithValue("@Start", start);
            cmd.Parameters.AddWithValue("@End", end);

            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value;
        }

        private async Task<int?> CreateCoachScheduleAsync(SqlConnection connection, SqlTransaction transaction,
            int coachId, DateTime date, DateTime start, DateTime end)
        {
            const string query = @"INSERT INTO CoachSchedule (CoachID, ScheduledDate, ScheduledTimeStart, ScheduledTimeEnd, MaxCapacity, CurrentCapacity, CreatedAt)
            OUTPUT INSERTED.ScheduleID
            VALUES (@CoachID, @Date, @Start, @End, @MaxCapacity, 1, GETDATE())";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@CoachID", coachId);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            cmd.Parameters.AddWithValue("@Start", start);
            cmd.Parameters.AddWithValue("@End", end);
            cmd.Parameters.AddWithValue("@MaxCapacity", MAX_SCHEDULE_CAPACITY);

            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
            {
                ShowToast("Error", "Failed to create coach schedule.", ToastType.Error);
                return null;
            }

            return Convert.ToInt32(result);
        }

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

        private async Task DecrementSessionCountAsync(SqlConnection connection, SqlTransaction transaction,
            int customerID, string customerType, int packageID)
        {
            try
            {
                string table = customerType == DEFAULT_CUSTOMER_TYPE ? "MemberSessions" : "WalkInSessions";
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

        #region UTILITY METHODS

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                const string query = @"
INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@username", ToDbValue(CurrentUserModel.Username));
                cmd.Parameters.AddWithValue("@role", ToDbValue(CurrentUserModel.Role));
                cmd.Parameters.AddWithValue("@actionType", ToDbValue(actionType));
                cmd.Parameters.AddWithValue("@description", ToDbValue(description));
                cmd.Parameters.AddWithValue("@success", success);
                cmd.Parameters.AddWithValue("@employeeID", ToDbValue(CurrentUserModel.UserId));

                await cmd.ExecuteNonQueryAsync();
                DashboardEventService.Instance.NotifyTrainingSessionsUpdated();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        private async Task<string> GetTrainingNameByIdAsync(SqlConnection connection, int trainingID)
        {
            const string query = "SELECT FirstName + ' ' + LastName AS FullName FROM Trainings WHERE TrainingID = @TrainingID";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@TrainingID", trainingID);

            var result = await command.ExecuteScalarAsync();
            return result?.ToString() ?? "Unknown Training";
        }

        #endregion

        #region MAPPING METHODS

        private TrainingModel MapTrainingFromReader(SqlDataReader reader)
        {
            return new TrainingModel
            {
                trainingID = reader.GetInt32(reader.GetOrdinal("TrainingID")),
                customerID = reader.GetInt32(reader.GetOrdinal("CustomerID")),
                firstName = reader["FirstName"]?.ToString() ?? string.Empty,
                lastName = reader["LastName"]?.ToString() ?? string.Empty,
                contactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                picture = !reader.IsDBNull(reader.GetOrdinal("ProfilePicture"))
                    ? (byte[])reader["ProfilePicture"]
                    : null,
                packageType = reader["PackageType"]?.ToString() ?? string.Empty,
                assignedCoach = reader["AssignedCoach"]?.ToString() ?? string.Empty,
                scheduledDate = reader.GetDateTime(reader.GetOrdinal("ScheduledDate")),
                scheduledTimeStart = reader.GetDateTime(reader.GetOrdinal("ScheduledTimeStart")),
                scheduledTimeEnd = reader.GetDateTime(reader.GetOrdinal("ScheduledTimeEnd")),
                attendance = reader["Attendance"]?.ToString(),
                createdAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                updatedAt = !reader.IsDBNull(reader.GetOrdinal("UpdatedAt"))
                    ? reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                    : DateTime.MinValue
            };
        }

        private TrainingModel MapTrainingDetailsFromReader(SqlDataReader reader)
        {
            return new TrainingModel
            {
                trainingID = reader.GetInt32(reader.GetOrdinal("TrainingID")),
                customerID = reader.GetInt32(reader.GetOrdinal("CustomerID")),
                customerType = reader["CustomerType"]?.ToString() ?? string.Empty,
                firstName = reader["FirstName"]?.ToString() ?? string.Empty,
                lastName = reader["LastName"]?.ToString() ?? string.Empty,
                contactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                picture = !reader.IsDBNull(reader.GetOrdinal("ProfilePicture"))
                    ? (byte[])reader["ProfilePicture"]
                    : Array.Empty<byte>(),
                packageID = reader.GetInt32(reader.GetOrdinal("PackageID")),
                packageType = reader["PackageType"]?.ToString() ?? string.Empty,
                assignedCoach = reader["AssignedCoach"]?.ToString() ?? string.Empty,
                scheduledDate = reader.GetDateTime(reader.GetOrdinal("ScheduledDate")),
                scheduledTimeStart = reader.GetDateTime(reader.GetOrdinal("ScheduledTimeStart")),
                scheduledTimeEnd = reader.GetDateTime(reader.GetOrdinal("ScheduledTimeEnd")),
                attendance = reader["Attendance"]?.ToString() ?? DEFAULT_ATTENDANCE,
                createdAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                updatedAt = !reader.IsDBNull(reader.GetOrdinal("UpdatedAt"))
                    ? reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                    : DateTime.MinValue,
                addedByEmployeeID = !reader.IsDBNull(reader.GetOrdinal("CreatedByEmployeeID"))
                    ? reader.GetInt32(reader.GetOrdinal("CreatedByEmployeeID"))
                    : 0
            };
        }

        private TraineeModel MapTraineeFromReader(SqlDataReader reader)
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

            return new TraineeModel
            {
                ID = reader.GetInt32(reader.GetOrdinal("CustomerID")),
                CustomerType = reader["CustomerType"]?.ToString() ?? string.Empty,
                FirstName = reader["FirstName"]?.ToString() ?? string.Empty,
                LastName = reader["LastName"]?.ToString() ?? string.Empty,
                ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                PackageType = reader["PackageType"]?.ToString() ?? string.Empty,
                PackageID = reader.GetInt32(reader.GetOrdinal("PackageID")),
                SessionLeft = reader.GetInt32(reader.GetOrdinal("SessionsLeft")),
                Picture = picture ?? new Bitmap(AssetLoader.Open(new Uri("avares://AHON_TRACK/Assets/MainWindowView/user.png")))
            };
        }

        #endregion

        #region TOAST & NOTIFICATION HELPERS

        private enum ToastType
        {
            Success,
            Error,
            Warning,
            Info
        }

        private void ShowToast(string title, string content, ToastType type, int delay = 3)
        {
            if (_toastManager == null) return;

            var toast = _toastManager.CreateToast(title).WithContent(content);

            if (delay != 3)
                toast.WithDelay(delay);

            switch (type)
            {
                case ToastType.Success:
                    toast.ShowSuccess();
                    break;
                case ToastType.Error:
                    toast.ShowError();
                    break;
                case ToastType.Warning:
                    toast.ShowWarning();
                    break;
                case ToastType.Info:
                    toast.ShowInfo();
                    break;
            }
        }

        private void ShowAccessDeniedToast(string action)
        {
            ShowToast("Access Denied", $"You don't have permission to {action}.", ToastType.Error);
        }

        private void NotifyTrainingAdded()
        {
            DashboardEventService.Instance.NotifyScheduleAdded();
            DashboardEventService.Instance.NotifyMemberUpdated();
            DashboardEventService.Instance.NotifyTrainingSessionsUpdated();
        }

        private static object ToDbValue(object? value)
        {
            return value ?? DBNull.Value;
        }

        #endregion
    }
}
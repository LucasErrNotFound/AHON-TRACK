using AHON_TRACK.Converters;
using AHON_TRACK.Helpers;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using AHON_TRACK.Services.Events;
using Dapper;

namespace AHON_TRACK.Services
{
    public class EmployeeService : IEmployeeService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public EmployeeService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        #region Role-Based Access Control

        private bool CanCreate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanUpdate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanDelete() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanView() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;

        #endregion

        #region CREATE

        public async Task<(bool Success, string Message, int? EmployeeId)> AddEmployeeAsync(ManageEmployeeModel employee)
        {
            if (!CanCreate())
            {
                ShowAccessDeniedToast("add employees");
                return (false, "Insufficient permissions to add employees.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    // Check for soft-deleted employee with same username
                    var (deletedId, oldPosition) = await GetDeletedEmployeeByUsernameAsync(conn, transaction, employee.Username);

                    if (deletedId.HasValue)
                    {
                        return await RestoreDeletedEmployeeAsync(conn, transaction, employee, deletedId.Value, oldPosition);
                    }

                    // Proceed with normal add operation
                    if (await IsDuplicateUsernameAsync(conn, transaction, employee.Username))
                    {
                        transaction.Rollback();
                        ShowWarningToast("Duplicate Username", $"Username '{employee.Username}' already exists.");
                        return (false, "Username already exists.", null);
                    }

                    // Validate Gym Admin username doesn't contain "coach"
                    if (IsInvalidAdminUsername(employee))
                    {
                        transaction.Rollback();
                        ShowWarningToast("Invalid Username", "Gym Admin usernames cannot contain 'coach'. This is reserved for Gym Staff who are coaches.");
                        return (false, "Gym Admin cannot have 'coach' in username.", null);
                    }

                    string hashedPassword = PasswordHelper.HashPassword(employee.Password ?? "DefaultPassword123!");
                    int employeeId = await InsertEmployeeAsync(conn, transaction, employee);
                    await InsertIntoRoleTableAsync(conn, transaction, employeeId, employee, hashedPassword);
                    await LogActionAsync(conn, "CREATE", $"Added new {employee.Position}: {employee.FirstName} {employee.LastName}", true, transaction);

                    transaction.Commit();
                    DashboardEventService.Instance.NotifyEmployeeAdded();

                    return (true, "Employee added successfully.", employeeId);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                ShowErrorToast("Database Error", $"Failed to add employee: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"AddEmployeeAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"AddEmployeeAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region READ

        public async Task<(bool Success, string Message, List<ManageEmployeeModel>? Employees)> GetEmployeesAsync()
        {
            if (!CanView())
            {
                ShowAccessDeniedToast("view employees");
                return (false, "Insufficient permissions to view employees.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT 
                        e.EmployeeId,
                        e.FirstName,
                        e.MiddleInitial,
                        e.LastName,
                        LTRIM(RTRIM(e.FirstName + ISNULL(' ' + e.MiddleInitial + '.', '') + ' ' + e.LastName)) AS Name,
                        e.ContactNumber,
                        e.Position,
                        e.Status,
                        e.DateJoined,
                        e.ProfilePicture,
                        COALESCE(a.Username, c.Username, s.Username) AS Username
                    FROM Employees e
                    LEFT JOIN Admins a ON e.EmployeeID = a.AdminID AND a.IsDeleted = 0
                    LEFT JOIN Coach c ON e.EmployeeID = c.CoachID AND c.IsDeleted = 0
                    LEFT JOIN Staffs s ON e.EmployeeID = s.StaffID AND s.IsDeleted = 0
                    WHERE e.IsDeleted = 0
                    ORDER BY Name";

                var employees = new List<ManageEmployeeModel>();
                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    employees.Add(MapEmployeeFromReader(reader));
                }

                return (true, "Employees retrieved successfully.", employees);
            }
            catch (SqlException ex)
            {
                ShowErrorToast("Database Error", $"Failed to load employees: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GetEmployeesAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GetEmployeesAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, ManageEmployeeModel? Employee)> GetEmployeeByIdAsync(int employeeId)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view employee.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT e.EmployeeId, e.FirstName, e.MiddleInitial, e.LastName, e.ContactNumber, 
                           e.Position, e.ProfilePicture, e.Status, e.DateJoined, e.Gender, e.Age, e.DateOfBirth,
                           e.HouseAddress, e.HouseNumber, e.Street, e.Barangay, e.CityTown, e.Province,
                           COALESCE(a.Username, c.Username, s.Username) AS Username
                    FROM Employees e
                    LEFT JOIN Admins a ON e.EmployeeID = a.AdminID AND a.IsDeleted = 0
                    LEFT JOIN Coach c ON e.EmployeeID = c.CoachID AND c.IsDeleted = 0
                    LEFT JOIN Staffs s ON e.EmployeeID = s.StaffID AND s.IsDeleted = 0
                    WHERE e.EmployeeId = @Id AND e.IsDeleted = 0";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", employeeId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var employee = MapEmployeeDetailsFromReader(reader);
                    return (true, "Employee retrieved successfully.", employee);
                }

                return (false, "Employee not found.", null);
            }
            catch (Exception ex)
            {
                ShowErrorToast("Database Error", $"Error retrieving employee: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GetEmployeeByIdAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, ManageEmployeeModel? Employee)> ViewEmployeeProfileAsync(int employeeId)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view employee profile.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT 
                        e.EmployeeID, e.FirstName, e.MiddleInitial, e.LastName, e.Gender, e.ProfilePicture, 
                        e.ContactNumber, e.Age, e.DateOfBirth, e.HouseAddress, e.HouseNumber, e.Street, 
                        e.Barangay, e.CityTown, e.Province, e.Position, e.Status, e.DateJoined,
                        COALESCE(a.Username, c.Username, s.Username) AS Username,
                        COALESCE(a.LastLogin, c.LastLogin, s.LastLogin) AS LastLogin
                    FROM Employees e
                    LEFT JOIN Admins a ON e.EmployeeID = a.AdminID AND a.IsDeleted = 0
                    LEFT JOIN Coach c ON e.EmployeeID = c.CoachID AND c.IsDeleted = 0
                    LEFT JOIN Staffs s ON e.EmployeeID = s.StaffID AND s.IsDeleted = 0
                    WHERE e.EmployeeID = @Id AND e.IsDeleted = 0";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", employeeId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var employee = MapEmployeeProfileFromReader(reader);
                    return (true, "Employee profile retrieved successfully.", employee);
                }

                return (false, "Employee not found.", null);
            }
            catch (Exception ex)
            {
                ShowErrorToast("Database Error", $"Error retrieving profile: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"ViewEmployeeProfileAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<byte[]?> GetEmployeeProfilePictureAsync(int employeeId)
        {
            const string query = @"SELECT ProfilePicture FROM Employees WHERE EmployeeID = @EmployeeID AND IsDeleted = 0";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            return await connection.QueryFirstOrDefaultAsync<byte[]?>(query, new { EmployeeID = employeeId });
        }

        #endregion

        #region UPDATE

        public async Task<(bool Success, string Message)> UpdateEmployeeAsync(ManageEmployeeModel employee)
        {
            if (!CanUpdate())
            {
                ShowAccessDeniedToast("update employee data");
                return (false, "Insufficient permissions to update employees.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    var oldPosition = await GetEmployeePositionAsync(conn, transaction, employee.EmployeeId);
                    if (oldPosition == null)
                    {
                        transaction.Rollback();
                        ShowWarningToast("Employee Not Found", "The employee you're trying to update doesn't exist.");
                        return (false, "Employee not found.");
                    }

                    if (await IsDuplicateUsernameForUpdateAsync(conn, transaction, employee.Username, employee.EmployeeId))
                    {
                        transaction.Rollback();
                        ShowWarningToast("Duplicate Username", $"Another employee with username '{employee.Username}' already exists.");
                        return (false, "Username already exists.");
                    }

                    if (IsInvalidAdminUsername(employee))
                    {
                        transaction.Rollback();
                        ShowWarningToast("Invalid Username", "Gym Admin usernames cannot contain 'coach'. This is reserved for Gym Staff who are coaches.");
                        return (false, "Gym Admin cannot have 'coach' in username.");
                    }

                    string passwordToStore = await GetPasswordForUpdateAsync(conn, transaction, employee);
                    await UpdateEmployeeRecordAsync(conn, transaction, employee);

                    bool positionChanged = !string.Equals(oldPosition, employee.Position, StringComparison.OrdinalIgnoreCase);
                    await HandleRoleTableUpdateAsync(conn, transaction, employee, oldPosition, positionChanged, passwordToStore);

                    await LogActionAsync(conn, "UPDATE", $"Updated employee: {employee.FirstName} {employee.LastName}", true, transaction);
                    transaction.Commit();

                    if (employee.EmployeeId == CurrentUserModel.UserId)
                    {
                        CurrentUserModel.AvatarBytes = employee.ProfilePicture;
                        DashboardEventService.Instance.NotifyEmployeeUpdated();
                        UserProfileEventService.Instance.NotifyProfilePictureUpdated();
                    }

                    return (true, "Employee updated successfully.");
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                ShowErrorToast("Database Error", $"Failed to update employee: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"UpdateEmployeeAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"UpdateEmployeeAsync Error: {ex}");
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, int? EmployeeId)> RestoreEmployeeAsync(string username)
        {
            if (!CanCreate())
            {
                ShowAccessDeniedToast("restore employees");
                return (false, "Insufficient permissions to restore employees.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    var (employeeId, position, employeeName) = await GetDeletedEmployeeInfoAsync(conn, transaction, username);

                    if (!employeeId.HasValue)
                    {
                        transaction.Rollback();
                        ShowWarningToast("Employee Not Found", $"No deleted employee with username '{username}' found.");
                        return (false, "No deleted employee found with this username.", null);
                    }

                    await RestoreEmployeeInDatabaseAsync(conn, transaction, employeeId.Value, position);
                    await LogActionAsync(conn, "RESTORE", $"Restored employee: {employeeName} (ID: {employeeId.Value})", true, transaction);
                    transaction.Commit();

                    ShowSuccessToast("Employee Restored", $"Successfully restored {employeeName}.");
                    return (true, "Employee restored successfully.", employeeId.Value);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                ShowErrorToast("Database Error", $"Failed to restore employee: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"RestoreEmployeeAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"RestoreEmployeeAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region DELETE

        public async Task<(bool Success, string Message)> DeleteEmployeeAsync(int employeeId)
        {
            if (!CanDelete())
            {
                ShowAccessDeniedToast("delete employees");
                return (false, "Insufficient permissions to delete employees.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    var (employeeName, position, isAlreadyDeleted) = await GetEmployeeInfoForDeletionAsync(conn, transaction, employeeId);

                    if (string.IsNullOrEmpty(employeeName))
                    {
                        transaction.Rollback();
                        ShowWarningToast("Employee Not Found", "The employee you're trying to delete doesn't exist.");
                        return (false, "Employee not found.");
                    }

                    if (isAlreadyDeleted)
                    {
                        transaction.Rollback();
                        ShowWarningToast("Already Deleted", $"{employeeName} has already been deleted.");
                        return (false, "Employee is already deleted.");
                    }

                    await SoftDeleteFromRoleTableAsync(conn, transaction, employeeId, position);
                    int rowsAffected = await SoftDeleteEmployeeAsync(conn, transaction, employeeId);

                    if (rowsAffected > 0)
                    {
                        await LogActionAsync(conn, "DELETE", $"Soft deleted employee: {employeeName} (ID: {employeeId})", true, transaction);
                        transaction.Commit();

                        DashboardEventService.Instance.NotifyEmployeeUpdated();
                        ShowSuccessToast("Employee Deleted", $"Successfully deleted {employeeName}.");

                        return (true, "Employee deleted successfully.");
                    }

                    transaction.Rollback();
                    return (false, "Failed to delete employee.");
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                ShowErrorToast("Database Error", $"Failed to delete employee: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"DeleteEmployeeAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"DeleteEmployeeAsync Error: {ex}");
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region AUTHENTICATION

        public async Task<(bool Success, string Message, int? EmployeeId, string? Role)> AuthenticateUserAsync(string username, string password)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Try Admin
                const string adminQuery = @"
                    SELECT a.AdminID, a.Password, e.FirstName, e.LastName, e.Status
                    FROM Admins a
                    INNER JOIN Employees e ON a.AdminID = e.EmployeeID
                    WHERE a.Username = @Username";

                using (var adminCmd = new SqlCommand(adminQuery, conn))
                {
                    adminCmd.Parameters.AddWithValue("@Username", username);

                    using var adminReader = await adminCmd.ExecuteReaderAsync();
                    if (await adminReader.ReadAsync())
                    {
                        string hashedPassword = adminReader.GetString(1);
                        string status = adminReader.GetString(4);

                        if (PasswordHelper.VerifyPassword(password, hashedPassword))
                        {
                            if (status != "Active")
                            {
                                await LogActionAsync(conn, "Login", $"Admin '{username}' attempted to log in (inactive account).", false);
                                return (false, "Account is not active.", null, null);
                            }

                            int employeeId = adminReader.GetInt32(0);
                            adminReader.Close();

                            using var updateLoginCmd = new SqlCommand(
                                "UPDATE Admins SET LastLogin = GETDATE() WHERE AdminID = @AdminID", conn);
                            updateLoginCmd.Parameters.AddWithValue("@AdminID", employeeId);
                            await updateLoginCmd.ExecuteNonQueryAsync();

                            CurrentUserModel.UserId = employeeId;
                            CurrentUserModel.Username = username;
                            CurrentUserModel.Role = "Admin";
                            await LogActionAsync(conn, "Login", $"Admin '{username}' logged in successfully.", true);

                            return (true, "Login successful.", employeeId, "Admin");
                        }
                        else
                        {
                            adminReader.Close();
                            await LogActionAsync(conn, "Login", $"Admin '{username}' failed login (wrong password).", false);
                            return (false, "Invalid username or password.", null, null);
                        }
                    }
                }

                // Try Coach
                const string coachQuery = @"
                    SELECT c.CoachID, c.Password, e.FirstName, e.LastName, e.Status
                    FROM Coach c
                    INNER JOIN Employees e ON c.CoachID = e.EmployeeID
                    WHERE c.Username = @Username AND c.IsDeleted = 0";

                using (var coachCmd = new SqlCommand(coachQuery, conn))
                {
                    coachCmd.Parameters.AddWithValue("@Username", username);

                    using var coachReader = await coachCmd.ExecuteReaderAsync();
                    if (await coachReader.ReadAsync())
                    {
                        string hashedPassword = coachReader.GetString(1);
                        string status = coachReader.GetString(4);

                        if (PasswordHelper.VerifyPassword(password, hashedPassword))
                        {
                            if (status != "Active")
                            {
                                await LogActionAsync(conn, "Login", $"Coach '{username}' attempted to log in (inactive account).", false);
                                return (false, "Account is not active.", null, null);
                            }

                            int employeeId = coachReader.GetInt32(0);
                            coachReader.Close();

                            using var updateLoginCmd = new SqlCommand(
                                "UPDATE Coach SET LastLogin = GETDATE() WHERE CoachID = @CoachID", conn);
                            updateLoginCmd.Parameters.AddWithValue("@CoachID", employeeId);
                            await updateLoginCmd.ExecuteNonQueryAsync();

                            CurrentUserModel.UserId = employeeId;
                            CurrentUserModel.Username = username;
                            CurrentUserModel.Role = "Coach";
                            await LogActionAsync(conn, "Login", $"Coach '{username}' logged in successfully.", true);

                            return (true, "Login successful.", employeeId, "Coach");
                        }
                        else
                        {
                            coachReader.Close();
                            await LogActionAsync(conn, "Login", $"Coach '{username}' failed login (wrong password).", false);
                            return (false, "Invalid username or password.", null, null);
                        }
                    }
                }

                // Try Staff
                const string staffQuery = @"
                    SELECT s.StaffID, s.Password, e.FirstName, e.LastName, e.Status
                    FROM Staffs s
                    INNER JOIN Employees e ON s.StaffID = e.EmployeeID
                    WHERE s.Username = @Username";

                using (var staffCmd = new SqlCommand(staffQuery, conn))
                {
                    staffCmd.Parameters.AddWithValue("@Username", username);

                    using var staffReader = await staffCmd.ExecuteReaderAsync();
                    if (await staffReader.ReadAsync())
                    {
                        string hashedPassword = staffReader.GetString(1);
                        string status = staffReader.GetString(4);

                        if (PasswordHelper.VerifyPassword(password, hashedPassword))
                        {
                            if (status != "Active")
                            {
                                await LogActionAsync(conn, "Login", $"Staff '{username}' attempted to log in (inactive account).", false);
                                return (false, "Account is not active.", null, null);
                            }

                            int employeeId = staffReader.GetInt32(0);
                            staffReader.Close();

                            using var updateLoginCmd = new SqlCommand(
                                "UPDATE Staffs SET LastLogin = GETDATE() WHERE StaffID = @StaffID", conn);
                            updateLoginCmd.Parameters.AddWithValue("@StaffID", employeeId);
                            await updateLoginCmd.ExecuteNonQueryAsync();

                            CurrentUserModel.UserId = employeeId;
                            CurrentUserModel.Username = username;
                            CurrentUserModel.Role = "Staff";
                            await LogActionAsync(conn, "Login", $"Staff '{username}' logged in successfully.", true);

                            return (true, "Login successful.", employeeId, "Staff");
                        }
                        else
                        {
                            staffReader.Close();
                            await LogActionAsync(conn, "Login", $"Staff '{username}' failed login (wrong password).", false);
                            return (false, "Invalid username or password.", null, null);
                        }
                    }
                }

                await LogActionAsync(conn, "Login", $"Unknown username '{username}' attempted to log in.", false);
                return (false, "Invalid username or password.", null, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AuthenticateUserAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null, null);
            }
        }

        #endregion

        #region UTILITY METHODS

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success, SqlTransaction transaction = null)
        {
            try
            {
                using var logCmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                      VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)",
                    conn, transaction);

                logCmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@success", success);
                logCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                await logCmd.ExecuteNonQueryAsync();

                DashboardEventService.Instance.NotifyRecentLogsUpdated();
                DashboardEventService.Instance.NotifyEmployeeUpdated();
                DashboardEventService.Instance.NotifyEmployeeAdded();
                DashboardEventService.Instance.NotifyEmployeeDeleted();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        public async Task<int> GetTotalEmployeeCountAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = "SELECT COUNT(*) FROM Employees WHERE IsDeleted = 0";
                using var cmd = new SqlCommand(query, conn);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTotalEmployeeCountAsync Error: {ex}");
                return 0;
            }
        }

        public async Task<int> GetEmployeeCountByStatusAsync(string status)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = "SELECT COUNT(*) FROM Employees WHERE Status = @Status AND IsDeleted = 0";
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Status", status);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetEmployeeCountByStatusAsync Error: {ex}");
                return 0;
            }
        }

        public async Task<(bool Success, int ActiveCount, int InactiveCount, int TerminatedCount)> GetEmployeeStatisticsAsync()
        {
            if (!CanView())
            {
                return (false, 0, 0, 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT 
                        SUM(CASE WHEN Status = 'Active' THEN 1 ELSE 0 END) as ActiveCount,
                        SUM(CASE WHEN Status = 'Inactive' THEN 1 ELSE 0 END) as InactiveCount,
                        SUM(CASE WHEN Status = 'Terminated' THEN 1 ELSE 0 END) as TerminatedCount
                      FROM Employees 
                      WHERE IsDeleted = 0", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (true,
                        reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        reader.IsDBNull(2) ? 0 : reader.GetInt32(2));
                }

                return (false, 0, 0, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetEmployeeStatisticsAsync] {ex.Message}");
                return (false, 0, 0, 0);
            }
        }

        #endregion

        #region PRIVATE HELPER METHODS

        // Toast notification helpers
        private void ShowAccessDeniedToast(string action) =>
            _toastManager?.CreateToast("Access Denied")
                .WithContent($"Only administrators can {action}.")
                .DismissOnClick()
                .ShowError();

        private void ShowWarningToast(string title, string message) =>
            _toastManager?.CreateToast(title)
                .WithContent(message)
                .DismissOnClick()
                .ShowWarning();

        private void ShowSuccessToast(string title, string message) =>
            _toastManager?.CreateToast(title)
                .WithContent(message)
                .DismissOnClick()
                .ShowSuccess();

        private void ShowErrorToast(string title, string message) =>
            _toastManager?.CreateToast(title)
                .WithContent(message)
                .DismissOnClick()
                .ShowError();

        // Database query helpers
        private static bool IsCoachUsername(ReadOnlySpan<char> username)
        {
            return username.Contains("coach", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInvalidAdminUsername(ManageEmployeeModel employee) =>
            employee.Position?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true &&
            employee.Username?.IndexOf("Coach", StringComparison.OrdinalIgnoreCase) >= 0;

        private async Task<(int? employeeId, string? position)> GetDeletedEmployeeByUsernameAsync(
            SqlConnection conn, SqlTransaction transaction, string username)
        {
            const string query = @"
                SELECT e.EmployeeID, e.Position
                FROM Employees e
                LEFT JOIN Admins a ON e.EmployeeID = a.AdminID
                LEFT JOIN Coach c ON e.EmployeeID = c.CoachID
                LEFT JOIN Staffs s ON e.EmployeeID = s.StaffID
                WHERE (a.Username = @username OR c.Username = @username OR s.Username = @username)
                AND e.IsDeleted = 1";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@username", username ?? (object)DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1));
            }

            return (null, null);
        }

        private async Task<bool> IsDuplicateUsernameAsync(
            SqlConnection conn, SqlTransaction transaction, string username)
        {
            const string query = @"
                SELECT COUNT(*) FROM 
                (SELECT Username FROM Admins WHERE Username = @username AND IsDeleted = 0
                 UNION ALL
                 SELECT Username FROM Coach WHERE Username = @username AND IsDeleted = 0
                 UNION ALL
                 SELECT Username FROM Staffs WHERE Username = @username AND IsDeleted = 0) AS AllUsernames";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@username", username ?? (object)DBNull.Value);

            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private async Task<bool> IsDuplicateUsernameForUpdateAsync(
            SqlConnection conn, SqlTransaction transaction, string username, int employeeId)
        {
            const string query = @"
                SELECT COUNT(*) FROM 
                (SELECT Username FROM Admins WHERE Username = @username AND AdminID != @employeeId AND IsDeleted = 0
                 UNION ALL
                 SELECT Username FROM Coach WHERE Username = @username AND CoachID != @employeeId AND IsDeleted = 0
                 UNION ALL
                 SELECT Username FROM Staffs WHERE Username = @username AND StaffID != @employeeId AND IsDeleted = 0) AS AllUsernames";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@username", username ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);

            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private async Task<int> InsertEmployeeAsync(
            SqlConnection conn, SqlTransaction transaction, ManageEmployeeModel employee)
        {
            const string query = @"
                INSERT INTO Employees 
                (FirstName, MiddleInitial, LastName, Gender, ProfilePicture, ContactNumber, Age, DateOfBirth, 
                 HouseAddress, HouseNumber, Street, Barangay, CityTown, Province, 
                 DateJoined, Status, Position, CreatedAt)
                OUTPUT INSERTED.EmployeeId
                VALUES 
                (@FirstName, @MiddleInitial, @LastName, @Gender, @ProfilePicture, @ContactNumber, @Age, @DateOfBirth, 
                 @HouseAddress, @HouseNumber, @Street, @Barangay, @CityTown, @Province, 
                 @DateJoined, @Status, @Position, GETDATE())";

            using var cmd = new SqlCommand(query, conn, transaction);
            AddEmployeeParameters(cmd, employee);

            return (int)await cmd.ExecuteScalarAsync();
        }

        private async Task InsertIntoRoleTableAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, ManageEmployeeModel employee, string hashedPassword)
        {
            if (employee.Position?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true)
            {
                await InsertIntoAdminsAsync(conn, transaction, employeeId, employee.Username, hashedPassword);
            }
            else if (employee.Position?.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (IsCoachUsername(employee.Username))
                {
                    await InsertIntoCoachAsync(conn, transaction, employeeId, employee.Username, hashedPassword);
                }
                else
                {
                    await InsertIntoStaffsAsync(conn, transaction, employeeId, employee.Username, hashedPassword);
                }
            }
        }

        private async Task InsertIntoAdminsAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, string username, string password)
        {
            const string query = @"
                INSERT INTO Admins (AdminID, Username, Password, CreatedAt)
                VALUES (@AdminID, @Username, @Password, GETDATE())";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@AdminID", employeeId);
            cmd.Parameters.AddWithValue("@Username", username ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Password", password);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task InsertIntoCoachAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, string username, string password)
        {
            const string query = @"
                INSERT INTO Coach (CoachID, Username, Password, CreatedAt)
                VALUES (@CoachID, @Username, @Password, GETDATE())";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@CoachID", employeeId);
            cmd.Parameters.AddWithValue("@Username", username ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Password", password);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task InsertIntoStaffsAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, string username, string password)
        {
            const string query = @"
                INSERT INTO Staffs (StaffID, Username, Password, CreatedAt)
                VALUES (@StaffID, @Username, @Password, GETDATE())";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@StaffID", employeeId);
            cmd.Parameters.AddWithValue("@Username", username ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Password", password);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<(bool Success, string Message, int? EmployeeId)> RestoreDeletedEmployeeAsync(
            SqlConnection conn, SqlTransaction transaction, ManageEmployeeModel employee, int deletedEmployeeId, string oldPosition)
        {
            // Validate Gym Admin username doesn't contain "coach"
            if (IsInvalidAdminUsername(employee))
            {
                transaction.Rollback();
                ShowWarningToast("Invalid Username", "Gym Admin usernames cannot contain 'coach'. This is reserved for Gym Staff who are coaches.");
                return (false, "Gym Admin cannot have 'coach' in username.", null);
            }

            string hashedPassword = PasswordHelper.HashPassword(employee.Password ?? "DefaultPassword123!");

            await UpdateEmployeeRecordAsync(conn, transaction, employee, deletedEmployeeId);

            bool positionChanged = !string.Equals(oldPosition, employee.Position, StringComparison.OrdinalIgnoreCase);
            await HandleRoleTableUpdateAsync(conn, transaction, employee, oldPosition, positionChanged, hashedPassword, deletedEmployeeId);

            await LogActionAsync(conn, "RESTORE", $"Restored and updated {employee.Position}: {employee.FirstName} {employee.LastName}", true, transaction);
            transaction.Commit();

            ShowSuccessToast("Employee Restored", $"Successfully restored {employee.FirstName} {employee.LastName}.");
            return (true, "Employee restored successfully.", deletedEmployeeId);
        }

        private async Task UpdateEmployeeRecordAsync(
            SqlConnection conn, SqlTransaction transaction, ManageEmployeeModel employee, int? employeeIdOverride = null)
        {
            const string query = @"
                UPDATE Employees 
                SET FirstName = @FirstName, 
                    MiddleInitial = @MiddleInitial, 
                    LastName = @LastName, 
                    Gender = @Gender,
                    ProfilePicture = @ProfilePicture,
                    ContactNumber = @ContactNumber, 
                    Age = @Age,
                    DateOfBirth = @DateOfBirth,
                    HouseAddress = @HouseAddress,
                    HouseNumber = @HouseNumber,
                    Street = @Street,
                    Barangay = @Barangay,
                    CityTown = @CityTown,
                    Province = @Province,
                    DateJoined = @DateJoined,
                    Position = @Position,
                    Status = @Status,
                    IsDeleted = 0,
                    UpdatedAt = GETDATE()
                WHERE EmployeeId = @EmployeeId";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@EmployeeId", employeeIdOverride ?? employee.EmployeeId);
            AddEmployeeParameters(cmd, employee);

            await cmd.ExecuteNonQueryAsync();
        }

        private void AddEmployeeParameters(SqlCommand cmd, ManageEmployeeModel employee)
        {
            cmd.Parameters.AddWithValue("@FirstName", employee.FirstName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MiddleInitial", string.IsNullOrWhiteSpace(employee.MiddleInitial) ? (object)DBNull.Value : employee.MiddleInitial);
            cmd.Parameters.AddWithValue("@LastName", employee.LastName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Gender", employee.Gender ?? (object)DBNull.Value);
            cmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary, -1).Value = employee.ProfilePicture ?? (object)DBNull.Value;
            cmd.Parameters.AddWithValue("@ContactNumber", employee.ContactNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Age", employee.Age > 0 ? employee.Age : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateOfBirth", employee.DateOfBirth ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@HouseAddress", employee.HouseAddress ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@HouseNumber", employee.HouseNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Street", employee.Street ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Barangay", employee.Barangay ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CityTown", employee.CityTown ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Province", employee.Province ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateJoined", employee.DateJoined == default ? DateTime.Now : employee.DateJoined);
            cmd.Parameters.AddWithValue("@Status", employee.Status ?? "Active");
            cmd.Parameters.AddWithValue("@Position", employee.Position ?? (object)DBNull.Value);
        }

        private async Task HandleRoleTableUpdateAsync(
            SqlConnection conn, SqlTransaction transaction, ManageEmployeeModel employee,
            string oldPosition, bool positionChanged, string passwordToStore, int? employeeIdOverride = null)
        {
            int empId = employeeIdOverride ?? employee.EmployeeId;

            if (positionChanged)
            {
                await DeleteFromOldRoleTableAsync(conn, transaction, empId, oldPosition);
                await InsertIntoNewRoleTableAsync(conn, transaction, empId, employee, passwordToStore);
            }
            else
            {
                await UpdateCurrentRoleTableAsync(conn, transaction, empId, employee, passwordToStore);
            }
        }

        private async Task DeleteFromOldRoleTableAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, string oldPosition)
        {
            if (oldPosition?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true)
            {
                using var cmd = new SqlCommand("DELETE FROM Admins WHERE AdminID = @EmployeeId", conn, transaction);
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                await cmd.ExecuteNonQueryAsync();
            }
            else if (oldPosition?.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase) == true)
            {
                bool isInCoachTable = await IsEmployeeInCoachTableAsync(conn, transaction, employeeId);

                if (isInCoachTable)
                {
                    using var cmd = new SqlCommand("DELETE FROM Coach WHERE CoachID = @EmployeeId", conn, transaction);
                    cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    using var cmd = new SqlCommand("DELETE FROM Staffs WHERE StaffID = @EmployeeId", conn, transaction);
                    cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task InsertIntoNewRoleTableAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, ManageEmployeeModel employee, string password)
        {
            if (employee.Position?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true)
            {
                await InsertIntoAdminsAsync(conn, transaction, employeeId, employee.Username, password);
            }
            else if (employee.Position?.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (IsCoachUsername(employee.Username))
                {
                    await InsertIntoCoachAsync(conn, transaction, employeeId, employee.Username, password);
                }
                else
                {
                    await InsertIntoStaffsAsync(conn, transaction, employeeId, employee.Username, password);
                }
            }
        }

        private async Task UpdateCurrentRoleTableAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, ManageEmployeeModel employee, string password)
        {
            if (employee.Position?.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase) == true)
            {
                bool isCurrentlyInCoachTable = await IsEmployeeInCoachTableAsync(conn, transaction, employeeId);
                bool shouldBeInCoachTable = IsCoachUsername(employee.Username);

                if (isCurrentlyInCoachTable && !shouldBeInCoachTable)
                {
                    // Move from Coach to Staffs
                    await DeleteFromCoachTableAsync(conn, transaction, employeeId);
                    await InsertIntoStaffsAsync(conn, transaction, employeeId, employee.Username, password);
                }
                else if (!isCurrentlyInCoachTable && shouldBeInCoachTable)
                {
                    // Move from Staffs to Coach
                    await DeleteFromStaffsTableAsync(conn, transaction, employeeId);
                    await InsertIntoCoachAsync(conn, transaction, employeeId, employee.Username, password);
                }
                else
                {
                    // Stay in same table, just update
                    if (isCurrentlyInCoachTable)
                    {
                        await UpdateCoachAsync(conn, transaction, employeeId, employee.Username, password);
                    }
                    else
                    {
                        await UpdateStaffAsync(conn, transaction, employeeId, employee.Username, password);
                    }
                }
            }
            else if (employee.Position?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true)
            {
                await UpdateAdminAsync(conn, transaction, employeeId, employee.Username, password);
            }
        }

        private async Task<bool> IsEmployeeInCoachTableAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId)
        {
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM Coach WHERE CoachID = @EmployeeId", conn, transaction);
            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private async Task DeleteFromCoachTableAsync(SqlConnection conn, SqlTransaction transaction, int employeeId)
        {
            using var cmd = new SqlCommand("DELETE FROM Coach WHERE CoachID = @EmployeeId", conn, transaction);
            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task DeleteFromStaffsTableAsync(SqlConnection conn, SqlTransaction transaction, int employeeId)
        {
            using var cmd = new SqlCommand("DELETE FROM Staffs WHERE StaffID = @EmployeeId", conn, transaction);
            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateAdminAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, string username, string password)
        {
            const string query = @"
                UPDATE Admins 
                SET Username = @Username, Password = @Password, UpdatedAt = GETDATE()
                WHERE AdminID = @EmployeeId";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
            cmd.Parameters.AddWithValue("@Username", username ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Password", password ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateCoachAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, string username, string password)
        {
            const string query = @"
                UPDATE Coach 
                SET Username = @Username, Password = @Password, UpdatedAt = GETDATE()
                WHERE CoachID = @EmployeeId";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
            cmd.Parameters.AddWithValue("@Username", username ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Password", password ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateStaffAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, string username, string password)
        {
            const string query = @"
                UPDATE Staffs 
                SET Username = @Username, Password = @Password, UpdatedAt = GETDATE()
                WHERE StaffID = @EmployeeId";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
            cmd.Parameters.AddWithValue("@Username", username ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Password", password ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<string?> GetEmployeePositionAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId)
        {
            using var cmd = new SqlCommand(
                "SELECT Position FROM Employees WHERE EmployeeId = @employeeId AND IsDeleted = 0",
                conn, transaction);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);

            return (await cmd.ExecuteScalarAsync())?.ToString();
        }

        private async Task<string> GetPasswordForUpdateAsync(
            SqlConnection conn, SqlTransaction transaction, ManageEmployeeModel employee)
        {
            if (!string.IsNullOrWhiteSpace(employee.Password) && employee.Password != "********")
            {
                return PasswordHelper.HashPassword(employee.Password);
            }

            const string query = @"
                SELECT COALESCE(a.Password, c.Password, s.Password) AS CurrentPassword
                FROM Employees e
                LEFT JOIN Admins a ON e.EmployeeID = a.AdminID AND a.IsDeleted = 0
                LEFT JOIN Coach c ON e.EmployeeID = c.CoachID AND c.IsDeleted = 0
                LEFT JOIN Staffs s ON e.EmployeeID = s.StaffID AND s.IsDeleted = 0
                WHERE e.EmployeeId = @employeeId";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@employeeId", employee.EmployeeId);

            return (await cmd.ExecuteScalarAsync())?.ToString() ?? string.Empty;
        }

        private async Task<(int? employeeId, string? position, string? employeeName)> GetDeletedEmployeeInfoAsync(
            SqlConnection conn, SqlTransaction transaction, string username)
        {
            const string query = @"
                SELECT e.EmployeeID, e.Position, e.FirstName, e.LastName
                FROM Employees e
                LEFT JOIN Admins a ON e.EmployeeID = a.AdminID
                LEFT JOIN Coach c ON e.EmployeeID = c.CoachID
                LEFT JOIN Staffs s ON e.EmployeeID = s.StaffID
                WHERE (a.Username = @username OR c.Username = @username OR s.Username = @username)
                AND e.IsDeleted = 1";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@username", username);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0),
                       reader.IsDBNull(1) ? null : reader.GetString(1),
                       $"{reader.GetString(2)} {reader.GetString(3)}");
            }

            return (null, null, null);
        }

        private async Task RestoreEmployeeInDatabaseAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, string position)
        {
            // Restore in Employees table
            using var restoreEmpCmd = new SqlCommand(
                "UPDATE Employees SET IsDeleted = 0, UpdatedAt = GETDATE() WHERE EmployeeId = @employeeId",
                conn, transaction);
            restoreEmpCmd.Parameters.AddWithValue("@employeeId", employeeId);
            await restoreEmpCmd.ExecuteNonQueryAsync();

            // Restore in role table
            if (position?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true)
            {
                await RestoreInAdminsAsync(conn, transaction, employeeId);
            }
            else if (position?.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase) == true)
            {
                bool isInCoachTable = await IsEmployeeInCoachTableAsync(conn, transaction, employeeId);

                if (isInCoachTable)
                {
                    await RestoreInCoachAsync(conn, transaction, employeeId);
                }
                else
                {
                    await RestoreInStaffsAsync(conn, transaction, employeeId);
                }
            }
        }

        private async Task RestoreInAdminsAsync(SqlConnection conn, SqlTransaction transaction, int employeeId)
        {
            using var cmd = new SqlCommand(
                "UPDATE Admins SET IsDeleted = 0, UpdatedAt = GETDATE() WHERE AdminID = @employeeId",
                conn, transaction);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task RestoreInCoachAsync(SqlConnection conn, SqlTransaction transaction, int employeeId)
        {
            using var cmd = new SqlCommand(
                "UPDATE Coach SET IsDeleted = 0, UpdatedAt = GETDATE() WHERE CoachID = @employeeId",
                conn, transaction);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task RestoreInStaffsAsync(SqlConnection conn, SqlTransaction transaction, int employeeId)
        {
            using var cmd = new SqlCommand(
                "UPDATE Staffs SET IsDeleted = 0, UpdatedAt = GETDATE() WHERE StaffID = @employeeId",
                conn, transaction);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<(string employeeName, string position, bool isAlreadyDeleted)> GetEmployeeInfoForDeletionAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId)
        {
            using var cmd = new SqlCommand(
                "SELECT FirstName, LastName, Position, IsDeleted FROM Employees WHERE EmployeeId = @employeeId",
                conn, transaction);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return ($"{reader[0]} {reader[1]}",
                       reader[2]?.ToString() ?? string.Empty,
                       !reader.IsDBNull(3) && reader.GetBoolean(3));
            }

            return (string.Empty, string.Empty, false);
        }

        private async Task SoftDeleteFromRoleTableAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId, string position)
        {
            if (position.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase))
            {
                using var cmd = new SqlCommand(
                    "UPDATE Admins SET IsDeleted = 1, UpdatedAt = GETDATE() WHERE AdminID = @employeeId",
                    conn, transaction);
                cmd.Parameters.AddWithValue("@employeeId", employeeId);
                await cmd.ExecuteNonQueryAsync();
            }
            else if (position.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase))
            {
                bool isInCoachTable = await IsEmployeeInCoachTableAsync(conn, transaction, employeeId);

                if (isInCoachTable)
                {
                    using var cmd = new SqlCommand(
                        "UPDATE Coach SET IsDeleted = 1, UpdatedAt = GETDATE() WHERE CoachID = @employeeId",
                        conn, transaction);
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    using var cmd = new SqlCommand(
                        "UPDATE Staffs SET IsDeleted = 1, UpdatedAt = GETDATE() WHERE StaffID = @employeeId",
                        conn, transaction);
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<int> SoftDeleteEmployeeAsync(
            SqlConnection conn, SqlTransaction transaction, int employeeId)
        {
            using var cmd = new SqlCommand(
                "UPDATE Employees SET IsDeleted = 1, UpdatedAt = GETDATE() WHERE EmployeeId = @employeeId",
                conn, transaction);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);
            return await cmd.ExecuteNonQueryAsync();
        }

        private ManageEmployeeModel MapEmployeeFromReader(SqlDataReader reader)
        {
            return new ManageEmployeeModel
            {
                ID = reader.GetInt32(0),
                EmployeeId = reader.GetInt32(0),
                FirstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                MiddleInitial = reader.IsDBNull(2) ? null : reader.GetString(2),
                LastName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Name = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                ContactNumber = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Position = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Status = reader.IsDBNull(7) ? "Active" : reader.GetString(7),
                DateJoined = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                AvatarBytes = reader.IsDBNull(9) ? null : (byte[])reader[9],
                AvatarSource = reader.IsDBNull(9)
                    ? ImageHelper.GetDefaultAvatar()
                    : ImageHelper.BytesToBitmap((byte[])reader[9]),
                Username = reader.IsDBNull(10) ? string.Empty : reader.GetString(10)
            };
        }

        private ManageEmployeeModel MapEmployeeDetailsFromReader(SqlDataReader reader)
        {
            var employee = new ManageEmployeeModel
            {
                ID = reader.GetInt32(0),
                EmployeeId = reader.GetInt32(0),
                FirstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                MiddleInitial = reader.IsDBNull(2) ? null : reader.GetString(2),
                LastName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                ContactNumber = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Position = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Status = reader.IsDBNull(7) ? "Active" : reader.GetString(7),
                DateJoined = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                Gender = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                Age = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                DateOfBirth = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                HouseAddress = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                HouseNumber = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                Street = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                Barangay = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                CityTown = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                Province = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
                Username = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                Password = "********",
                AvatarBytes = reader.IsDBNull(6) ? null : (byte[])reader[6],
                AvatarSource = reader.IsDBNull(6)
                    ? ImageHelper.GetDefaultAvatar()
                    : ImageHelper.BytesToBitmap((byte[])reader[6])
            };

            employee.Name = $"{employee.FirstName} {(string.IsNullOrWhiteSpace(employee.MiddleInitial) ? "" : employee.MiddleInitial + ". ")}{employee.LastName}";
            return employee;
        }

        private ManageEmployeeModel MapEmployeeProfileFromReader(SqlDataReader reader)
        {
            var employee = new ManageEmployeeModel
            {
                ID = reader.GetInt32(0),
                EmployeeId = reader.GetInt32(0),
                FirstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                MiddleInitial = reader.IsDBNull(2) ? null : reader.GetString(2),
                LastName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Gender = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                ContactNumber = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Age = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                DateOfBirth = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                HouseAddress = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                HouseNumber = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                Street = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                Barangay = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                CityTown = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                Province = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                Position = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                Status = reader.IsDBNull(16) ? "Active" : reader.GetString(16),
                DateJoined = reader.IsDBNull(17) ? DateTime.MinValue : reader.GetDateTime(17),
                Username = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                Password = "********",
                LastLogin = reader.IsDBNull(19) ? "Never logged in" : reader.GetDateTime(19).ToString("MMMM dd, yyyy h:mm tt"),
                AvatarBytes = reader.IsDBNull(5) ? null : (byte[])reader[5],
                AvatarSource = reader.IsDBNull(5)
                    ? ImageHelper.GetDefaultAvatar()
                    : ImageHelper.BytesToBitmap((byte[])reader[5])
            };

            employee.Name = $"{employee.FirstName} {(string.IsNullOrWhiteSpace(employee.MiddleInitial) ? "" : employee.MiddleInitial + ". ")}{employee.LastName}";
            employee.Birthdate = employee.DateOfBirth?.ToString("MMMM d, yyyy") ?? string.Empty;
            employee.CityProvince = $"{employee.CityTown}, {employee.Province}".TrimEnd(',', ' ');

            return employee;
        }

        #endregion
    }
}
using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Microsoft.Data.SqlClient;
using ShadUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

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

        private bool CanCreate()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanUpdate()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanDelete()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanView()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true;
        }

        #endregion

        #region CREATE

        public async Task<(bool Success, string Message, int? EmployeeId)> AddEmployeeAsync(ManageEmployeeModel employee)
        {
            if (!CanCreate())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("Only administrators can add employees.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to add employees.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check for duplicate username
                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Employees WHERE Username = @username", conn);
                checkCmd.Parameters.AddWithValue("@username", employee.Username ?? (object)DBNull.Value);

                var count = (int)await checkCmd.ExecuteScalarAsync();
                if (count > 0)
                {
                    _toastManager?.CreateToast("Duplicate Username")
                        .WithContent($"Username '{employee.Username}' already exists.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Username already exists.", null);
                }

                // Insert into Employees table
                string employeeQuery = @"
                    INSERT INTO Employees 
                    (FirstName, MiddleInitial, LastName, Gender, ProfilePicture, ContactNumber, Age, DateOfBirth, 
                     HouseAddress, HouseNumber, Street, Barangay, CityTown, Province, 
                     Username, Password, Status, Position)
                    OUTPUT INSERTED.EmployeeId
                    VALUES 
                    (@FirstName, @MiddleInitial, @LastName, @Gender, @ProfilePicture, @ContactNumber, @Age, @DateOfBirth, 
                     @HouseAddress, @HouseNumber, @Street, @Barangay, @CityTown, @Province, 
                     @Username, @Password, @Status, @Position)";

                using var cmd = new SqlCommand(employeeQuery, conn);
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
                cmd.Parameters.AddWithValue("@Username", employee.Username ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Password", employee.Password ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", employee.Status ?? "Active");
                cmd.Parameters.AddWithValue("@Position", employee.Position ?? (object)DBNull.Value);

                int employeeId = (int)await cmd.ExecuteScalarAsync();

                // Insert into Admins or Staffs table
                if (employee.Position?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true)
                {
                    string adminQuery = @"INSERT INTO Admins (EmployeeID, Username, Password) 
                                         VALUES (@EmployeeID, @Username, @Password)";
                    using var adminCmd = new SqlCommand(adminQuery, conn);
                    adminCmd.Parameters.AddWithValue("@EmployeeID", employeeId);
                    adminCmd.Parameters.AddWithValue("@Username", employee.Username);
                    adminCmd.Parameters.AddWithValue("@Password", employee.Password);
                    await adminCmd.ExecuteNonQueryAsync();
                }
                else if (employee.Position?.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase) == true)
                {
                    string staffQuery = @"INSERT INTO Staffs (EmployeeID, Username, Password) 
                                         VALUES (@EmployeeID, @Username, @Password)";
                    using var staffCmd = new SqlCommand(staffQuery, conn);
                    staffCmd.Parameters.AddWithValue("@EmployeeID", employeeId);
                    staffCmd.Parameters.AddWithValue("@Username", employee.Username);
                    staffCmd.Parameters.AddWithValue("@Password", employee.Password);
                    await staffCmd.ExecuteNonQueryAsync();
                }

                await LogActionAsync(conn, "CREATE", $"Added new {employee.Position}: {employee.FirstName} {employee.LastName}", true);

                _toastManager?.CreateToast("Employee Added")
                    .WithContent($"Successfully added {employee.FirstName} {employee.LastName}.")
                    .DismissOnClick()
                    .ShowSuccess();

                return (true, "Employee added successfully.", employeeId);
            }
            catch (SqlException ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to add employee: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"AddEmployeeAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

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
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to view employees.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to view employees.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        EmployeeId,
                        FirstName,
                        MiddleInitial,
                        LastName,
                        LTRIM(RTRIM(FirstName + ISNULL(' ' + MiddleInitial + '.', '') + ' ' + LastName)) AS Name,
                        Username,
                        ContactNumber,
                        Position,
                        Status,
                        DateJoined,
                        ProfilePicture
                    FROM Employees
                    ORDER BY Name;";

                var employees = new List<ManageEmployeeModel>();

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    employees.Add(new ManageEmployeeModel
                    {
                        ID = reader.GetInt32(0),
                        EmployeeId = reader.GetInt32(0),
                        FirstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        MiddleInitial = reader.IsDBNull(2) ? null : reader.GetString(2),
                        LastName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Name = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        Username = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        ContactNumber = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        Position = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                        Status = reader.IsDBNull(8) ? "Active" : reader.GetString(8),
                        DateJoined = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                        AvatarBytes = reader.IsDBNull(10) ? null : (byte[])reader[10],
                        AvatarSource = reader.IsDBNull(10)
                            ? ImageHelper.GetDefaultAvatar()
                            : ImageHelper.BytesToBitmap((byte[])reader[10])
                    });
                }

                return (true, "Employees retrieved successfully.", employees);
            }
            catch (SqlException ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load employees: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"GetEmployeesAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

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

                var query = @"SELECT EmployeeId, FirstName, MiddleInitial, LastName, Username, ContactNumber, 
                             Position, ProfilePicture, Status, DateJoined, Gender, Age, DateOfBirth,
                             HouseAddress, HouseNumber, Street, Barangay, CityTown, Province
                      FROM Employees 
                      WHERE EmployeeId = @Id";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", employeeId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var employee = new ManageEmployeeModel
                    {
                        ID = reader.GetInt32(0),
                        EmployeeId = reader.GetInt32(0),
                        FirstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        MiddleInitial = reader.IsDBNull(2) ? null : reader.GetString(2),
                        LastName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Username = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        ContactNumber = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        Position = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        Status = reader.IsDBNull(8) ? "Active" : reader.GetString(8),
                        DateJoined = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                        Gender = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                        Age = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                        DateOfBirth = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                        HouseAddress = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                        HouseNumber = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                        Street = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                        Barangay = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                        CityTown = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
                        Province = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                        AvatarBytes = reader.IsDBNull(7) ? null : (byte[])reader[7],
                        AvatarSource = reader.IsDBNull(7)
                            ? ImageHelper.GetDefaultAvatar()
                            : ImageHelper.BytesToBitmap((byte[])reader[7])
                    };

                    employee.Name = $"{employee.FirstName} {(string.IsNullOrWhiteSpace(employee.MiddleInitial) ? "" : employee.MiddleInitial + ". ")}{employee.LastName}";

                    return (true, "Employee retrieved successfully.", employee);
                }

                return (false, "Employee not found.", null);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error retrieving employee: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"GetEmployeeByIdAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, ManageEmployeeModel? Employee)> ViewEmployeeProfileAsync(int employeeId)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view employee profile.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT e.EmployeeId, e.FirstName, e.MiddleInitial, e.LastName, e.Gender, e.ProfilePicture, 
                           e.ContactNumber, e.Age, e.DateOfBirth, e.HouseAddress, e.HouseNumber, e.Street, 
                           e.Barangay, e.CityTown, e.Province, e.Username, e.Position, e.Status, e.DateJoined, e.Password,
                           COALESCE(a.LastLogin, s.LastLogin) AS LastLogin
                    FROM Employees e
                    LEFT JOIN Admins a ON e.EmployeeId = a.EmployeeId
                    LEFT JOIN Staffs s ON e.EmployeeId = s.EmployeeId
                    WHERE e.EmployeeId = @Id;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", employeeId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
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
                        Username = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                        Position = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                        Status = reader.IsDBNull(17) ? "Active" : reader.GetString(17),
                        DateJoined = reader.IsDBNull(18) ? DateTime.MinValue : reader.GetDateTime(18),
                        Password = reader.IsDBNull(19) ? string.Empty : reader.GetString(19),
                        LastLogin = reader.IsDBNull(20) ? "Never logged in" : reader.GetDateTime(20).ToString("MMMM dd, yyyy h:mm tt"),
                        AvatarBytes = reader.IsDBNull(5) ? null : (byte[])reader[5],
                        AvatarSource = reader.IsDBNull(5)
                            ? ImageHelper.GetDefaultAvatar()
                            : ImageHelper.BytesToBitmap((byte[])reader[5])
                    };

                    employee.Name = $"{employee.FirstName} {(string.IsNullOrWhiteSpace(employee.MiddleInitial) ? "" : employee.MiddleInitial + ". ")}{employee.LastName}";
                    employee.Birthdate = employee.DateOfBirth?.ToString("yyyy-MM-dd") ?? string.Empty;
                    employee.CityProvince = $"{employee.CityTown}, {employee.Province}".TrimEnd(',', ' ');

                    return (true, "Employee profile retrieved successfully.", employee);
                }

                return (false, "Employee not found.", null);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error retrieving profile: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"ViewEmployeeProfileAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region UPDATE

        public async Task<(bool Success, string Message)> UpdateEmployeeAsync(ManageEmployeeModel employee)
        {
            if (!CanUpdate())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("Only administrators can update employee data.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to update employees.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check if employee exists
                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Employees WHERE EmployeeId = @employeeId", conn);
                checkCmd.Parameters.AddWithValue("@employeeId", employee.EmployeeId);

                var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;
                if (!exists)
                {
                    _toastManager?.CreateToast("Employee Not Found")
                        .WithContent("The employee you're trying to update doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Employee not found.");
                }

                // Check for duplicate username (excluding current employee)
                using var dupCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Employees WHERE Username = @username AND EmployeeId != @employeeId", conn);
                dupCmd.Parameters.AddWithValue("@username", employee.Username ?? (object)DBNull.Value);
                dupCmd.Parameters.AddWithValue("@employeeId", employee.EmployeeId);

                var duplicateCount = (int)await dupCmd.ExecuteScalarAsync();
                if (duplicateCount > 0)
                {
                    _toastManager?.CreateToast("Duplicate Username")
                        .WithContent($"Another employee with username '{employee.Username}' already exists.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Username already exists.");
                }

                // Update employee
                string query = @"UPDATE Employees 
                     SET FirstName = @FirstName, 
                         MiddleInitial = @MiddleInitial, 
                         LastName = @LastName, 
                         Gender = @Gender,
                         Username = @Username, 
                         ContactNumber = @ContactNumber, 
                         Age = @Age,
                         DateOfBirth = @DateOfBirth,
                         HouseAddress = @HouseAddress,
                         HouseNumber = @HouseNumber,
                         Street = @Street,
                         Barangay = @Barangay,
                         CityTown = @CityTown,
                         Province = @Province,
                         Position = @Position,
                         Status = @Status,
                         ProfilePicture = @ProfilePicture
                     WHERE EmployeeId = @EmployeeId";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@EmployeeId", employee.EmployeeId);
                cmd.Parameters.AddWithValue("@FirstName", employee.FirstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@MiddleInitial", string.IsNullOrWhiteSpace(employee.MiddleInitial) ? (object)DBNull.Value : employee.MiddleInitial);
                cmd.Parameters.AddWithValue("@LastName", employee.LastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Gender", employee.Gender ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Username", employee.Username ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ContactNumber", employee.ContactNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Age", employee.Age > 0 ? employee.Age : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateOfBirth", employee.DateOfBirth ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@HouseAddress", employee.HouseAddress ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@HouseNumber", employee.HouseNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Street", employee.Street ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Barangay", employee.Barangay ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@CityTown", employee.CityTown ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Province", employee.Province ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Position", employee.Position ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", employee.Status ?? "Active");
                cmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary, -1).Value = employee.ProfilePicture ?? (object)DBNull.Value;

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                {
                    await LogActionAsync(conn, "UPDATE", $"Updated employee: {employee.FirstName} {employee.LastName}", true);

                    _toastManager?.CreateToast("Employee Updated")
                        .WithContent($"Successfully updated {employee.FirstName} {employee.LastName}.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Employee updated successfully.");
                }

                return (false, "Failed to update employee.");
            }
            catch (SqlException ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to update employee: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"UpdateEmployeeAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"UpdateEmployeeAsync Error: {ex}");
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region DELETE

        public async Task<(bool Success, string Message)> DeleteEmployeeAsync(int employeeId)
        {
            if (!CanDelete())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete employees.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to delete employees.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();

                try
                {
                    // Get employee name for logging
                    string employeeName = string.Empty;
                    using var getNameCmd = new SqlCommand(
                        "SELECT FirstName, LastName FROM Employees WHERE EmployeeId = @employeeId", conn, transaction);
                    getNameCmd.Parameters.AddWithValue("@employeeId", employeeId);

                    using var nameReader = await getNameCmd.ExecuteReaderAsync();
                    if (await nameReader.ReadAsync())
                    {
                        employeeName = $"{nameReader[0]} {nameReader[1]}";
                    }
                    nameReader.Close();

                    if (string.IsNullOrEmpty(employeeName))
                    {
                        _toastManager?.CreateToast("Employee Not Found")
                            .WithContent("The employee you're trying to delete doesn't exist.")
                            .DismissOnClick()
                            .ShowWarning();
                        return (false, "Employee not found.");
                    }

                    // Delete from Staffs table
                    using var deleteStaffCmd = new SqlCommand(
                        "DELETE FROM Staffs WHERE EmployeeID = @employeeId", conn, transaction);
                    deleteStaffCmd.Parameters.AddWithValue("@employeeId", employeeId);
                    await deleteStaffCmd.ExecuteNonQueryAsync();

                    // Delete from Admins table
                    using var deleteAdminCmd = new SqlCommand(
                        "DELETE FROM Admins WHERE EmployeeID = @employeeId", conn, transaction);
                    deleteAdminCmd.Parameters.AddWithValue("@employeeId", employeeId);
                    await deleteAdminCmd.ExecuteNonQueryAsync();

                    // Delete from Employees table
                    using var deleteEmpCmd = new SqlCommand(
                        "DELETE FROM Employees WHERE EmployeeId = @employeeId", conn, transaction);
                    deleteEmpCmd.Parameters.AddWithValue("@employeeId", employeeId);
                    int rowsAffected = await deleteEmpCmd.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        await LogActionAsync(conn, "DELETE", $"Deleted employee: {employeeName} (ID: {employeeId})", true);
                        transaction.Commit();

                        _toastManager?.CreateToast("Employee Deleted")
                            .WithContent($"Successfully deleted {employeeName}.")
                            .DismissOnClick()
                            .ShowSuccess();

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
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to delete employee: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"DeleteEmployeeAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"DeleteEmployeeAsync Error: {ex}");
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion
        #region UTILITY METHODS

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

                string query = "SELECT COUNT(*) FROM Employees";
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

                string query = "SELECT COUNT(*) FROM Employees WHERE Status = @Status";
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
                      FROM Employees", conn);

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
    }
}
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

                using var transaction = conn.BeginTransaction();

                try
                {
                    // Check for duplicate username in both Admins and Staffs tables
                    using var checkCmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM 
                        (SELECT Username FROM Admins WHERE Username = @username
                         UNION ALL
                         SELECT Username FROM Staffs WHERE Username = @username) AS AllUsernames",
                        conn, transaction);
                    checkCmd.Parameters.AddWithValue("@username", employee.Username ?? (object)DBNull.Value);

                    var count = (int)await checkCmd.ExecuteScalarAsync();
                    if (count > 0)
                    {
                        transaction.Rollback();
                        _toastManager?.CreateToast("Duplicate Username")
                            .WithContent($"Username '{employee.Username}' already exists.")
                            .DismissOnClick()
                            .ShowWarning();
                        return (false, "Username already exists.", null);
                    }

                    // 🔐 HASH THE PASSWORD BEFORE STORING
                    string hashedPassword = PasswordHelper.HashPassword(employee.Password ?? "DefaultPassword123!");

                    // Insert into Employees table
                    string employeeQuery = @"
                        INSERT INTO Employees 
                        (FirstName, MiddleInitial, LastName, Gender, ProfilePicture, ContactNumber, Age, DateOfBirth, 
                         HouseAddress, HouseNumber, Street, Barangay, CityTown, Province, 
                         DateJoined, Status, Position, CreatedAt)
                        OUTPUT INSERTED.EmployeeId
                        VALUES 
                        (@FirstName, @MiddleInitial, @LastName, @Gender, @ProfilePicture, @ContactNumber, @Age, @DateOfBirth, 
                         @HouseAddress, @HouseNumber, @Street, @Barangay, @CityTown, @Province, 
                         @DateJoined, @Status, @Position, GETDATE())";

                    using var cmd = new SqlCommand(employeeQuery, conn, transaction);
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
                    cmd.Parameters.AddWithValue("@DateJoined", employee.DateJoined);
                    cmd.Parameters.AddWithValue("@Status", employee.Status ?? "Active");
                    cmd.Parameters.AddWithValue("@Position", employee.Position ?? (object)DBNull.Value);

                    int employeeId = (int)await cmd.ExecuteScalarAsync();

                    // Insert into Admins or Staffs table based on Position with HASHED PASSWORD
                    if (employee.Position?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        string adminQuery = @"
                            INSERT INTO Admins (AdminID, Username, Password, CreatedAt)
                            VALUES (@AdminID, @Username, @Password, GETDATE())";

                        using var adminCmd = new SqlCommand(adminQuery, conn, transaction);
                        adminCmd.Parameters.AddWithValue("@AdminID", employeeId);
                        adminCmd.Parameters.AddWithValue("@Username", employee.Username ?? (object)DBNull.Value);
                        adminCmd.Parameters.AddWithValue("@Password", hashedPassword);
                        await adminCmd.ExecuteNonQueryAsync();
                    }
                    else if (employee.Position?.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        string staffQuery = @"
                            INSERT INTO Staffs (StaffID, Username, Password, CreatedAt)
                            VALUES (@StaffID, @Username, @Password, GETDATE())";

                        using var staffCmd = new SqlCommand(staffQuery, conn, transaction);
                        staffCmd.Parameters.AddWithValue("@StaffID", employeeId);
                        staffCmd.Parameters.AddWithValue("@Username", employee.Username ?? (object)DBNull.Value);
                        staffCmd.Parameters.AddWithValue("@Password", hashedPassword);
                        await staffCmd.ExecuteNonQueryAsync();
                    }

                    await LogActionAsync(conn, "CREATE", $"Added new {employee.Position}: {employee.FirstName} {employee.LastName}", true, transaction);

                    transaction.Commit();

                    _toastManager?.CreateToast("Employee Added")
                        .WithContent($"Successfully added {employee.FirstName} {employee.LastName}.")
                        .DismissOnClick()
                        .ShowSuccess();

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

                // ✅ Updated query with JOIN to get Username
                string query = @"
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
                COALESCE(a.Username, s.Username) AS Username
            FROM Employees e
            LEFT JOIN Admins a ON e.EmployeeID = a.AdminID
            LEFT JOIN Staffs s ON e.EmployeeID = s.StaffID
            ORDER BY Name";

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
                        ContactNumber = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        Position = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        Status = reader.IsDBNull(7) ? "Active" : reader.GetString(7),
                        DateJoined = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                        AvatarBytes = reader.IsDBNull(9) ? null : (byte[])reader[9],
                        AvatarSource = reader.IsDBNull(9)
                            ? ImageHelper.GetDefaultAvatar()
                            : ImageHelper.BytesToBitmap((byte[])reader[9]),
                        Username = reader.IsDBNull(10) ? string.Empty : reader.GetString(10) // ✅ Add Username
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

                var query = @"
                    SELECT e.EmployeeId, e.FirstName, e.MiddleInitial, e.LastName, e.ContactNumber, 
                           e.Position, e.ProfilePicture, e.Status, e.DateJoined, e.Gender, e.Age, e.DateOfBirth,
                           e.HouseAddress, e.HouseNumber, e.Street, e.Barangay, e.CityTown, e.Province,
                           COALESCE(a.Username, s.Username) AS Username
                    FROM Employees e
                    LEFT JOIN Admins a ON e.EmployeeID = a.AdminID
                    LEFT JOIN Staffs s ON e.EmployeeID = s.StaffID
                    WHERE e.EmployeeId = @Id";

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
                        Password = "********", // Never return actual password
                        AvatarBytes = reader.IsDBNull(6) ? null : (byte[])reader[6],
                        AvatarSource = reader.IsDBNull(6)
                            ? ImageHelper.GetDefaultAvatar()
                            : ImageHelper.BytesToBitmap((byte[])reader[6])
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
                return (false, "Insufficient permissions to view employee profile.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // JOIN query to get data from Employees and either Admins or Staffs
                string query = @"
                    SELECT 
                        e.EmployeeID, e.FirstName, e.MiddleInitial, e.LastName, e.Gender, e.ProfilePicture, 
                        e.ContactNumber, e.Age, e.DateOfBirth, e.HouseAddress, e.HouseNumber, e.Street, 
                        e.Barangay, e.CityTown, e.Province, e.Position, e.Status, e.DateJoined,
                        COALESCE(a.Username, s.Username) AS Username,
                        COALESCE(a.LastLogin, s.LastLogin) AS LastLogin
                    FROM Employees e
                    LEFT JOIN Admins a ON e.EmployeeID = a.AdminID
                    LEFT JOIN Staffs s ON e.EmployeeID = s.StaffID
                    WHERE e.EmployeeID = @Id";

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
                        Position = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                        Status = reader.IsDBNull(16) ? "Active" : reader.GetString(16),
                        DateJoined = reader.IsDBNull(17) ? DateTime.MinValue : reader.GetDateTime(17),
                        Username = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                        Password = "********", // Never return actual password
                        LastLogin = reader.IsDBNull(19) ? "Never logged in" : reader.GetDateTime(19).ToString("MMMM dd, yyyy h:mm tt"),
                        AvatarBytes = reader.IsDBNull(5) ? null : (byte[])reader[5],
                        AvatarSource = reader.IsDBNull(5)
                            ? ImageHelper.GetDefaultAvatar()
                            : ImageHelper.BytesToBitmap((byte[])reader[5])
                    };

                    employee.Name = $"{employee.FirstName} {(string.IsNullOrWhiteSpace(employee.MiddleInitial) ? "" : employee.MiddleInitial + ". ")}{employee.LastName}";
                    employee.Birthdate = employee.DateOfBirth?.ToString("MMMM d, yyyy") ?? string.Empty;
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

                using var transaction = conn.BeginTransaction();

                try
                {
                    // Get the current position to check if it changed
                    string? oldPosition = null;
                    using var getPositionCmd = new SqlCommand(
                        "SELECT Position FROM Employees WHERE EmployeeId = @employeeId", conn, transaction);
                    getPositionCmd.Parameters.AddWithValue("@employeeId", employee.EmployeeId);

                    var positionResult = await getPositionCmd.ExecuteScalarAsync();
                    if (positionResult == null)
                    {
                        transaction.Rollback();
                        _toastManager?.CreateToast("Employee Not Found")
                            .WithContent("The employee you're trying to update doesn't exist.")
                            .DismissOnClick()
                            .ShowWarning();
                        return (false, "Employee not found.");
                    }
                    oldPosition = positionResult.ToString();

                    // Check for duplicate username (excluding current employee)
                    using var dupCmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM 
                        (SELECT Username FROM Admins WHERE Username = @username AND AdminID != @employeeId
                         UNION ALL
                         SELECT Username FROM Staffs WHERE Username = @username AND StaffID != @employeeId) AS AllUsernames",
                        conn, transaction);
                    dupCmd.Parameters.AddWithValue("@username", employee.Username ?? (object)DBNull.Value);
                    dupCmd.Parameters.AddWithValue("@employeeId", employee.EmployeeId);

                    var duplicateCount = (int)await dupCmd.ExecuteScalarAsync();
                    if (duplicateCount > 0)
                    {
                        transaction.Rollback();
                        _toastManager?.CreateToast("Duplicate Username")
                            .WithContent($"Another employee with username '{employee.Username}' already exists.")
                            .DismissOnClick()
                            .ShowWarning();
                        return (false, "Username already exists.");
                    }

                    // 🔐 Check if password has changed (if it's not the default "********" placeholder)
                    string? passwordToStore = null;
                    if (!string.IsNullOrWhiteSpace(employee.Password) && employee.Password != "********")
                    {
                        // Hash the new password
                        passwordToStore = PasswordHelper.HashPassword(employee.Password);
                    }
                    else
                    {
                        // Keep the existing password - retrieve it first
                        using var getPasswordCmd = new SqlCommand(@"
                            SELECT COALESCE(a.Password, s.Password) AS CurrentPassword
                            FROM Employees e
                            LEFT JOIN Admins a ON e.EmployeeID = a.AdminID
                            LEFT JOIN Staffs s ON e.EmployeeID = s.StaffID
                            WHERE e.EmployeeId = @employeeId", conn, transaction);
                        getPasswordCmd.Parameters.AddWithValue("@employeeId", employee.EmployeeId);

                        var existingPassword = await getPasswordCmd.ExecuteScalarAsync();
                        passwordToStore = existingPassword?.ToString();
                    }

                    // Update Employees table
                    string query = @"UPDATE Employees 
                         SET FirstName = @FirstName, 
                             MiddleInitial = @MiddleInitial, 
                             LastName = @LastName, 
                             Gender = @Gender,
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
                             ProfilePicture = @ProfilePicture,
                             UpdatedAt = GETDATE()
                         WHERE EmployeeId = @EmployeeId";

                    using var cmd = new SqlCommand(query, conn, transaction);
                    cmd.Parameters.AddWithValue("@EmployeeId", employee.EmployeeId);
                    cmd.Parameters.AddWithValue("@FirstName", employee.FirstName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MiddleInitial", string.IsNullOrWhiteSpace(employee.MiddleInitial) ? (object)DBNull.Value : employee.MiddleInitial);
                    cmd.Parameters.AddWithValue("@LastName", employee.LastName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Gender", employee.Gender ?? (object)DBNull.Value);
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
                        // Handle position change: Admin <-> Staff
                        bool positionChanged = !string.Equals(oldPosition, employee.Position, StringComparison.OrdinalIgnoreCase);

                        if (positionChanged)
                        {
                            // If changed from Gym Admin to Gym Staff
                            if (oldPosition?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true &&
                                employee.Position?.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                // Delete from Admins
                                using var deleteAdminCmd = new SqlCommand(
                                    "DELETE FROM Admins WHERE AdminID = @EmployeeId", conn, transaction);
                                deleteAdminCmd.Parameters.AddWithValue("@EmployeeId", employee.EmployeeId);
                                await deleteAdminCmd.ExecuteNonQueryAsync();

                                // Insert into Staffs with hashed password
                                using var insertStaffCmd = new SqlCommand(@"
                                    INSERT INTO Staffs (StaffID, Username, Password, CreatedAt)
                                    VALUES (@StaffID, @Username, @Password, GETDATE())", conn, transaction);
                                insertStaffCmd.Parameters.AddWithValue("@StaffID", employee.EmployeeId);
                                insertStaffCmd.Parameters.AddWithValue("@Username", employee.Username ?? (object)DBNull.Value);
                                insertStaffCmd.Parameters.AddWithValue("@Password", passwordToStore ?? (object)DBNull.Value);
                                await insertStaffCmd.ExecuteNonQueryAsync();
                            }
                            // If changed from Gym Staff to Gym Admin
                            else if (oldPosition?.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase) == true &&
                                     employee.Position?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                // Delete from Staffs
                                using var deleteStaffCmd = new SqlCommand(
                                    "DELETE FROM Staffs WHERE StaffID = @EmployeeId", conn, transaction);
                                deleteStaffCmd.Parameters.AddWithValue("@EmployeeId", employee.EmployeeId);
                                await deleteStaffCmd.ExecuteNonQueryAsync();

                                // Insert into Admins with hashed password
                                using var insertAdminCmd = new SqlCommand(@"
                                    INSERT INTO Admins (AdminID, Username, Password, CreatedAt)
                                    VALUES (@AdminID, @Username, @Password, GETDATE())", conn, transaction);
                                insertAdminCmd.Parameters.AddWithValue("@AdminID", employee.EmployeeId);
                                insertAdminCmd.Parameters.AddWithValue("@Username", employee.Username ?? (object)DBNull.Value);
                                insertAdminCmd.Parameters.AddWithValue("@Password", passwordToStore ?? (object)DBNull.Value);
                                await insertAdminCmd.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            // Update Username and Password in respective table if position didn't change
                            if (employee.Position?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                using var updateAdminCmd = new SqlCommand(@"
                                    UPDATE Admins 
                                    SET Username = @Username, Password = @Password, UpdatedAt = GETDATE()
                                    WHERE AdminID = @EmployeeId", conn, transaction);
                                updateAdminCmd.Parameters.AddWithValue("@EmployeeId", employee.EmployeeId);
                                updateAdminCmd.Parameters.AddWithValue("@Username", employee.Username ?? (object)DBNull.Value);
                                updateAdminCmd.Parameters.AddWithValue("@Password", passwordToStore ?? (object)DBNull.Value);
                                await updateAdminCmd.ExecuteNonQueryAsync();
                            }
                            else if (employee.Position?.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                using var updateStaffCmd = new SqlCommand(@"
                                    UPDATE Staffs 
                                    SET Username = @Username, Password = @Password, UpdatedAt = GETDATE()
                                    WHERE StaffID = @EmployeeId", conn, transaction);
                                updateStaffCmd.Parameters.AddWithValue("@EmployeeId", employee.EmployeeId);
                                updateStaffCmd.Parameters.AddWithValue("@Username", employee.Username ?? (object)DBNull.Value);
                                updateStaffCmd.Parameters.AddWithValue("@Password", passwordToStore ?? (object)DBNull.Value);
                                await updateStaffCmd.ExecuteNonQueryAsync();
                            }
                        }

                        await LogActionAsync(conn, "UPDATE", $"Updated employee: {employee.FirstName} {employee.LastName}", true, transaction);

                        transaction.Commit();

                        _toastManager?.CreateToast("Employee Updated")
                            .WithContent($"Successfully updated {employee.FirstName} {employee.LastName}.")
                            .DismissOnClick()
                            .ShowSuccess();

                        return (true, "Employee updated successfully.");
                    }

                    transaction.Rollback();
                    return (false, "Failed to update employee.");
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
                    // Get employee name and position for logging
                    string employeeName = string.Empty;
                    string employeePosition = string.Empty;

                    using var getInfoCmd = new SqlCommand(
                        "SELECT FirstName, LastName, Position FROM Employees WHERE EmployeeId = @employeeId", conn, transaction);
                    getInfoCmd.Parameters.AddWithValue("@employeeId", employeeId);

                    using var infoReader = await getInfoCmd.ExecuteReaderAsync();
                    if (await infoReader.ReadAsync())
                    {
                        employeeName = $"{infoReader[0]} {infoReader[1]}";
                        employeePosition = infoReader[2]?.ToString() ?? string.Empty;
                    }
                    infoReader.Close();

                    if (string.IsNullOrEmpty(employeeName))
                    {
                        transaction.Rollback();
                        _toastManager?.CreateToast("Employee Not Found")
                            .WithContent("The employee you're trying to delete doesn't exist.")
                            .DismissOnClick()
                            .ShowWarning();
                        return (false, "Employee not found.");
                    }

                    // Delete from Admins or Staffs table based on position
                    if (employeePosition.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        using var deleteAdminCmd = new SqlCommand(
                            "DELETE FROM Admins WHERE AdminID = @employeeId", conn, transaction);
                        deleteAdminCmd.Parameters.AddWithValue("@employeeId", employeeId);
                        await deleteAdminCmd.ExecuteNonQueryAsync();
                    }
                    else if (employeePosition.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase))
                    {
                        using var deleteStaffCmd = new SqlCommand(
                            "DELETE FROM Staffs WHERE StaffID = @employeeId", conn, transaction);
                        deleteStaffCmd.Parameters.AddWithValue("@employeeId", employeeId);
                        await deleteStaffCmd.ExecuteNonQueryAsync();
                    }

                    // Set PerformedByEmployeeID to NULL in SystemLogs (preserves audit trail)
                    using var updateLogsCmd = new SqlCommand(
                        "UPDATE SystemLogs SET PerformedByEmployeeID = NULL WHERE PerformedByEmployeeID = @employeeId",
                        conn, transaction);
                    updateLogsCmd.Parameters.AddWithValue("@employeeId", employeeId);
                    await updateLogsCmd.ExecuteNonQueryAsync();

                    // Delete from Employees table
                    using var deleteEmpCmd = new SqlCommand(
                        "DELETE FROM Employees WHERE EmployeeId = @employeeId", conn, transaction);
                    deleteEmpCmd.Parameters.AddWithValue("@employeeId", employeeId);
                    int rowsAffected = await deleteEmpCmd.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        await LogActionAsync(conn, "DELETE", $"Deleted employee: {employeeName} (ID: {employeeId})", true, transaction);
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

        #region AUTHENTICATION

        public async Task<(bool Success, string Message, int? EmployeeId, string? Role)> AuthenticateUserAsync(string username, string password)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // 🔹 Admin Login
                string adminQuery = @"
            SELECT a.AdminID, a.Password, e.FirstName, e.LastName, e.Status
            FROM Admins a
            INNER JOIN Employees e ON a.AdminID = e.EmployeeID
            WHERE a.Username = @Username";

                using var adminCmd = new SqlCommand(adminQuery, conn);
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

                        // Update last login
                        using var updateLoginCmd = new SqlCommand(
                            "UPDATE Admins SET LastLogin = GETDATE() WHERE AdminID = @AdminID", conn);
                        updateLoginCmd.Parameters.AddWithValue("@AdminID", employeeId);
                        await updateLoginCmd.ExecuteNonQueryAsync();

                        // ✅ Log success
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
                adminReader.Close();

                // 🔹 Staff Login
                string staffQuery = @"
            SELECT s.StaffID, s.Password, e.FirstName, e.LastName, e.Status
            FROM Staffs s
            INNER JOIN Employees e ON s.StaffID = e.EmployeeID
            WHERE s.Username = @Username";

                using var staffCmd = new SqlCommand(staffQuery, conn);
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

                        // ✅ Log success
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

                // 🔹 Log invalid username
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
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using AHON_TRACK.Converters;
using System.Linq;

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

        public async Task<bool> AddEmployeeAsync(EmployeeModel employee)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // 1️⃣ Insert into Employees first
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
                cmd.Parameters.AddWithValue("@MiddleInitial", employee.MiddleInitial ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LastName", employee.LastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Gender", employee.Gender ?? (object)DBNull.Value);

                byte[]? profilePictureBytes = employee.ProfilePicture as byte[];
                cmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary, -1).Value = profilePictureBytes ?? (object)DBNull.Value;

                cmd.Parameters.AddWithValue("@ContactNumber", employee.ContactNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Age", employee.Age ?? (object)DBNull.Value);
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

                // 🔑 Get the new EmployeeId
                int employeeId = (int)await cmd.ExecuteScalarAsync();

                // 2️⃣ Insert into Admin or Staff depending on Position
                if (employee.Position?.Equals("Gym Admin", StringComparison.OrdinalIgnoreCase) == true)
                {
                    string adminQuery = @"
                INSERT INTO Admins (EmployeeID, Username, Password) 
                VALUES (@EmployeeID, @Username, @Password)";

                    using var adminCmd = new SqlCommand(adminQuery, conn);
                    adminCmd.Parameters.AddWithValue("@EmployeeID", employeeId);
                    adminCmd.Parameters.AddWithValue("@Username", employee.Username);
                    adminCmd.Parameters.AddWithValue("@Password", employee.Password); // ⚠️ Consider hashing

                    await adminCmd.ExecuteNonQueryAsync();
                }
                else if (employee.Position?.Equals("Gym Staff", StringComparison.OrdinalIgnoreCase) == true)
                {
                    string staffQuery = @"
                INSERT INTO Staffs (EmployeeID, Username, Password) 
                VALUES (@EmployeeID, @Username, @Password)";

                    using var staffCmd = new SqlCommand(staffQuery, conn);
                    staffCmd.Parameters.AddWithValue("@EmployeeID", employeeId);
                    staffCmd.Parameters.AddWithValue("@Username", employee.Username);
                    staffCmd.Parameters.AddWithValue("@Password", employee.Password); // ⚠️ Consider hashing

                    await staffCmd.ExecuteNonQueryAsync();
                }

                // ✅ Success Toast
                _toastManager?.CreateToast("Success")
                    .WithContent("Employee added successfully!")
                    .DismissOnClick()
                    .ShowSuccess();

                await LogActionAsync(conn, "Add Employee", $"Added new {employee.Position}: {employee.FirstName} {employee.LastName}", true);

                return true;
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error adding employee: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"AddEmployeeAsync Error: {ex}");

                try
                {
                    await using var conn2 = new SqlConnection(_connectionString);
                    await conn2.OpenAsync();
                    await LogActionAsync(conn2, "Add Employee", $"Error adding employee: {ex.Message}", false);
                }
                catch { }

                return false;
            }
        }



        public async Task<ManageEmployeeModel?> GetEmployeeByIdAsync(int employeeId)
        {
            ManageEmployeeModel? employee = null;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    var query = @"SELECT EmployeeId, FirstName, MiddleInitial, LastName, Username, ContactNumber, 
                         Position, ProfilePicture, Status, DateJoined
                      FROM Employees 
                      WHERE EmployeeId = @Id";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", employeeId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                employee = new ManageEmployeeModel
                                {
                                    ID = reader["EmployeeId"].ToString() ?? string.Empty,
                                    Name = $"{reader["FirstName"]} {(string.IsNullOrWhiteSpace(reader["MiddleInitial"]?.ToString()) ? "" : reader["MiddleInitial"] + ". ")}{reader["LastName"]}",
                                    Username = reader["Username"]?.ToString() ?? string.Empty,
                                    ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                                    Position = reader["Position"]?.ToString() ?? string.Empty,
                                    Status = reader["Status"]?.ToString() ?? "Active",
                                    DateJoined = reader["DateJoined"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["DateJoined"]),

                                    // SIMPLIFIED: Use ImageHelper to handle avatar conversion
                                    AvatarSource = reader["ProfilePicture"] == DBNull.Value ? ImageHelper.GetDefaultAvatar() : ImageHelper.GetAvatarOrDefault((byte[])reader["ProfilePicture"])

                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error retrieving employee: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"GetEmployeeByIdAsync Error: {ex}");
            }

            return employee;
        }

        public async Task<List<ManageEmployeeModel>> GetEmployeesAsync()
        {
            var employees = new List<ManageEmployeeModel>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT 
                            EmployeeId,
                            LTRIM(RTRIM(
                                FirstName + 
                                ISNULL(' ' + MiddleInitial + '.', '') + 
                                ' ' + LastName
                            )) AS Name,
                            Username,
                            ContactNumber,
                            Position,
                            Status,
                            DateJoined,
                            ProfilePicture
                        FROM Employees
                        ORDER BY Name;";

                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            employees.Add(new ManageEmployeeModel
                            {
                                ID = reader["EmployeeId"]?.ToString() ?? string.Empty,
                                Name = reader["Name"]?.ToString() ?? string.Empty,
                                Username = reader["Username"]?.ToString() ?? string.Empty,
                                ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                                Position = reader["Position"]?.ToString() ?? string.Empty,
                                Status = reader["Status"]?.ToString() ?? "Active",
                                DateJoined = reader["DateJoined"] == DBNull.Value
                                    ? DateTime.MinValue
                                    : Convert.ToDateTime(reader["DateJoined"]),

                                // SIMPLIFIED: Use ImageHelper for all avatar handling
                                AvatarBytes = reader["ProfilePicture"] == DBNull.Value ? null : (byte[])reader["ProfilePicture"],

                                AvatarSource = reader["ProfilePicture"] == DBNull.Value ? ImageHelper.GetDefaultAvatar() : ImageHelper.BytesToBitmap((byte[])reader["ProfilePicture"])
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error loading employees: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"GetEmployeesAsync Error: {ex}");
            }

            return employees;
        }

        // ✅ NEW: Search functionality for employees based on search term
        public async Task<List<ManageEmployeeModel>> SearchEmployeesAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetEmployeesAsync(); // Return all employees if search is empty
            }

            var employees = new List<ManageEmployeeModel>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT 
                            EmployeeId,
                            LTRIM(RTRIM(
                                FirstName + 
                                ISNULL(' ' + MiddleInitial + '.', '') + 
                                ' ' + LastName
                            )) AS Name,
                            Username,
                            ContactNumber,
                            Position,
                            Status,
                            DateJoined,
                            ProfilePicture
                        FROM Employees
                        WHERE FirstName LIKE @SearchTerm 
                           OR LastName LIKE @SearchTerm 
                           OR Username LIKE @SearchTerm 
                           OR ContactNumber LIKE @SearchTerm
                           OR Position LIKE @SearchTerm
                           OR CONCAT(FirstName, ' ', LastName) LIKE @SearchTerm
                        ORDER BY Name;";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@SearchTerm", $"%{searchTerm}%");

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                employees.Add(new ManageEmployeeModel
                                {
                                    ID = reader["EmployeeId"]?.ToString() ?? string.Empty,
                                    Name = reader["Name"]?.ToString() ?? string.Empty,
                                    Username = reader["Username"]?.ToString() ?? string.Empty,
                                    ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                                    Position = reader["Position"]?.ToString() ?? string.Empty,
                                    Status = reader["Status"]?.ToString() ?? "Active",
                                    DateJoined = reader["DateJoined"] == DBNull.Value
                                        ? DateTime.MinValue
                                        : Convert.ToDateTime(reader["DateJoined"]),
                                    AvatarBytes = reader["ProfilePicture"] == DBNull.Value
                                        ? null
                                        : (byte[])reader["ProfilePicture"],
                                    AvatarSource = reader["ProfilePicture"] == DBNull.Value
                                        ? ImageHelper.GetDefaultAvatar()
                                        : ImageHelper.BytesToBitmap((byte[])reader["ProfilePicture"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error searching employees: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"SearchEmployeesAsync Error: {ex}");
            }

            return employees;
        }

        // ✅ NEW: Get employees by status (Active, Inactive, Terminated)
        public async Task<List<ManageEmployeeModel>> GetEmployeesByStatusAsync(string status)
        {
            var employees = new List<ManageEmployeeModel>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT 
                            EmployeeId,
                            LTRIM(RTRIM(
                                FirstName + 
                                ISNULL(' ' + MiddleInitial + '.', '') + 
                                ' ' + LastName
                            )) AS Name,
                            Username,
                            ContactNumber,
                            Position,
                            Status,
                            DateJoined,
                            ProfilePicture
                        FROM Employees
                        WHERE Status = @Status
                        ORDER BY Name;";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Status", status);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                employees.Add(new ManageEmployeeModel
                                {
                                    ID = reader["EmployeeId"]?.ToString() ?? string.Empty,
                                    Name = reader["Name"]?.ToString() ?? string.Empty,
                                    Username = reader["Username"]?.ToString() ?? string.Empty,
                                    ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                                    Position = reader["Position"]?.ToString() ?? string.Empty,
                                    Status = reader["Status"]?.ToString() ?? "Active",
                                    DateJoined = reader["DateJoined"] == DBNull.Value
                                        ? DateTime.MinValue
                                        : Convert.ToDateTime(reader["DateJoined"]),
                                    AvatarBytes = reader["ProfilePicture"] == DBNull.Value
                                        ? null
                                        : (byte[])reader["ProfilePicture"],
                                    AvatarSource = reader["ProfilePicture"] == DBNull.Value
                                        ? ImageHelper.GetDefaultAvatar()
                                        : ImageHelper.BytesToBitmap((byte[])reader["ProfilePicture"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error filtering employees by status: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"GetEmployeesByStatusAsync Error: {ex}");
            }

            return employees;
        }

        // ✅ NEW: Get employees with sorting options
        public async Task<List<ManageEmployeeModel>> GetEmployeesSortedAsync(string sortBy, bool descending = false)
        {
            var employees = new List<ManageEmployeeModel>();
            string orderByClause = sortBy.ToLower() switch
            {
                "id" => descending ? "EmployeeId DESC" : "EmployeeId ASC",
                "name" => descending ? "Name DESC" : "Name ASC",
                "username" => descending ? "Username DESC" : "Username ASC",
                "datejoined" => descending ? "DateJoined DESC" : "DateJoined ASC",
                _ => "Name ASC" // Default sort by name
            };

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string query = $@"
                        SELECT 
                            EmployeeId,
                            LTRIM(RTRIM(
                                FirstName + 
                                ISNULL(' ' + MiddleInitial + '.', '') + 
                                ' ' + LastName
                            )) AS Name,
                            Username,
                            ContactNumber,
                            Position,
                            Status,
                            DateJoined,
                            ProfilePicture
                        FROM Employees
                        ORDER BY {orderByClause};";

                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            employees.Add(new ManageEmployeeModel
                            {
                                ID = reader["EmployeeId"]?.ToString() ?? string.Empty,
                                Name = reader["Name"]?.ToString() ?? string.Empty,
                                Username = reader["Username"]?.ToString() ?? string.Empty,
                                ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                                Position = reader["Position"]?.ToString() ?? string.Empty,
                                Status = reader["Status"]?.ToString() ?? "Active",
                                DateJoined = reader["DateJoined"] == DBNull.Value
                                    ? DateTime.MinValue
                                    : Convert.ToDateTime(reader["DateJoined"]),
                                AvatarBytes = reader["ProfilePicture"] == DBNull.Value
                                    ? null
                                    : (byte[])reader["ProfilePicture"],
                                AvatarSource = reader["ProfilePicture"] == DBNull.Value
                                    ? ImageHelper.GetDefaultAvatar()
                                    : ImageHelper.BytesToBitmap((byte[])reader["ProfilePicture"])
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error sorting employees: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"GetEmployeesSortedAsync Error: {ex}");
            }

            return employees;
        }

        // ✅ NEW: Get employee count by status
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
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error getting employee count: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"GetEmployeeCountByStatusAsync Error: {ex}");
                return 0;
            }
        }

        // ✅ NEW: Get total employee count
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
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error getting total employee count: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"GetTotalEmployeeCountAsync Error: {ex}");
                return 0;
            }
        }

        public async Task<bool> UpdateEmployeeAsync(EmployeeModel employee)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"UPDATE Employees 
                     SET FirstName=@FirstName, 
                         MiddleInitial=@MiddleInitial, 
                         LastName=@LastName, 
                         Username=@Username, 
                         ContactNumber=@ContactNumber, 
                         Position=@Position,
                         ProfilePicture=@ProfilePicture
                     WHERE EmployeeId=@EmployeeId";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@EmployeeId", employee.EmployeeId);
                cmd.Parameters.AddWithValue("@FirstName", employee.FirstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@MiddleInitial", employee.MiddleInitial ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LastName", employee.LastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Username", employee.Username ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ContactNumber", employee.ContactNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Position", employee.Position ?? (object)DBNull.Value);

                // SIMPLIFIED: Use ImageHelper for conversion
                byte[]? profilePictureBytes = null;
                if (employee.ProfilePicture != null)
                {
                    if (employee.ProfilePicture is byte[] bytes)
                    {
                        profilePictureBytes = bytes;
                    }
                    else if ((object)employee.ProfilePicture is Bitmap bitmap)
                    {
                        profilePictureBytes = ImageHelper.BitmapToBytes(bitmap);
                    }
                }

                cmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary, -1).Value =
                profilePictureBytes ?? (object)DBNull.Value;

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                {
                    await LogActionAsync(conn, "Update Employee", $"Updated employee: {employee.FirstName} {employee.LastName}", true);
                    return true;
                }
                else
                {
                    await LogActionAsync(conn, "Update Employee", $"Failed to update employee: {employee.EmployeeId}", false);
                    return false;
                }
            }

            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error updating employee: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"UpdateEmployeeAsync Error: {ex}");
                return false;
            }
        }

        public async Task<bool> DeleteEmployeeAsync(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Start a transaction to ensure all deletions succeed or fail together
                using var transaction = conn.BeginTransaction();

                try
                {
                    // Get employee details for logging before deletion
                    string employeeName = "";
                    string getEmployeeQuery = "SELECT FirstName, LastName FROM Employees WHERE EmployeeId = @EmployeeId";
                    using (var getCmd = new SqlCommand(getEmployeeQuery, conn, transaction))
                    {
                        getCmd.Parameters.AddWithValue("@EmployeeId", id);
                        using var reader = await getCmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            employeeName = $"{reader["FirstName"]} {reader["LastName"]}";
                        }
                    }

                    // 1. Delete from Staffs table first (if exists)
                    string deleteStaffQuery = "DELETE FROM Staffs WHERE EmployeeID = @EmployeeId";
                    using (var staffCmd = new SqlCommand(deleteStaffQuery, conn, transaction))
                    {
                        staffCmd.Parameters.AddWithValue("@EmployeeId", id);
                        await staffCmd.ExecuteNonQueryAsync();
                    }

                    // 2. Delete from Admins table (if exists)
                    string deleteAdminQuery = "DELETE FROM Admins WHERE EmployeeID = @EmployeeId";
                    using (var adminCmd = new SqlCommand(deleteAdminQuery, conn, transaction))
                    {
                        adminCmd.Parameters.AddWithValue("@EmployeeId", id);
                        await adminCmd.ExecuteNonQueryAsync();
                    }

                    // 3. Finally, delete from Employees table
                    string deleteEmployeeQuery = "DELETE FROM Employees WHERE EmployeeId = @EmployeeId";
                    using (var empCmd = new SqlCommand(deleteEmployeeQuery, conn, transaction))
                    {
                        empCmd.Parameters.AddWithValue("@EmployeeId", id);
                        int rows = await empCmd.ExecuteNonQueryAsync();

                        if (rows > 0)
                        {
                            // Commit the transaction
                            transaction.Commit();

                            // Log success
                            using var conn2 = new SqlConnection(_connectionString);
                            await conn2.OpenAsync();
                            await LogActionAsync(conn2, "Delete Employee", $"Successfully deleted employee: {employeeName} (ID: {id})", true);

                            // Show success toast
                            _toastManager?.CreateToast("Success")
                                .WithContent($"Employee {employeeName} deleted successfully!")
                                .DismissOnClick()
                                .ShowSuccess();

                            return true;
                        }
                        else
                        {
                            // Rollback if no employee was found
                            transaction.Rollback();

                            _toastManager?.CreateToast("Warning")
                                .WithContent("Employee not found or already deleted.")
                                .DismissOnClick()
                                .ShowWarning();

                            return false;
                        }
                    }
                }
                catch
                {
                    // Rollback transaction on any error
                    transaction.Rollback();
                    throw; // Re-throw to be caught by outer catch block
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error deleting employee: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"DeleteEmployeeAsync Error: {ex}");

                // Log the error
                try
                {
                    using var conn2 = new SqlConnection(_connectionString);
                    await conn2.OpenAsync();
                    await LogActionAsync(conn2, "Delete Employee", $"Failed to delete employee ID: {id} - {ex.Message}", false);
                }
                catch { /* Ignore logging errors */ }

                return false;
            }
        }

        public async Task<ManageEmployeeModel?> ViewEmployeeProfileAsync(int employeeId)
        {
            ManageEmployeeModel employee = new ManageEmployeeModel(); // will already have defaults

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
            SELECT e.EmployeeId, 
       e.FirstName, 
       e.MiddleInitial, 
       e.LastName, 
       e.Gender, 
       e.ProfilePicture, 
       e.ContactNumber, 
       e.Age, 
       e.DateOfBirth, 
       e.HouseAddress, 
       e.HouseNumber, 
       e.Street, 
       e.Barangay, 
       e.CityTown, 
       e.Province,
       e.Username, 
       e.Position, 
       e.Status, 
       e.DateJoined,
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
                    employee.ID = reader["EmployeeId"]?.ToString() ?? employee.ID;
                    employee.Name = $"{reader["FirstName"]} {(string.IsNullOrWhiteSpace(reader["MiddleInitial"]?.ToString()) ? "" : reader["MiddleInitial"] + ". ")}{reader["LastName"]}";
                    employee.Username = reader["Username"]?.ToString() ?? employee.Username;
                    employee.ContactNumber = reader["ContactNumber"]?.ToString() ?? employee.ContactNumber;
                    employee.Position = reader["Position"]?.ToString() ?? employee.Position;
                    employee.Status = reader["Status"]?.ToString() ?? employee.Status;
                    employee.DateJoined = reader["DateJoined"] == DBNull.Value
                        ? employee.DateJoined
                        : Convert.ToDateTime(reader["DateJoined"]);

                    // Profile Picture
                    if (reader["ProfilePicture"] != DBNull.Value)
                    {
                        var bytes = (byte[])reader["ProfilePicture"];
                        employee.AvatarBytes = bytes;
                        employee.AvatarSource = ImageHelper.BytesToBitmap(bytes);
                    }
                    else
                    {
                        employee.AvatarSource = ImageHelper.GetDefaultAvatar();
                    }

                    // Extra fields with fallback
                    employee.Gender = reader["Gender"]?.ToString() ?? employee.Gender;
                    employee.Age = reader["Age"]?.ToString() ?? employee.Age;
                    employee.Birthdate = reader["DateOfBirth"] == DBNull.Value
                        ? employee.Birthdate
                        : Convert.ToDateTime(reader["DateOfBirth"]).ToString("yyyy-MM-dd");

                    employee.HouseAddress = reader["HouseAddress"]?.ToString() ?? employee.HouseAddress;
                    employee.HouseNumber = reader["HouseNumber"]?.ToString() ?? employee.HouseNumber;
                    employee.Street = reader["Street"]?.ToString() ?? employee.Street;
                    employee.Barangay = reader["Barangay"]?.ToString() ?? employee.Barangay;
                    employee.CityProvince = $"{reader["CityTown"]?.ToString() ?? ""}, {reader["Province"]?.ToString() ?? ""}".TrimEnd(',', ' ');
                    employee.LastLogin = reader["LastLogin"] == DBNull.Value ? "Never logged in" : Convert.ToDateTime(reader["LastLogin"]).ToString("MMMM dd, yyyy h:mm tt");
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error retrieving profile: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"ViewEmployeeProfileAsync Error: {ex}");
            }

            return employee;
        }

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool? success = null)
        {
            try
            {
                using var logCmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime) 
              VALUES (@username, @role, @actionType, @description, @success, GETDATE())", conn);

                logCmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@success", success.HasValue ? success.Value : (object)DBNull.Value);

                await logCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Log Error")
                    .WithContent($"Failed to save log: {ex.Message}")
                    .WithDelay(10)
                    .ShowError();
            }
        }

    }
}
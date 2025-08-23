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
                // Add this debug line to check if ProfilePicture is null
                System.Diagnostics.Debug.WriteLine($"ProfilePicture is null: {employee.ProfilePicture == null}");
                if (employee.ProfilePicture != null)
                {
                    System.Diagnostics.Debug.WriteLine($"ProfilePicture type: {employee.ProfilePicture.GetType()}");
                }


                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    INSERT INTO Employees 
                    (FirstName, MiddleInitial, LastName, Gender, ProfilePicture, ContactNumber, Age, DateOfBirth, 
                     HouseAddress, HouseNumber, Street, Barangay, CityTown, Province, 
                     Username, Password, Status, Position)
                    VALUES 
                    (@FirstName, @MiddleInitial, @LastName, @Gender, @ProfilePicture, @ContactNumber, @Age, @DateOfBirth, 
                     @HouseAddress, @HouseNumber, @Street, @Barangay, @CityTown, @Province, 
                     @Username, @Password, @Status, @Position)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@FirstName", employee.FirstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@MiddleInitial", employee.MiddleInitial ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LastName", employee.LastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Gender", employee.Gender ?? (object)DBNull.Value);

                // SIMPLIFIED: Use ImageHelper to convert ProfilePicture to bytes
                byte[]? profilePictureBytes = null;
                if (employee.ProfilePicture != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Converting ProfilePicture of type: {employee.ProfilePicture.GetType()}");

                    try
                    {
                        if (employee.ProfilePicture is byte[] bytes)
                        {
                            profilePictureBytes = bytes;
                            System.Diagnostics.Debug.WriteLine($"Used byte[] directly, length: {bytes.Length}");
                        }
                        else if (employee.ProfilePicture is Bitmap bitmap)
                        {
                            profilePictureBytes = ImageHelper.BitmapToBytes(bitmap);
                            System.Diagnostics.Debug.WriteLine($"Converted from Bitmap, length: {profilePictureBytes?.Length}");
                        }
                        else if (employee.ProfilePicture is string base64String && !string.IsNullOrEmpty(base64String))
                        {
                            var bitmapFromBase64 = ImageHelper.Base64ToBitmap(base64String);
                            profilePictureBytes = ImageHelper.BitmapToBytes(bitmapFromBase64);
                            System.Diagnostics.Debug.WriteLine($"Converted from Base64, length: {profilePictureBytes?.Length}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Unsupported ProfilePicture type: {employee.ProfilePicture.GetType()}");
                        }
                    }
                    catch (Exception conversionEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error converting ProfilePicture: {conversionEx.Message}");
                        _toastManager?.CreateToast("Image Error")
                            .WithContent("Failed to process profile picture")
                            .DismissOnClick()
                            .ShowError();
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Final profilePictureBytes is null: {profilePictureBytes == null}");

                cmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary, -1).Value =
                profilePictureBytes ?? (object)DBNull.Value;

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

                int rows = await cmd.ExecuteNonQueryAsync();

                if (rows > 0)
                {
                    _toastManager?.CreateToast("Success")
                        .WithContent("Employee added successfully!")
                        .DismissOnClick()
                        .ShowSuccess();
                    return true;
                }
                else
                {
                    _toastManager?.CreateToast("Error")
                        .WithContent("Failed to add employee - no rows affected")
                        .DismissOnClick()
                        .ShowError();
                    return false;
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error adding employee: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"AddEmployeeAsync Error: {ex}");
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
                                    AvatarSource = reader["ProfilePicture"] == DBNull.Value
                                        ? ImageHelper.GetDefaultAvatar()
                                        : ImageHelper.GetAvatarOrDefault((byte[])reader["ProfilePicture"])
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
                                AvatarSource = reader["ProfilePicture"] == DBNull.Value
                                    ? ImageHelper.GetDefaultAvatar()
                                    : ImageHelper.GetAvatarOrDefault((byte[])reader["ProfilePicture"])
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
                return rows > 0;
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

                string query = "DELETE FROM Employees WHERE EmployeeId=@EmployeeId";
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@EmployeeId", id);

                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error deleting employee: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"DeleteEmployeeAsync Error: {ex}");
                return false;
            }
        }
    }
}
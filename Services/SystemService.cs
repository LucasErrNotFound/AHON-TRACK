using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
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
    public class SystemService : ISystemService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public SystemService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        // Logs user actions to SystemLogs table
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
        #region Package Management - Updated with DiscountFor
        // Simple method with minimal parameters (2 parameters)
        public async Task AddPackageAsync(string packageName, decimal price)
        {
            const string query = @"
        INSERT INTO Package (packageName, price, description, duration, features1, features2, features3, features4, features5, discount, discountType, discountFor, discountedPrice, validFrom, validTo)
        VALUES (@packageName, @price, @description, @duration, @features1, @features2, @features3, @features4, @features5, @discount, @discountType, @discountFor, @discountedPrice, @validFrom, @validTo)";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@packageName", packageName);
                        command.Parameters.AddWithValue("@price", price);
                        command.Parameters.AddWithValue("@description", DBNull.Value);
                        command.Parameters.AddWithValue("@duration", "monthly"); // Changed to string
                        command.Parameters.AddWithValue("@features1", DBNull.Value);
                        command.Parameters.AddWithValue("@features2", DBNull.Value);
                        command.Parameters.AddWithValue("@features3", DBNull.Value);
                        command.Parameters.AddWithValue("@features4", DBNull.Value);
                        command.Parameters.AddWithValue("@features5", DBNull.Value);
                        command.Parameters.AddWithValue("@discount", 0);
                        command.Parameters.AddWithValue("@discountType", DBNull.Value);
                        command.Parameters.AddWithValue("@discountFor", DBNull.Value);
                        command.Parameters.AddWithValue("@discountedPrice", price);
                        command.Parameters.AddWithValue("@validFrom", DateTime.Now);
                        command.Parameters.AddWithValue("@validTo", DateTime.Now.AddDays(365));

                        await command.ExecuteNonQueryAsync();
                    }

                    await LogActionAsync(connection, "Added new package", $"Added new package: '{packageName}' with price ${price:F2}", true);
                }

                _toastManager.CreateToast($"Package '{packageName}' added successfully!");
            }
            catch (SqlException ex)
            {
                // Log failed action
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to add package", $"Failed to add package: '{packageName}' - SQL Error: {ex.Message}", false);
                }

                // Handle SQL-specific errors
                _toastManager.CreateToast($"Database error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                // Log failed action
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to add package", $"Failed to add package: '{packageName}' - Error: {ex.Message}", false);
                }

                // Handle general errors
                _toastManager.CreateToast($"Error adding package: {ex.Message}");
                throw;
            }
        }

        // Full method with all parameters (14 parameters - now includes discountFor)
        // Full method with proper discounted price calculation
        public async Task AddPackageAsync(string packageName, decimal price, string description,
            string duration, string features1, string features2, string features3,
            string features4, string features5, decimal discount, string discountType, string discountFor,
            DateTime validFrom, DateTime validTo)
        {
            const string query = @"
INSERT INTO Package (packageName, price, description, duration, features1, features2, features3, features4, features5, discount, discountType, discountFor, discountedPrice, validFrom, validTo)
VALUES (@packageName, @price, @description, @duration, @features1, @features2, @features3, @features4, @features5, @discount, @discountType, @discountFor, @discountedPrice, @validFrom, @validTo)";

            try
            {
                // ✅ calculate discounted price only if discount is set
                decimal discountedPrice = CalculateDiscountedPrice(price, discount, discountType);

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@packageName", packageName);
                        command.Parameters.AddWithValue("@price", price);
                        command.Parameters.AddWithValue("@description", (object)description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@duration", duration);
                        command.Parameters.AddWithValue("@features1", (object)features1 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features2", (object)features2 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features3", (object)features3 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features4", (object)features4 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features5", (object)features5 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@discount", discount);
                        command.Parameters.AddWithValue("@discountType", (object)discountType ?? DBNull.Value);
                        command.Parameters.AddWithValue("@discountFor", (object)discountFor ?? DBNull.Value);
                        command.Parameters.AddWithValue("@discountedPrice", discountedPrice); // ✅ uses calculated value
                        command.Parameters.AddWithValue("@validFrom", validFrom);
                        command.Parameters.AddWithValue("@validTo", validTo);

                        await command.ExecuteNonQueryAsync();
                    }

                    // Log
                    var logDescription = $"Added package: '{packageName}' - Price: {price:C}, Duration: {duration}";
                    if (discount > 0)
                    {
                        logDescription += $", Discount: {discount}{(discountType?.ToLower() == "percentage" ? "%" : " fixed")} for {discountFor ?? "All"}, Final Price: {discountedPrice:C}";
                    }

                    await LogActionAsync(connection, "Add new package", logDescription, true);
                }

                _toastManager.CreateToast($"Package '{packageName}' added successfully!");
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to add package", $"Failed to add package '{packageName}' - Error: {ex.Message}", false);
                }

                _toastManager.CreateToast($"Error adding package: {ex.Message}");
                throw;
            }
        }

        // Method to add package using a model (1 parameter)
        public async Task AddPackageAsync(PackageModel package)
        {
            await AddPackageAsync(
                package.packageName,
                package.price,
                package.description,
                package.duration,
                package.features1,
                package.features2,
                package.features3,
                package.features4,
                package.features5,
                package.discount,
                package.discountType,
                package.discountFor, // Added discountFor
                package.validFrom,
                package.validTo
            );
        }

        public async Task<List<Package>> GetPackagesAsync()
        {
            var packages = new List<Package>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT packageID, packageName, price, description, duration, 
                       features1, features2, features3, features4, features5, 
                       discount, discountType, discountFor, discountedPrice, validFrom, validTo
                FROM Package 
                ORDER BY packageName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var features = new List<string>();

                                for (int i = 1; i <= 5; i++)
                                {
                                    var feature = reader[$"features{i}"]?.ToString()?.Trim();
                                    if (!string.IsNullOrWhiteSpace(feature))
                                    {
                                        features.Add(feature);
                                    }
                                }

                                packages.Add(new Package
                                {
                                    PackageId = reader.GetInt32("packageID"), // Add this
                                    Title = reader["packageName"]?.ToString() ?? "",
                                    Description = reader["description"]?.ToString() ?? "",
                                    Price = Convert.ToInt32(reader["price"]),
                                    DiscountedPrice = Convert.ToInt32(reader["discountedPrice"]), // Add this
                                    Duration = reader["duration"]?.ToString() ?? "",
                                    Features = features,
                                    IsDiscountChecked = Convert.ToDecimal(reader["discount"]) > 0,
                                    DiscountValue = reader["discount"] != DBNull.Value ? (int?)Convert.ToDecimal(reader["discount"]) : null,
                                    SelectedDiscountType = reader["discountType"]?.ToString() ?? "",
                                    SelectedDiscountFor = reader["discountFor"]?.ToString() ?? "",
                                    DiscountValidFrom = reader["validFrom"] != DBNull.Value ? DateOnly.FromDateTime(reader.GetDateTime("validFrom")) : null,
                                    DiscountValidTo = reader["validTo"] != DBNull.Value ? DateOnly.FromDateTime(reader.GetDateTime("validTo")) : null
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to load packages: {ex.Message}")
                    .ShowError();
            }
            return packages;
        }

        public async Task<bool> UpdatePackageAsync(PackageModel package)
        {
            const string query = @"
        UPDATE Package SET 
            packageName = @packageName,
            price = @price,
            description = @description,
            duration = @duration,
            features1 = @features1,
            features2 = @features2,
            features3 = @features3,
            features4 = @features4,
            features5 = @features5,
            discount = @discount,
            discountType = @discountType,
            discountFor = @discountFor,
            discountedPrice = @discountedPrice,
            validFrom = @validFrom,
            validTo = @validTo
        WHERE packageID = @packageID";

            try
            {
                // Calculate discounted price
                decimal discountedPrice = CalculateDiscountedPrice(package.price, package.discount, package.discountType);

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@packageID", package.packageID);
                        command.Parameters.AddWithValue("@packageName", package.packageName);
                        command.Parameters.AddWithValue("@price", package.price);
                        command.Parameters.AddWithValue("@description", (object)package.description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@duration", package.duration);
                        command.Parameters.AddWithValue("@features1", (object)package.features1 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features2", (object)package.features2 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features3", (object)package.features3 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features4", (object)package.features4 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@features5", (object)package.features5 ?? DBNull.Value);
                        command.Parameters.AddWithValue("@discount", package.discount);
                        command.Parameters.AddWithValue("@discountType", (object)package.discountType ?? DBNull.Value);
                        command.Parameters.AddWithValue("@discountFor", (object)package.discountFor ?? DBNull.Value);
                        command.Parameters.AddWithValue("@discountedPrice", discountedPrice);
                        command.Parameters.AddWithValue("@validFrom", package.validFrom);
                        command.Parameters.AddWithValue("@validTo", package.validTo);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Update Package", $"Updated package: '{package.packageName}' (ID: {package.packageID})", true);
                            _toastManager.CreateToast($"Package '{package.packageName}' updated successfully!");
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
                    await LogActionAsync(connection, "Failed to update package", $"Failed to update package: '{package.packageName}' - SQL Error: {ex.Message}", false);
                }
                _toastManager.CreateToast($"Database error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to update package", $"Failed to update package: '{package.packageName}' - Error: {ex.Message}", false);
                }
                _toastManager.CreateToast($"Error updating package: {ex.Message}");
                throw;
            }
            return false;
        }

        private decimal CalculateDiscountedPrice(decimal originalPrice, decimal discount, string discountType)
        {
            if (discount <= 0) return originalPrice;

            decimal discountedPrice = originalPrice;

            if (discountType?.ToLower() == "percentage")
            {
                discountedPrice = originalPrice - (originalPrice * discount / 100);
            }
            else if (discountType?.ToLower() == "fixed")
            {
                discountedPrice = originalPrice - discount;
                if (discountedPrice < 0) discountedPrice = 0;
            }

            return discountedPrice;
        }

        public async Task<bool> DeletePackageAsync(int packageId)
        {
            const string query = "DELETE FROM Package WHERE packageID = @packageID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var packageName = await GetPackageNameByIdAsync(connection, packageId);

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@packageID", packageId);
                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Delete Package", $"Deleted package: '{packageName}' (ID: {packageId})", true);
                            _toastManager.CreateToast($"Package '{packageName}' deleted successfully!");
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
                    await LogActionAsync(connection, "Failed to delete package", $"Failed to delete package ID {packageId} - Error: {ex.Message}", false);
                }
                _toastManager.CreateToast($"Error deleting package: {ex.Message}");
                throw;
            }
            return false;
        }

        private async Task<string> GetPackageNameByIdAsync(SqlConnection connection, int packageId)
        {
            const string query = "SELECT packageName FROM Package WHERE packageID = @packageID";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@packageID", packageId);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? "Unknown Package";
            }
        }

        #endregion

        // Other service methods can be added here

        #region Member Check-in/out Operations

        public async Task<List<MemberPerson>> GetMemberCheckInsAsync(DateTime date)
        {
            var memberCheckIns = new List<MemberPerson>();
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        mci.CheckInId,
                        m.Firstname + ' ' + ISNULL(m.MiddleInitial + '. ', '') + m.Lastname AS FullName,
                        m.ContactNumber,
                        m.Status,
                        m.ProfilePicture,
                        mci.CheckInTime,
                        mci.CheckOutTime,
                        mci.DateAttendance
                    FROM MemberCheckIns mci
                    INNER JOIN Members m ON mci.MemberId = m.MemberId
                    WHERE CAST(mci.DateAttendance AS DATE) = @Date
                    ORDER BY mci.CheckInTime DESC";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Date", date.Date);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var nameParts = (reader["FullName"]?.ToString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string firstName = nameParts.Length > 0 ? nameParts[0] : "";
                    string lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";

                    byte[]? profilePictureBytes = null;
                    if (!reader.IsDBNull("ProfilePicture"))
                        profilePictureBytes = (byte[])reader["ProfilePicture"];

                    memberCheckIns.Add(new MemberPerson
                    {
                        ID = reader.GetInt32("CheckInId"), // Use CheckInId for operations
                        FirstName = firstName,
                        LastName = lastName,
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                        MembershipType = "Member", // You might want to add this to your Members table
                        Status = reader["Status"]?.ToString() ?? "",
                        DateAttendance = reader.GetDateTime("DateAttendance"),
                        CheckInTime = reader.GetDateTime("CheckInTime"),
                        CheckOutTime = reader.IsDBNull("CheckOutTime") ? null : reader.GetDateTime("CheckOutTime"),
                        MemberPicture = GetMemberPicturePath(profilePictureBytes)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMemberCheckInsAsync] {ex.Message}");
                _toastManager?.CreateToast("Database Error")
                              .WithContent($"Failed to load member check-ins: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return memberCheckIns;
        }

        public async Task<bool> CheckInMemberAsync(int memberId)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check if member already checked in today
                const string checkQuery = @"
                    SELECT COUNT(*) FROM MemberCheckIns 
                    WHERE MemberId = @MemberId AND CAST(DateAttendance AS DATE) = CAST(GETDATE() AS DATE)
                    AND CheckOutTime IS NULL";

                await using var checkCommand = new SqlCommand(checkQuery, connection);
                checkCommand.Parameters.AddWithValue("@MemberId", memberId);
                var existingCount = (int)await checkCommand.ExecuteScalarAsync();

                if (existingCount > 0)
                {
                    _toastManager?.CreateToast("Already Checked In")
                                 .WithContent("Member is already checked in today")
                                 .ShowWarning();
                    return false;
                }

                // Insert new check-in record
                const string insertQuery = @"
                    INSERT INTO MemberCheckIns (MemberId, CheckInTime, DateAttendance)
                    VALUES (@MemberId, GETDATE(), CAST(GETDATE() AS DATE))";

                await using var command = new SqlCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@MemberId", memberId);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    // Get member info for logging
                    var memberInfo = await GetMemberInfoAsync(connection, memberId);

                    await LogActionAsync(connection, "Member Check-In",
                        $"Member {memberInfo?.Name ?? memberId.ToString()} checked in", true);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckInMemberAsync] {ex.Message}");
                _toastManager?.CreateToast("Check-In Error")
                              .WithContent($"Failed to check in member: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return false;
        }

        public async Task<bool> CheckOutMemberAsync(int memberCheckInId)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string updateQuery = @"
                    UPDATE MemberCheckIns 
                    SET CheckOutTime = GETDATE() 
                    WHERE CheckInId = @CheckInId AND CheckOutTime IS NULL";

                await using var command = new SqlCommand(updateQuery, connection);
                command.Parameters.AddWithValue("@CheckInId", memberCheckInId);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(connection, "Member Check-Out",
                        $"Member checked out (CheckInId: {memberCheckInId})", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckOutMemberAsync] {ex.Message}");
                _toastManager?.CreateToast("Check-Out Error")
                              .WithContent($"Failed to check out member: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return false;
        }

        #endregion

        #region Walk-in Check-in/out Operations

        public async Task<List<WalkInPerson>> GetWalkInCheckInsAsync(DateTime date)
        {
            var walkInCheckIns = new List<WalkInPerson>();
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        CheckInId,
                        FirstName,
                        LastName,
                        Age,
                        ContactNumber,
                        PackageType,
                        CheckInTime,
                        CheckOutTime,
                        DateAttendance
                    FROM WalkInCheckIns
                    WHERE CAST(DateAttendance AS DATE) = @Date
                    ORDER BY CheckInTime DESC";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Date", date.Date);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    walkInCheckIns.Add(new WalkInPerson
                    {
                        ID = reader.GetInt32("CheckInId"),
                        FirstName = reader["FirstName"]?.ToString() ?? "",
                        LastName = reader["LastName"]?.ToString() ?? "",
                        Age = reader.GetInt32("Age"),
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                        PackageType = reader["PackageType"]?.ToString() ?? "",
                        DateAttendance = reader.GetDateTime("DateAttendance"),
                        CheckInTime = reader.GetDateTime("CheckInTime"),
                        CheckOutTime = reader.IsDBNull("CheckOutTime") ? null : reader.GetDateTime("CheckOutTime")
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetWalkInCheckInsAsync] {ex.Message}");
                _toastManager?.CreateToast("Database Error")
                              .WithContent($"Failed to load walk-in check-ins: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return walkInCheckIns;
        }

        public async Task<bool> CheckOutWalkInAsync(int walkInCheckInId)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string updateQuery = @"
                    UPDATE WalkInCheckIns 
                    SET CheckOutTime = GETDATE() 
                    WHERE CheckInId = @CheckInId AND CheckOutTime IS NULL";

                await using var command = new SqlCommand(updateQuery, connection);
                command.Parameters.AddWithValue("@CheckInId", walkInCheckInId);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(connection, "Walk-in Check-Out",
                        $"Walk-in checked out (CheckInId: {walkInCheckInId})", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckOutWalkInAsync] {ex.Message}");
                _toastManager?.CreateToast("Check-Out Error")
                              .WithContent($"Failed to check out walk-in: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return false;
        }

        #endregion

        #region Delete Operations

        public async Task<bool> DeleteMemberCheckInAsync(int memberCheckInId)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string deleteQuery = "DELETE FROM MemberCheckIns WHERE CheckInId = @CheckInId";
                await using var command = new SqlCommand(deleteQuery, connection);
                command.Parameters.AddWithValue("@CheckInId", memberCheckInId);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(connection, "Delete Member Check-In",
                        $"Deleted member check-in record (CheckInId: {memberCheckInId})", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteMemberCheckInAsync] {ex.Message}");
                _toastManager?.CreateToast("Delete Error")
                              .WithContent($"Failed to delete member check-in: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return false;
        }

        public async Task<bool> DeleteWalkInCheckInAsync(int walkInCheckInId)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string deleteQuery = "DELETE FROM WalkInCheckIns WHERE CheckInId = @CheckInId";
                await using var command = new SqlCommand(deleteQuery, connection);
                command.Parameters.AddWithValue("@CheckInId", walkInCheckInId);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(connection, "Delete Walk-in Check-In",
                        $"Deleted walk-in check-in record (CheckInId: {walkInCheckInId})", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteWalkInCheckInAsync] {ex.Message}");
                _toastManager?.CreateToast("Delete Error")
                              .WithContent($"Failed to delete walk-in check-in: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return false;
        }

        #endregion

        #region Available Members

        public async Task<List<ManageMemberModel>> GetAvailableMembersForCheckInAsync()
        {
            var members = new List<ManageMemberModel>();
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Simplified query - just get all active members
                const string query = @"SELECT m.MemberID, (m.Firstname + ' '  + ISNULL(NULLIF(m.MiddleInitial, '') + '. ', '')  + m.Lastname) AS Name,
                    m.ContactNumber,
                    m.Status,
                    m.Validity,
                    m.ProfilePicture
                    FROM Members m
                    WHERE m.Status = 'Active'
                    ORDER BY MemberID;";

                await using var command = new SqlCommand(query, connection);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    byte[]? bytes = null;
                    if (!reader.IsDBNull("ProfilePicture"))
                        bytes = (byte[])reader["ProfilePicture"];

                    string validityDisplay = string.Empty;
                    if (!reader.IsDBNull("Validity"))
                    {
                        var validityDate = reader.GetDateTime("Validity");
                        validityDisplay = validityDate.ToString("MMM dd, yyyy");
                    }

                    members.Add(new ManageMemberModel
                    {
                        MemberID = reader.GetInt32("MemberId"),
                        Name = reader["Name"]?.ToString() ?? string.Empty,
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                        Status = reader["Status"]?.ToString() ?? string.Empty,
                        Validity = validityDisplay,
                        ProfilePicture = ImageHelper.GetAvatarOrDefault(bytes)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAvailableMembersForCheckInAsync] {ex.Message}");
                _toastManager?.CreateToast("Database Error")
                              .WithContent($"Failed to load available members: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return members;
        }

        #endregion

        #region Helper Methods

        public async Task<MemberPerson?> GetMemberCheckInByIdAsync(int memberCheckInId)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        mci.CheckInId,
                        m.Firstname + ' ' + ISNULL(m.MiddleInitial + '. ', '') + m.Lastname AS FullName,
                        m.ContactNumber,
                        m.Status,
                        m.ProfilePicture,
                        mci.CheckInTime,
                        mci.CheckOutTime,
                        mci.DateAttendance
                    FROM MemberCheckIns mci
                    INNER JOIN Members m ON mci.MemberId = m.MemberId
                    WHERE mci.CheckInId = @CheckInId";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CheckInId", memberCheckInId);
                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var nameParts = (reader["FullName"]?.ToString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string firstName = nameParts.Length > 0 ? nameParts[0] : "";
                    string lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";

                    byte[]? profilePictureBytes = null;
                    if (!reader.IsDBNull("ProfilePicture"))
                        profilePictureBytes = (byte[])reader["ProfilePicture"];

                    return new MemberPerson
                    {
                        ID = reader.GetInt32("CheckInId"),
                        FirstName = firstName,
                        LastName = lastName,
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                        MembershipType = "Member",
                        Status = reader["Status"]?.ToString() ?? "",
                        DateAttendance = reader.GetDateTime("DateAttendance"),
                        CheckInTime = reader.GetDateTime("CheckInTime"),
                        CheckOutTime = reader.IsDBNull("CheckOutTime") ? null : reader.GetDateTime("CheckOutTime"),
                        MemberPicture = GetMemberPicturePath(profilePictureBytes)
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMemberCheckInByIdAsync] {ex.Message}");
            }
            return null;
        }

        public async Task<WalkInPerson?> GetWalkInCheckInByIdAsync(int walkInCheckInId)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        CheckInId,
                        FirstName,
                        LastName,
                        Age,
                        ContactNumber,
                        PackageType,
                        CheckInTime,
                        CheckOutTime,
                        DateAttendance
                    FROM WalkInCheckIns
                    WHERE CheckInId = @CheckInId";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CheckInId", walkInCheckInId);
                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new WalkInPerson
                    {
                        ID = reader.GetInt32("CheckInId"),
                        FirstName = reader["FirstName"]?.ToString() ?? "",
                        LastName = reader["LastName"]?.ToString() ?? "",
                        Age = reader.GetInt32("Age"),
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                        PackageType = reader["PackageType"]?.ToString() ?? "",
                        DateAttendance = reader.GetDateTime("DateAttendance"),
                        CheckInTime = reader.GetDateTime("CheckInTime"),
                        CheckOutTime = reader.IsDBNull("CheckOutTime") ? null : reader.GetDateTime("CheckOutTime")
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetWalkInCheckInByIdAsync] {ex.Message}");
            }
            return null;
        }

        private async Task<ManageMemberModel?> GetMemberInfoAsync(SqlConnection connection, int memberId)
        {
            const string query = @"
                SELECT 
                    MemberId,
                    (Firstname + ' ' + ISNULL(MiddleInitial + '. ', '') + Lastname) AS Name,
                    ContactNumber,
                    Status
                FROM Members WHERE MemberId = @MemberId";

            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@MemberId", memberId);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new ManageMemberModel
                {
                    MemberID = reader.GetInt32("MemberId"),
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? string.Empty
                };
            }
            return null;
        }

        private string GetMemberPicturePath(byte[]? profilePictureBytes)
        {
            if (profilePictureBytes != null && profilePictureBytes.Length > 0)
            {
                // You might want to convert bytes to image path or return a default
                return "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png";
            }
            return "avares://AHON_TRACK/Assets/MainWindowView/user.png";
        }

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
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
                logCmd.Parameters.AddWithValue("@success", success);

                await logCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        #endregion

        #region Equipment Management

        public async Task<List<EquipmentModel>> GetEquipmentAsync()
        {
            var equipment = new List<EquipmentModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT equipmentID, equipmentName, category, currentStock, 
                       purchaseDate, purchasePrice, supplier, warrantyExpiry, 
                       condition, status, lastMaintenance, nextMaintenance
                FROM Equipment 
                ORDER BY equipmentName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                equipment.Add(new EquipmentModel
                                {
                                    EquipmentID = reader["equipmentID"]?.ToString() ?? "",
                                    EquipmentName = reader["equipmentName"]?.ToString() ?? "",
                                    Category = reader["category"]?.ToString() ?? "",
                                    CurrentStock = reader["currentStock"] != DBNull.Value ? reader.GetInt32("currentStock") : 0,
                                    PurchaseDate = reader["purchaseDate"] != DBNull.Value ? reader.GetDateTime("purchaseDate") : null,
                                    PurchasePrice = reader["purchasePrice"] != DBNull.Value ? reader.GetDecimal("purchasePrice") : null,
                                    Supplier = reader["supplier"]?.ToString() ?? "",
                                    WarrantyExpiry = reader["warrantyExpiry"] != DBNull.Value ? reader.GetDateTime("warrantyExpiry") : null,
                                    Condition = reader["condition"]?.ToString() ?? "",
                                    Status = reader["status"]?.ToString() ?? "",
                                    LastMaintenance = reader["lastMaintenance"] != DBNull.Value ? reader.GetDateTime("lastMaintenance") : null,
                                    NextMaintenance = reader["nextMaintenance"] != DBNull.Value ? reader.GetDateTime("nextMaintenance") : null
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load equipment: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }
            return equipment;
        }

        public async Task<bool> AddEquipmentAsync(EquipmentModel equipment)
        {
            const string query = @"
        INSERT INTO Equipment (equipmentID, equipmentName, category, currentStock, 
                              purchaseDate, purchasePrice, supplier, warrantyExpiry, 
                              condition, status, lastMaintenance, nextMaintenance)
        VALUES (@equipmentID, @equipmentName, @category, @currentStock, 
                @purchaseDate, @purchasePrice, @supplier, @warrantyExpiry, 
                @condition, @status, @lastMaintenance, @nextMaintenance)";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check if equipment ID already exists
                    const string checkQuery = "SELECT COUNT(*) FROM Equipment WHERE equipmentID = @equipmentID";
                    using (var checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@equipmentID", equipment.EquipmentID);
                        var exists = (int)await checkCommand.ExecuteScalarAsync() > 0;

                        if (exists)
                        {
                            _toastManager?.CreateToast("Duplicate Equipment ID")
                                .WithContent($"Equipment ID '{equipment.EquipmentID}' already exists")
                                .ShowError();
                            return false;
                        }
                    }

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@equipmentID", equipment.EquipmentID);
                        command.Parameters.AddWithValue("@equipmentName", equipment.EquipmentName);
                        command.Parameters.AddWithValue("@category", equipment.Category);
                        command.Parameters.AddWithValue("@currentStock", equipment.CurrentStock);
                        command.Parameters.AddWithValue("@purchaseDate", (object)equipment.PurchaseDate ?? DBNull.Value);
                        command.Parameters.AddWithValue("@purchasePrice", (object)equipment.PurchasePrice ?? DBNull.Value);
                        command.Parameters.AddWithValue("@supplier", (object)equipment.Supplier ?? DBNull.Value);
                        command.Parameters.AddWithValue("@warrantyExpiry", (object)equipment.WarrantyExpiry ?? DBNull.Value);
                        command.Parameters.AddWithValue("@condition", (object)equipment.Condition ?? DBNull.Value);
                        command.Parameters.AddWithValue("@status", (object)equipment.Status ?? DBNull.Value);
                        command.Parameters.AddWithValue("@lastMaintenance", (object)equipment.LastMaintenance ?? DBNull.Value);
                        command.Parameters.AddWithValue("@nextMaintenance", (object)equipment.NextMaintenance ?? DBNull.Value);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Add Equipment",
                                $"Added equipment: '{equipment.EquipmentName}' (ID: {equipment.EquipmentID})", true);

                            _toastManager?.CreateToast("Equipment Added")
                                .WithContent($"Equipment '{equipment.EquipmentName}' added successfully!")
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
                    await LogActionAsync(connection, "Failed to add equipment",
                        $"Failed to add equipment '{equipment.EquipmentName}' - SQL Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to add equipment: {ex.Message}")
                    .ShowError();
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to add equipment",
                        $"Failed to add equipment '{equipment.EquipmentName}' - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error adding equipment: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> UpdateEquipmentAsync(EquipmentModel equipment)
        {
            const string query = @"
        UPDATE Equipment SET 
            equipmentName = @equipmentName,
            category = @category,
            currentStock = @currentStock,
            purchaseDate = @purchaseDate,
            purchasePrice = @purchasePrice,
            supplier = @supplier,
            warrantyExpiry = @warrantyExpiry,
            condition = @condition,
            status = @status,
            lastMaintenance = @lastMaintenance,
            nextMaintenance = @nextMaintenance
        WHERE equipmentID = @equipmentID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@equipmentID", equipment.EquipmentID);
                        command.Parameters.AddWithValue("@equipmentName", equipment.EquipmentName);
                        command.Parameters.AddWithValue("@category", equipment.Category);
                        command.Parameters.AddWithValue("@currentStock", equipment.CurrentStock);
                        command.Parameters.AddWithValue("@purchaseDate", (object)equipment.PurchaseDate ?? DBNull.Value);
                        command.Parameters.AddWithValue("@purchasePrice", (object)equipment.PurchasePrice ?? DBNull.Value);
                        command.Parameters.AddWithValue("@supplier", (object)equipment.Supplier ?? DBNull.Value);
                        command.Parameters.AddWithValue("@warrantyExpiry", (object)equipment.WarrantyExpiry ?? DBNull.Value);
                        command.Parameters.AddWithValue("@condition", (object)equipment.Condition ?? DBNull.Value);
                        command.Parameters.AddWithValue("@status", (object)equipment.Status ?? DBNull.Value);
                        command.Parameters.AddWithValue("@lastMaintenance", (object)equipment.LastMaintenance ?? DBNull.Value);
                        command.Parameters.AddWithValue("@nextMaintenance", (object)equipment.NextMaintenance ?? DBNull.Value);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Update Equipment",
                                $"Updated equipment: '{equipment.EquipmentName}' (ID: {equipment.EquipmentID})", true);

                            _toastManager?.CreateToast("Equipment Updated")
                                .WithContent($"Equipment '{equipment.EquipmentName}' updated successfully!")
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
                    await LogActionAsync(connection, "Failed to update equipment",
                        $"Failed to update equipment '{equipment.EquipmentName}' - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error updating equipment: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> DeleteEquipmentAsync(string equipmentID)
        {
            const string query = "DELETE FROM Equipment WHERE equipmentID = @equipmentID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Get equipment name for logging
                    var equipmentName = await GetEquipmentNameByIdAsync(connection, equipmentID);

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@equipmentID", equipmentID);
                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Delete Equipment",
                                $"Deleted equipment: '{equipmentName}' (ID: {equipmentID})", true);

                            _toastManager?.CreateToast("Equipment Deleted")
                                .WithContent($"Equipment '{equipmentName}' deleted successfully!")
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
                    await LogActionAsync(connection, "Failed to delete equipment",
                        $"Failed to delete equipment ID {equipmentID} - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error deleting equipment: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<List<EquipmentModel>> GetEquipmentByStatusAsync(string status)
        {
            var equipment = new List<EquipmentModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT equipmentID, equipmentName, category, currentStock, 
                       purchaseDate, purchasePrice, supplier, warrantyExpiry, 
                       condition, status, lastMaintenance, nextMaintenance
                FROM Equipment 
                WHERE status = @status
                ORDER BY equipmentName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@status", status);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                equipment.Add(new EquipmentModel
                                {
                                    EquipmentID = reader["equipmentID"]?.ToString() ?? "",
                                    EquipmentName = reader["equipmentName"]?.ToString() ?? "",
                                    Category = reader["category"]?.ToString() ?? "",
                                    CurrentStock = reader["currentStock"] != DBNull.Value ? reader.GetInt32("currentStock") : 0,
                                    PurchaseDate = reader["purchaseDate"] != DBNull.Value ? reader.GetDateTime("purchaseDate") : null,
                                    PurchasePrice = reader["purchasePrice"] != DBNull.Value ? reader.GetDecimal("purchasePrice") : null,
                                    Supplier = reader["supplier"]?.ToString() ?? "",
                                    WarrantyExpiry = reader["warrantyExpiry"] != DBNull.Value ? reader.GetDateTime("warrantyExpiry") : null,
                                    Condition = reader["condition"]?.ToString() ?? "",
                                    Status = reader["status"]?.ToString() ?? "",
                                    LastMaintenance = reader["lastMaintenance"] != DBNull.Value ? reader.GetDateTime("lastMaintenance") : null,
                                    NextMaintenance = reader["nextMaintenance"] != DBNull.Value ? reader.GetDateTime("nextMaintenance") : null
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load equipment by status: {ex.Message}")
                    .ShowError();
            }
            return equipment;
        }

        public async Task<List<EquipmentModel>> GetEquipmentNeedingMaintenanceAsync()
        {
            var equipment = new List<EquipmentModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT equipmentID, equipmentName, category, currentStock, 
                       purchaseDate, purchasePrice, supplier, warrantyExpiry, 
                       condition, status, lastMaintenance, nextMaintenance
                FROM Equipment 
                WHERE nextMaintenance <= DATEADD(day, 7, GETDATE()) 
                   AND nextMaintenance IS NOT NULL
                   AND status = 'Active'
                ORDER BY nextMaintenance";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                equipment.Add(new EquipmentModel
                                {
                                    EquipmentID = reader["equipmentID"]?.ToString() ?? "",
                                    EquipmentName = reader["equipmentName"]?.ToString() ?? "",
                                    Category = reader["category"]?.ToString() ?? "",
                                    CurrentStock = reader["currentStock"] != DBNull.Value ? reader.GetInt32("currentStock") : 0,
                                    PurchaseDate = reader["purchaseDate"] != DBNull.Value ? reader.GetDateTime("purchaseDate") : null,
                                    PurchasePrice = reader["purchasePrice"] != DBNull.Value ? reader.GetDecimal("purchasePrice") : null,
                                    Supplier = reader["supplier"]?.ToString() ?? "",
                                    WarrantyExpiry = reader["warrantyExpiry"] != DBNull.Value ? reader.GetDateTime("warrantyExpiry") : null,
                                    Condition = reader["condition"]?.ToString() ?? "",
                                    Status = reader["status"]?.ToString() ?? "",
                                    LastMaintenance = reader["lastMaintenance"] != DBNull.Value ? reader.GetDateTime("lastMaintenance") : null,
                                    NextMaintenance = reader["nextMaintenance"] != DBNull.Value ? reader.GetDateTime("nextMaintenance") : null
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load equipment needing maintenance: {ex.Message}")
                    .ShowError();
            }
            return equipment;
        }

        private async Task<string> GetEquipmentNameByIdAsync(SqlConnection connection, string equipmentID)
        {
            const string query = "SELECT equipmentName FROM Equipment WHERE equipmentID = @equipmentID";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@equipmentID", equipmentID);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? "Unknown Equipment";
            }
        }

        #endregion

        #region Product Management



        #endregion

    }
}

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
                                    EquipmentID = reader["equipmentID"] != DBNull.Value ? reader.GetInt32("equipmentID") : 0,
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
            // Updated query - removed equipmentID from INSERT, using OUTPUT to get generated ID
            const string query = @"
INSERT INTO Equipment (equipmentName, category, currentStock, 
                      purchaseDate, purchasePrice, supplier, warrantyExpiry, 
                      condition, status, lastMaintenance, nextMaintenance)
OUTPUT INSERTED.equipmentID
VALUES (@equipmentName, @category, @currentStock, 
        @purchaseDate, @purchasePrice, @supplier, @warrantyExpiry, 
        @condition, @status, @lastMaintenance, @nextMaintenance)";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        // No longer adding equipmentID parameter - it will be auto-generated
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

                        // ExecuteScalar returns the generated ID
                        var newId = await command.ExecuteScalarAsync();

                        if (newId != null && newId != DBNull.Value)
                        {
                            equipment.EquipmentID = Convert.ToInt32(newId);

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

        public async Task<bool> DeleteEquipmentAsync(int equipmentID)
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
                                    EquipmentID = reader["equipmentID"] != DBNull.Value ? reader.GetInt32("equipmentID") : 0,
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
                                    EquipmentID = reader["equipmentID"] != DBNull.Value ? reader.GetInt32("equipmentID") : 0,
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

        private async Task<string> GetEquipmentNameByIdAsync(SqlConnection connection, int equipmentID)
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

        public async Task<List<ProductModel>> GetProductsAsync()
        {
            var products = new List<ProductModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                ORDER BY ProductName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load products: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }
            return products;
        }

        public async Task<bool> AddProductAsync(ProductModel product)
        {
            product.Status = product.CurrentStock > 0 ? "In Stock" : "Out Of Stock";

            const string query = @"
        INSERT INTO Products (ProductName, SKU, ProductSupplier, Description, 
                             Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                             ExpiryDate, Status, Category, CurrentStock)
        OUTPUT INSERTED.ProductID
        VALUES (@ProductName, @SKU, @ProductSupplier, @Description,
                @Price, @DiscountedPrice, @IsPercentageDiscount, @ProductImagePath,
                @ExpiryDate, @Status, @Category, @CurrentStock)";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductName", product.ProductName);
                        command.Parameters.AddWithValue("@SKU", product.SKU);
                        command.Parameters.AddWithValue("@ProductSupplier", (object)product.ProductSupplier ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Description", (object)product.Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Price", product.Price);
                        command.Parameters.AddWithValue("@DiscountedPrice", (object)product.DiscountedPrice ?? DBNull.Value);
                        command.Parameters.AddWithValue("@IsPercentageDiscount", product.IsPercentageDiscount);
                        command.Parameters.AddWithValue("@ProductImagePath", (object)product.ProductImagePath ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ExpiryDate", (object)product.ExpiryDate ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Status", product.Status);
                        command.Parameters.AddWithValue("@Category", product.Category);
                        command.Parameters.AddWithValue("@CurrentStock", product.CurrentStock);

                        var newId = await command.ExecuteScalarAsync();

                        if (newId != null && newId != DBNull.Value)
                        {
                            product.ProductID = Convert.ToInt32(newId);

                            string logDescription = $"Added product: '{product.ProductName}' (SKU: {product.SKU}) - Price: ₱{product.Price:N2}, Stock: {product.CurrentStock}";
                            if (product.HasDiscount)
                            {
                                logDescription += $", Discount: {product.DiscountedPrice}{(product.IsPercentageDiscount ? "%" : " fixed")}, Final Price: ₱{product.FinalPrice:N2}";
                            }

                            await LogActionAsync(connection, "Add Product", logDescription, true);

                            _toastManager?.CreateToast("Product Added")
                                .WithContent($"Product '{product.ProductName}' added successfully!")
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
                    await LogActionAsync(connection, "Failed to add product",
                        $"Failed to add product '{product.ProductName}' - SQL Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to add product: {ex.Message}")
                    .ShowError();
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to add product",
                        $"Failed to add product '{product.ProductName}' - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error adding product: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> UpdateProductAsync(ProductModel product)
        {

            product.Status = product.CurrentStock > 0 ? "In Stock" : "Out Of Stock";

            const string query = @"
        UPDATE Products SET 
            ProductName = @ProductName,
            SKU = @SKU,
            ProductSupplier = @ProductSupplier,
            Description = @Description,
            Price = @Price,
            DiscountedPrice = @DiscountedPrice,
            IsPercentageDiscount = @IsPercentageDiscount,
            ProductImagePath = @ProductImagePath,
            ExpiryDate = @ExpiryDate,
            Status = @Status,
            Category = @Category,
            CurrentStock = @CurrentStock
        WHERE ProductID = @ProductID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductID", product.ProductID);
                        command.Parameters.AddWithValue("@ProductName", product.ProductName);
                        command.Parameters.AddWithValue("@SKU", product.SKU);
                        command.Parameters.AddWithValue("@ProductSupplier", (object)product.ProductSupplier ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Description", (object)product.Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Price", product.Price);
                        command.Parameters.AddWithValue("@DiscountedPrice", (object)product.DiscountedPrice ?? DBNull.Value);
                        command.Parameters.AddWithValue("@IsPercentageDiscount", product.IsPercentageDiscount);
                        command.Parameters.AddWithValue("@ProductImagePath", (object)product.ProductImagePath ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ExpiryDate", (object)product.ExpiryDate ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Status", product.Status);
                        command.Parameters.AddWithValue("@Category", product.Category);
                        command.Parameters.AddWithValue("@CurrentStock", product.CurrentStock);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Update Product",
                                $"Updated product: '{product.ProductName}' (ID: {product.ProductID})", true);

                            _toastManager?.CreateToast("Product Updated")
                                .WithContent($"Product '{product.ProductName}' updated successfully!")
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
                    await LogActionAsync(connection, "Failed to update product",
                        $"Failed to update product '{product.ProductName}' - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error updating product: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> DeleteProductAsync(int productID)
        {
            const string query = "DELETE FROM Products WHERE ProductID = @ProductID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var productName = await GetProductNameByIdAsync(connection, productID);

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductID", productID);
                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Delete Product",
                                $"Deleted product: '{productName}' (ID: {productID})", true);

                            _toastManager?.CreateToast("Product Deleted")
                                .WithContent($"Product '{productName}' deleted successfully!")
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
                    await LogActionAsync(connection, "Failed to delete product",
                        $"Failed to delete product ID {productID} - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error deleting product: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<List<ProductModel>> GetProductsByCategoryAsync(string category)
        {
            var products = new List<ProductModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE Category = @Category
                ORDER BY ProductName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Category", category);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load products by category: {ex.Message}")
                    .ShowError();
            }
            return products;
        }

        public async Task<List<ProductModel>> GetProductsByStatusAsync(string status)
        {
            var products = new List<ProductModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE Status = @Status
                ORDER BY ProductName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Status", status);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load products by status: {ex.Message}")
                    .ShowError();
            }
            return products;
        }

        public async Task<List<ProductModel>> GetExpiredProductsAsync()
        {
            var products = new List<ProductModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE ExpiryDate IS NOT NULL 
                  AND ExpiryDate < CAST(GETDATE() AS DATE)
                ORDER BY ExpiryDate DESC";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load expired products: {ex.Message}")
                    .ShowError();
            }
            return products;
        }

        public async Task<List<ProductModel>> GetProductsExpiringSoonAsync(int daysThreshold = 30)
        {
            var products = new List<ProductModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE ExpiryDate IS NOT NULL 
                  AND ExpiryDate >= CAST(GETDATE() AS DATE)
                  AND ExpiryDate <= DATEADD(day, @DaysThreshold, CAST(GETDATE() AS DATE))
                ORDER BY ExpiryDate";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@DaysThreshold", daysThreshold);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load expiring products: {ex.Message}")
                    .ShowError();
            }
            return products;
        }

        public async Task<ProductModel?> GetProductByIdAsync(int productID)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE ProductID = @ProductID";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductID", productID);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load product: {ex.Message}")
                    .ShowError();
            }
            return null;
        }

        public async Task<ProductModel?> GetProductBySKUAsync(string sku)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE SKU = @SKU";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SKU", sku);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load product by SKU: {ex.Message}")
                    .ShowError();
            }
            return null;
        }

        private async Task<string> GetProductNameByIdAsync(SqlConnection connection, int productID)
        {
            const string query = "SELECT ProductName FROM Products WHERE ProductID = @ProductID";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@ProductID", productID);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? "Unknown Product";
            }
        }

        #endregion

        #region Training Schedule Management

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

        #endregion

    }
}

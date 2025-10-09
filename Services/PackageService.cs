using AHON_TRACK.Models;
using AHON_TRACK.ViewModels;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AHON_TRACK.Services.Interface;

namespace AHON_TRACK.Services
{
    public class PackageService : IPackageService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public PackageService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
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
    }
}

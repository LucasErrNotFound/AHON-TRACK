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
using AHON_TRACK.Services.Events;

namespace AHON_TRACK.Services
{
    public class PackageService : IPackageService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;
        private const string FEATURE_SEPARATOR = "|";

        public event EventHandler? PackagesChanged;

        public PackageService(string connectionString, ToastManager toastManager)
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
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true || CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanDelete()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanView()
        {
            // All authenticated users can view packages
            return !string.IsNullOrWhiteSpace(CurrentUserModel.Username);
        }

        #endregion

        #region CREATE

        public async Task<(bool Success, string Message, int? PackageId)> AddPackageAsync(PackageModel package)
        {
            if (!CanCreate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You don't have permission to add packages.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to add packages.", null);
            }

            const string query = @"
                INSERT INTO Packages (PackageName, Price, Description, Duration, Features, Discount, DiscountType, DiscountFor, DiscountedPrice, ValidFrom, ValidTo, AddedByEmployeeID)
                OUTPUT INSERTED.PackageID
                VALUES (@packageName, @price, @description, @duration, @features, @discount, @discountType, @discountFor, @discountedPrice, @validFrom, @validTo, @addedByEmployeeID)";

            try
            {
                // Build features list from individual fields
                var features = new List<string>();
                if (!string.IsNullOrWhiteSpace(package.features1)) features.Add(package.features1);
                if (!string.IsNullOrWhiteSpace(package.features2)) features.Add(package.features2);
                if (!string.IsNullOrWhiteSpace(package.features3)) features.Add(package.features3);
                if (!string.IsNullOrWhiteSpace(package.features4)) features.Add(package.features4);
                if (!string.IsNullOrWhiteSpace(package.features5)) features.Add(package.features5);

                string featuresString = FeaturesToString(features);
                decimal discountedPrice = CalculateDiscountedPrice(package.price, package.discount, package.discountType);

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@packageName", package.packageName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@price", package.price);
                cmd.Parameters.AddWithValue("@description", (object)package.description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@duration", package.duration ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@features", (object)featuresString ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@discount", package.discount);
                cmd.Parameters.AddWithValue("@discountType", (object)package.discountType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@discountFor", (object)package.discountFor ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@discountedPrice", discountedPrice);
                cmd.Parameters.AddWithValue("@validFrom", package.validFrom);
                cmd.Parameters.AddWithValue("@validTo", package.validTo);
                cmd.Parameters.AddWithValue("@addedByEmployeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                var packageId = (int)await cmd.ExecuteScalarAsync();

                string logDescription = $"Added package: '{package.packageName}' - Price: ₱{package.price:N2}, Duration: {package.duration}";
                if (package.discount > 0)
                {
                    logDescription += $", Discount: {package.discount}{(package.discountType?.ToLower() == "percentage" ? "%" : " fixed")} for {package.discountFor ?? "All"}, Final Price: ₱{discountedPrice:N2}";
                }
                PackagesChanged?.Invoke(this, EventArgs.Empty);
                PackageEventService.Instance.NotifyPackagesChanged();

                await LogActionAsync(conn, "Added new package", logDescription, true);

                _toastManager.CreateToast("Package Added")
                    .WithContent($"Successfully added package '{package.packageName}'.")
                    .DismissOnClick()
                    .ShowSuccess();

                return (true, "Package added successfully.", packageId);
            }
            catch (SqlException ex)
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await LogActionAsync(conn, "Failed to add package", $"Failed to add package: '{package.packageName}' - SQL Error: {ex.Message}", false);

                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to add package: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                Console.WriteLine($"SQL Error: {ex.Message}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await LogActionAsync(conn, "Failed to add package", $"Failed to add package: '{package.packageName}' - Error: {ex.Message}", false);

                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                Console.WriteLine($"General Error: {ex.Message}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region READ

        public async Task<List<Package>> GetPackagesAsync()
        {
            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You don't have permission to view packages.")
                    .DismissOnClick()
                    .ShowError();
                return new List<Package>();
            }

            var packages = new List<Package>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT PackageID, PackageName, Price, Description, Duration, 
                           Features, Discount, DiscountType, DiscountFor, DiscountedPrice, 
                           ValidFrom, ValidTo, AddedByEmployeeID
                    FROM Packages 
                    ORDER BY PackageName";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var featuresString = reader["Features"]?.ToString() ?? string.Empty;
                    var features = StringToFeatures(featuresString);

                    packages.Add(new Package
                    {
                        PackageId = reader.GetInt32("PackageID"),
                        Title = reader["PackageName"]?.ToString() ?? "",
                        Description = reader["Description"]?.ToString() ?? "",
                        Price = Convert.ToInt32(reader["Price"]),
                        DiscountedPrice = Convert.ToInt32(reader["DiscountedPrice"]),
                        Duration = reader["Duration"]?.ToString() ?? "",
                        Features = features,
                        IsDiscountChecked = Convert.ToDecimal(reader["Discount"]) > 0,
                        DiscountValue = reader["Discount"] != DBNull.Value ? (int?)Convert.ToDecimal(reader["Discount"]) : null,
                        SelectedDiscountType = reader["DiscountType"]?.ToString() ?? "",
                        SelectedDiscountFor = reader["DiscountFor"]?.ToString() ?? "",
                        DiscountValidFrom = reader["ValidFrom"] != DBNull.Value ? DateOnly.FromDateTime(reader.GetDateTime("ValidFrom")) : null,
                        DiscountValidTo = reader["ValidTo"] != DBNull.Value ? DateOnly.FromDateTime(reader.GetDateTime("ValidTo")) : null
                    });
                }

                return packages;
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to load packages: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                Console.WriteLine($"SQL Error: {ex.Message}");
                return packages;
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                Console.WriteLine($"General Error: {ex.Message}");
                return packages;
            }
        }

        public async Task<(bool Success, string Message, PackageModel? Package)> GetPackageByIdAsync(int packageId)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view packages.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT PackageID, PackageName, Price, Description, Duration, 
                           Features, Discount, DiscountType, DiscountFor, DiscountedPrice, 
                           ValidFrom, ValidTo, AddedByEmployeeID
                    FROM Packages
                    WHERE PackageID = @packageId";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@packageId", packageId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var package = MapPackageFromReader(reader);
                    return (true, "Package retrieved successfully.", package);
                }

                return (false, "Package not found.", null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<Package>? Packages)> GetPackagesByDurationAsync(string duration)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view packages.", null);
            }

            var packages = new List<Package>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT PackageID, PackageName, Price, Description, Duration, 
                           Features, Discount, DiscountType, DiscountFor, DiscountedPrice, 
                           ValidFrom, ValidTo, AddedByEmployeeID
                    FROM Packages 
                    WHERE Duration = @duration
                    ORDER BY PackageName";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@duration", duration ?? (object)DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var featuresString = reader["Features"]?.ToString() ?? string.Empty;
                    var features = StringToFeatures(featuresString);

                    packages.Add(new Package
                    {
                        PackageId = reader.GetInt32("PackageID"),
                        Title = reader["PackageName"]?.ToString() ?? "",
                        Description = reader["Description"]?.ToString() ?? "",
                        Price = Convert.ToInt32(reader["Price"]),
                        DiscountedPrice = Convert.ToInt32(reader["DiscountedPrice"]),
                        Duration = reader["Duration"]?.ToString() ?? "",
                        Features = features,
                        IsDiscountChecked = Convert.ToDecimal(reader["Discount"]) > 0,
                        DiscountValue = reader["Discount"] != DBNull.Value ? (int?)Convert.ToDecimal(reader["Discount"]) : null,
                        SelectedDiscountType = reader["DiscountType"]?.ToString() ?? "",
                        SelectedDiscountFor = reader["DiscountFor"]?.ToString() ?? "",
                        DiscountValidFrom = reader["ValidFrom"] != DBNull.Value ? DateOnly.FromDateTime(reader.GetDateTime("ValidFrom")) : null,
                        DiscountValidTo = reader["ValidTo"] != DBNull.Value ? DateOnly.FromDateTime(reader.GetDateTime("ValidTo")) : null
                    });
                }

                return (true, "Packages retrieved successfully.", packages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region UPDATE

        public async Task<bool> UpdatePackageAsync(PackageModel package)
        {
            if (!CanUpdate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators and managers can update package information.")
                    .DismissOnClick()
                    .ShowError();
                return false;
            }

            const string query = @"
                UPDATE Packages SET 
                    PackageName = @packageName,
                    Price = @price,
                    Description = @description,
                    Duration = @duration,
                    Features = @features,
                    Discount = @discount,
                    DiscountType = @discountType,
                    DiscountFor = @discountFor,
                    DiscountedPrice = @discountedPrice,
                    ValidFrom = @validFrom,
                    ValidTo = @validTo
                WHERE PackageID = @packageID";

            try
            {
                decimal discountedPrice = CalculateDiscountedPrice(package.price, package.discount, package.discountType);

                var features = new List<string>();
                if (!string.IsNullOrWhiteSpace(package.features1)) features.Add(package.features1);
                if (!string.IsNullOrWhiteSpace(package.features2)) features.Add(package.features2);
                if (!string.IsNullOrWhiteSpace(package.features3)) features.Add(package.features3);
                if (!string.IsNullOrWhiteSpace(package.features4)) features.Add(package.features4);
                if (!string.IsNullOrWhiteSpace(package.features5)) features.Add(package.features5);

                string featuresString = FeaturesToString(features);

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check if package exists
                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Packages WHERE PackageID = @packageID", conn);
                checkCmd.Parameters.AddWithValue("@packageID", package.packageID);
                var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                if (!exists)
                {
                    _toastManager.CreateToast("Package Not Found")
                        .WithContent("The package you're trying to update doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return false;
                }

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@packageID", package.packageID);
                cmd.Parameters.AddWithValue("@packageName", package.packageName);
                cmd.Parameters.AddWithValue("@price", package.price);
                cmd.Parameters.AddWithValue("@description", (object)package.description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@duration", package.duration);
                cmd.Parameters.AddWithValue("@features", (object)featuresString ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@discount", package.discount);
                cmd.Parameters.AddWithValue("@discountType", (object)package.discountType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@discountFor", (object)package.discountFor ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@discountedPrice", discountedPrice);
                cmd.Parameters.AddWithValue("@validFrom", package.validFrom);
                cmd.Parameters.AddWithValue("@validTo", package.validTo);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    PackagesChanged?.Invoke(this, EventArgs.Empty);
                    PackageEventService.Instance.NotifyPackagesChanged();
                    await LogActionAsync(conn, "Updated a package", $"Updated package: '{package.packageName}' (ID: {package.packageID})", true);

                    _toastManager.CreateToast("Package Updated")
                        .WithContent($"Successfully updated package '{package.packageName}'.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return true;
                }

                return false;
            }
            catch (SqlException ex)
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await LogActionAsync(conn, "Failed to update package", $"Failed to update package: '{package.packageName}' - SQL Error: {ex.Message}", false);

                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to update package: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                return false;
            }
            catch (Exception ex)
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await LogActionAsync(conn, "Failed to update package", $"Failed to update package: '{package.packageName}' - Error: {ex.Message}", false);

                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                return false;
            }
        }

        #endregion

        #region DELETE

        public async Task<bool> DeletePackageAsync(int packageId)
        {
            if (!CanDelete())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete packages.")
                    .DismissOnClick()
                    .ShowError();
                return false;
            }

            const string query = "DELETE FROM Packages WHERE PackageID = @packageID";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Get package name for logging
                using var getNameCmd = new SqlCommand(
                    "SELECT PackageName FROM Packages WHERE PackageID = @packageID", conn);
                getNameCmd.Parameters.AddWithValue("@packageID", packageId);
                var packageName = await getNameCmd.ExecuteScalarAsync() as string;

                if (string.IsNullOrEmpty(packageName))
                {
                    _toastManager.CreateToast("Package Not Found")
                        .WithContent("The package you're trying to delete doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return false;
                }

                // Delete package
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@packageID", packageId);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    PackagesChanged?.Invoke(this, EventArgs.Empty);
                    PackageEventService.Instance.NotifyPackagesChanged();
                    await LogActionAsync(conn, "Deleted a package", $"Deleted package: '{packageName}' (ID: {packageId})", true);

                    _toastManager.CreateToast("Package Deleted")
                        .WithContent($"Successfully deleted package '{packageName}'.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return true;
                }

                return false;
            }
            catch (SqlException ex)
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await LogActionAsync(conn, "Failed to delete package", $"Failed to delete package ID {packageId} - SQL Error: {ex.Message}", false);

                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to delete package: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                return false;
            }
            catch (Exception ex)
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await LogActionAsync(conn, "Failed to delete package", $"Failed to delete package ID {packageId} - Error: {ex.Message}", false);

                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                return false;
            }
        }

        public async Task<(bool Success, string Message, int DeletedCount)> DeleteMultiplePackagesAsync(List<int> packageIds)
        {
            if (!CanDelete())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete packages.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to delete packages.", 0);
            }

            if (packageIds == null || packageIds.Count == 0)
            {
                return (false, "No packages selected for deletion.", 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var deletedCount = 0;
                var packageNames = new List<string>();

                using var transaction = conn.BeginTransaction();
                try
                {
                    foreach (var packageId in packageIds)
                    {
                        // Get package name
                        using var getNameCmd = new SqlCommand(
                            "SELECT PackageName FROM Packages WHERE PackageID = @packageID", conn, transaction);
                        getNameCmd.Parameters.AddWithValue("@packageID", packageId);
                        var name = await getNameCmd.ExecuteScalarAsync() as string;

                        if (!string.IsNullOrEmpty(name))
                        {
                            packageNames.Add(name);
                        }

                        // Delete package
                        using var deleteCmd = new SqlCommand(
                            "DELETE FROM Packages WHERE PackageID = @packageID", conn, transaction);
                        deleteCmd.Parameters.AddWithValue("@packageID", packageId);
                        deletedCount += await deleteCmd.ExecuteNonQueryAsync();
                    }
                    PackagesChanged?.Invoke(this, EventArgs.Empty);
                    PackageEventService.Instance.NotifyPackagesChanged();
                    await LogActionAsync(conn, "Deleted multiple packages", $"Deleted {deletedCount} packages: {string.Join(", ", packageNames)}", true);

                    transaction.Commit();

                    _toastManager.CreateToast("Packages Deleted")
                        .WithContent($"Successfully deleted {deletedCount} package(s).")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, $"Successfully deleted {deletedCount} package(s).", deletedCount);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to delete packages: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}", 0);
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}", 0);
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
                DashboardEventService.Instance.NotifyRecentLogsUpdated();
                PackageEventService.Instance.NotifyPackagesChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        private string FeaturesToString(List<string> features)
        {
            if (features == null || !features.Any())
                return string.Empty;

            return string.Join(FEATURE_SEPARATOR, features.Where(f => !string.IsNullOrWhiteSpace(f)));
        }

        private List<string> StringToFeatures(string featuresString)
        {
            if (string.IsNullOrWhiteSpace(featuresString))
                return new List<string>();

            return featuresString.Split(FEATURE_SEPARATOR, StringSplitOptions.RemoveEmptyEntries)
                                .Select(f => f.Trim())
                                .Where(f => !string.IsNullOrWhiteSpace(f))
                                .ToList();
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

        private PackageModel MapPackageFromReader(SqlDataReader reader)
        {
            var featuresString = reader["Features"]?.ToString() ?? string.Empty;
            var features = StringToFeatures(featuresString);

            return new PackageModel
            {
                packageID = reader.GetInt32(reader.GetOrdinal("PackageID")),
                packageName = reader["PackageName"]?.ToString() ?? "",
                price = reader.GetDecimal(reader.GetOrdinal("Price")),
                description = reader["Description"]?.ToString() ?? "",
                duration = reader["Duration"]?.ToString() ?? "",
                features1 = features.Count > 0 ? features[0] : string.Empty,
                features2 = features.Count > 1 ? features[1] : string.Empty,
                features3 = features.Count > 2 ? features[2] : string.Empty,
                features4 = features.Count > 3 ? features[3] : string.Empty,
                features5 = features.Count > 4 ? features[4] : string.Empty,
                discount = reader.GetDecimal(reader.GetOrdinal("Discount")),
                discountType = reader["DiscountType"]?.ToString() ?? "",
                discountFor = reader["DiscountFor"]?.ToString() ?? "",
                discountedPrice = reader.GetDecimal(reader.GetOrdinal("DiscountedPrice")),
                validFrom = reader.GetDateTime(reader.GetOrdinal("ValidFrom")),
                validTo = reader.GetDateTime(reader.GetOrdinal("ValidTo"))
            };
        }

        public async Task<(bool Success, int TotalPackages, int WithDiscount, int Active)> GetPackageStatisticsAsync()
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
                        COUNT(*) as TotalPackages,
                        SUM(CASE WHEN Discount > 0 THEN 1 ELSE 0 END) as WithDiscount,
                        SUM(CASE WHEN ValidTo >= GETDATE() THEN 1 ELSE 0 END) as Active
                      FROM Package", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (true,
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetInt32(2));
                }

                return (false, 0, 0, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetPackageStatisticsAsync] {ex.Message}");
                return (false, 0, 0, 0);
            }
        }

        #endregion
    }
}
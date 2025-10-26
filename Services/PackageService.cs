using AHON_TRACK.Models;
using AHON_TRACK.ViewModels;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
        private const string DISCOUNT_TYPE_PERCENTAGE = "percentage";
        private const string DISCOUNT_TYPE_FIXED = "fixed";

        public event EventHandler? PackagesChanged;

        public PackageService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
        }

        #region Role-Based Access Control

        private bool CanCreate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanUpdate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanDelete() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanView() =>
            !string.IsNullOrWhiteSpace(CurrentUserModel.Username);

        #endregion

        #region CREATE

        public async Task<(bool Success, string Message, int? PackageId)> AddPackageAsync(PackageModel package)
        {
            if (package == null)
                return (false, "Package data is required.", null);

            if (!CanCreate())
            {
                ShowAccessDeniedToast("add packages");
                return (false, "Insufficient permissions to add packages.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check for soft-deleted package with same name
                var existingId = await GetSoftDeletedPackageIdAsync(conn, package.packageName);
                if (existingId.HasValue)
                {
                    return await RestorePackageAsync(conn, existingId.Value, package);
                }

                return await InsertNewPackageAsync(conn, package);
            }
            catch (SqlException ex)
            {
                return await HandleSqlExceptionAsync("add", package.packageName, ex);
            }
            catch (Exception ex)
            {
                return await HandleGeneralExceptionAsync("add", package.packageName, ex);
            }
        }

        private async Task<int?> GetSoftDeletedPackageIdAsync(SqlConnection conn, string packageName)
        {
            const string query = "SELECT PackageID FROM Packages WHERE PackageName = @packageName AND IsDeleted = 1";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@packageName", packageName ?? (object)DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : null;
        }

        private async Task<(bool, string, int?)> RestorePackageAsync(SqlConnection conn, int packageId, PackageModel package)
        {
            var featuresString = BuildFeaturesString(package);
            var discountedPrice = CalculateDiscountedPrice(package.price, package.discount, package.discountType);

            var restored = await RestoreAndUpdatePackageAsync(conn, packageId, package, discountedPrice, featuresString);
            if (!restored)
                return (false, "Failed to restore package.", null);

            var logDescription = BuildLogDescription(package, discountedPrice, "Restored package");
            await LogActionAsync(conn, "Restored package", logDescription, true);

            PackagesChanged?.Invoke(this, EventArgs.Empty);

            ShowSuccessToast("Package Restored", $"Successfully restored package '{package.packageName}'.");
            return (true, "Package restored successfully.", packageId);
        }

        private async Task<(bool, string, int?)> InsertNewPackageAsync(SqlConnection conn, PackageModel package)
        {
            const string query = @"
                INSERT INTO Packages (PackageName, Price, Description, Duration, Features, Discount, DiscountType, 
                                     DiscountFor, DiscountedPrice, ValidFrom, ValidTo, AddedByEmployeeID)
                OUTPUT INSERTED.PackageID
                VALUES (@packageName, @price, @description, @duration, @features, @discount, @discountType, 
                        @discountFor, @discountedPrice, @validFrom, @validTo, @addedByEmployeeID)";

            var featuresString = BuildFeaturesString(package);
            var discountedPrice = CalculateDiscountedPrice(package.price, package.discount, package.discountType);

            using var cmd = new SqlCommand(query, conn);
            AddPackageParameters(cmd, package, featuresString, discountedPrice);

            var packageId = (int)await cmd.ExecuteScalarAsync();

            var logDescription = BuildLogDescription(package, discountedPrice, "Added package");
            await LogActionAsync(conn, "Added new package", logDescription, true);

            PackagesChanged?.Invoke(this, EventArgs.Empty);
            DashboardEventService.Instance.NotifyPackageAdded();

            return (true, "Package added successfully.", packageId);
        }

        #endregion

        #region READ

        public async Task<List<Package>> GetPackagesAsync()
        {
            if (!CanView())
            {
                ShowAccessDeniedToast("view packages");
                return new List<Package>();
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
                    WHERE IsDeleted = 0
                    ORDER BY PackageName";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                var packages = new List<Package>();
                while (await reader.ReadAsync())
                {
                    packages.Add(MapToPackageViewModel(reader));
                }

                return packages;
            }
            catch (SqlException ex)
            {
                ShowErrorToast("Database Error", $"Failed to load packages: {ex.Message}");
                Console.WriteLine($"SQL Error: {ex.Message}");
                return new List<Package>();
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                Console.WriteLine($"General Error: {ex.Message}");
                return new List<Package>();
            }
        }

        public async Task<(bool Success, string Message, PackageModel? Package)> GetPackageByIdAsync(int packageId)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view packages.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT PackageID, PackageName, Price, Description, Duration, 
                           Features, Discount, DiscountType, DiscountFor, DiscountedPrice, 
                           ValidFrom, ValidTo, AddedByEmployeeID
                    FROM Packages
                    WHERE PackageID = @packageId AND IsDeleted = 0";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@packageId", packageId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var package = MapToPackageModel(reader);
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
                return (false, "Insufficient permissions to view packages.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT PackageID, PackageName, Price, Description, Duration, 
                           Features, Discount, DiscountType, DiscountFor, DiscountedPrice, 
                           ValidFrom, ValidTo, AddedByEmployeeID
                    FROM Packages 
                    WHERE Duration = @duration AND IsDeleted = 0
                    ORDER BY PackageName";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@duration", duration ?? (object)DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();

                var packages = new List<Package>();
                while (await reader.ReadAsync())
                {
                    packages.Add(MapToPackageViewModel(reader));
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
            if (package == null)
                return false;

            if (!CanUpdate())
            {
                ShowAccessDeniedToast("update package information", "Only administrators and managers can");
                return false;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                if (!await PackageExistsAsync(conn, package.packageID))
                {
                    ShowWarningToast("Package Not Found", "The package you're trying to update doesn't exist.");
                    return false;
                }

                var updated = await ExecuteUpdatePackageAsync(conn, package);

                if (updated)
                {
                    await LogActionAsync(conn, "Updated a package",
                        $"Updated package: '{package.packageName}' (ID: {package.packageID})", true);
                    DashboardEventService.Instance.NotifyPackageUpdated();
                }

                return updated;
            }
            catch (SqlException ex)
            {
                await LogFailedActionAsync("update", package.packageName, ex.Message, true);
                ShowErrorToast("Database Error", $"Failed to update package: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                await LogFailedActionAsync("update", package.packageName, ex.Message, false);
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> PackageExistsAsync(SqlConnection conn, int packageId)
        {
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM Packages WHERE PackageID = @packageID AND IsDeleted = 0", conn);
            cmd.Parameters.AddWithValue("@packageID", packageId);
            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private async Task<bool> ExecuteUpdatePackageAsync(SqlConnection conn, PackageModel package)
        {
            const string query = @"
                UPDATE Packages SET 
                    PackageName = @packageName, Price = @price, Description = @description,
                    Duration = @duration, Features = @features, Discount = @discount,
                    DiscountType = @discountType, DiscountFor = @discountFor,
                    DiscountedPrice = @discountedPrice, ValidFrom = @validFrom, ValidTo = @validTo
                WHERE PackageID = @packageID AND IsDeleted = 0";

            var featuresString = BuildFeaturesString(package);
            var discountedPrice = CalculateDiscountedPrice(package.price, package.discount, package.discountType);

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@packageID", package.packageID);
            AddPackageParameters(cmd, package, featuresString, discountedPrice);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        #endregion

        #region DELETE

        public async Task<bool> DeletePackageAsync(int packageId)
        {
            if (!CanDelete())
            {
                ShowAccessDeniedToast("delete packages", "Only administrators can");
                return false;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var packageName = await GetPackageNameAsync(conn, packageId);
                if (string.IsNullOrEmpty(packageName))
                {
                    ShowWarningToast("Package Not Found", "The package you're trying to delete doesn't exist.");
                    return false;
                }

                var deleted = await ExecuteSoftDeleteAsync(conn, packageId);

                if (deleted)
                {
                    await LogActionAsync(conn, "Deleted a package",
                        $"Deleted package: '{packageName}' (ID: {packageId})", true);
                    DashboardEventService.Instance.NotifyPackageDeleted();
                }

                return deleted;
            }
            catch (SqlException ex)
            {
                await LogFailedActionAsync("delete", $"package ID {packageId}", ex.Message, true);
                ShowErrorToast("Database Error", $"Failed to delete package: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                await LogFailedActionAsync("delete", $"package ID {packageId}", ex.Message, false);
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                return false;
            }
        }

        public async Task<(bool Success, string Message, int DeletedCount)> DeleteMultiplePackagesAsync(List<int> packageIds)
        {
            if (!CanDelete())
            {
                ShowAccessDeniedToast("delete packages", "Only administrators can");
                return (false, "Insufficient permissions to delete packages.", 0);
            }

            if (packageIds == null || packageIds.Count == 0)
                return (false, "No packages selected for deletion.", 0);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();
                try
                {
                    var (deletedCount, packageNames) = await ExecuteMultipleDeletesAsync(conn, transaction, packageIds);

                    await LogActionAsync(conn, "Deleted multiple packages",
                        $"Deleted {deletedCount} packages: {string.Join(", ", packageNames)}", true);

                    transaction.Commit();
                    DashboardEventService.Instance.NotifyPackageDeleted();

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
                ShowErrorToast("Database Error", $"Failed to delete packages: {ex.Message}");
                return (false, $"Database error: {ex.Message}", 0);
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                return (false, $"Error: {ex.Message}", 0);
            }
        }

        private async Task<string?> GetPackageNameAsync(SqlConnection conn, int packageId)
        {
            using var cmd = new SqlCommand("SELECT PackageName FROM Packages WHERE PackageID = @packageID", conn);
            cmd.Parameters.AddWithValue("@packageID", packageId);
            return await cmd.ExecuteScalarAsync() as string;
        }

        private async Task<bool> ExecuteSoftDeleteAsync(SqlConnection conn, int packageId)
        {
            using var cmd = new SqlCommand("UPDATE Packages SET IsDeleted = 1 WHERE PackageID = @packageID", conn);
            cmd.Parameters.AddWithValue("@packageID", packageId);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private async Task<(int DeletedCount, List<string> PackageNames)> ExecuteMultipleDeletesAsync(
            SqlConnection conn, SqlTransaction transaction, List<int> packageIds)
        {
            var deletedCount = 0;
            var packageNames = new List<string>();

            foreach (var packageId in packageIds)
            {
                using var getNameCmd = new SqlCommand(
                    "SELECT PackageName FROM Packages WHERE PackageID = @packageID", conn, transaction);
                getNameCmd.Parameters.AddWithValue("@packageID", packageId);

                var name = await getNameCmd.ExecuteScalarAsync() as string;
                if (!string.IsNullOrEmpty(name))
                    packageNames.Add(name);

                using var deleteCmd = new SqlCommand(
                    "UPDATE Packages SET IsDeleted = 1 WHERE PackageID = @packageID", conn, transaction);
                deleteCmd.Parameters.AddWithValue("@packageID", packageId);
                deletedCount += await deleteCmd.ExecuteNonQueryAsync();
            }

            return (deletedCount, packageNames);
        }

        #endregion

        #region STATISTICS

        public async Task<(bool Success, int TotalPackages, int WithDiscount, int Active)> GetPackageStatisticsAsync()
        {
            if (!CanView())
                return (false, 0, 0, 0);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT 
                        COUNT(*) as TotalPackages,
                        SUM(CASE WHEN Discount > 0 THEN 1 ELSE 0 END) as WithDiscount,
                        SUM(CASE WHEN ValidTo >= GETDATE() THEN 1 ELSE 0 END) as Active
                      FROM Packages
                      WHERE IsDeleted = 0", conn);

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

        #region UTILITY METHODS

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                using var cmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                      VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)", conn);

                cmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@success", success);
                cmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                DashboardEventService.Instance.NotifyRecentLogsUpdated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        private async Task LogFailedActionAsync(string action, string packageInfo, string error, bool isSqlError)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var errorType = isSqlError ? "SQL Error" : "Error";
                await LogActionAsync(conn, $"Failed to {action} package",
                    $"Failed to {action} package: '{packageInfo}' - {errorType}: {error}", false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogFailedActionAsync] {ex.Message}");
            }
        }

        private string BuildFeaturesString(PackageModel package)
        {
            var features = new List<string>();
            if (!string.IsNullOrWhiteSpace(package.features1)) features.Add(package.features1);
            if (!string.IsNullOrWhiteSpace(package.features2)) features.Add(package.features2);
            if (!string.IsNullOrWhiteSpace(package.features3)) features.Add(package.features3);
            if (!string.IsNullOrWhiteSpace(package.features4)) features.Add(package.features4);
            if (!string.IsNullOrWhiteSpace(package.features5)) features.Add(package.features5);

            return FeaturesToString(features);
        }

        private string BuildLogDescription(PackageModel package, decimal discountedPrice, string prefix)
        {
            var description = $"{prefix}: '{package.packageName}' - Price: ₱{package.price:N2}, Duration: {package.duration}";

            if (package.discount > 0)
            {
                var discountTypeDisplay = package.discountType?.ToLower() == DISCOUNT_TYPE_PERCENTAGE ? "%" : " fixed";
                description += $", Discount: {package.discount}{discountTypeDisplay} for {package.discountFor ?? "All"}, Final Price: ₱{discountedPrice:N2}";
            }

            return description;
        }

        private void AddPackageParameters(SqlCommand cmd, PackageModel package, string featuresString, decimal discountedPrice)
        {
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
            if (discount <= 0)
                return originalPrice;

            if (discountType?.ToLower() == DISCOUNT_TYPE_PERCENTAGE)
            {
                return originalPrice - (originalPrice * discount / 100);
            }
            else if (discountType?.ToLower() == DISCOUNT_TYPE_FIXED)
            {
                var discountedPrice = originalPrice - discount;
                return discountedPrice < 0 ? 0 : discountedPrice;
            }

            return originalPrice;
        }

        private Package MapToPackageViewModel(SqlDataReader reader)
        {
            var featuresString = reader["Features"]?.ToString() ?? string.Empty;
            var features = StringToFeatures(featuresString);

            return new Package
            {
                PackageId = reader.GetInt32(reader.GetOrdinal("PackageID")),
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
            };
        }

        private PackageModel MapToPackageModel(SqlDataReader reader)
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

        private async Task<bool> RestoreAndUpdatePackageAsync(SqlConnection conn, int packageId, PackageModel package,
            decimal discountedPrice, string featuresString)
        {
            try
            {
                const string query = @"
                    UPDATE Packages SET
                        PackageName = @packageName, Price = @price, Description = @description,
                        Duration = @duration, Features = @features, Discount = @discount,
                        DiscountType = @discountType, DiscountFor = @discountFor,
                        DiscountedPrice = @discountedPrice, ValidFrom = @validFrom, ValidTo = @validTo,
                        AddedByEmployeeID = @addedByEmployeeID, IsDeleted = 0
                    WHERE PackageID = @packageID";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@packageID", packageId);
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

                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RestoreAndUpdatePackageAsync] {ex.Message}");
                return false;
            }
        }

        private async Task<(bool Success, string Message, int? PackageId)> HandleSqlExceptionAsync(
            string action, string packageName, SqlException ex)
        {
            await LogFailedActionAsync(action, packageName, ex.Message, true);
            ShowErrorToast("Database Error", $"Failed to {action} package: {ex.Message}");
            Console.WriteLine($"SQL Error: {ex.Message}");
            return (false, $"Database error: {ex.Message}", null);
        }

        private async Task<(bool Success, string Message, int? PackageId)> HandleGeneralExceptionAsync(
            string action, string packageName, Exception ex)
        {
            await LogFailedActionAsync(action, packageName, ex.Message, false);
            ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
            Console.WriteLine($"General Error: {ex.Message}");
            return (false, $"Error: {ex.Message}", null);
        }

        private void ShowAccessDeniedToast(string action, string prefix = "You don't have permission to")
        {
            _toastManager.CreateToast("Access Denied")
                .WithContent($"{prefix} {action}.")
                .DismissOnClick()
                .ShowError();
        }

        private void ShowSuccessToast(string title, string content)
        {
            _toastManager.CreateToast(title)
                .WithContent(content)
                .DismissOnClick()
                .ShowSuccess();
        }

        private void ShowWarningToast(string title, string content)
        {
            _toastManager.CreateToast(title)
                .WithContent(content)
                .DismissOnClick()
                .ShowWarning();
        }

        private void ShowErrorToast(string title, string content)
        {
            _toastManager.CreateToast(title)
                .WithContent(content)
                .DismissOnClick()
                .ShowError();
        }

        #endregion
    }
}
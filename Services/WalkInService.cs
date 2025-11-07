using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services
{
    public class WalkInService : IWalkInService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        // Constants
        private const string ADMIN_ROLE = "Admin";
        private const string STAFF_ROLE = "Staff";
        private const string COACH_ROLE = "Coach";
        private const string FREE_TRIAL = "Free Trial";
        private const string REGULAR = "Regular";
        private const string NONE_PACKAGE = "None";

        public WalkInService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        #region Role-Based Access Control

        private bool HasRole(params string[] roles)
        {
            return roles.Any(role => CurrentUserModel.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true);
        }

        private bool CanCreate() => HasRole(ADMIN_ROLE, STAFF_ROLE, COACH_ROLE);
        private bool CanUpdate() => HasRole(ADMIN_ROLE, STAFF_ROLE, COACH_ROLE);
        private bool CanDelete() => HasRole(ADMIN_ROLE);
        private bool CanView() => HasRole(ADMIN_ROLE, STAFF_ROLE, COACH_ROLE);

        #endregion

        #region DATE VALIDATION

        private bool IsValidCheckInDate(DateTime selectedDate)
        {
            return selectedDate.Date == DateTime.Today;
        }

        #endregion

        #region CREATE

        public async Task<(bool Success, string Message, int? CustomerID)> AddWalkInCustomerAsync(ManageWalkInModel walkIn, DateTime selectedDate)
        {
            if (!CanCreate())
            {
                ShowAccessDeniedToast("register walk-in customers");
                return (false, "Insufficient permissions to register walk-in customers.", null);
            }

            if (!IsValidCheckInDate(selectedDate))
            {
                ShowWarningToast("Invalid Date",
                    "Walk-in check-in is only allowed for today's date.");
                return (false, "Check-in is only allowed for today's date.", null);
            }
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    // ✅ STRICT CHECK: Block if FirstName + LastName matches ANY active member
                    var (isActiveMember, isExpiredMember, memberId, memberName, multipleMatches) =
                        await CheckMemberStatusAsync(conn, transaction, walkIn);

                    if (isActiveMember)
                    {
                        // BLOCK: Active member detected
                        string blockMessage = $"❌ Cannot register as walk-in.\n\n" +
                            $"'{walkIn.FirstName} {walkIn.LastName}' matches an active member:\n" +
                            $"• Member: {memberName} (ID: {memberId})\n\n" +
                            $"Please use the MEMBER CHECK-IN system instead.";

                        ShowWarningToast("Active Member Detected", blockMessage);

                        await LogActionAsync(conn, transaction, "BLOCKED",
                            $"Walk-in blocked - {walkIn.FirstName} {walkIn.LastName} (Age: {walkIn.Age}) matches active member {memberName} (ID: {memberId})", true);

                        transaction.Commit();
                        return (false, $"Active member '{memberName}' detected. Use member check-in system.", null);
                    }

                    // If expired/inactive member found, notify but allow
                    if (isExpiredMember)
                    {
                        string expiredMessage = multipleMatches != null && multipleMatches.Count > 1
                            ? $"Found {multipleMatches.Count} expired member records with this name. Allowing walk-in registration."
                            : $"{memberName}'s membership has expired. Allowing walk-in registration.";

                        Debug.WriteLine($"[AddWalkInCustomerAsync] {expiredMessage}");
                        _toastManager?.CreateToast("Expired Member Detected")
                            .WithContent(expiredMessage)
                            .DismissOnClick()
                            .ShowInfo();
                    }

                    // Continue with existing walk-in logic
                    var (existingId, existingType) = await GetExistingCustomerAsync(conn, transaction, walkIn);

                    if (existingId.HasValue)
                    {
                        var result = await HandleExistingCustomerAsync(conn, transaction, walkIn, existingId.Value, existingType);
                        if (!result.Success)
                        {
                            transaction.Rollback();
                            return result;
                        }

                        transaction.Commit();
                        NotifyDashboardEvents();
                        return result;
                    }

                    // New customer flow
                    var customerId = await CreateNewCustomerAsync(conn, transaction, walkIn);

                    if (IsRegularWithPackage(walkIn))
                    {
                        await ProcessPackageSaleAsync(conn, transaction, customerId, walkIn);
                    }

                    await CreateCheckInRecordAsync(conn, transaction, customerId);

                    string logMessage = isExpiredMember
                        ? $"Registered expired member as walk-in: {walkIn.FirstName} {walkIn.LastName} ({walkIn.WalkInType}) - Previous Member ID: {memberId}"
                        : $"Registered walk-in customer: {walkIn.FirstName} {walkIn.LastName} ({walkIn.WalkInType}) with {walkIn.WalkInPackage}";

                    await LogActionAsync(conn, transaction, "CREATE", logMessage, true);

                    transaction.Commit();
                    NotifyDashboardEvents();

                    ShowSuccessToast("Walk-in Registered",
                        $"{walkIn.FirstName} {walkIn.LastName} checked in successfully.");

                    return (true, "Walk-in customer registered and checked in successfully.", customerId);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                ShowErrorToast("Database Error", $"Failed to register customer: {ex.Message}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        private async Task<(int? CustomerId, string? WalkInType)> GetExistingCustomerAsync(
            SqlConnection conn, SqlTransaction transaction, ManageWalkInModel walkIn)
        {
            using var cmd = new SqlCommand(
                @"SELECT CustomerID, WalkinType FROM WalkInCustomers 
                  WHERE FirstName = @firstName AND LastName = @lastName AND ContactNumber = @contactNumber
                  AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn, transaction);

            cmd.Parameters.AddWithValue("@firstName", walkIn.FirstName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@lastName", walkIn.LastName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@contactNumber", walkIn.ContactNumber ?? (object)DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1));
            }

            return (null, null);
        }

        private async Task<(bool Success, string Message, int? CustomerID)> HandleExistingCustomerAsync(
            SqlConnection conn, SqlTransaction transaction, ManageWalkInModel walkIn,
            int existingCustomerId, string? existingType)
        {
            // Prevent Regular customers from downgrading to Free Trial
            if (IsRegular(existingType) && IsFreeTrial(walkIn.WalkInType))
            {
                ShowWarningToast("Invalid Selection",
                    $"{walkIn.FirstName} {walkIn.LastName} is already a Regular customer and cannot use Free Trial.");
                return (false, "Regular customers cannot downgrade to Free Trial.", null);
            }

            // Free Trial customer trying to use Free Trial again
            if (IsFreeTrial(existingType) && IsFreeTrial(walkIn.WalkInType))
            {
                ShowWarningToast("Free Trial Already Used",
                    $"{walkIn.FirstName} {walkIn.LastName} has already availed a free trial.");
                return (false, "This customer has already used their free trial.", null);
            }

            // Free Trial upgrading to Regular
            if (IsFreeTrial(existingType) && IsRegular(walkIn.WalkInType))
            {
                await UpgradeToRegularAsync(conn, transaction, existingCustomerId, walkIn);
                await CreateCheckInRecordAsync(conn, transaction, existingCustomerId);
                await LogActionAsync(conn, transaction, "UPDATE",
                    $"Updated walk-in customer from Free Trial to Regular and checked in: {walkIn.FirstName} {walkIn.LastName}", true);

                return (true, "Customer updated to Regular and checked in.", existingCustomerId);
            }

            // Regular customer returning
            await CreateCheckInRecordAsync(conn, transaction, existingCustomerId);
            await LogActionAsync(conn, transaction, "CREATE",
                $"Checked in existing walk-in customer: {walkIn.FirstName} {walkIn.LastName}", true);

            return (true, "Existing customer checked in.", existingCustomerId);
        }

        private async Task<int> CreateNewCustomerAsync(SqlConnection conn, SqlTransaction transaction, ManageWalkInModel walkIn)
        {
            using var cmd = new SqlCommand(
                @"INSERT INTO WalkInCustomers (FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, 
                  WalkInType, WalkInPackage, PaymentMethod, Quantity, RegisteredByEmployeeID, IsDeleted) 
                  OUTPUT INSERTED.CustomerID
                  VALUES (@firstName, @middleInitial, @lastName, @contactNumber, @age, @gender, 
                  @walkInType, @walkInPackage, @paymentMethod, @quantity, @employeeID, 0)", conn, transaction);

            AddWalkInParameters(cmd, walkIn);
            return (int)await cmd.ExecuteScalarAsync();
        }

        private async Task UpgradeToRegularAsync(SqlConnection conn, SqlTransaction transaction,
            int customerId, ManageWalkInModel walkIn)
        {
            using var cmd = new SqlCommand(
                @"UPDATE WalkInCustomers 
                  SET WalkinType = @walkInType, WalkinPackage = @walkInPackage, PaymentMethod = @paymentMethod,
                      Quantity = @quantity, Age = @age, Gender = @gender, MiddleInitial = @middleInitial
                  WHERE CustomerID = @customerId", conn, transaction);

            cmd.Parameters.AddWithValue("@customerId", customerId);
            cmd.Parameters.AddWithValue("@walkInType", walkIn.WalkInType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@walkInPackage", walkIn.WalkInPackage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@paymentMethod", walkIn.PaymentMethod ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@quantity", walkIn.Quantity ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@age", walkIn.Age);
            cmd.Parameters.AddWithValue("@gender", walkIn.Gender ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@middleInitial", walkIn.MiddleInitial ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task CreateCheckInRecordAsync(SqlConnection conn, SqlTransaction transaction, int customerId)
        {
            using var cmd = new SqlCommand(
                @"INSERT INTO WalkInRecords (CustomerID, CheckIn, CheckOut, RegisteredByEmployeeID)
                  VALUES (@customerID, @checkIn, NULL, @employeeID)", conn, transaction);

            cmd.Parameters.AddWithValue("@customerID", customerId);
            cmd.Parameters.Add("@checkIn", SqlDbType.DateTime).Value = DateTime.Now;
            cmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task ProcessPackageSaleAsync(SqlConnection conn, SqlTransaction transaction,
            int customerId, ManageWalkInModel walkIn)
        {
            var (packageId, price) = await GetPackageDetailsAsync(conn, transaction, walkIn.WalkInPackage);

            if (!packageId.HasValue) return;

            decimal totalAmount = price * (walkIn.Quantity ?? 1);

            await RecordSaleAsync(conn, transaction, packageId.Value, customerId, walkIn.Quantity ?? 1, totalAmount);
            await UpdateDailySalesAsync(conn, transaction, totalAmount);
        }

        private async Task<(int? PackageId, decimal Price)> GetPackageDetailsAsync(
            SqlConnection conn, SqlTransaction transaction, string? packageName)
        {
            using var cmd = new SqlCommand(
                @"SELECT PackageID, Price FROM Packages 
                  WHERE PackageName = @packageName AND IsDeleted = 0", conn, transaction);

            cmd.Parameters.AddWithValue("@packageName", packageName ?? (object)DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0), reader.GetDecimal(1));
            }

            return (null, 0);
        }

        private async Task RecordSaleAsync(SqlConnection conn, SqlTransaction transaction,
            int packageId, int customerId, int quantity, decimal amount)
        {
            using var cmd = new SqlCommand(
                @"INSERT INTO Sales (SaleDate, PackageID, CustomerID, Quantity, Amount, RecordedBy)
                  VALUES (GETDATE(), @packageId, @customerId, @quantity, @amount, @employeeId)", conn, transaction);

            cmd.Parameters.AddWithValue("@packageId", packageId);
            cmd.Parameters.AddWithValue("@customerId", customerId);
            cmd.Parameters.AddWithValue("@quantity", quantity);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.Parameters.AddWithValue("@employeeId", CurrentUserModel.UserId ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateDailySalesAsync(SqlConnection conn, SqlTransaction transaction, decimal totalAmount)
        {
            using var cmd = new SqlCommand(
                @"MERGE DailySales AS target
                  USING (SELECT CAST(GETDATE() AS DATE) AS SaleDate, @EmployeeID AS EmployeeID) AS source
                  ON target.SaleDate = source.SaleDate AND target.TransactionByEmployeeID = source.EmployeeID
                  WHEN MATCHED THEN
                      UPDATE SET TotalSales = target.TotalSales + @TotalAmount,
                                 TotalTransactions = target.TotalTransactions + 1,
                                 TransactionUpdatedDate = SYSDATETIME()
                  WHEN NOT MATCHED THEN
                      INSERT (SaleDate, TotalSales, TotalTransactions, TransactionByEmployeeID)
                      VALUES (source.SaleDate, @TotalAmount, 1, source.EmployeeID);", conn, transaction);

            cmd.Parameters.AddWithValue("@EmployeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TotalAmount", totalAmount);

            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region READ

        public async Task<(bool Success, string Message, List<ManageWalkInModel>? WalkIns)> GetAllWalkInCustomersAsync()
        {
            if (!CanView())
            {
                ShowAccessDeniedToast("view walk-in customers");
                return (false, "Insufficient permissions to view walk-in customers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT CustomerID, FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, 
                      WalkinType, WalkinPackage, PaymentMethod, RegisteredByEmployeeID 
                      FROM WalkInCustomers 
                      WHERE IsDeleted = 0 OR IsDeleted IS NULL
                      ORDER BY CustomerID DESC", conn);

                var walkIns = await ReadWalkInCustomersAsync(cmd);
                return (true, "Walk-in customers retrieved successfully.", walkIns);
            }
            catch (SqlException ex)
            {
                ShowErrorToast("Database Error", $"Failed to retrieve walk-in customers: {ex.Message}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, ManageWalkInModel? WalkIn)> GetWalkInCustomerByIdAsync(int customerId)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view walk-in customers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT CustomerID, FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, 
                      WalkinType, WalkinPackage, PaymentMethod, RegisteredByEmployeeID 
                      FROM WalkInCustomers 
                      WHERE CustomerID = @customerId AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);

                cmd.Parameters.AddWithValue("@customerId", customerId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var walkIn = MapToWalkInModel(reader);
                    return (true, "Walk-in customer retrieved successfully.", walkIn);
                }

                return (false, "Walk-in customer not found.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<ManageWalkInModel>? WalkIns)> GetWalkInsByTypeAsync(string walkInType)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view walk-in customers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT CustomerID, FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, 
                      WalkinType, WalkinPackage, PaymentMethod, RegisteredByEmployeeID 
                      FROM WalkInCustomers 
                      WHERE WalkinType = @walkInType AND (IsDeleted = 0 OR IsDeleted IS NULL)
                      ORDER BY CustomerID DESC", conn);

                cmd.Parameters.AddWithValue("@walkInType", walkInType ?? (object)DBNull.Value);
                var walkIns = await ReadWalkInCustomersAsync(cmd);
                return (true, "Walk-in customers retrieved successfully.", walkIns);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<ManageWalkInModel>? WalkIns)> GetWalkInsByPackageAsync(string package)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view walk-in customers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT CustomerID, FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, 
                      WalkinType, WalkinPackage, PaymentMethod, RegisteredByEmployeeID 
                      FROM WalkInCustomers 
                      WHERE WalkinPackage = @package AND (IsDeleted = 0 OR IsDeleted IS NULL)
                      ORDER BY CustomerID DESC", conn);

                cmd.Parameters.AddWithValue("@package", package ?? (object)DBNull.Value);
                var walkIns = await ReadWalkInCustomersAsync(cmd);
                return (true, "Walk-in customers retrieved successfully.", walkIns);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<List<SellingModel>> GetAvailablePackagesForWalkInAsync(string? walkInType = null)
        {
            var packages = new List<SellingModel>();

            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to view gym packages.")
                    .ShowError();
                return packages;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query;

                if (walkInType == "Regular")
                {
                    // For Regular: Only show "One-time Only" packages
                    query = @"
                SELECT PackageID, PackageName, Description, Price, Duration, Features, 
                       Discount, DiscountType, DiscountFor, DiscountedPrice, ValidFrom, ValidTo
                FROM Packages
                WHERE GETDATE() BETWEEN ValidFrom AND ValidTo 
                AND IsDeleted = 0
                AND Duration LIKE '%Per day%'
                ORDER BY Price ASC";
                }
                else if (walkInType == "Free Trial")
                {
                    // For Free Trial: Show all packages except those with "Month"
                    query = @"
                SELECT PackageID, PackageName, Description, Price, Duration, Features, 
                       Discount, DiscountType, DiscountFor, DiscountedPrice, ValidFrom, ValidTo
                FROM Packages
                WHERE GETDATE() BETWEEN ValidFrom AND ValidTo 
                AND IsDeleted = 0
                AND Duration NOT LIKE '%Month%'
                ORDER BY Price ASC";
                }
                else
                {
                    // Default: Show all non-monthly packages
                    query = @"
                SELECT PackageID, PackageName, Description, Price, Duration, Features, 
                       Discount, DiscountType, DiscountFor, DiscountedPrice, ValidFrom, ValidTo
                FROM Packages
                WHERE GETDATE() BETWEEN ValidFrom AND ValidTo 
                AND IsDeleted = 0
                AND Duration NOT LIKE '%Month%'
                ORDER BY Price ASC";
                }

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    packages.Add(new SellingModel
                    {
                        SellingID = reader.GetInt32(reader.GetOrdinal("PackageID")),
                        Title = reader["PackageName"]?.ToString() ?? string.Empty,
                        Description = reader["Description"]?.ToString() ?? string.Empty,
                        Category = CategoryConstants.GymPackage,
                        Price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0,
                        Stock = 999,
                        ImagePath = null,
                        Features = reader["Features"]?.ToString() ?? string.Empty
                    });
                }

                return packages;
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Load Packages Failed")
                    .WithContent($"Error loading packages: {ex.Message}")
                    .ShowError();
                return packages;
            }
        }

        #endregion

        #region UPDATE

        public async Task<(bool Success, string Message)> UpdateWalkInCustomerAsync(ManageWalkInModel walkIn)
        {
            if (!CanUpdate())
            {
                ShowAccessDeniedToast("update walk-in customer information", "administrators");
                return (false, "Insufficient permissions to update walk-in customers.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                if (!await CustomerExistsAsync(conn, walkIn.WalkInID))
                {
                    ShowWarningToast("Customer Not Found", "The walk-in customer you're trying to update doesn't exist.");
                    return (false, "Walk-in customer not found.");
                }

                using var cmd = new SqlCommand(
                    @"UPDATE WalkInCustomers 
                      SET FirstName = @firstName, MiddleInitial = @middleInitial, LastName = @lastName,
                          ContactNumber = @contactNumber, Age = @age, Gender = @gender,
                          WalkinType = @walkInType, WalkinPackage = @walkInPackage, PaymentMethod = @paymentMethod
                      WHERE CustomerID = @customerId", conn);

                cmd.Parameters.AddWithValue("@customerId", walkIn.WalkInID);
                AddWalkInParameters(cmd, walkIn);

                await cmd.ExecuteNonQueryAsync();
                await LogActionAsync(conn, "UPDATE", $"Updated walk-in customer: {walkIn.FirstName} {walkIn.LastName}", true);

                ShowSuccessToast("Customer Updated", $"Successfully updated {walkIn.FirstName} {walkIn.LastName}.");
                return (true, "Walk-in customer updated successfully.");
            }
            catch (SqlException ex)
            {
                ShowErrorToast("Database Error", $"Failed to update customer: {ex.Message}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region DELETE

        public async Task<(bool Success, string Message)> DeleteWalkInCustomerAsync(int customerId)
        {
            if (!CanDelete())
            {
                ShowAccessDeniedToast("delete walk-in customers", "administrators");
                return (false, "Insufficient permissions to delete walk-in customers.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var customerName = await GetCustomerNameAsync(conn, customerId);
                if (string.IsNullOrEmpty(customerName))
                {
                    ShowWarningToast("Customer Not Found", "The walk-in customer you're trying to delete doesn't exist.");
                    return (false, "Walk-in customer not found.");
                }

                using var cmd = new SqlCommand("UPDATE WalkInCustomers SET IsDeleted = 1 WHERE CustomerID = @customerId", conn);
                cmd.Parameters.AddWithValue("@customerId", customerId);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "DELETE", $"Deleted walk-in customer: {customerName}", true);
                    ShowSuccessToast("Customer Deleted", $"Successfully deleted walk-in customer '{customerName}'.");
                    return (true, "Walk-in customer deleted successfully.");
                }

                return (false, "Failed to delete walk-in customer.");
            }
            catch (SqlException ex)
            {
                ShowErrorToast("Database Error", $"Failed to delete customer: {ex.Message}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, int DeletedCount)> DeleteMultipleWalkInCustomersAsync(List<int> customerIds)
        {
            if (!CanDelete())
            {
                ShowAccessDeniedToast("delete walk-in customers", "administrators");
                return (false, "Insufficient permissions to delete walk-in customers.", 0);
            }

            if (customerIds == null || customerIds.Count == 0)
            {
                return (false, "No customers selected for deletion.", 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var deletedCount = 0;
                var customerNames = new List<string>();

                using var transaction = conn.BeginTransaction();
                try
                {
                    foreach (var customerId in customerIds)
                    {
                        var name = await GetCustomerNameAsync(conn, customerId, transaction);
                        if (!string.IsNullOrEmpty(name))
                        {
                            customerNames.Add(name);
                        }

                        using var cmd = new SqlCommand("UPDATE WalkInCustomers SET IsDeleted = 1 WHERE CustomerID = @customerId", conn, transaction);
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        deletedCount += await cmd.ExecuteNonQueryAsync();
                    }

                    await LogActionAsync(conn, transaction, "DELETE",
                        $"Deleted {deletedCount} walk-in customers: {string.Join(", ", customerNames)}", true);

                    transaction.Commit();
                    ShowSuccessToast("Customers Deleted", $"Successfully deleted {deletedCount} walk-in customer(s).");
                    return (true, $"Successfully deleted {deletedCount} walk-in customer(s).", deletedCount);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                ShowErrorToast("Database Error", $"Failed to delete customers: {ex.Message}");
                return (false, $"Database error: {ex.Message}", 0);
            }
            catch (Exception ex)
            {
                ShowErrorToast("Error", $"An unexpected error occurred: {ex.Message}");
                return (false, $"Error: {ex.Message}", 0);
            }
        }

        #endregion

        #region BUSINESS LOGIC

        public async Task<(bool Success, bool HasUsedFreeTrial, string Message)> CheckFreeTrialEligibilityAsync(
            string firstName, string lastName, string? contactNumber)
        {
            if (!CanView())
            {
                return (false, false, "Insufficient permissions.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT COUNT(*) FROM WalkInCustomers 
                      WHERE FirstName = @firstName AND LastName = @lastName AND ContactNumber = @contactNumber
                      AND WalkinType = 'Free Trial'", conn);

                cmd.Parameters.AddWithValue("@firstName", firstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@lastName", lastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@contactNumber", contactNumber ?? (object)DBNull.Value);

                var count = (int)await cmd.ExecuteScalarAsync();
                return count > 0
                    ? (true, true, "Customer has already used their free trial.")
                    : (true, false, "Customer is eligible for free trial.");
            }
            catch (Exception ex)
            {
                return (false, false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, List<ManageWalkInModel>? History, string Message)> GetCustomerHistoryAsync(
            string firstName, string lastName, string? contactNumber)
        {
            if (!CanView())
            {
                return (false, null, "Insufficient permissions.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT CustomerID, FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, 
                      WalkinType, WalkinPackage, PaymentMethod, RegisteredByEmployeeID 
                      FROM WalkInCustomers 
                      WHERE FirstName = @firstName AND LastName = @lastName AND ContactNumber = @contactNumber
                      ORDER BY CustomerID DESC", conn);

                cmd.Parameters.AddWithValue("@firstName", firstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@lastName", lastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@contactNumber", contactNumber ?? (object)DBNull.Value);

                var history = await ReadWalkInCustomersAsync(cmd);
                return (true, history, $"Found {history.Count} visit(s) for this customer.");
            }
            catch (Exception ex)
            {
                return (false, null, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region STATISTICS

        public async Task<(bool Success, int FreeTrialCount, int RegularCount, int TotalCount)> GetWalkInStatisticsAsync()
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
                        SUM(CASE WHEN WalkinType = 'Free Trial' THEN 1 ELSE 0 END) as FreeTrialCount,
                        SUM(CASE WHEN WalkinType = 'Regular' THEN 1 ELSE 0 END) as RegularCount,
                        COUNT(*) as TotalCount
                      FROM WalkInCustomers", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (true, reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
                }

                return (false, 0, 0, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetWalkInStatisticsAsync] {ex.Message}");
                return (false, 0, 0, 0);
            }
        }

        public async Task<(bool Success, Dictionary<string, int>? PackageStats)> GetPackageStatisticsAsync()
        {
            if (!CanView())
            {
                return (false, null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT WalkinPackage, COUNT(*) as Count
                      FROM WalkInCustomers
                      GROUP BY WalkinPackage", conn);

                var stats = new Dictionary<string, int>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    stats[reader.GetString(0)] = reader.GetInt32(1);
                }

                return (true, stats);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetPackageStatisticsAsync] {ex.Message}");
                return (false, null);
            }
        }

        #endregion

        #region MEMBER STATUS VALIDATION

        /// <summary>
        /// STRICT VALIDATION: Checks if FirstName + LastName matches ANY active member
        /// If match found, blocks walk-in registration completely
        /// </summary>
        private async Task<(bool IsActiveMember, bool IsExpiredMember, int? MemberId, string? MemberName, List<string>? MultipleMatches)> CheckMemberStatusAsync(
    SqlConnection conn, SqlTransaction transaction, ManageWalkInModel walkIn)
        {
            try
            {
                const string query = @"
    SELECT 
        m.MemberID, 
        m.Firstname, 
        m.MiddleInitial,
        m.Lastname,
        m.ContactNumber,
        m.Age,
        m.Status,
        m.ValidUntil,
        CASE 
            WHEN m.ValidUntil < CAST(GETDATE() AS DATE) THEN 1
            ELSE 0
        END AS IsExpired
    FROM Members m
    WHERE m.FirstName = @FirstName 
        AND m.LastName = @LastName 
        AND m.Age = @Age
        AND (m.IsDeleted = 0 OR m.IsDeleted IS NULL)
    ORDER BY 
        CASE 
            WHEN m.Status = 'Active' THEN 1
            WHEN m.ValidUntil >= CAST(GETDATE() AS DATE) THEN 2
            ELSE 3
        END,
        m.DateJoined DESC";

                using var cmd = new SqlCommand(query, conn, transaction);
                cmd.Parameters.AddWithValue("@FirstName", walkIn.FirstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LastName", walkIn.LastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Age", walkIn.Age); // ✅ Added age parameter

                var matches = new List<(int MemberId, string MemberName, string Status, bool IsExpired, int Age, string Contact)>();

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int memberId = reader.GetInt32(0);
                    string firstName = reader.GetString(1);
                    string middleInitial = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    string lastName = reader.GetString(3);
                    string contactNumber = reader.IsDBNull(4) ? "N/A" : reader.GetString(4);
                    int age = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                    string status = reader.IsDBNull(6) ? "Active" : reader.GetString(6);
                    bool isExpired = reader.GetInt32(8) == 1;

                    string memberName = string.IsNullOrWhiteSpace(middleInitial)
                        ? $"{firstName} {lastName}"
                        : $"{firstName} {middleInitial}. {lastName}";

                    matches.Add((memberId, memberName, status, isExpired, age, contactNumber));

                    Debug.WriteLine($"[CheckMemberStatusAsync] Found member match: {memberName}");
                    Debug.WriteLine($"  ID: {memberId}, Age: {age}, Contact: {contactNumber}");
                    Debug.WriteLine($"  Walk-in Age: {walkIn.Age} ✅ AGES MATCH");
                    Debug.WriteLine($"  Status: {status}, Expired: {isExpired}");
                }

                if (matches.Count == 0)
                {
                    Debug.WriteLine("[CheckMemberStatusAsync] ✓ No member matches found (name + age) - allowing walk-in");
                    return (false, false, null, null, null);
                }

                // Check each match for active status
                foreach (var match in matches)
                {
                    bool isActive = !match.IsExpired &&
                                   !match.Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase) &&
                                   !match.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase);

                    if (isActive)
                    {
                        Debug.WriteLine($"[CheckMemberStatusAsync] ❌ BLOCKING - Active member found: {match.MemberName} (ID: {match.MemberId}, Age: {match.Age})");
                        return (true, false, match.MemberId, match.MemberName, null);
                    }
                }

                // All matches are expired/inactive - allow walk-in but notify
                var firstMatch = matches.First();
                Debug.WriteLine($"[CheckMemberStatusAsync] ⚠️ Allowing - All matches are expired/inactive (Age: {firstMatch.Age})");

                return (false, true, firstMatch.MemberId, firstMatch.MemberName,
                        matches.Count > 1 ? matches.Select(m => m.MemberName).ToList() : null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CheckMemberStatusAsync] Error: {ex.Message}");
                return (false, false, null, null, null);
            }
        }

        #endregion

        #region HELPER METHODS

        // Type checking helpers
        private bool IsRegular(string? type) =>
            type?.Equals(REGULAR, StringComparison.OrdinalIgnoreCase) == true;

        private bool IsFreeTrial(string? type) =>
            type?.Equals(FREE_TRIAL, StringComparison.OrdinalIgnoreCase) == true;

        private bool IsRegularWithPackage(ManageWalkInModel walkIn) =>
            IsRegular(walkIn.WalkInType) &&
            !string.IsNullOrEmpty(walkIn.WalkInPackage) &&
            walkIn.WalkInPackage != NONE_PACKAGE;

        // Parameter helpers
        private void AddWalkInParameters(SqlCommand cmd, ManageWalkInModel walkIn)
        {
            cmd.Parameters.AddWithValue("@firstName", walkIn.FirstName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@middleInitial", walkIn.MiddleInitial ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@lastName", walkIn.LastName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@contactNumber", walkIn.ContactNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@age", walkIn.Age);
            cmd.Parameters.AddWithValue("@gender", walkIn.Gender ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@walkInType", walkIn.WalkInType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@walkInPackage", walkIn.WalkInPackage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@paymentMethod", walkIn.PaymentMethod ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@quantity", walkIn.Quantity ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);
        }

        // Data reading helpers
        private async Task<List<ManageWalkInModel>> ReadWalkInCustomersAsync(SqlCommand cmd)
        {
            var walkIns = new List<ManageWalkInModel>();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                walkIns.Add(MapToWalkInModel(reader));
            }

            return walkIns;
        }

        private ManageWalkInModel MapToWalkInModel(SqlDataReader reader)
        {
            var firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var middleInitial = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var lastName = reader.IsDBNull(3) ? "" : reader.GetString(3);

            return new ManageWalkInModel
            {
                WalkInID = reader.GetInt32(0),
                FirstName = firstName,
                MiddleInitial = middleInitial,
                LastName = lastName,
                Name = $"{firstName} {middleInitial} {lastName}".Trim(),
                ContactNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                Age = reader.GetInt32(5),
                Gender = reader.IsDBNull(6) ? null : reader.GetString(6),
                WalkInType = reader.IsDBNull(7) ? null : reader.GetString(7),
                WalkInPackage = reader.IsDBNull(8) ? null : reader.GetString(8),
                PaymentMethod = reader.IsDBNull(9) ? null : reader.GetString(9),
                RegisteredByEmployeeID = reader.IsDBNull(10) ? null : reader.GetInt32(10)
            };
        }

        // Customer existence checks
        private async Task<bool> CustomerExistsAsync(SqlConnection conn, int customerId)
        {
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM WalkInCustomers WHERE CustomerID = @customerId AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);
            cmd.Parameters.AddWithValue("@customerId", customerId);
            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private async Task<string?> GetCustomerNameAsync(SqlConnection conn, int customerId, SqlTransaction? transaction = null)
        {
            using var cmd = new SqlCommand(
                "SELECT FirstName, LastName FROM WalkInCustomers WHERE CustomerID = @customerId AND (IsDeleted = 0 OR IsDeleted IS NULL)",
                conn, transaction);
            cmd.Parameters.AddWithValue("@customerId", customerId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return $"{reader.GetString(0)} {reader.GetString(1)}";
            }

            return null;
        }

        // Logging
        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                using var cmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                      VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)", conn);

                AddLogParameters(cmd, actionType, description, success);
                await cmd.ExecuteNonQueryAsync();
                NotifyDashboardEvents();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        private async Task LogActionAsync(SqlConnection conn, SqlTransaction transaction,
            string actionType, string description, bool success)
        {
            try
            {
                using var cmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                      VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)",
                    conn, transaction);

                AddLogParameters(cmd, actionType, description, success);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        private void AddLogParameters(SqlCommand cmd, string actionType, string description, bool success)
        {
            cmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@success", success);
            cmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);
        }

        // Event notifications
        private void NotifyDashboardEvents()
        {
            DashboardEventService.Instance.NotifyCheckinAdded();
            DashboardEventService.Instance.NotifyPopulationDataChanged();
            DashboardEventService.Instance.NotifySalesUpdated();
            DashboardEventService.Instance.NotifyRecentLogsUpdated();
            DashboardEventService.Instance.NotifyChartDataUpdated();
        }

        // Toast notifications
        private void ShowAccessDeniedToast(string action, string? requiredRole = null)
        {
            var message = requiredRole != null
                ? $"Only {requiredRole} can {action}."
                : $"You don't have permission to {action}.";

            _toastManager.CreateToast("Access Denied")
                .WithContent(message)
                .DismissOnClick()
                .ShowError();
        }

        private void ShowErrorToast(string title, string message)
        {
            _toastManager.CreateToast(title)
                .WithContent(message)
                .DismissOnClick()
                .ShowError();
        }

        private void ShowWarningToast(string title, string message)
        {
            _toastManager.CreateToast(title)
                .WithContent(message)
                .DismissOnClick()
                .ShowWarning();
        }

        private void ShowSuccessToast(string title, string message)
        {
            _toastManager.CreateToast(title)
                .WithContent(message)
                .DismissOnClick()
                .ShowSuccess();
        }

        #endregion
    }
}
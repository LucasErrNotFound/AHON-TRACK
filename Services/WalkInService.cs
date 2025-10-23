using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
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
    public class WalkInService : IWalkInService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public WalkInService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        #region Role-Based Access Control

        private bool CanCreate()
        {
            // Both Admin and Staff can create
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanUpdate()
        {
            // Only Admin can update
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanDelete()
        {
            // Only Admin can delete
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanView()
        {
            // Both Admin and Staff can view
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;
        }

        #endregion

        #region CREATE

        public async Task<(bool Success, string Message, int? CustomerID)> AddWalkInCustomerAsync(ManageWalkInModel walkIn)
        {
            if (!CanCreate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You don't have permission to register walk-in customers.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to register walk-in customers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    // Check if customer already exists (not deleted)
                    using var checkExistingCmd = new SqlCommand(
                        @"SELECT CustomerID, WalkinType FROM WalkInCustomers 
                  WHERE FirstName = @firstName 
                  AND LastName = @lastName 
                  AND ContactNumber = @contactNumber
                  AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn, transaction);

                    checkExistingCmd.Parameters.AddWithValue("@firstName", walkIn.FirstName ?? (object)DBNull.Value);
                    checkExistingCmd.Parameters.AddWithValue("@lastName", walkIn.LastName ?? (object)DBNull.Value);
                    checkExistingCmd.Parameters.AddWithValue("@contactNumber", walkIn.ContactNumber ?? (object)DBNull.Value);

                    int? existingCustomerId = null;
                    string? existingWalkInType = null;

                    using (var reader = await checkExistingCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            existingCustomerId = reader.GetInt32(0);
                            existingWalkInType = reader.IsDBNull(1) ? null : reader.GetString(1);
                        }
                    }

                    // Handle existing customer logic
                    if (existingCustomerId.HasValue)
                    {
                        // ❌ NEW: Prevent Regular customers from downgrading to Free Trial
                        if (existingWalkInType?.Equals("Regular", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            if (walkIn.WalkInType?.Equals("Free Trial", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                _toastManager.CreateToast("Invalid Selection")
                                    .WithContent($"{walkIn.FirstName} {walkIn.LastName} is already a Regular customer and cannot use Free Trial.")
                                    .DismissOnClick()
                                    .ShowWarning();
                                return (false, "Regular customers cannot downgrade to Free Trial.", null);
                            }

                            // Regular customer returning for another Regular visit
                            var currentDateTime = DateTime.Now;
                            using var checkInCmd = new SqlCommand(
                                @"INSERT INTO WalkInRecords (CustomerID, CheckIn, CheckOut, RegisteredByEmployeeID)
              VALUES (@customerID, @checkIn, NULL, @employeeID)", conn, transaction);

                            checkInCmd.Parameters.AddWithValue("@customerID", existingCustomerId.Value);
                            checkInCmd.Parameters.Add("@checkIn", SqlDbType.DateTime).Value = currentDateTime;
                            checkInCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                            await checkInCmd.ExecuteNonQueryAsync();

                            await LogActionAsync(conn, transaction, "CREATE",
                                $"Checked in existing walk-in customer: {walkIn.FirstName} {walkIn.LastName}", true);

                            transaction.Commit();

                            return (true, "Existing customer checked in.", existingCustomerId.Value);
                        }

                        if (existingWalkInType?.Equals("Free Trial", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            if (walkIn.WalkInType?.Equals("Free Trial", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                _toastManager.CreateToast("Free Trial Already Used")
                                    .WithContent($"{walkIn.FirstName} {walkIn.LastName} has already availed a free trial.")
                                    .DismissOnClick()
                                    .ShowWarning();
                                return (false, "This customer has already used their free trial.", null);
                            }

                            // Update to Regular
                            if (walkIn.WalkInType?.Equals("Regular", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                using var updateCmd = new SqlCommand(
                                    @"UPDATE WalkInCustomers 
                              SET WalkinType = @walkInType,
                                  WalkinPackage = @walkInPackage,
                                  PaymentMethod = @paymentMethod,
                                  Quantity = @quantity,
                                  Age = @age,
                                  Gender = @gender,
                                  MiddleInitial = @middleInitial
                              WHERE CustomerID = @customerId", conn, transaction);

                                updateCmd.Parameters.AddWithValue("@customerId", existingCustomerId.Value);
                                updateCmd.Parameters.AddWithValue("@walkInType", walkIn.WalkInType ?? (object)DBNull.Value);
                                updateCmd.Parameters.AddWithValue("@walkInPackage", walkIn.WalkInPackage ?? (object)DBNull.Value);
                                updateCmd.Parameters.AddWithValue("@paymentMethod", walkIn.PaymentMethod ?? (object)DBNull.Value);
                                updateCmd.Parameters.AddWithValue("@quantity", walkIn.Quantity ?? (object)DBNull.Value);
                                updateCmd.Parameters.AddWithValue("@age", walkIn.Age);
                                updateCmd.Parameters.AddWithValue("@gender", walkIn.Gender ?? (object)DBNull.Value);
                                updateCmd.Parameters.AddWithValue("@middleInitial", walkIn.MiddleInitial ?? (object)DBNull.Value);

                                await updateCmd.ExecuteNonQueryAsync();

                                // Check in the customer
                                var currentDateTime = DateTime.Now;
                                using var checkInCmd = new SqlCommand(
                                    @"INSERT INTO WalkInRecords (CustomerID, CheckIn, CheckOut, RegisteredByEmployeeID)
                              VALUES (@customerID, @checkIn, NULL, @employeeID)", conn, transaction);

                                checkInCmd.Parameters.AddWithValue("@customerID", existingCustomerId.Value);
                                checkInCmd.Parameters.Add("@checkIn", SqlDbType.DateTime).Value = currentDateTime;
                                checkInCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                                await checkInCmd.ExecuteNonQueryAsync();

                                await LogActionAsync(conn, transaction, "UPDATE",
                                    $"Updated walk-in customer from Free Trial to Regular and checked in: {walkIn.FirstName} {walkIn.LastName}", true);

                                transaction.Commit();

                                return (true, "Customer updated to Regular and checked in.", existingCustomerId.Value);
                            }
                        }

                        // Regular customer returning
                        if (existingWalkInType?.Equals("Regular", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            var currentDateTime = DateTime.Now;
                            using var checkInCmd = new SqlCommand(
                                @"INSERT INTO WalkInRecords (CustomerID, CheckIn, CheckOut, RegisteredByEmployeeID)
                          VALUES (@customerID, @checkIn, NULL, @employeeID)", conn, transaction);

                            checkInCmd.Parameters.AddWithValue("@customerID", existingCustomerId.Value);
                            checkInCmd.Parameters.Add("@checkIn", SqlDbType.DateTime).Value = currentDateTime;
                            checkInCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                            await checkInCmd.ExecuteNonQueryAsync();

                            await LogActionAsync(conn, transaction, "CREATE",
                                $"Checked in existing walk-in customer: {walkIn.FirstName} {walkIn.LastName}", true);

                            transaction.Commit();

                            return (true, "Existing customer checked in.", existingCustomerId.Value);
                        }
                    }

                    // NEW CUSTOMER - Create customer record
                    using var cmd = new SqlCommand(
                        @"INSERT INTO WalkInCustomers (FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, WalkInType, WalkInPackage, PaymentMethod, Quantity, RegisteredByEmployeeID, IsDeleted) 
                  OUTPUT INSERTED.CustomerID
                  VALUES (@firstName, @middleInitial, @lastName, @contactNumber, @age, @gender, @walkInType, @walkInPackage, @paymentMethod, @quantity, @employeeID, 0)",
                        conn, transaction);

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

                    var customerId = (int)await cmd.ExecuteScalarAsync();

                    // ✅ NEW: If Regular and has a package (not "None"), record sale and create session
                    if (walkIn.WalkInType == "Regular" &&
                        !string.IsNullOrEmpty(walkIn.WalkInPackage) &&
                        walkIn.WalkInPackage != "None")
                    {
                        // Get package details
                        using var pkgCmd = new SqlCommand(
                            @"SELECT PackageID, Price, Duration FROM Packages 
                      WHERE PackageName = @packageName AND IsDeleted = 0",
                            conn, transaction);
                        pkgCmd.Parameters.AddWithValue("@packageName", walkIn.WalkInPackage);

                        int? packageId = null;
                        decimal packagePrice = 0;
                        string duration = "";

                        using (var pkgReader = await pkgCmd.ExecuteReaderAsync())
                        {
                            if (await pkgReader.ReadAsync())
                            {
                                packageId = pkgReader.GetInt32(0);
                                packagePrice = pkgReader.GetDecimal(1);
                                duration = pkgReader.GetString(2);
                            }
                        }

                        if (packageId.HasValue)
                        {
                            decimal totalAmount = packagePrice * (walkIn.Quantity ?? 1);

                            // Record in Sales table
                            using var salesCmd = new SqlCommand(
                                @"INSERT INTO Sales (SaleDate, PackageID, CustomerID, Quantity, Amount, RecordedBy)
                          VALUES (GETDATE(), @packageId, @customerId, @quantity, @amount, @employeeId)",
                                conn, transaction);

                            salesCmd.Parameters.AddWithValue("@packageId", packageId.Value);
                            salesCmd.Parameters.AddWithValue("@customerId", customerId);
                            salesCmd.Parameters.AddWithValue("@quantity", walkIn.Quantity ?? 1);
                            salesCmd.Parameters.AddWithValue("@amount", totalAmount);
                            salesCmd.Parameters.AddWithValue("@employeeId", CurrentUserModel.UserId ?? (object)DBNull.Value);

                            await salesCmd.ExecuteNonQueryAsync();

                            // DO NOT REMOVE FOR FUTURE PURPOSES
                            /* // Calculate sessions
                             int sessionsLeft = duration.ToLower() switch
                             {
                                 "one-time only" => 1,
                                 "session" => 1,
                                 "/session" => 1,
                                 _ => 1
                             };

                             // Insert into WalkInSessions
                             using var sessionCmd = new SqlCommand(
                                 @"INSERT INTO WalkInSessions (CustomerID, PackageID, SessionsLeft, StartDate)
                           VALUES (@customerId, @packageId, @sessionsLeft, GETDATE())",
                                 conn, transaction);

                             sessionCmd.Parameters.AddWithValue("@customerId", customerId);
                             sessionCmd.Parameters.AddWithValue("@packageId", packageId.Value);
                             sessionCmd.Parameters.AddWithValue("@sessionsLeft", sessionsLeft * (walkIn.Quantity ?? 1));

                             await sessionCmd.ExecuteNonQueryAsync(); */

                            // Update DailySales
                            using var dailySalesCmd = new SqlCommand(
                                @"MERGE DailySales AS target
                          USING (SELECT CAST(GETDATE() AS DATE) AS SaleDate, @EmployeeID AS EmployeeID) AS source
                          ON target.SaleDate = source.SaleDate AND target.TransactionByEmployeeID = source.EmployeeID
                          WHEN MATCHED THEN
                              UPDATE SET 
                                  TotalSales = target.TotalSales + @TotalAmount,
                                  TotalTransactions = target.TotalTransactions + 1,
                                  TransactionUpdatedDate = SYSDATETIME()
                          WHEN NOT MATCHED THEN
                              INSERT (SaleDate, TotalSales, TotalTransactions, TransactionByEmployeeID)
                              VALUES (source.SaleDate, @TotalAmount, 1, source.EmployeeID);",
                                conn, transaction);

                            dailySalesCmd.Parameters.AddWithValue("@EmployeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);
                            dailySalesCmd.Parameters.AddWithValue("@TotalAmount", totalAmount);

                            await dailySalesCmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Create check-in record
                    var checkInDateTime = DateTime.Now;
                    using var checkInCmd2 = new SqlCommand(
                        @"INSERT INTO WalkInRecords (CustomerID, CheckIn, CheckOut, RegisteredByEmployeeID)
                  VALUES (@customerID, @checkIn, NULL, @employeeID)",
                        conn, transaction);

                    checkInCmd2.Parameters.AddWithValue("@customerID", customerId);
                    checkInCmd2.Parameters.Add("@checkIn", SqlDbType.DateTime).Value = checkInDateTime;
                    checkInCmd2.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                    await checkInCmd2.ExecuteNonQueryAsync();

                    // Log
                    await LogActionAsync(conn, transaction, "CREATE",
                        $"Registered walk-in customer: {walkIn.FirstName} {walkIn.LastName} ({walkIn.WalkInType}) with {walkIn.WalkInPackage}",
                        true);

                    transaction.Commit();
                    DashboardEventService.Instance.NotifyCheckinAdded();
                    DashboardEventService.Instance.NotifyPopulationDataChanged();
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
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to register customer: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region READ

        public async Task<(bool Success, string Message, List<ManageWalkInModel>? WalkIns)> GetAllWalkInCustomersAsync()
        {
            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You don't have permission to view walk-in customers.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to view walk-in customers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT CustomerID, FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, WalkinType, WalkinPackage, PaymentMethod, RegisteredByEmployeeID 
                      FROM WalkInCustomers 
                      WHERE IsDeleted = 0 OR IsDeleted IS NULL
                      ORDER BY CustomerID DESC", conn);

                var walkIns = new List<ManageWalkInModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var middleInitial = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    var lastName = reader.IsDBNull(3) ? "" : reader.GetString(3);

                    walkIns.Add(new ManageWalkInModel
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
                    });
                }

                return (true, "Walk-in customers retrieved successfully.", walkIns);
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to retrieve walk-in customers: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
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
                    @"SELECT CustomerID, FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, WalkinType, WalkinPackage, PaymentMethod, RegisteredByEmployeeID 
                      FROM WalkInCustomers 
                      WHERE CustomerID = @customerId AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);

                cmd.Parameters.AddWithValue("@customerId", customerId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var middleInitial = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    var lastName = reader.IsDBNull(3) ? "" : reader.GetString(3);

                    var walkIn = new ManageWalkInModel
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
                    @"SELECT CustomerID, FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, WalkinType, WalkinPackage, PaymentMethod, RegisteredByEmployeeID 
                      FROM WalkInCustomers 
                      WHERE WalkinType = @walkInType AND (IsDeleted = 0 OR IsDeleted IS NULL)
                      ORDER BY CustomerID DESC", conn);

                cmd.Parameters.AddWithValue("@walkInType", walkInType ?? (object)DBNull.Value);

                var walkIns = new List<ManageWalkInModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var middleInitial = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    var lastName = reader.IsDBNull(3) ? "" : reader.GetString(3);

                    walkIns.Add(new ManageWalkInModel
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
                    });
                }

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
                    @"SELECT CustomerID, FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, WalkinType, WalkinPackage, PaymentMethod, RegisteredByEmployeeID 
                      FROM WalkInCustomers 
                      WHERE WalkinPackage = @package AND (IsDeleted = 0 OR IsDeleted IS NULL)
                      ORDER BY CustomerID DESC", conn);

                cmd.Parameters.AddWithValue("@package", package ?? (object)DBNull.Value);

                var walkIns = new List<ManageWalkInModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var middleInitial = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    var lastName = reader.IsDBNull(3) ? "" : reader.GetString(3);

                    walkIns.Add(new ManageWalkInModel
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
                    });
                }

                return (true, "Walk-in customers retrieved successfully.", walkIns);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<List<SellingModel>> GetAvailablePackagesForWalkInAsync()
        {
            System.Diagnostics.Debug.WriteLine("🔹 GetAvailablePackagesForWalkInAsync called");
            var packages = new List<SellingModel>();

            if (!CanView())
            {
                System.Diagnostics.Debug.WriteLine("❌ Access denied - CanView() returned false");
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to view gym packages.")
                    .ShowError();
                return packages;
            }

            System.Diagnostics.Debug.WriteLine("✅ Permission check passed");

            try
            {
                System.Diagnostics.Debug.WriteLine($"🔌 Connecting to database...");
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                System.Diagnostics.Debug.WriteLine("✅ Database connection opened");

                // In WalkInService.GetAvailablePackagesForWalkInAsync method
                string query = @"
    SELECT 
        PackageID,
        PackageName,
        Description,
        Price,
        Duration,
        Features,
        Discount,
        DiscountType,
        DiscountFor,
        DiscountedPrice,
        ValidFrom,
        ValidTo
    FROM Packages
    WHERE GETDATE() BETWEEN ValidFrom AND ValidTo 
    AND IsDeleted = 0
    AND (Duration NOT LIKE '%month%' AND Duration NOT LIKE '%Month%')
    ORDER BY Price ASC;";

                System.Diagnostics.Debug.WriteLine($"📝 Executing query");

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                int count = 0;
                while (await reader.ReadAsync())
                {
                    count++;
                    var packageName = reader["PackageName"]?.ToString() ?? string.Empty;
                    var duration = reader["Duration"]?.ToString() ?? string.Empty;
                    var price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0;

                    var package = new SellingModel
                    {
                        SellingID = reader.GetInt32(reader.GetOrdinal("PackageID")),
                        Title = packageName,
                        Description = reader["Description"]?.ToString() ?? string.Empty,
                        Category = CategoryConstants.GymPackage,
                        Price = price,
                        Stock = 999,
                        ImagePath = null,
                        Features = reader["Features"]?.ToString() ?? string.Empty
                    };

                    packages.Add(package);
                    System.Diagnostics.Debug.WriteLine($"  ✓ Package {count}: {packageName} (Duration: {duration}, Price: ₱{price})");
                }

                System.Diagnostics.Debug.WriteLine($"✅ Total packages loaded: {count}");

                if (count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Query returned 0 results - check database conditions");
                }

                return packages;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in GetAvailablePackagesForWalkInAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can update walk-in customer information.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to update walk-in customers.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check if customer exists and is not deleted
                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM WalkInCustomers WHERE CustomerID = @customerId AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);
                checkCmd.Parameters.AddWithValue("@customerId", walkIn.WalkInID);

                var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;
                if (!exists)
                {
                    _toastManager.CreateToast("Customer Not Found")
                        .WithContent("The walk-in customer you're trying to update doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Walk-in customer not found.");
                }

                // Update customer
                using var cmd = new SqlCommand(
                    @"UPDATE WalkInCustomers 
                      SET FirstName = @firstName,
                          MiddleInitial = @middleInitial,
                          LastName = @lastName,
                          ContactNumber = @contactNumber,
                          Age = @age,
                          Gender = @gender,
                          WalkinType = @walkInType,
                          WalkinPackage = @walkInPackage,
                          PaymentMethod = @paymentMethod
                      WHERE CustomerID = @customerId", conn);

                cmd.Parameters.AddWithValue("@customerId", walkIn.WalkInID);
                cmd.Parameters.AddWithValue("@firstName", walkIn.FirstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@middleInitial", walkIn.MiddleInitial ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@lastName", walkIn.LastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@contactNumber", walkIn.ContactNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@age", walkIn.Age);
                cmd.Parameters.AddWithValue("@gender", walkIn.Gender ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@walkInType", walkIn.WalkInType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@walkInPackage", walkIn.WalkInPackage ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@paymentMethod", walkIn.PaymentMethod ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                await LogActionAsync(conn, "UPDATE", $"Updated walk-in customer: {walkIn.FirstName} {walkIn.LastName}", true);

                _toastManager.CreateToast("Customer Updated")
                    .WithContent($"Successfully updated {walkIn.FirstName} {walkIn.LastName}.")
                    .DismissOnClick()
                    .ShowSuccess();

                return (true, "Walk-in customer updated successfully.");
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to update customer: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}");
            }
        }


        #endregion

        #region DELETE

        public async Task<(bool Success, string Message)> DeleteWalkInCustomerAsync(int customerId)
        {
            if (!CanDelete())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete walk-in customers.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to delete walk-in customers.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Get customer name for logging
                using var getNameCmd = new SqlCommand(
                    "SELECT FirstName, LastName FROM WalkInCustomers WHERE CustomerID = @customerId AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);
                getNameCmd.Parameters.AddWithValue("@customerId", customerId);

                using var reader = await getNameCmd.ExecuteReaderAsync();
                string customerName = "";
                if (await reader.ReadAsync())
                {
                    customerName = $"{reader.GetString(0)} {reader.GetString(1)}";
                }
                reader.Close();

                if (string.IsNullOrEmpty(customerName))
                {
                    _toastManager.CreateToast("Customer Not Found")
                        .WithContent("The walk-in customer you're trying to delete doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Walk-in customer not found.");
                }

                // Delete customer
                using var cmd = new SqlCommand(
                    "UPDATE WalkInCustomers SET IsDeleted = 1 WHERE CustomerID = @customerId", conn);
                cmd.Parameters.AddWithValue("@customerId", customerId);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "DELETE", $"Deleted walk-in customer: {customerName}", true);

                    _toastManager.CreateToast("Customer Deleted")
                        .WithContent($"Successfully deleted walk-in customer '{customerName}'.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Walk-in customer deleted successfully.");
                }

                return (false, "Failed to delete walk-in customer.");
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to delete customer: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, int DeletedCount)> DeleteMultipleWalkInCustomersAsync(List<int> customerIds)
        {
            if (!CanDelete())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete walk-in customers.")
                    .DismissOnClick()
                    .ShowError();
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
                        // Get customer name
                        using var getNameCmd = new SqlCommand(
                            "SELECT FirstName, LastName FROM WalkInCustomers WHERE CustomerID = @customerId AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn, transaction);
                        getNameCmd.Parameters.AddWithValue("@customerId", customerId);

                        using var reader = await getNameCmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            customerNames.Add($"{reader.GetString(0)} {reader.GetString(1)}");
                        }
                        reader.Close();

                        // Delete customer
                        using var deleteCmd = new SqlCommand(
                            "UPDATE WalkInCustomers SET IsDeleted = 1 WHERE CustomerID = @customerId", conn, transaction);
                        deleteCmd.Parameters.AddWithValue("@customerId", customerId);
                        deletedCount += await deleteCmd.ExecuteNonQueryAsync();
                    }

                    await LogActionAsync(conn, "DELETE", $"Deleted {deletedCount} walk-in customers: {string.Join(", ", customerNames)}", true);

                    transaction.Commit();

                    _toastManager.CreateToast("Customers Deleted")
                        .WithContent($"Successfully deleted {deletedCount} walk-in customer(s).")
                        .DismissOnClick()
                        .ShowSuccess();

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
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to delete customers: {ex.Message}")
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

        #region BUSINESS LOGIC

        public async Task<(bool Success, bool HasUsedFreeTrial, string Message)> CheckFreeTrialEligibilityAsync(string firstName, string lastName, string? contactNumber)
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
                      WHERE FirstName = @firstName 
                      AND LastName = @lastName 
                      AND ContactNumber = @contactNumber
                      AND WalkinType = 'Free Trial'", conn);

                cmd.Parameters.AddWithValue("@firstName", firstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@lastName", lastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@contactNumber", contactNumber ?? (object)DBNull.Value);

                var count = (int)await cmd.ExecuteScalarAsync();

                if (count > 0)
                {
                    return (true, true, "Customer has already used their free trial.");
                }

                return (true, false, "Customer is eligible for free trial.");
            }
            catch (Exception ex)
            {
                return (false, false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, List<ManageWalkInModel>? History, string Message)> GetCustomerHistoryAsync(string firstName, string lastName, string? contactNumber)
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
                    @"SELECT CustomerID, FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, WalkinType, WalkinPackage, PaymentMethod, RegisteredByEmployeeID 
                      FROM WalkInCustomers 
                      WHERE FirstName = @firstName 
                      AND LastName = @lastName 
                      AND ContactNumber = @contactNumber
                      ORDER BY CustomerID DESC", conn);

                cmd.Parameters.AddWithValue("@firstName", firstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@lastName", lastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@contactNumber", contactNumber ?? (object)DBNull.Value);

                var history = new List<ManageWalkInModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var fName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    var middleInitial = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    var lName = reader.IsDBNull(3) ? "" : reader.GetString(3);

                    history.Add(new ManageWalkInModel
                    {
                        WalkInID = reader.GetInt32(0),
                        FirstName = fName,
                        MiddleInitial = middleInitial,
                        LastName = lName,
                        Name = $"{fName} {middleInitial} {lName}".Trim(),
                        ContactNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Age = reader.GetInt32(5),
                        Gender = reader.IsDBNull(6) ? null : reader.GetString(6),
                        WalkInType = reader.IsDBNull(7) ? null : reader.GetString(7),
                        WalkInPackage = reader.IsDBNull(8) ? null : reader.GetString(8),
                        PaymentMethod = reader.IsDBNull(9) ? null : reader.GetString(9),
                        RegisteredByEmployeeID = reader.IsDBNull(10) ? null : reader.GetInt32(10)
                    });
                }

                return (true, history, $"Found {history.Count} visit(s) for this customer.");
            }
            catch (Exception ex)
            {
                return (false, null, $"Error: {ex.Message}");
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
                DashboardEventService.Instance.NotifyChartDataUpdated();
                DashboardEventService.Instance.NotifyPopulationDataChanged();
                DashboardEventService.Instance.NotifySalesUpdated();
                DashboardEventService.Instance.NotifyRecentLogsUpdated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        private async Task LogActionAsync(SqlConnection conn, SqlTransaction transaction, string actionType, string description, bool success)
        {
            try
            {
                using var logCmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
              VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)", conn, transaction);

                logCmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@success", success);
                logCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                await logCmd.ExecuteNonQueryAsync();
                DashboardEventService.Instance.NotifyRecentLogsUpdated();
                DashboardEventService.Instance.NotifyPopulationDataChanged();
                DashboardEventService.Instance.NotifySalesUpdated();
                DashboardEventService.Instance.NotifyChartDataUpdated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

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
                    return (true,
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetInt32(2));
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
    }
}
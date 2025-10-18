using AHON_TRACK.Models;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using AHON_TRACK.Services.Interface;
using System.Data;
using AHON_TRACK.Services.Events;

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
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true;
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
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true;
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

                // Start a transaction to ensure both operations succeed or fail together
                using var transaction = conn.BeginTransaction();

                try
                {
                    // Check if customer already had a free trial (if current registration is Free Trial)
                    if (walkIn.WalkInType?.Equals("Free Trial", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        using var checkFreeTrialCmd = new SqlCommand(
                            @"SELECT COUNT(*) FROM WalkInCustomers 
                      WHERE FirstName = @firstName 
                      AND LastName = @lastName 
                      AND ContactNumber = @contactNumber
                      AND WalkinType = 'Free Trial'", conn, transaction);

                        checkFreeTrialCmd.Parameters.AddWithValue("@firstName", walkIn.FirstName ?? (object)DBNull.Value);
                        checkFreeTrialCmd.Parameters.AddWithValue("@lastName", walkIn.LastName ?? (object)DBNull.Value);
                        checkFreeTrialCmd.Parameters.AddWithValue("@contactNumber", walkIn.ContactNumber ?? (object)DBNull.Value);

                        var freeTrialCount = (int)await checkFreeTrialCmd.ExecuteScalarAsync();
                        if (freeTrialCount > 0)
                        {
                            _toastManager.CreateToast("Free Trial Already Used")
                                .WithContent($"{walkIn.FirstName} {walkIn.LastName} has already availed a free trial. Please register as 'Regular' customer.")
                                .DismissOnClick()
                                .ShowWarning();
                            return (false, "This customer has already used their free trial.", null);
                        }
                    }

                    // Insert new walk-in customer
                    using var cmd = new SqlCommand(
                        @"INSERT INTO WalkInCustomers (FirstName, MiddleInitial, LastName, ContactNumber, Age, Gender, WalkinType, WalkinPackage, PaymentMethod, Quantity, RegisteredByEmployeeID) 
                  OUTPUT INSERTED.CustomerID
                  VALUES (@firstName, @middleInitial, @lastName, @contactNumber, @age, @gender, @walkInType, @walkInPackage, @paymentMethod, @quantity, @employeeID)", conn, transaction);

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

                    // Get current datetime for check-in
                    var currentDateTime = DateTime.Now;

                    // Automatically create check-in record in WalkInRecords
                    using var checkInCmd = new SqlCommand(
                        @"INSERT INTO WalkInRecords (CustomerID, CheckIn, CheckOut, RegisteredByEmployeeID)
                  VALUES (@customerID, @checkIn, NULL, @employeeID)", conn, transaction);

                    checkInCmd.Parameters.AddWithValue("@customerID", customerId);
                    checkInCmd.Parameters.Add("@checkIn", SqlDbType.DateTime).Value = currentDateTime;
                    checkInCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                    await checkInCmd.ExecuteNonQueryAsync();

                    // Log the action
                    await LogActionAsync(conn, transaction, "CREATE",
                        $"Registered walk-in customer: {walkIn.FirstName} {walkIn.LastName} ({walkIn.WalkInType}) and checked in", true);

                    // Commit the transaction - both customer and check-in record are saved
                    transaction.Commit();

                    _toastManager.CreateToast("Customer Registered & Checked In")
                        .WithContent($"Successfully registered and checked in {walkIn.FirstName} {walkIn.LastName}.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Walk-in customer registered and checked in successfully.", customerId);
                }
                catch
                {
                    // Rollback if anything fails
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
                      WHERE CustomerID = @customerId", conn);

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
                      WHERE WalkinType = @walkInType
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
                      WHERE WalkinPackage = @package
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

                // Check if customer exists
                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM WalkInCustomers WHERE CustomerID = @customerId", conn);
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
                    "SELECT FirstName, LastName FROM WalkInCustomers WHERE CustomerID = @customerId", conn);
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
                    "DELETE FROM WalkInCustomers WHERE CustomerID = @customerId", conn);
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
                            "SELECT FirstName, LastName FROM WalkInCustomers WHERE CustomerID = @customerId", conn, transaction);
                        getNameCmd.Parameters.AddWithValue("@customerId", customerId);

                        using var reader = await getNameCmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            customerNames.Add($"{reader.GetString(0)} {reader.GetString(1)}");
                        }
                        reader.Close();

                        // Delete customer
                        using var deleteCmd = new SqlCommand(
                            "DELETE FROM WalkInCustomers WHERE CustomerID = @customerId", conn, transaction);
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
                DashboardEventService.Instance.NotifyWalkInAdded();
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
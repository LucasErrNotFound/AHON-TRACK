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
    public class SupplierService : ISupplierService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public SupplierService(string connectionString, ToastManager toastManager)
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
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;
        }

        #endregion

        #region CREATE (with restore logic)

        public async Task<(bool Success, string Message, int? SupplierId)> AddSupplierAsync(SupplierManagementModel supplier)
        {
            if (!CanCreate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You don't have permission to add suppliers.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to add suppliers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check for existing active supplier
                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Suppliers WHERE SupplierName = @supplierName AND IsDeleted = 0", conn);
                checkCmd.Parameters.AddWithValue("@supplierName", supplier.SupplierName ?? (object)DBNull.Value);

                var existingCount = (int)await checkCmd.ExecuteScalarAsync();
                if (existingCount > 0)
                {
                    _toastManager.CreateToast("Duplicate Supplier")
                        .WithContent($"Supplier '{supplier.SupplierName}' already exists.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Supplier already exists.", null);
                }

                // Check if soft deleted supplier exists (restore)
                using var restoreCheckCmd = new SqlCommand(
                    "SELECT SupplierID FROM Suppliers WHERE SupplierName = @supplierName AND IsDeleted = 1", conn);
                restoreCheckCmd.Parameters.AddWithValue("@supplierName", supplier.SupplierName ?? (object)DBNull.Value);

                var restoreResult = await restoreCheckCmd.ExecuteScalarAsync();
                if (restoreResult != null)
                {
                    int supplierId = Convert.ToInt32(restoreResult);

                    using var restoreCmd = new SqlCommand(
                        @"UPDATE Suppliers 
                          SET IsDeleted = 0,
                              Status = 'Active',
                              ContactPerson = @contactPerson,
                              Email = @email,
                              PhoneNumber = @phoneNumber,
                              Products = @products,
                              UpdatedAt = GETDATE()
                          WHERE SupplierID = @supplierId", conn);

                    restoreCmd.Parameters.AddWithValue("@supplierId", supplierId);
                    restoreCmd.Parameters.AddWithValue("@contactPerson", supplier.ContactPerson ?? (object)DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@email", supplier.Email ?? (object)DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@phoneNumber", supplier.PhoneNumber ?? (object)DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@products", supplier.Products ?? (object)DBNull.Value);

                    await restoreCmd.ExecuteNonQueryAsync();

                    await LogActionAsync(conn, "Restored supplier.", $"Restored supplier: {supplier.SupplierName}", true);

                    _toastManager.CreateToast("Supplier Restored")
                        .WithContent($"Supplier '{supplier.SupplierName}' was restored successfully.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Supplier restored successfully.", supplierId);
                }

                // Insert new supplier
                using var cmd = new SqlCommand(
                    @"INSERT INTO Suppliers (SupplierName, ContactPerson, Email, PhoneNumber, Products, Status, AddedByEmployeeID, IsDeleted) 
                      OUTPUT INSERTED.SupplierID
                      VALUES (@supplierName, @contactPerson, @email, @phoneNumber, @products, @status, @employeeID, 0)", conn);

                cmd.Parameters.AddWithValue("@supplierName", supplier.SupplierName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@contactPerson", supplier.ContactPerson ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@email", supplier.Email ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@phoneNumber", supplier.PhoneNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@products", supplier.Products ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", supplier.Status ?? "Active");
                cmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                var supplierIdInserted = (int)await cmd.ExecuteScalarAsync();

                await LogActionAsync(conn, "Added new supplier.", $"Added new supplier: {supplier.SupplierName}", true);

                _toastManager.CreateToast("Supplier Added")
                    .WithContent($"Successfully added supplier '{supplier.SupplierName}'.")
                    .DismissOnClick()
                    .ShowSuccess();

                return (true, "Supplier added successfully.", supplierIdInserted);
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to add supplier: {ex.Message}")
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

        public async Task<(bool Success, string Message, List<SupplierManagementModel>? Suppliers)> GetAllSuppliersAsync()
        {
            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You don't have permission to view suppliers.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to view suppliers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT SupplierID, SupplierName, ContactPerson, Email, PhoneNumber, Products, Status 
                      FROM Suppliers 
                      WHERE IsDeleted = 0
                      ORDER BY SupplierName", conn);

                var suppliers = new List<SupplierManagementModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    suppliers.Add(new SupplierManagementModel
                    {
                        SupplierID = reader.GetInt32(0),
                        SupplierName = reader.GetString(1),
                        ContactPerson = reader.GetString(2),
                        Email = reader.GetString(3),
                        PhoneNumber = reader.GetString(4),
                        Products = reader.GetString(5),
                        Status = reader.GetString(6),
                        IsSelected = false
                    });
                }

                return (true, "Suppliers retrieved successfully.", suppliers);
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to retrieve suppliers: {ex.Message}")
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

        public async Task<(bool Success, string Message, SupplierManagementModel? Supplier)> GetSupplierByIdAsync(int supplierId)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view suppliers.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT SupplierID, SupplierName, ContactPerson, Email, PhoneNumber, Products, Status 
                      FROM Suppliers 
                      WHERE SupplierID = @supplierId AND IsDeleted = 0", conn);

                cmd.Parameters.AddWithValue("@supplierId", supplierId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var supplier = new SupplierManagementModel
                    {
                        SupplierID = reader.GetInt32(0),
                        SupplierName = reader.GetString(1),
                        ContactPerson = reader.GetString(2),
                        Email = reader.GetString(3),
                        PhoneNumber = reader.GetString(4),
                        Products = reader.GetString(5),
                        Status = reader.GetString(6),
                        IsSelected = false
                    };

                    return (true, "Supplier retrieved successfully.", supplier);
                }

                return (false, "Supplier not found.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<SupplierManagementModel>? Suppliers)> GetSuppliersByProductTypeAsync(string productType)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view suppliers.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT SupplierID, SupplierName, ContactPerson, Email, PhoneNumber, Products, Status 
                      FROM Suppliers 
                      WHERE Products = @productType AND IsDeleted = 0
                      ORDER BY SupplierName", conn);

                cmd.Parameters.AddWithValue("@productType", productType ?? (object)DBNull.Value);

                var suppliers = new List<SupplierManagementModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    suppliers.Add(new SupplierManagementModel
                    {
                        SupplierID = reader.GetInt32(0),
                        SupplierName = reader.GetString(1),
                        ContactPerson = reader.GetString(2),
                        Email = reader.GetString(3),
                        PhoneNumber = reader.GetString(4),
                        Products = reader.GetString(5),
                        Status = reader.GetString(6),
                        IsSelected = false
                    });
                }

                return (true, "Suppliers retrieved successfully.", suppliers);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region UPDATE

        public async Task<(bool Success, string Message)> UpdateSupplierAsync(SupplierManagementModel supplier)
        {
            if (!CanUpdate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can update supplier information.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to update suppliers.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check if supplier exists
                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Suppliers WHERE SupplierID = @supplierId", conn);
                checkCmd.Parameters.AddWithValue("@supplierId", supplier.SupplierID);

                var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;
                if (!exists)
                {
                    _toastManager.CreateToast("Supplier Not Found")
                        .WithContent("The supplier you're trying to update doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Supplier not found.");
                }

                // Check for duplicate name (excluding current supplier)
                using var dupCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Suppliers WHERE SupplierName = @supplierName AND SupplierID != @supplierId", conn);
                dupCmd.Parameters.AddWithValue("@supplierName", supplier.SupplierName ?? (object)DBNull.Value);
                dupCmd.Parameters.AddWithValue("@supplierId", supplier.SupplierID);

                var duplicateCount = (int)await dupCmd.ExecuteScalarAsync();
                if (duplicateCount > 0)
                {
                    _toastManager.CreateToast("Duplicate Supplier")
                        .WithContent($"Another supplier with name '{supplier.SupplierName}' already exists.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Supplier name already exists.");
                }

                // Update supplier
                using var cmd = new SqlCommand(
                    @"UPDATE Suppliers 
                      SET SupplierName = @supplierName,
                          ContactPerson = @contactPerson,
                          Email = @email,
                          PhoneNumber = @phoneNumber,
                          Products = @products,
                          Status = @status,
                          UpdatedAt = GETDATE()
                      WHERE SupplierID = @supplierId", conn);

                cmd.Parameters.AddWithValue("@supplierId", supplier.SupplierID);
                cmd.Parameters.AddWithValue("@supplierName", supplier.SupplierName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@contactPerson", supplier.ContactPerson ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@email", supplier.Email ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@phoneNumber", supplier.PhoneNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@products", supplier.Products ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", supplier.Status ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                await LogActionAsync(conn, "Updated supplier data.", $"Updated supplier: {supplier.SupplierName}", true);

                _toastManager.CreateToast("Supplier Updated")
                    .WithContent($"Successfully updated supplier '{supplier.SupplierName}'.")
                    .DismissOnClick()
                    .ShowSuccess();

                return (true, "Supplier updated successfully.");
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to update supplier: {ex.Message}")
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

        public async Task<(bool Success, string Message)> UpdateSupplierStatusAsync(int supplierId, string newStatus)
        {
            if (!CanUpdate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can update supplier status.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to update supplier status.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"UPDATE Suppliers 
                      SET Status = @status, UpdatedAt = GETDATE() 
                      WHERE SupplierID = @supplierId", conn);

                cmd.Parameters.AddWithValue("@supplierId", supplierId);
                cmd.Parameters.AddWithValue("@status", newStatus ?? (object)DBNull.Value);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "Updated supplier status.", $"Updated supplier status to: {newStatus}", true);

                    _toastManager.CreateToast("Status Updated")
                        .WithContent($"Supplier status updated to '{newStatus}'.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Status updated successfully.");
                }

                return (false, "Supplier not found.");
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"Failed to update status: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region DELETE (Soft Delete)

        public async Task<(bool Success, string Message)> DeleteSupplierAsync(int supplierId)
        {
            if (!CanDelete())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete suppliers.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to delete suppliers.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"UPDATE Suppliers 
                      SET IsDeleted = 1, UpdatedAt = GETDATE() 
                      WHERE SupplierID = @supplierId AND IsDeleted = 0", conn);

                cmd.Parameters.AddWithValue("@supplierId", supplierId);

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                {
                    await LogActionAsync(conn, "Soft deleted supplier.", $"Soft deleted supplier ID: {supplierId}", true);
                    _toastManager.CreateToast("Supplier Deleted")
                        .WithContent("Supplier has been moved to archive.")
                        .DismissOnClick()
                        .ShowSuccess();
                    return (true, "Supplier deleted (soft delete).");
                }

                return (false, "Supplier not found or already deleted.");
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"Failed to delete supplier: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, int DeletedCount)> DeleteMultipleSuppliersAsync(List<int> supplierIds)
        {
            if (!CanDelete())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete suppliers.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to delete suppliers.", 0);
            }

            if (supplierIds == null || supplierIds.Count == 0)
            {
                return (false, "No suppliers selected for deletion.", 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var deletedCount = 0;
                var supplierNames = new List<string>();

                using var transaction = conn.BeginTransaction();
                try
                {
                    foreach (var supplierId in supplierIds)
                    {
                        // Get supplier name
                        using var getNameCmd = new SqlCommand(
                            "SELECT SupplierName FROM Suppliers WHERE SupplierID = @supplierId", conn, transaction);
                        getNameCmd.Parameters.AddWithValue("@supplierId", supplierId);
                        var name = await getNameCmd.ExecuteScalarAsync() as string;

                        if (!string.IsNullOrEmpty(name))
                        {
                            supplierNames.Add(name);
                        }

                        // 🔹 Soft delete supplier instead of removing it
                        using var deleteCmd = new SqlCommand(
                            @"UPDATE Suppliers 
                      SET IsDeleted = 1, UpdatedAt = GETDATE() 
                      WHERE SupplierID = @supplierId AND IsDeleted = 0", conn, transaction);
                        deleteCmd.Parameters.AddWithValue("@supplierId", supplierId);
                        deletedCount += await deleteCmd.ExecuteNonQueryAsync();
                    }

                    await LogActionAsync(conn, "Soft deleted multiple suppliers.", $"Soft deleted {deletedCount} supplier(s): {string.Join(", ", supplierNames)}", true);

                    transaction.Commit();

                    _toastManager.CreateToast("Suppliers Deleted")
                        .WithContent($"Successfully deleted (soft) {deletedCount} supplier(s).")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, $"Successfully deleted (soft) {deletedCount} supplier(s).", deletedCount);
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
                    .WithContent($"Failed to delete suppliers: {ex.Message}")
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        public async Task<(bool Success, int ActiveCount, int InactiveCount, int SuspendedCount)> GetSupplierStatisticsAsync()
        {
            if (!CanView())
                return (false, 0, 0, 0);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT 
                        SUM(CASE WHEN Status = 'Active' THEN 1 ELSE 0 END) as ActiveCount,
                        SUM(CASE WHEN Status = 'Inactive' THEN 1 ELSE 0 END) as InactiveCount,
                        SUM(CASE WHEN Status = 'Suspended' THEN 1 ELSE 0 END) as SuspendedCount
                      FROM Suppliers
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
                Console.WriteLine($"[GetSupplierStatisticsAsync] {ex.Message}");
                return (false, 0, 0, 0);
            }
        }

        #endregion
    }
}
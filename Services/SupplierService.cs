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

        private bool CanCreate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanUpdate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanDelete() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanView() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;

        #endregion

        #region CREATE (with restore logic)

        public async Task<(bool Success, string Message, int? SupplierId)> AddSupplierAsync(SupplierManagementModel supplier)
        {
            if (!CanCreate())
            {
                ShowToast("Access Denied", "You don't have permission to add suppliers.", ToastType.Error);
                return (false, "Insufficient permissions to add suppliers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check if soft deleted supplier exists (restore)
                var deletedSupplierId = await GetDeletedSupplierIdAsync(conn, supplier.SupplierName);
                if (deletedSupplierId.HasValue)
                {
                    await RestoreSupplierAsync(conn, deletedSupplierId.Value, supplier);
                    await LogActionAsync(conn, "Restored supplier.", $"Restored supplier: {supplier.SupplierName}", true);
                    ShowToast("Supplier Restored", $"Supplier '{supplier.SupplierName}' was restored successfully.", ToastType.Success);
                    return (true, "Supplier restored successfully.", deletedSupplierId.Value);
                }

                // Insert new supplier
                var supplierId = await InsertNewSupplierAsync(conn, supplier);
                await LogActionAsync(conn, "Added new supplier.", $"Added new supplier: {supplier.SupplierName}", true);
                return (true, "Supplier added successfully.", supplierId);
            }
            catch (SqlException ex)
            {
                ShowToast("Database Error", $"Failed to add supplier: {ex.Message}", ToastType.Error);
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"An unexpected error occurred: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}", null);
            }
        }

        private async Task<int?> GetDeletedSupplierIdAsync(SqlConnection conn, string supplierName)
        {
            using var cmd = new SqlCommand(
                "SELECT SupplierID FROM Suppliers WHERE SupplierName = @supplierName AND IsDeleted = 1", conn);
            cmd.Parameters.AddWithValue("@supplierName", supplierName ?? (object)DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : null;
        }

        private async Task RestoreSupplierAsync(SqlConnection conn, int supplierId, SupplierManagementModel supplier)
        {
            using var cmd = new SqlCommand(
                @"UPDATE Suppliers 
          SET IsDeleted = 0,
              Status = 'Active',
              ContactPerson = @contactPerson,
              Email = @email,
              PhoneNumber = @phoneNumber,
              Address = @address,
              Products = @products,
              DeliverySchedule = @deliverySchedule,
              DeliveryPattern = @deliveryPattern,
              ContractTerms = @contractTerms,
              UpdatedAt = GETDATE()
          WHERE SupplierID = @supplierId", conn);

            cmd.Parameters.AddWithValue("@supplierId", supplierId);
            AddSupplierParameters(cmd, supplier);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<int> InsertNewSupplierAsync(SqlConnection conn, SupplierManagementModel supplier)
        {
            using var cmd = new SqlCommand(
                @"INSERT INTO Suppliers (SupplierName, ContactPerson, Email, PhoneNumber, Address, 
                                  Products, Status, DeliverySchedule, DeliveryPattern, 
                                  ContractTerms, AddedByEmployeeID, IsDeleted) 
          OUTPUT INSERTED.SupplierID
          VALUES (@supplierName, @contactPerson, @email, @phoneNumber, @address,
                  @products, @status, @deliverySchedule, @deliveryPattern, 
                  @contractTerms, @employeeID, 0)", conn);

            cmd.Parameters.AddWithValue("@supplierName", supplier.SupplierName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", supplier.Status ?? "Active");
            cmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

            // This call adds all the other parameters including @address
            AddSupplierParameters(cmd, supplier);

            return (int)await cmd.ExecuteScalarAsync();
        }

        private void AddSupplierParameters(SqlCommand cmd, SupplierManagementModel supplier)
        {
            cmd.Parameters.AddWithValue("@contactPerson", supplier.ContactPerson ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@email", supplier.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@phoneNumber", supplier.PhoneNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@address", supplier.Address ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@products", supplier.Products ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@deliverySchedule", supplier.DeliverySchedule ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@deliveryPattern", supplier.DeliveryPattern ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@contractTerms", supplier.ContractTerms.HasValue
                ? (object)supplier.ContractTerms.Value
                : DBNull.Value);
        }

        #endregion

        #region READ

        public async Task<(bool Success, string Message, List<SupplierManagementModel>? Suppliers)> GetAllSuppliersAsync()
        {
            if (!CanView())
            {
                ShowToast("Access Denied", "You don't have permission to view suppliers.", ToastType.Error);
                return (false, "Insufficient permissions to view suppliers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT SupplierID, SupplierName, ContactPerson, Email, PhoneNumber, Address, 
                     Products, Status, DeliverySchedule, DeliveryPattern, ContractTerms
              FROM Suppliers 
              WHERE IsDeleted = 0
              ORDER BY SupplierName", conn);

                var suppliers = new List<SupplierManagementModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    suppliers.Add(MapSupplierFromReader(reader));
                }

                return (true, "Suppliers retrieved successfully.", suppliers);
            }
            catch (SqlException ex)
            {
                ShowToast("Database Error", $"Failed to retrieve suppliers: {ex.Message}", ToastType.Error);
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"An unexpected error occurred: {ex.Message}", ToastType.Error);
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
                    @"SELECT SupplierID, SupplierName, ContactPerson, Email, PhoneNumber, Address,
                     Products, Status, DeliverySchedule, DeliveryPattern, ContractTerms
              FROM Suppliers 
              WHERE SupplierID = @supplierId AND IsDeleted = 0", conn);

                cmd.Parameters.AddWithValue("@supplierId", supplierId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (true, "Supplier retrieved successfully.", MapSupplierFromReader(reader));
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
                    @"SELECT SupplierID, SupplierName, ContactPerson, Email, PhoneNumber, Address,
                     Products, Status, DeliverySchedule, DeliveryPattern, ContractTerms
              FROM Suppliers 
              WHERE Products = @productType AND IsDeleted = 0
              ORDER BY SupplierName", conn);

                cmd.Parameters.AddWithValue("@productType", productType ?? (object)DBNull.Value);

                var suppliers = new List<SupplierManagementModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    suppliers.Add(MapSupplierFromReader(reader));
                }

                return (true, "Suppliers retrieved successfully.", suppliers);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        private SupplierManagementModel MapSupplierFromReader(SqlDataReader reader)
        {
            return new SupplierManagementModel
            {
                SupplierID = reader.GetInt32(0),           // Column 0: SupplierID
                SupplierName = reader.GetString(1),        // Column 1: SupplierName
                ContactPerson = reader.GetString(2),       // Column 2: ContactPerson
                Email = reader.GetString(3),               // Column 3: Email
                PhoneNumber = reader.GetString(4),         // Column 4: PhoneNumber
                Address = reader.IsDBNull(5) ? null : reader.GetString(5),      // Column 5: Address
                Products = reader.IsDBNull(6) ? null : reader.GetString(6),     // Column 6: Products
                Status = reader.GetString(7),              // Column 7: Status
                DeliverySchedule = reader.IsDBNull(8) ? null : reader.GetString(8),  // Column 8: DeliverySchedule
                DeliveryPattern = reader.IsDBNull(9) ? null : reader.GetString(9),   // Column 9: DeliveryPattern
                ContractTerms = reader.IsDBNull(10) ? null : reader.GetDateTime(10), // Column 10: ContractTerms
                IsSelected = false
            };
        }

        #endregion

        #region UPDATE

        public async Task<(bool Success, string Message)> UpdateSupplierAsync(SupplierManagementModel supplier)
        {
            if (!CanUpdate())
            {
                ShowToast("Access Denied", "Only administrators can update supplier information.", ToastType.Error);
                return (false, "Insufficient permissions to update suppliers.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check if supplier exists
                if (!await SupplierExistsByIdAsync(conn, supplier.SupplierID))
                {
                    ShowToast("Supplier Not Found", "The supplier you're trying to update doesn't exist.", ToastType.Warning);
                    return (false, "Supplier not found.");
                }

                // Update supplier
                await ExecuteUpdateSupplierAsync(conn, supplier);
                await LogActionAsync(conn, "Updated supplier data.", $"Updated supplier: {supplier.SupplierName}", true);

                return (true, "Supplier updated successfully.");
            }
            catch (SqlException ex)
            {
                ShowToast("Database Error", $"Failed to update supplier: {ex.Message}", ToastType.Error);
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"An unexpected error occurred: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}");
            }
        }

        private async Task<bool> SupplierExistsByIdAsync(SqlConnection conn, int supplierId)
        {
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM Suppliers WHERE SupplierID = @supplierId", conn);
            cmd.Parameters.AddWithValue("@supplierId", supplierId);

            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private async Task ExecuteUpdateSupplierAsync(SqlConnection conn, SupplierManagementModel supplier)
        {
            using var cmd = new SqlCommand(
                @"UPDATE Suppliers 
          SET SupplierName = @supplierName,
              ContactPerson = @contactPerson,
              Email = @email,
              PhoneNumber = @phoneNumber,
              Address = @address,
              Products = @products,
              Status = @status,
              DeliverySchedule = @deliverySchedule,
              DeliveryPattern = @deliveryPattern,
              ContractTerms = @contractTerms,
              UpdatedAt = GETDATE()
          WHERE SupplierID = @supplierId", conn);

            cmd.Parameters.AddWithValue("@supplierId", supplier.SupplierID);
            cmd.Parameters.AddWithValue("@supplierName", supplier.SupplierName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", supplier.Status ?? (object)DBNull.Value);
            AddSupplierParameters(cmd, supplier);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<(bool Success, string Message)> UpdateSupplierStatusAsync(int supplierId, string newStatus)
        {
            if (!CanUpdate())
            {
                ShowToast("Access Denied", "Only administrators can update supplier status.", ToastType.Error);
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
                    return (true, "Status updated successfully.");
                }

                return (false, "Supplier not found.");
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"Failed to update status: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region DELETE (Soft Delete)

        public async Task<(bool Success, string Message)> DeleteSupplierAsync(int supplierId)
        {
            if (!CanDelete())
            {
                ShowToast("Access Denied", "Only administrators can delete suppliers.", ToastType.Error);
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

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "Soft deleted supplier.", $"Soft deleted supplier ID: {supplierId}", true);
                    return (true, "Supplier deleted (soft delete).");
                }

                return (false, "Supplier not found or already deleted.");
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"Failed to delete supplier: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, int DeletedCount)> DeleteMultipleSuppliersAsync(List<int> supplierIds)
        {
            if (!CanDelete())
            {
                ShowToast("Access Denied", "Only administrators can delete suppliers.", ToastType.Error);
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

                using var transaction = conn.BeginTransaction();
                try
                {
                    var (deletedCount, supplierNames) = await ExecuteMultipleDeletesAsync(conn, transaction, supplierIds);

                    await LogActionAsync(conn, "Soft deleted multiple suppliers.",
                        $"Soft deleted {deletedCount} supplier(s): {string.Join(", ", supplierNames)}", true);

                    transaction.Commit();

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
                ShowToast("Database Error", $"Failed to delete suppliers: {ex.Message}", ToastType.Error);
                return (false, $"Database error: {ex.Message}", 0);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"An unexpected error occurred: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}", 0);
            }
        }

        private async Task<(int DeletedCount, List<string> SupplierNames)> ExecuteMultipleDeletesAsync(
            SqlConnection conn, SqlTransaction transaction, List<int> supplierIds)
        {
            var deletedCount = 0;
            var supplierNames = new List<string>();

            foreach (var supplierId in supplierIds)
            {
                // Get supplier name
                var name = await GetSupplierNameAsync(conn, transaction, supplierId);
                if (!string.IsNullOrEmpty(name))
                {
                    supplierNames.Add(name);
                }

                // Soft delete supplier
                using var deleteCmd = new SqlCommand(
                    @"UPDATE Suppliers 
                      SET IsDeleted = 1, UpdatedAt = GETDATE() 
                      WHERE SupplierID = @supplierId AND IsDeleted = 0", conn, transaction);
                deleteCmd.Parameters.AddWithValue("@supplierId", supplierId);
                deletedCount += await deleteCmd.ExecuteNonQueryAsync();
            }

            return (deletedCount, supplierNames);
        }

        private async Task<string?> GetSupplierNameAsync(SqlConnection conn, SqlTransaction transaction, int supplierId)
        {
            using var cmd = new SqlCommand(
                "SELECT SupplierName FROM Suppliers WHERE SupplierID = @supplierId", conn, transaction);
            cmd.Parameters.AddWithValue("@supplierId", supplierId);
            return await cmd.ExecuteScalarAsync() as string;
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

        private void ShowToast(string title, string content, ToastType type)
        {
            var toast = _toastManager.CreateToast(title)
                .WithContent(content)
                .DismissOnClick();

            switch (type)
            {
                case ToastType.Success:
                    toast.ShowSuccess();
                    break;
                case ToastType.Warning:
                    toast.ShowWarning();
                    break;
                case ToastType.Error:
                    toast.ShowError();
                    break;
            }
        }

        private enum ToastType
        {
            Success,
            Warning,
            Error
        }

        #endregion
        
        public async Task<(bool Success, string Message, SupplierManagementModel Supplier)> GetSupplierByNameAsync(string supplierName)
        {
            if (!CanView())
            {
                ShowToast("Access Denied", "You don't have permission to view suppliers.", ToastType.Error);
                return (false, "Insufficient permissions to view suppliers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
            SELECT 
                SupplierID,
                SupplierName,
                ContactPerson,
                Email,
                PhoneNumber,
                Address,
                Products,
                Status,
                DeliverySchedule,
                ContractTerms,
                CreatedAt,
                UpdatedAt
            FROM Suppliers
            WHERE SupplierName = @supplierName
            AND IsDeleted = 0";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@supplierName", supplierName);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var supplier = new SupplierManagementModel
                    {
                        SupplierID = reader.GetInt32(reader.GetOrdinal("SupplierID")),
                        SupplierName = reader.GetString(reader.GetOrdinal("SupplierName")),
                        ContactPerson = reader.GetString(reader.GetOrdinal("ContactPerson")),
                        Email = reader.GetString(reader.GetOrdinal("Email")),
                        PhoneNumber = reader.GetString(reader.GetOrdinal("PhoneNumber")),
                        Address = reader.IsDBNull(reader.GetOrdinal("Address")) 
                            ? null 
                            : reader.GetString(reader.GetOrdinal("Address")),
                        Products = reader.GetString(reader.GetOrdinal("Products")),
                        Status = reader.GetString(reader.GetOrdinal("Status")),
                        DeliverySchedule = reader.IsDBNull(reader.GetOrdinal("DeliverySchedule"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("DeliverySchedule")),
                        ContractTerms = reader.IsDBNull(reader.GetOrdinal("ContractTerms"))
                            ? null
                            : reader.GetDateTime(reader.GetOrdinal("ContractTerms"))
                    };

                    return (true, "Supplier found.", supplier);
                }

                return (false, "Supplier not found.", null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetSupplierByNameAsync] {ex.Message}");
                return (false, $"Error: {ex.Message}", null);
            }
        }
    }
}
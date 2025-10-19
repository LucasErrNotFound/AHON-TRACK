using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
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
    public class InventoryService : IInventoryService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public InventoryService(string connectionString, ToastManager toastManager)
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

        #region Logging

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

        #endregion

        #region Supplier Helper Methods

        public async Task<(bool Success, string Message, List<SupplierDropdownModel>? Suppliers)> GetSuppliersForDropdownAsync()
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view suppliers.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT SupplierID, SupplierName 
                      FROM Suppliers 
                      WHERE Status = 'Active'
                      ORDER BY SupplierName", conn);

                var suppliers = new List<SupplierDropdownModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    suppliers.Add(new SupplierDropdownModel
                    {
                        SupplierID = reader.GetInt32(0),
                        SupplierName = reader.GetString(1)
                    });
                }

                return (true, "Suppliers retrieved successfully.", suppliers);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load suppliers: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}", null);
            }
        }

        private async Task<string> GetSupplierNameByIdAsync(SqlConnection connection, int? supplierId)
        {
            if (!supplierId.HasValue)
                return "N/A";

            try
            {
                const string query = "SELECT SupplierName FROM Suppliers WHERE SupplierID = @supplierId";
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@supplierId", supplierId.Value);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? "Unknown Supplier";
            }
            catch
            {
                return "Unknown Supplier";
            }
        }

        #endregion

        #region CREATE

        public async Task<(bool Success, string Message, int? EquipmentId)> AddEquipmentAsync(EquipmentModel equipment)
        {
            if (!CanCreate())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to add equipment.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to add equipment.", null);
            }

            const string query = @"
                INSERT INTO Equipment (EquipmentName, Category, CurrentStock, 
                                      PurchaseDate, PurchasePrice, SupplierID, WarrantyExpiry, 
                                      Condition, Status, LastMaintenance, NextMaintenance, AddedByEmployeeID)
                OUTPUT INSERTED.EquipmentID
                VALUES (@equipmentName, @category, @currentStock, 
                        @purchaseDate, @purchasePrice, @supplierId, @warrantyExpiry, 
                        @condition, @status, @lastMaintenance, @nextMaintenance, @employeeID)";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Validate supplier exists if provided
                if (equipment.SupplierID.HasValue)
                {
                    using var checkCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM Suppliers WHERE SupplierID = @supplierId", connection);
                    checkCmd.Parameters.AddWithValue("@supplierId", equipment.SupplierID.Value);
                    var supplierExists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                    if (!supplierExists)
                    {
                        _toastManager?.CreateToast("Invalid Supplier")
                            .WithContent("The selected supplier does not exist.")
                            .DismissOnClick()
                            .ShowWarning();
                        return (false, "Invalid supplier selected.", null);
                    }
                }

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@equipmentName", equipment.EquipmentName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@category", equipment.Category ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@currentStock", equipment.CurrentStock);
                command.Parameters.AddWithValue("@purchaseDate", (object)equipment.PurchaseDate ?? DBNull.Value);
                command.Parameters.AddWithValue("@purchasePrice", (object)equipment.PurchasePrice ?? DBNull.Value);
                command.Parameters.AddWithValue("@supplierId", (object)equipment.SupplierID ?? DBNull.Value);
                command.Parameters.AddWithValue("@warrantyExpiry", (object)equipment.WarrantyExpiry ?? DBNull.Value);
                command.Parameters.AddWithValue("@condition", (object)equipment.Condition ?? DBNull.Value);
                command.Parameters.AddWithValue("@status", (object)equipment.Status ?? DBNull.Value);
                command.Parameters.AddWithValue("@lastMaintenance", (object)equipment.LastMaintenance ?? DBNull.Value);
                command.Parameters.AddWithValue("@nextMaintenance", (object)equipment.NextMaintenance ?? DBNull.Value);
                command.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                var newId = await command.ExecuteScalarAsync();

                if (newId != null && newId != DBNull.Value)
                {
                    equipment.EquipmentID = Convert.ToInt32(newId);

                    var supplierName = await GetSupplierNameByIdAsync(connection, equipment.SupplierID);

                    await LogActionAsync(connection, "CREATE",
                        $"Added equipment: '{equipment.EquipmentName}' (ID: {equipment.EquipmentID}, Supplier: {supplierName})", true);

                    _toastManager?.CreateToast("Equipment Added")
                        .WithContent($"Equipment '{equipment.EquipmentName}' added successfully!")
                        .DismissOnClick()
                        .ShowSuccess();
                    return (true, "Equipment added successfully.", equipment.EquipmentID);
                }

                return (false, "Failed to add equipment.", null);
            }
            catch (SqlException ex)
            {
                try
                {
                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "CREATE",
                        $"Failed to add equipment '{equipment.EquipmentName}' - SQL Error: {ex.Message}", false);
                }
                catch { }

                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to add equipment: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                try
                {
                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "CREATE",
                        $"Failed to add equipment '{equipment.EquipmentName}' - Error: {ex.Message}", false);
                }
                catch { }

                _toastManager?.CreateToast("Error")
                    .WithContent($"Error adding equipment: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region READ

        public async Task<(bool Success, string Message, List<EquipmentModel>? Equipment)> GetEquipmentAsync()
        {
            if (!CanView())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to view equipment.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to view equipment.", null);
            }

            var equipment = new List<EquipmentModel>();
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT e.EquipmentID, e.EquipmentName, e.Category, e.CurrentStock, 
                           e.PurchaseDate, e.PurchasePrice, e.SupplierID, s.SupplierName,
                           e.WarrantyExpiry, e.Condition, e.Status, 
                           e.LastMaintenance, e.NextMaintenance
                    FROM Equipment e
                    LEFT JOIN Suppliers s ON e.SupplierID = s.SupplierID
                    ORDER BY e.EquipmentName";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    equipment.Add(new EquipmentModel
                    {
                        EquipmentID = reader["EquipmentID"] != DBNull.Value ? reader.GetInt32("EquipmentID") : 0,
                        EquipmentName = reader["EquipmentName"]?.ToString() ?? "",
                        Category = reader["Category"]?.ToString() ?? "",
                        CurrentStock = reader["CurrentStock"] != DBNull.Value ? reader.GetInt32("CurrentStock") : 0,
                        PurchaseDate = reader["PurchaseDate"] != DBNull.Value ? reader.GetDateTime("PurchaseDate") : null,
                        PurchasePrice = reader["PurchasePrice"] != DBNull.Value ? reader.GetDecimal("PurchasePrice") : null,
                        SupplierID = reader["SupplierID"] != DBNull.Value ? reader.GetInt32("SupplierID") : null,
                        SupplierName = reader["SupplierName"]?.ToString() ?? "N/A",
                        WarrantyExpiry = reader["WarrantyExpiry"] != DBNull.Value ? reader.GetDateTime("WarrantyExpiry") : null,
                        Condition = reader["Condition"]?.ToString() ?? "",
                        Status = reader["Status"]?.ToString() ?? "",
                        LastMaintenance = reader["LastMaintenance"] != DBNull.Value ? reader.GetDateTime("LastMaintenance") : null,
                        NextMaintenance = reader["NextMaintenance"] != DBNull.Value ? reader.GetDateTime("NextMaintenance") : null
                    });
                }

                return (true, "Equipment retrieved successfully.", equipment);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load equipment: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, EquipmentModel? Equipment)> GetEquipmentByIdAsync(int equipmentId)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view equipment.", null);
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT e.EquipmentID, e.EquipmentName, e.Category, e.CurrentStock, 
                           e.PurchaseDate, e.PurchasePrice, e.SupplierID, s.SupplierName,
                           e.WarrantyExpiry, e.Condition, e.Status, 
                           e.LastMaintenance, e.NextMaintenance
                    FROM Equipment e
                    LEFT JOIN Suppliers s ON e.SupplierID = s.SupplierID
                    WHERE e.EquipmentID = @equipmentId";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@equipmentId", equipmentId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var equipment = new EquipmentModel
                    {
                        EquipmentID = reader["EquipmentID"] != DBNull.Value ? reader.GetInt32("EquipmentID") : 0,
                        EquipmentName = reader["EquipmentName"]?.ToString() ?? "",
                        Category = reader["Category"]?.ToString() ?? "",
                        CurrentStock = reader["CurrentStock"] != DBNull.Value ? reader.GetInt32("CurrentStock") : 0,
                        PurchaseDate = reader["PurchaseDate"] != DBNull.Value ? reader.GetDateTime("PurchaseDate") : null,
                        PurchasePrice = reader["PurchasePrice"] != DBNull.Value ? reader.GetDecimal("PurchasePrice") : null,
                        SupplierID = reader["SupplierID"] != DBNull.Value ? reader.GetInt32("SupplierID") : null,
                        SupplierName = reader["SupplierName"]?.ToString() ?? "N/A",
                        WarrantyExpiry = reader["WarrantyExpiry"] != DBNull.Value ? reader.GetDateTime("WarrantyExpiry") : null,
                        Condition = reader["Condition"]?.ToString() ?? "",
                        Status = reader["Status"]?.ToString() ?? "",
                        LastMaintenance = reader["LastMaintenance"] != DBNull.Value ? reader.GetDateTime("LastMaintenance") : null,
                        NextMaintenance = reader["NextMaintenance"] != DBNull.Value ? reader.GetDateTime("NextMaintenance") : null
                    };

                    return (true, "Equipment retrieved successfully.", equipment);
                }

                return (false, "Equipment not found.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<EquipmentModel>? Equipment)> GetEquipmentByStatusAsync(string status)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view equipment.", null);
            }

            var equipment = new List<EquipmentModel>();
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT e.EquipmentID, e.EquipmentName, e.Category, e.CurrentStock, 
                           e.PurchaseDate, e.PurchasePrice, e.SupplierID, s.SupplierName,
                           e.WarrantyExpiry, e.Condition, e.Status, 
                           e.LastMaintenance, e.NextMaintenance
                    FROM Equipment e
                    LEFT JOIN Suppliers s ON e.SupplierID = s.SupplierID
                    WHERE e.Status = @status
                    ORDER BY e.EquipmentName";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@status", status ?? (object)DBNull.Value);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    equipment.Add(new EquipmentModel
                    {
                        EquipmentID = reader["EquipmentID"] != DBNull.Value ? reader.GetInt32("EquipmentID") : 0,
                        EquipmentName = reader["EquipmentName"]?.ToString() ?? "",
                        Category = reader["Category"]?.ToString() ?? "",
                        CurrentStock = reader["CurrentStock"] != DBNull.Value ? reader.GetInt32("CurrentStock") : 0,
                        PurchaseDate = reader["PurchaseDate"] != DBNull.Value ? reader.GetDateTime("PurchaseDate") : null,
                        PurchasePrice = reader["PurchasePrice"] != DBNull.Value ? reader.GetDecimal("PurchasePrice") : null,
                        SupplierID = reader["SupplierID"] != DBNull.Value ? reader.GetInt32("SupplierID") : null,
                        SupplierName = reader["SupplierName"]?.ToString() ?? "N/A",
                        WarrantyExpiry = reader["WarrantyExpiry"] != DBNull.Value ? reader.GetDateTime("WarrantyExpiry") : null,
                        Condition = reader["Condition"]?.ToString() ?? "",
                        Status = reader["Status"]?.ToString() ?? "",
                        LastMaintenance = reader["LastMaintenance"] != DBNull.Value ? reader.GetDateTime("LastMaintenance") : null,
                        NextMaintenance = reader["NextMaintenance"] != DBNull.Value ? reader.GetDateTime("NextMaintenance") : null
                    });
                }

                return (true, "Equipment retrieved successfully.", equipment);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load equipment by status: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<EquipmentModel>? Equipment)> GetEquipmentNeedingMaintenanceAsync()
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view equipment.", null);
            }

            var equipment = new List<EquipmentModel>();
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT e.EquipmentID, e.EquipmentName, e.Category, e.CurrentStock, 
                           e.PurchaseDate, e.PurchasePrice, e.SupplierID, s.SupplierName,
                           e.WarrantyExpiry, e.Condition, e.Status, 
                           e.LastMaintenance, e.NextMaintenance
                    FROM Equipment e
                    LEFT JOIN Suppliers s ON e.SupplierID = s.SupplierID
                    WHERE e.NextMaintenance <= DATEADD(day, 7, GETDATE()) 
                       AND e.NextMaintenance IS NOT NULL
                       AND e.Status = 'Active'
                    ORDER BY e.NextMaintenance";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    equipment.Add(new EquipmentModel
                    {
                        EquipmentID = reader["EquipmentID"] != DBNull.Value ? reader.GetInt32("EquipmentID") : 0,
                        EquipmentName = reader["EquipmentName"]?.ToString() ?? "",
                        Category = reader["Category"]?.ToString() ?? "",
                        CurrentStock = reader["CurrentStock"] != DBNull.Value ? reader.GetInt32("CurrentStock") : 0,
                        PurchaseDate = reader["PurchaseDate"] != DBNull.Value ? reader.GetDateTime("PurchaseDate") : null,
                        PurchasePrice = reader["PurchasePrice"] != DBNull.Value ? reader.GetDecimal("PurchasePrice") : null,
                        SupplierID = reader["SupplierID"] != DBNull.Value ? reader.GetInt32("SupplierID") : null,
                        SupplierName = reader["SupplierName"]?.ToString() ?? "N/A",
                        WarrantyExpiry = reader["WarrantyExpiry"] != DBNull.Value ? reader.GetDateTime("WarrantyExpiry") : null,
                        Condition = reader["Condition"]?.ToString() ?? "",
                        Status = reader["Status"]?.ToString() ?? "",
                        LastMaintenance = reader["LastMaintenance"] != DBNull.Value ? reader.GetDateTime("LastMaintenance") : null,
                        NextMaintenance = reader["NextMaintenance"] != DBNull.Value ? reader.GetDateTime("NextMaintenance") : null
                    });
                }

                return (true, "Equipment retrieved successfully.", equipment);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load equipment needing maintenance: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<EquipmentModel>? Equipment)> GetEquipmentBySupplierAsync(int supplierId)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view equipment.", null);
            }

            var equipment = new List<EquipmentModel>();
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT e.EquipmentID, e.EquipmentName, e.Category, e.CurrentStock, 
                           e.PurchaseDate, e.PurchasePrice, e.SupplierID, s.SupplierName,
                           e.WarrantyExpiry, e.Condition, e.Status, 
                           e.LastMaintenance, e.NextMaintenance
                    FROM Equipment e
                    LEFT JOIN Suppliers s ON e.SupplierID = s.SupplierID
                    WHERE e.SupplierID = @supplierId
                    ORDER BY e.EquipmentName";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@supplierId", supplierId);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    equipment.Add(new EquipmentModel
                    {
                        EquipmentID = reader["EquipmentID"] != DBNull.Value ? reader.GetInt32("EquipmentID") : 0,
                        EquipmentName = reader["EquipmentName"]?.ToString() ?? "",
                        Category = reader["Category"]?.ToString() ?? "",
                        CurrentStock = reader["CurrentStock"] != DBNull.Value ? reader.GetInt32("CurrentStock") : 0,
                        PurchaseDate = reader["PurchaseDate"] != DBNull.Value ? reader.GetDateTime("PurchaseDate") : null,
                        PurchasePrice = reader["PurchasePrice"] != DBNull.Value ? reader.GetDecimal("PurchasePrice") : null,
                        SupplierID = reader["SupplierID"] != DBNull.Value ? reader.GetInt32("SupplierID") : null,
                        SupplierName = reader["SupplierName"]?.ToString() ?? "N/A",
                        WarrantyExpiry = reader["WarrantyExpiry"] != DBNull.Value ? reader.GetDateTime("WarrantyExpiry") : null,
                        Condition = reader["Condition"]?.ToString() ?? "",
                        Status = reader["Status"]?.ToString() ?? "",
                        LastMaintenance = reader["LastMaintenance"] != DBNull.Value ? reader.GetDateTime("LastMaintenance") : null,
                        NextMaintenance = reader["NextMaintenance"] != DBNull.Value ? reader.GetDateTime("NextMaintenance") : null
                    });
                }

                return (true, "Equipment retrieved successfully.", equipment);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region UPDATE

        public async Task<(bool Success, string Message)> UpdateEquipmentAsync(EquipmentModel equipment)
        {
            if (!CanUpdate())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("Only administrators can update equipment information.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to update equipment.");
            }

            const string query = @"
                UPDATE Equipment SET 
                    EquipmentName = @equipmentName,
                    Category = @category,
                    CurrentStock = @currentStock,
                    PurchaseDate = @purchaseDate,
                    PurchasePrice = @purchasePrice,
                    SupplierID = @supplierId,
                    WarrantyExpiry = @warrantyExpiry,
                    Condition = @condition,
                    Status = @status,
                    LastMaintenance = @lastMaintenance,
                    NextMaintenance = @nextMaintenance
                WHERE EquipmentID = @equipmentID";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check if equipment exists
                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Equipment WHERE EquipmentID = @equipmentID", connection);
                checkCmd.Parameters.AddWithValue("@equipmentID", equipment.EquipmentID);

                var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;
                if (!exists)
                {
                    _toastManager?.CreateToast("Equipment Not Found")
                        .WithContent("The equipment you're trying to update doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Equipment not found.");
                }

                // Validate supplier exists if provided
                if (equipment.SupplierID.HasValue)
                {
                    using var supplierCheckCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM Suppliers WHERE SupplierID = @supplierId", connection);
                    supplierCheckCmd.Parameters.AddWithValue("@supplierId", equipment.SupplierID.Value);
                    var supplierExists = (int)await supplierCheckCmd.ExecuteScalarAsync() > 0;

                    if (!supplierExists)
                    {
                        _toastManager?.CreateToast("Invalid Supplier")
                            .WithContent("The selected supplier does not exist.")
                            .DismissOnClick()
                            .ShowWarning();
                        return (false, "Invalid supplier selected.");
                    }
                }

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@equipmentID", equipment.EquipmentID);
                command.Parameters.AddWithValue("@equipmentName", equipment.EquipmentName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@category", equipment.Category ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@currentStock", equipment.CurrentStock);
                command.Parameters.AddWithValue("@purchaseDate", (object)equipment.PurchaseDate ?? DBNull.Value);
                command.Parameters.AddWithValue("@purchasePrice", (object)equipment.PurchasePrice ?? DBNull.Value);
                command.Parameters.AddWithValue("@supplierId", (object)equipment.SupplierID ?? DBNull.Value);
                command.Parameters.AddWithValue("@warrantyExpiry", (object)equipment.WarrantyExpiry ?? DBNull.Value);
                command.Parameters.AddWithValue("@condition", (object)equipment.Condition ?? DBNull.Value);
                command.Parameters.AddWithValue("@status", (object)equipment.Status ?? DBNull.Value);
                command.Parameters.AddWithValue("@lastMaintenance", (object)equipment.LastMaintenance ?? DBNull.Value);
                command.Parameters.AddWithValue("@nextMaintenance", (object)equipment.NextMaintenance ?? DBNull.Value);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    var supplierName = await GetSupplierNameByIdAsync(connection, equipment.SupplierID);

                    await LogActionAsync(connection, "UPDATE",
                        $"Updated equipment: '{equipment.EquipmentName}' (ID: {equipment.EquipmentID}, Supplier: {supplierName})", true);

                    _toastManager?.CreateToast("Equipment Updated")
                        .WithContent($"Equipment '{equipment.EquipmentName}' updated successfully!")
                        .DismissOnClick()
                        .ShowSuccess();
                    return (true, "Equipment updated successfully.");
                }

                return (false, "Failed to update equipment.");
            }
            catch (SqlException ex)
            {
                try
                {
                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "UPDATE",
                        $"Failed to update equipment '{equipment.EquipmentName}' - SQL Error: {ex.Message}", false);
                }
                catch { }

                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to update equipment: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                try
                {
                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "UPDATE",
                        $"Failed to update equipment '{equipment.EquipmentName}' - Error: {ex.Message}", false);
                }
                catch { }

                _toastManager?.CreateToast("Error")
                    .WithContent($"Error updating equipment: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateEquipmentStatusAsync(int equipmentId, string newStatus)
        {
            if (!CanUpdate())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("Only administrators can update equipment status.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to update equipment status.");
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Get equipment name for logging
                var equipmentName = await GetEquipmentNameByIdAsync(connection, equipmentId);

                using var command = new SqlCommand(
                    "UPDATE Equipment SET Status = @status WHERE EquipmentID = @equipmentID", connection);

                command.Parameters.AddWithValue("@equipmentID", equipmentId);
                command.Parameters.AddWithValue("@status", newStatus ?? (object)DBNull.Value);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(connection, "UPDATE",
                        $"Updated equipment '{equipmentName}' status to: {newStatus}", true);

                    _toastManager?.CreateToast("Status Updated")
                        .WithContent($"Equipment status updated to '{newStatus}'.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Status updated successfully.");
                }

                return (false, "Equipment not found.");
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent($"Failed to update status: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region DELETE

        public async Task<(bool Success, string Message)> DeleteEquipmentAsync(int equipmentID)
        {
            if (!CanDelete())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete equipment.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to delete equipment.");
            }

            const string query = "DELETE FROM Equipment WHERE EquipmentID = @equipmentID";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Get equipment name and supplier for logging
                var equipmentName = await GetEquipmentNameByIdAsync(connection, equipmentID);

                if (equipmentName == "Unknown Equipment")
                {
                    _toastManager?.CreateToast("Equipment Not Found")
                        .WithContent("The equipment you're trying to delete doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Equipment not found.");
                }

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@equipmentID", equipmentID);
                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(connection, "DELETE",
                        $"Deleted equipment: '{equipmentName}' (ID: {equipmentID})", true);

                    _toastManager?.CreateToast("Equipment Deleted")
                        .WithContent($"Equipment '{equipmentName}' deleted successfully!")
                        .DismissOnClick()
                        .ShowSuccess();
                    return (true, "Equipment deleted successfully.");
                }

                return (false, "Failed to delete equipment.");
            }
            catch (SqlException ex)
            {
                try
                {
                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "DELETE",
                        $"Failed to delete equipment ID {equipmentID} - SQL Error: {ex.Message}", false);
                }
                catch { }

                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to delete equipment: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                try
                {
                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "DELETE",
                        $"Failed to delete equipment ID {equipmentID} - Error: {ex.Message}", false);
                }
                catch { }

                _toastManager?.CreateToast("Error")
                    .WithContent($"Error deleting equipment: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, int DeletedCount)> DeleteMultipleEquipmentAsync(List<int> equipmentIds)
        {
            if (!CanDelete())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete equipment.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to delete equipment.", 0);
            }

            if (equipmentIds == null || equipmentIds.Count == 0)
            {
                return (false, "No equipment selected for deletion.", 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var deletedCount = 0;
                var equipmentNames = new List<string>();

                using var transaction = conn.BeginTransaction();
                try
                {
                    foreach (var equipmentId in equipmentIds)
                    {
                        // Get equipment name
                        var name = await GetEquipmentNameByIdAsync(conn, equipmentId);
                        if (name != "Unknown Equipment")
                        {
                            equipmentNames.Add(name);
                        }

                        // Delete equipment
                        using var deleteCmd = new SqlCommand(
                            "DELETE FROM Equipment WHERE EquipmentID = @equipmentID", conn, transaction);
                        deleteCmd.Parameters.AddWithValue("@equipmentID", equipmentId);
                        deletedCount += await deleteCmd.ExecuteNonQueryAsync();
                    }

                    await LogActionAsync(conn, "DELETE",
                        $"Deleted {deletedCount} equipment items: {string.Join(", ", equipmentNames)}", true);

                    transaction.Commit();

                    _toastManager?.CreateToast("Equipment Deleted")
                        .WithContent($"Successfully deleted {deletedCount} equipment item(s).")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, $"Successfully deleted {deletedCount} equipment item(s).", deletedCount);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to delete equipment: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}", 0);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}", 0);
            }
        }

        #endregion

        #region STATISTICS & UTILITY

        private async Task<string> GetEquipmentNameByIdAsync(SqlConnection connection, int equipmentID)
        {
            const string query = "SELECT EquipmentName FROM Equipment WHERE EquipmentID = @equipmentID";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@equipmentID", equipmentID);
            var result = await command.ExecuteScalarAsync();
            return result?.ToString() ?? "Unknown Equipment";
        }

        public async Task<(bool Success, int TotalEquipment, int ActiveCount, int InactiveCount, int MaintenanceCount, int RetiredCount)> GetEquipmentStatisticsAsync()
        {
            if (!CanView())
            {
                return (false, 0, 0, 0, 0, 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT 
                        COUNT(*) as TotalCount,
                        SUM(CASE WHEN Status = 'Active' THEN 1 ELSE 0 END) as ActiveCount,
                        SUM(CASE WHEN Status = 'Inactive' THEN 1 ELSE 0 END) as InactiveCount,
                        SUM(CASE WHEN Status = 'Under Maintenance' THEN 1 ELSE 0 END) as MaintenanceCount,
                        SUM(CASE WHEN Status = 'Retired' THEN 1 ELSE 0 END) as RetiredCount
                      FROM Equipment", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (true,
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetInt32(2),
                        reader.GetInt32(3),
                        reader.GetInt32(4));
                }

                return (false, 0, 0, 0, 0, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetEquipmentStatisticsAsync] {ex.Message}");
                return (false, 0, 0, 0, 0, 0);
            }
        }

        public async Task<(bool Success, string Message, decimal TotalValue)> GetTotalEquipmentValueAsync()
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions.", 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT ISNULL(SUM(PurchasePrice * CurrentStock), 0) as TotalValue
                      FROM Equipment
                      WHERE PurchasePrice IS NOT NULL", conn);

                var result = await cmd.ExecuteScalarAsync();
                var totalValue = result != DBNull.Value ? Convert.ToDecimal(result) : 0;

                return (true, "Total value calculated successfully.", totalValue);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", 0);
            }
        }

        public async Task<(bool Success, string Message, int LowStockCount)> GetLowStockEquipmentCountAsync(int threshold = 5)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions.", 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT COUNT(*) FROM Equipment 
                      WHERE CurrentStock <= @threshold 
                      AND Status = 'Active'", conn);

                cmd.Parameters.AddWithValue("@threshold", threshold);

                var count = (int)await cmd.ExecuteScalarAsync();

                return (true, "Low stock count retrieved successfully.", count);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", 0);
            }
        }

        #endregion
    }

    #region Supporting Models

    public class SupplierDropdownModel
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; } = string.Empty;
    }

    #endregion
}
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
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanUpdate()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanDelete()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanView()
        {
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

        #region CREATE / RESTORE

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

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check if equipment with same name exists (including deleted ones)
                using var checkCmd = new SqlCommand(
                    @"SELECT EquipmentID, IsDeleted FROM Equipment 
                      WHERE EquipmentName = @equipmentName", connection);
                checkCmd.Parameters.AddWithValue("@equipmentName", equipment.EquipmentName ?? (object)DBNull.Value);

                int? existingId = null;
                bool isDeleted = false;

                using var reader = await checkCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    existingId = reader.GetInt32(0);
                    isDeleted = reader.GetBoolean(1);
                }
                reader.Close();

                // If equipment exists and is deleted, restore it
                if (existingId.HasValue && isDeleted)
                {
                    const string restoreQuery = @"
                        UPDATE Equipment SET 
                            IsDeleted = 0,
                            Category = @category,
                            CurrentStock = @currentStock,
                            PurchaseDate = @purchaseDate,
                            PurchasePrice = @purchasePrice,
                            SupplierID = @supplierId,
                            WarrantyExpiry = @warrantyExpiry,
                            Condition = @condition,
                            Status = @status,
                            LastMaintenance = @lastMaintenance,
                            NextMaintenance = @nextMaintenance,
                            AddedByEmployeeID = @employeeID
                        WHERE EquipmentID = @equipmentID";

                    using var restoreCmd = new SqlCommand(restoreQuery, connection);
                    restoreCmd.Parameters.AddWithValue("@equipmentID", existingId.Value);
                    restoreCmd.Parameters.AddWithValue("@category", equipment.Category ?? (object)DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@currentStock", equipment.CurrentStock);
                    restoreCmd.Parameters.AddWithValue("@purchaseDate", (object)equipment.PurchaseDate ?? DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@purchasePrice", (object)equipment.PurchasePrice ?? DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@supplierId", (object)equipment.SupplierID ?? DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@warrantyExpiry", (object)equipment.WarrantyExpiry ?? DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@condition", (object)equipment.Condition ?? DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@status", (object)equipment.Status ?? DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@lastMaintenance", (object)equipment.LastMaintenance ?? DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@nextMaintenance", (object)equipment.NextMaintenance ?? DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                    await restoreCmd.ExecuteNonQueryAsync();

                    var supplierName = await GetSupplierNameByIdAsync(connection, equipment.SupplierID);

                    await LogActionAsync(connection, "RESTORE",
                        $"Restored equipment: '{equipment.EquipmentName}' (ID: {existingId.Value}, Supplier: {supplierName})", true);

                    _toastManager?.CreateToast("Equipment Restored")
                        .WithContent($"Equipment '{equipment.EquipmentName}' has been restored successfully!")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Equipment restored successfully.", existingId.Value);
                }

                // If equipment exists and is NOT deleted, prevent duplicate
                if (existingId.HasValue && !isDeleted)
                {
                    _toastManager?.CreateToast("Duplicate Equipment")
                        .WithContent($"Equipment with name '{equipment.EquipmentName}' already exists.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Equipment with this name already exists.", null);
                }

                // Validate supplier exists if provided
                if (equipment.SupplierID.HasValue)
                {
                    using var supplierCheckCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM Suppliers WHERE SupplierID = @supplierId AND IsDeleted = 0", connection);
                    supplierCheckCmd.Parameters.AddWithValue("@supplierId", equipment.SupplierID.Value);
                    var supplierExists = (int)await supplierCheckCmd.ExecuteScalarAsync() > 0;

                    if (!supplierExists)
                    {
                        _toastManager?.CreateToast("Invalid Supplier")
                            .WithContent("The selected supplier does not exist.")
                            .DismissOnClick()
                            .ShowWarning();
                        return (false, "Invalid supplier selected.", null);
                    }
                }

                // Insert new equipment
                const string insertQuery = @"
                    INSERT INTO Equipment (EquipmentName, Category, CurrentStock, 
                                          PurchaseDate, PurchasePrice, SupplierID, WarrantyExpiry, 
                                          Condition, Status, LastMaintenance, NextMaintenance, 
                                          AddedByEmployeeID, IsDeleted)
                    OUTPUT INSERTED.EquipmentID
                    VALUES (@equipmentName, @category, @currentStock, 
                            @purchaseDate, @purchasePrice, @supplierId, @warrantyExpiry, 
                            @condition, @status, @lastMaintenance, @nextMaintenance, 
                            @employeeID, 0)";

                using var command = new SqlCommand(insertQuery, connection);
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

                    /* _toastManager?.CreateToast("Equipment Added")
                         .WithContent($"Equipment '{equipment.EquipmentName}' added successfully!")
                         .DismissOnClick()
                         .ShowSuccess(); */
                    DashboardEventService.Instance.NotifyEquipmentAdded();
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
                    WHERE e.IsDeleted = 0
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
                    WHERE e.EquipmentID = @equipmentId AND e.IsDeleted = 0";

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
                    WHERE e.Status = @status AND e.IsDeleted = 0
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
                       AND e.IsDeleted = 0
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
                    WHERE e.SupplierID = @supplierId AND e.IsDeleted = 0
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
                WHERE EquipmentID = @equipmentID AND IsDeleted = 0";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check if equipment exists and is not deleted
                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Equipment WHERE EquipmentID = @equipmentID AND IsDeleted = 0", connection);
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

                    /*  _toastManager?.CreateToast("Equipment Updated")
                          .WithContent($"Equipment '{equipment.EquipmentName}' updated successfully!")
                          .DismissOnClick()
                          .ShowSuccess(); */
                    DashboardEventService.Instance.NotifyEquipmentUpdated();
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
                    "UPDATE Equipment SET Status = @status WHERE EquipmentID = @equipmentID AND IsDeleted = 0", connection);

                command.Parameters.AddWithValue("@equipmentID", equipmentId);
                command.Parameters.AddWithValue("@status", newStatus ?? (object)DBNull.Value);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(connection, "UPDATE",
                        $"Updated equipment '{equipmentName}' status to: {newStatus}", true);

                    /*  _toastManager?.CreateToast("Status Updated")
                          .WithContent($"Equipment status updated to '{newStatus}'.")
                          .DismissOnClick()
                          .ShowSuccess(); */

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

        #region DELETE (Soft Delete)

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

            const string query = "UPDATE Equipment SET IsDeleted = 1 WHERE EquipmentID = @equipmentID AND IsDeleted = 0";

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

                    /*   _toastManager?.CreateToast("Equipment Deleted")
                           .WithContent($"Equipment '{equipmentName}' deleted successfully!")
                           .DismissOnClick()
                           .ShowSuccess(); */
                    DashboardEventService.Instance.NotifyEquipmentDeleted();
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

                        // Soft delete equipment
                        using var deleteCmd = new SqlCommand(
                            "UPDATE Equipment SET IsDeleted = 1 WHERE EquipmentID = @equipmentID AND IsDeleted = 0", conn, transaction);
                        deleteCmd.Parameters.AddWithValue("@equipmentID", equipmentId);
                        deletedCount += await deleteCmd.ExecuteNonQueryAsync();
                    }

                    await LogActionAsync(conn, "DELETE",
                        $"Deleted {deletedCount} equipment items: {string.Join(", ", equipmentNames)}", true);

                    transaction.Commit();

                    /*   _toastManager?.CreateToast("Equipment Deleted")
                           .WithContent($"Successfully deleted {deletedCount} equipment item(s).")
                           .DismissOnClick()
                           .ShowSuccess(); */
                    DashboardEventService.Instance.NotifyEquipmentDeleted();
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

        #region NOTIFICATIONS

        public async Task ShowEquipmentAlertsAsync()
        {
            try
            {
                // Get all alerts
                var lowStockCount = await GetLowStockCountAsync();
                var maintenanceDueCount = await GetMaintenanceDueCountAsync();
                var warrantyExpiringCount = await GetWarrantyExpiringCountAsync();
                var conditionAlertCount = await GetConditionAlertCountAsync();

                // Show notifications based on priority
                // HIGHEST PRIORITY: Equipment condition issues
                if (conditionAlertCount > 0)
                {
                    _toastManager?.CreateToast("Equipment Condition Alert")
                        .WithContent($"{conditionAlertCount} equipment item(s) need attention (Repairing/Broken)!")
                        .DismissOnClick()
                        .ShowError();
                }

                if (maintenanceDueCount > 0)
                {
                    _toastManager?.CreateToast("Maintenance Alert")
                        .WithContent($"{maintenanceDueCount} equipment item(s) require maintenance within 7 days!")
                        .DismissOnClick()
                        .ShowWarning();
                }

                if (warrantyExpiringCount > 0)
                {
                    _toastManager?.CreateToast("Warranty Expiring")
                        .WithContent($"{warrantyExpiringCount} equipment warranty(ies) expiring within 30 days!")
                        .DismissOnClick()
                        .ShowInfo();
                }

                if (lowStockCount > 0)
                {
                    _toastManager?.CreateToast("Low Stock Alert")
                        .WithContent($"{lowStockCount} equipment item(s) have low stock (≤5 units)!")
                        .DismissOnClick()
                        .ShowWarning();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ShowEquipmentAlertsAsync] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get detailed equipment alerts including condition alerts
        /// </summary>
        public async Task<EquipmentAlertSummary> GetEquipmentAlertSummaryAsync()
        {
            var summary = new EquipmentAlertSummary();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Get condition alert items (NEW)
                summary.ConditionAlertItems = await GetConditionAlertItemsAsync(conn);
                summary.ConditionAlertCount = summary.ConditionAlertItems.Count;

                // Get low stock items
                summary.LowStockItems = await GetLowStockItemsAsync(conn);
                summary.LowStockCount = summary.LowStockItems.Count;

                // Get maintenance due items
                summary.MaintenanceDueItems = await GetMaintenanceDueItemsAsync(conn);
                summary.MaintenanceDueCount = summary.MaintenanceDueItems.Count;

                // Get warranty expiring items
                summary.WarrantyExpiringItems = await GetWarrantyExpiringItemsAsync(conn);
                summary.WarrantyExpiringCount = summary.WarrantyExpiringItems.Count;

                summary.TotalAlerts = summary.ConditionAlertCount + summary.LowStockCount +
                                      summary.MaintenanceDueCount + summary.WarrantyExpiringCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetEquipmentAlertSummaryAsync] Error: {ex.Message}");
            }

            return summary;
        }

        #endregion

        #region Private Helper Methods

        private async Task<int> GetLowStockCountAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT COUNT(*) FROM Equipment 
              WHERE CurrentStock <= 5 
              AND Status = 'Active'
              AND IsDeleted = 0", conn);

                return (int)await cmd.ExecuteScalarAsync();
            }
            catch
            {
                return 0;
            }
        }

        private async Task<int> GetMaintenanceDueCountAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT COUNT(*) FROM Equipment 
              WHERE NextMaintenance <= DATEADD(day, 7, GETDATE())
              AND NextMaintenance IS NOT NULL
              AND Status = 'Active'
              AND IsDeleted = 0", conn);

                return (int)await cmd.ExecuteScalarAsync();
            }
            catch
            {
                return 0;
            }
        }

        private async Task<int> GetWarrantyExpiringCountAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT COUNT(*) FROM Equipment 
              WHERE WarrantyExpiry <= DATEADD(day, 30, GETDATE())
              AND WarrantyExpiry >= GETDATE()
              AND Status = 'Active'
              AND IsDeleted = 0", conn);

                return (int)await cmd.ExecuteScalarAsync();
            }
            catch
            {
                return 0;
            }
        }

        // NEW: Get count of equipment with poor condition
        private async Task<int> GetConditionAlertCountAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT COUNT(*) FROM Equipment 
              WHERE (Condition = 'Repairing' OR Condition = 'Broken')
              AND Status = 'Active'
              AND IsDeleted = 0", conn);

                return (int)await cmd.ExecuteScalarAsync();
            }
            catch
            {
                return 0;
            }
        }

        private async Task<List<EquipmentAlertItem>> GetLowStockItemsAsync(SqlConnection conn)
        {
            var items = new List<EquipmentAlertItem>();

            try
            {
                using var cmd = new SqlCommand(
                    @"SELECT EquipmentID, EquipmentName, CurrentStock 
              FROM Equipment 
              WHERE CurrentStock <= 5 
              AND Status = 'Active'
              AND IsDeleted = 0
              ORDER BY CurrentStock ASC", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new EquipmentAlertItem
                    {
                        EquipmentID = reader.GetInt32(0),
                        EquipmentName = reader.GetString(1),
                        AlertType = "Low Stock",
                        AlertSeverity = "Warning",
                        Details = $"Current stock: {reader.GetInt32(2)} units"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetLowStockItemsAsync] Error: {ex.Message}");
            }

            return items;
        }

        private async Task<List<EquipmentAlertItem>> GetMaintenanceDueItemsAsync(SqlConnection conn)
        {
            var items = new List<EquipmentAlertItem>();

            try
            {
                using var cmd = new SqlCommand(
                    @"SELECT EquipmentID, EquipmentName, NextMaintenance 
              FROM Equipment 
              WHERE NextMaintenance <= DATEADD(day, 7, GETDATE())
              AND NextMaintenance IS NOT NULL
              AND Status = 'Active'
              AND IsDeleted = 0
              ORDER BY NextMaintenance ASC", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var nextMaintenance = reader.GetDateTime(2);
                    var daysUntil = (nextMaintenance - DateTime.Now).Days;
                    var status = daysUntil < 0 ? "Overdue" : $"Due in {daysUntil} day(s)";
                    var severity = daysUntil < 0 ? "Error" : "Warning";

                    items.Add(new EquipmentAlertItem
                    {
                        EquipmentID = reader.GetInt32(0),
                        EquipmentName = reader.GetString(1),
                        AlertType = "Maintenance Due",
                        AlertSeverity = severity,
                        Details = $"{status} - {nextMaintenance:MMM dd, yyyy}"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMaintenanceDueItemsAsync] Error: {ex.Message}");
            }

            return items;
        }

        private async Task<List<EquipmentAlertItem>> GetWarrantyExpiringItemsAsync(SqlConnection conn)
        {
            var items = new List<EquipmentAlertItem>();

            try
            {
                using var cmd = new SqlCommand(
                    @"SELECT EquipmentID, EquipmentName, WarrantyExpiry 
              FROM Equipment 
              WHERE WarrantyExpiry <= DATEADD(day, 30, GETDATE())
              AND WarrantyExpiry >= GETDATE()
              AND Status = 'Active'
              AND IsDeleted = 0
              ORDER BY WarrantyExpiry ASC", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var warrantyExpiry = reader.GetDateTime(2);
                    var daysUntil = (warrantyExpiry - DateTime.Now).Days;

                    items.Add(new EquipmentAlertItem
                    {
                        EquipmentID = reader.GetInt32(0),
                        EquipmentName = reader.GetString(1),
                        AlertType = "Warranty Expiring",
                        AlertSeverity = "Info",
                        Details = $"Expires in {daysUntil} day(s) - {warrantyExpiry:MMM dd, yyyy}"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetWarrantyExpiringItemsAsync] Error: {ex.Message}");
            }

            return items;
        }

        // NEW: Get equipment with poor condition (Repairing/Broken)
        private async Task<List<EquipmentAlertItem>> GetConditionAlertItemsAsync(SqlConnection conn)
        {
            var items = new List<EquipmentAlertItem>();

            try
            {
                using var cmd = new SqlCommand(
                    @"SELECT EquipmentID, EquipmentName, Condition, CurrentStock 
              FROM Equipment 
              WHERE (Condition = 'Repairing' OR Condition = 'Broken')
              AND Status = 'Active'
              AND IsDeleted = 0
              ORDER BY 
                CASE Condition 
                    WHEN 'Broken' THEN 1 
                    WHEN 'Repairing' THEN 2 
                    ELSE 3 
                END,
                EquipmentName ASC", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var condition = reader.GetString(2);
                    var currentStock = reader.GetInt32(3);
                    var severity = condition.Equals("Broken", StringComparison.OrdinalIgnoreCase) ? "Error" : "Warning";
                    var icon = condition.Equals("Broken", StringComparison.OrdinalIgnoreCase) ? "🔴" : "🟠";

                    items.Add(new EquipmentAlertItem
                    {
                        EquipmentID = reader.GetInt32(0),
                        EquipmentName = reader.GetString(1),
                        AlertType = "Condition Alert",
                        AlertSeverity = severity,
                        Details = $"{icon} {condition} - {currentStock} unit(s) affected"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetConditionAlertItemsAsync] Error: {ex.Message}");
            }

            return items;
        }

        #endregion

        #region STATISTICS & UTILITY

        private async Task<string> GetEquipmentNameByIdAsync(SqlConnection connection, int equipmentID)
        {
            const string query = "SELECT EquipmentName FROM Equipment WHERE EquipmentID = @equipmentID AND IsDeleted = 0";
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
                      FROM Equipment
                      WHERE IsDeleted = 0", conn);

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
                      WHERE PurchasePrice IS NOT NULL AND IsDeleted = 0", conn);

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
                      AND Status = 'Active'
                      AND IsDeleted = 0", conn);

                cmd.Parameters.AddWithValue("@threshold", threshold);

                var count = (int)await cmd.ExecuteScalarAsync();

                return (true, "Low stock count retrieved successfully.", count);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", 0);
            }
        }
    }
    #endregion

    #region Supporting Models

    public class SupplierDropdownModel
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; } = string.Empty;
    }

    public class EquipmentAlertSummary
    {
        public int TotalAlerts { get; set; }
        public int ConditionAlertCount { get; set; }  // NEW
        public int LowStockCount { get; set; }
        public int MaintenanceDueCount { get; set; }
        public int WarrantyExpiringCount { get; set; }
        public List<EquipmentAlertItem> ConditionAlertItems { get; set; } = new List<EquipmentAlertItem>();  // NEW
        public List<EquipmentAlertItem> LowStockItems { get; set; } = new List<EquipmentAlertItem>();
        public List<EquipmentAlertItem> MaintenanceDueItems { get; set; } = new List<EquipmentAlertItem>();
        public List<EquipmentAlertItem> WarrantyExpiringItems { get; set; } = new List<EquipmentAlertItem>();
    }

    public class EquipmentAlertItem
    {
        public int EquipmentID { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string AlertSeverity { get; set; } = "Info";  // "Error", "Warning", "Info"
        public string Details { get; set; } = string.Empty;
    }

    #endregion
}
using AHON_TRACK.Models;
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

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                using var logCmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                      VALUES (@username, @role, @actionType, @description, @success, GETDATE()), @employeeID", conn);

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

        public async Task<List<EquipmentModel>> GetEquipmentAsync()
        {
            var equipment = new List<EquipmentModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
        SELECT equipmentID, equipmentName, category, currentStock, 
               purchaseDate, purchasePrice, supplier, warrantyExpiry, 
               condition, status, lastMaintenance, nextMaintenance
        FROM Equipment 
        ORDER BY equipmentName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                equipment.Add(new EquipmentModel
                                {
                                    EquipmentID = reader["equipmentID"] != DBNull.Value ? reader.GetInt32("equipmentID") : 0,
                                    EquipmentName = reader["equipmentName"]?.ToString() ?? "",
                                    Category = reader["category"]?.ToString() ?? "",
                                    CurrentStock = reader["currentStock"] != DBNull.Value ? reader.GetInt32("currentStock") : 0,
                                    PurchaseDate = reader["purchaseDate"] != DBNull.Value ? reader.GetDateTime("purchaseDate") : null,
                                    PurchasePrice = reader["purchasePrice"] != DBNull.Value ? reader.GetDecimal("purchasePrice") : null,
                                    Supplier = reader["supplier"]?.ToString() ?? "",
                                    WarrantyExpiry = reader["warrantyExpiry"] != DBNull.Value ? reader.GetDateTime("warrantyExpiry") : null,
                                    Condition = reader["condition"]?.ToString() ?? "",
                                    Status = reader["status"]?.ToString() ?? "",
                                    LastMaintenance = reader["lastMaintenance"] != DBNull.Value ? reader.GetDateTime("lastMaintenance") : null,
                                    NextMaintenance = reader["nextMaintenance"] != DBNull.Value ? reader.GetDateTime("nextMaintenance") : null
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load equipment: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }
            return equipment;
        }

        public async Task<bool> AddEquipmentAsync(EquipmentModel equipment)
        {
            // Updated query - removed equipmentID from INSERT, using OUTPUT to get generated ID
            const string query = @"
INSERT INTO Equipment (equipmentName, category, currentStock, 
                      purchaseDate, purchasePrice, supplier, warrantyExpiry, 
                      condition, status, lastMaintenance, nextMaintenance)
OUTPUT INSERTED.equipmentID
VALUES (@equipmentName, @category, @currentStock, 
        @purchaseDate, @purchasePrice, @supplier, @warrantyExpiry, 
        @condition, @status, @lastMaintenance, @nextMaintenance)";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        // No longer adding equipmentID parameter - it will be auto-generated
                        command.Parameters.AddWithValue("@equipmentName", equipment.EquipmentName);
                        command.Parameters.AddWithValue("@category", equipment.Category);
                        command.Parameters.AddWithValue("@currentStock", equipment.CurrentStock);
                        command.Parameters.AddWithValue("@purchaseDate", (object)equipment.PurchaseDate ?? DBNull.Value);
                        command.Parameters.AddWithValue("@purchasePrice", (object)equipment.PurchasePrice ?? DBNull.Value);
                        command.Parameters.AddWithValue("@supplier", (object)equipment.Supplier ?? DBNull.Value);
                        command.Parameters.AddWithValue("@warrantyExpiry", (object)equipment.WarrantyExpiry ?? DBNull.Value);
                        command.Parameters.AddWithValue("@condition", (object)equipment.Condition ?? DBNull.Value);
                        command.Parameters.AddWithValue("@status", (object)equipment.Status ?? DBNull.Value);
                        command.Parameters.AddWithValue("@lastMaintenance", (object)equipment.LastMaintenance ?? DBNull.Value);
                        command.Parameters.AddWithValue("@nextMaintenance", (object)equipment.NextMaintenance ?? DBNull.Value);

                        // ExecuteScalar returns the generated ID
                        var newId = await command.ExecuteScalarAsync();

                        if (newId != null && newId != DBNull.Value)
                        {
                            equipment.EquipmentID = Convert.ToInt32(newId);

                            await LogActionAsync(connection, "Add Equipment",
                                $"Added equipment: '{equipment.EquipmentName}' (ID: {equipment.EquipmentID})", true);

                            _toastManager?.CreateToast("Equipment Added")
                                .WithContent($"Equipment '{equipment.EquipmentName}' added successfully!")
                                .ShowSuccess();
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
                    await LogActionAsync(connection, "Failed to add equipment",
                        $"Failed to add equipment '{equipment.EquipmentName}' - SQL Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to add equipment: {ex.Message}")
                    .ShowError();
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to add equipment",
                        $"Failed to add equipment '{equipment.EquipmentName}' - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error adding equipment: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> UpdateEquipmentAsync(EquipmentModel equipment)
        {
            const string query = @"
UPDATE Equipment SET 
    equipmentName = @equipmentName,
    category = @category,
    currentStock = @currentStock,
    purchaseDate = @purchaseDate,
    purchasePrice = @purchasePrice,
    supplier = @supplier,
    warrantyExpiry = @warrantyExpiry,
    condition = @condition,
    status = @status,
    lastMaintenance = @lastMaintenance,
    nextMaintenance = @nextMaintenance
WHERE equipmentID = @equipmentID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@equipmentID", equipment.EquipmentID);
                        command.Parameters.AddWithValue("@equipmentName", equipment.EquipmentName);
                        command.Parameters.AddWithValue("@category", equipment.Category);
                        command.Parameters.AddWithValue("@currentStock", equipment.CurrentStock);
                        command.Parameters.AddWithValue("@purchaseDate", (object)equipment.PurchaseDate ?? DBNull.Value);
                        command.Parameters.AddWithValue("@purchasePrice", (object)equipment.PurchasePrice ?? DBNull.Value);
                        command.Parameters.AddWithValue("@supplier", (object)equipment.Supplier ?? DBNull.Value);
                        command.Parameters.AddWithValue("@warrantyExpiry", (object)equipment.WarrantyExpiry ?? DBNull.Value);
                        command.Parameters.AddWithValue("@condition", (object)equipment.Condition ?? DBNull.Value);
                        command.Parameters.AddWithValue("@status", (object)equipment.Status ?? DBNull.Value);
                        command.Parameters.AddWithValue("@lastMaintenance", (object)equipment.LastMaintenance ?? DBNull.Value);
                        command.Parameters.AddWithValue("@nextMaintenance", (object)equipment.NextMaintenance ?? DBNull.Value);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Update Equipment",
                                $"Updated equipment: '{equipment.EquipmentName}' (ID: {equipment.EquipmentID})", true);

                            _toastManager?.CreateToast("Equipment Updated")
                                .WithContent($"Equipment '{equipment.EquipmentName}' updated successfully!")
                                .ShowSuccess();
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
                    await LogActionAsync(connection, "Failed to update equipment",
                        $"Failed to update equipment '{equipment.EquipmentName}' - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error updating equipment: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> DeleteEquipmentAsync(int equipmentID)
        {
            const string query = "DELETE FROM Equipment WHERE equipmentID = @equipmentID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Get equipment name for logging
                    var equipmentName = await GetEquipmentNameByIdAsync(connection, equipmentID);

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@equipmentID", equipmentID);
                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Delete Equipment",
                                $"Deleted equipment: '{equipmentName}' (ID: {equipmentID})", true);

                            _toastManager?.CreateToast("Equipment Deleted")
                                .WithContent($"Equipment '{equipmentName}' deleted successfully!")
                                .ShowSuccess();
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
                    await LogActionAsync(connection, "Failed to delete equipment",
                        $"Failed to delete equipment ID {equipmentID} - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error deleting equipment: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<List<EquipmentModel>> GetEquipmentByStatusAsync(string status)
        {
            var equipment = new List<EquipmentModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
        SELECT equipmentID, equipmentName, category, currentStock, 
               purchaseDate, purchasePrice, supplier, warrantyExpiry, 
               condition, status, lastMaintenance, nextMaintenance
        FROM Equipment 
        WHERE status = @status
        ORDER BY equipmentName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@status", status);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                equipment.Add(new EquipmentModel
                                {
                                    EquipmentID = reader["equipmentID"] != DBNull.Value ? reader.GetInt32("equipmentID") : 0,
                                    EquipmentName = reader["equipmentName"]?.ToString() ?? "",
                                    Category = reader["category"]?.ToString() ?? "",
                                    CurrentStock = reader["currentStock"] != DBNull.Value ? reader.GetInt32("currentStock") : 0,
                                    PurchaseDate = reader["purchaseDate"] != DBNull.Value ? reader.GetDateTime("purchaseDate") : null,
                                    PurchasePrice = reader["purchasePrice"] != DBNull.Value ? reader.GetDecimal("purchasePrice") : null,
                                    Supplier = reader["supplier"]?.ToString() ?? "",
                                    WarrantyExpiry = reader["warrantyExpiry"] != DBNull.Value ? reader.GetDateTime("warrantyExpiry") : null,
                                    Condition = reader["condition"]?.ToString() ?? "",
                                    Status = reader["status"]?.ToString() ?? "",
                                    LastMaintenance = reader["lastMaintenance"] != DBNull.Value ? reader.GetDateTime("lastMaintenance") : null,
                                    NextMaintenance = reader["nextMaintenance"] != DBNull.Value ? reader.GetDateTime("nextMaintenance") : null
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load equipment by status: {ex.Message}")
                    .ShowError();
            }
            return equipment;
        }

        public async Task<List<EquipmentModel>> GetEquipmentNeedingMaintenanceAsync()
        {
            var equipment = new List<EquipmentModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
        SELECT equipmentID, equipmentName, category, currentStock, 
               purchaseDate, purchasePrice, supplier, warrantyExpiry, 
               condition, status, lastMaintenance, nextMaintenance
        FROM Equipment 
        WHERE nextMaintenance <= DATEADD(day, 7, GETDATE()) 
           AND nextMaintenance IS NOT NULL
           AND status = 'Active'
        ORDER BY nextMaintenance";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                equipment.Add(new EquipmentModel
                                {
                                    EquipmentID = reader["equipmentID"] != DBNull.Value ? reader.GetInt32("equipmentID") : 0,
                                    EquipmentName = reader["equipmentName"]?.ToString() ?? "",
                                    Category = reader["category"]?.ToString() ?? "",
                                    CurrentStock = reader["currentStock"] != DBNull.Value ? reader.GetInt32("currentStock") : 0,
                                    PurchaseDate = reader["purchaseDate"] != DBNull.Value ? reader.GetDateTime("purchaseDate") : null,
                                    PurchasePrice = reader["purchasePrice"] != DBNull.Value ? reader.GetDecimal("purchasePrice") : null,
                                    Supplier = reader["supplier"]?.ToString() ?? "",
                                    WarrantyExpiry = reader["warrantyExpiry"] != DBNull.Value ? reader.GetDateTime("warrantyExpiry") : null,
                                    Condition = reader["condition"]?.ToString() ?? "",
                                    Status = reader["status"]?.ToString() ?? "",
                                    LastMaintenance = reader["lastMaintenance"] != DBNull.Value ? reader.GetDateTime("lastMaintenance") : null,
                                    NextMaintenance = reader["nextMaintenance"] != DBNull.Value ? reader.GetDateTime("nextMaintenance") : null
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load equipment needing maintenance: {ex.Message}")
                    .ShowError();
            }
            return equipment;
        }

        private async Task<string> GetEquipmentNameByIdAsync(SqlConnection connection, int equipmentID)
        {
            const string query = "SELECT equipmentName FROM Equipment WHERE equipmentID = @equipmentID";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@equipmentID", equipmentID);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? "Unknown Equipment";
            }
        }
    }
}

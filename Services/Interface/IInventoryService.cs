using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface IInventoryService
    {
        Task<(bool Success, string Message, int? EquipmentId)> AddEquipmentAsync(EquipmentModel equipment);
        Task<(bool Success, string Message, List<EquipmentModel>? Equipment)> GetEquipmentAsync();
        Task<(bool Success, string Message, EquipmentModel? Equipment)> GetEquipmentByIdAsync(int equipmentId);
        Task<(bool Success, string Message, List<EquipmentModel>? Equipment)> GetEquipmentByStatusAsync(string status);
        Task<(bool Success, string Message, List<EquipmentModel>? Equipment)> GetEquipmentNeedingMaintenanceAsync();
        Task<(bool Success, string Message, List<EquipmentModel>? Equipment)> GetEquipmentBySupplierAsync(int supplierId);
        Task<(bool Success, string Message)> UpdateEquipmentAsync(EquipmentModel equipment);
        Task<(bool Success, string Message)> UpdateEquipmentStatusAsync(int equipmentId, string newStatus);
        Task<(bool Success, string Message)> DeleteEquipmentAsync(int equipmentID);
        Task<(bool Success, string Message, int DeletedCount)> DeleteMultipleEquipmentAsync(List<int> equipmentIds);

        Task<(bool Success, string Message, List<SupplierDropdownModel>? Suppliers)> GetSuppliersForDropdownAsync();

        // NOTIFICATIONS

        Task ShowEquipmentAlertsAsync();
        Task<EquipmentAlertSummary> GetEquipmentAlertSummaryAsync();

    }
}

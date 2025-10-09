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
        Task<List<EquipmentModel>> GetEquipmentAsync();
        Task<bool> AddEquipmentAsync(EquipmentModel equipment);
        Task<bool> UpdateEquipmentAsync(EquipmentModel equipment);
        Task<bool> DeleteEquipmentAsync(int equipmentId);
        Task<List<EquipmentModel>> GetEquipmentByStatusAsync(string status);
        Task<List<EquipmentModel>> GetEquipmentNeedingMaintenanceAsync();
    }
}

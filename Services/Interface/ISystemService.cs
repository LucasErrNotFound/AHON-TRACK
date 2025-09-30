using AHON_TRACK.Models;
using AHON_TRACK.Services;
using AHON_TRACK.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface ISystemService
    {
        #region Package Settings
        // Basic method with minimal parameters (2 parameters)
        Task AddPackageAsync(string packageName, decimal price);

        // Overloaded method with all parameters (13 parameters)
        Task AddPackageAsync(string packageName, decimal price, string description,
            string duration, string features1, string features2, string features3,
            string features4, string features5, decimal discount, string discountType, string discountFor,
            DateTime validFrom, DateTime validTo);

        Task AddPackageAsync(PackageModel package);
        Task<List<Package>> GetPackagesAsync();
        Task<bool> UpdatePackageAsync(PackageModel package);
        Task<bool> DeletePackageAsync(int packageId);

        #endregion

        #region Chckin/out Settings

        Task<List<MemberPerson>> GetMemberCheckInsAsync(DateTime date);
        Task<bool> CheckInMemberAsync(int memberId);
        Task<bool> CheckOutMemberAsync(int memberCheckInId);
        Task<MemberPerson?> GetMemberCheckInByIdAsync(int memberCheckInId);

        // Walk-in check-in/out operations  
        Task<List<WalkInPerson>> GetWalkInCheckInsAsync(DateTime date);
        Task<bool> CheckOutWalkInAsync(int walkInCheckInId);
        Task<WalkInPerson?> GetWalkInCheckInByIdAsync(int walkInCheckInId);

        // Delete operations
        Task<bool> DeleteMemberCheckInAsync(int memberCheckInId);
        Task<bool> DeleteWalkInCheckInAsync(int walkInCheckInId);

        // Get available members for check-in
        Task<List<ManageMemberModel>> GetAvailableMembersForCheckInAsync();

        #endregion

        #region Inventory Settings
        Task<List<EquipmentModel>> GetEquipmentAsync();
        Task<bool> AddEquipmentAsync(EquipmentModel equipment);
        Task<bool> UpdateEquipmentAsync(EquipmentModel equipment);
        Task<bool> DeleteEquipmentAsync(int equipmentId);
        Task<List<EquipmentModel>> GetEquipmentByStatusAsync(string status);
        Task<List<EquipmentModel>> GetEquipmentNeedingMaintenanceAsync();
        #endregion
    }
}

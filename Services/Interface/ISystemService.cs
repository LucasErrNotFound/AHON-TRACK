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
        // Basic method with minimal parameters (2 parameters)
        Task AddPackageAsync(string packageName, decimal price);

        // Overloaded method with all parameters (13 parameters)
        Task AddPackageAsync(string packageName, decimal price, string description,
            int duration, string features1, string features2, string features3,
            string features4, string features5, decimal discount, string discountType,
            DateTime validFrom, DateTime validTo);

        // Method using model (1 parameter)
        Task AddPackageAsync(PackageModel package);

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
    }
}

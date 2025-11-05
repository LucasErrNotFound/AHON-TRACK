using AHON_TRACK.Models;
using AHON_TRACK.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface ICheckInOutService
    {
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
    }
}

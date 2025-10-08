using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface IMemberService
    {
        Task<List<ManageMemberModel>> GetMemberAsync();
        Task AddMemberAsync(ManageMemberModel member);
        Task<bool> DeleteMemberAsync(string memberId);
        Task<bool> DeleteMultipleMembersAsync(List<string> memberIds);
        Task<bool> UpdateMemberAsync(string memberId, ManageMemberModel member);
        Task<ManageMemberModel?> GetMemberByIdAsync(string memberId);
    }
}

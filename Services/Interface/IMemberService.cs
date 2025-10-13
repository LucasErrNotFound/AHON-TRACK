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
        Task<(bool Success, string Message, int? MemberId)> AddMemberAsync(ManageMemberModel member);
        Task<(bool Success, string Message, List<ManageMemberModel>? Members)> GetMembersAsync();
        Task<(bool Success, string Message, ManageMemberModel? Member)> GetMemberByIdAsync(int memberId);
        Task<(bool Success, string Message)> UpdateMemberAsync(ManageMemberModel member);
        Task<(bool Success, string Message)> DeleteMemberAsync(int memberId);
        Task<(bool Success, string Message)> DeleteMultipleMembersAsync(List<int> memberIds);
        Task<(bool Success, int? PackageID)> GetPackageIdByNameAsync(string packageName);
        Task<(bool Success, List<PackageModel>? Packages)> GetAllPackagesAsync();
    }
}

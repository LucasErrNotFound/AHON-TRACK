using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface IWalkInService
    {
        Task<(bool Success, string Message, int? CustomerID)> AddWalkInCustomerAsync(ManageWalkInModel walkIn, DateTime selectedDate);
        Task<(bool Success, string Message, List<ManageWalkInModel>? WalkIns)> GetAllWalkInCustomersAsync();
        Task<(bool Success, string Message, ManageWalkInModel? WalkIn)> GetWalkInCustomerByIdAsync(int customerId);
        Task<(bool Success, string Message, List<ManageWalkInModel>? WalkIns)> GetWalkInsByTypeAsync(string walkInType);
        Task<(bool Success, string Message, List<ManageWalkInModel>? WalkIns)> GetWalkInsByPackageAsync(string package);
        Task<(bool Success, string Message)> UpdateWalkInCustomerAsync(ManageWalkInModel walkIn);
        Task<(bool Success, string Message)> DeleteWalkInCustomerAsync(int customerId);
        Task<(bool Success, string Message, int DeletedCount)> DeleteMultipleWalkInCustomersAsync(List<int> customerIds);
        Task<(bool Success, bool HasUsedFreeTrial, string Message)> CheckFreeTrialEligibilityAsync(string firstName, string lastName, string? contactNumber);
        Task<List<SellingModel>> GetAvailablePackagesForWalkInAsync(string? walkInType = null);
    }
}

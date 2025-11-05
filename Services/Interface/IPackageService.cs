using AHON_TRACK.Models;
using AHON_TRACK.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface IPackageService
    {
        Task<(bool Success, string Message, int? PackageId)> AddPackageAsync(PackageModel package);
        Task<List<Package>> GetPackagesAsync();
        Task<(bool Success, string Message, PackageModel? Package)> GetPackageByIdAsync(int packageId);
        Task<(bool Success, string Message, List<Package>? Packages)> GetPackagesByDurationAsync(string duration);
        Task<bool> UpdatePackageAsync(PackageModel package);
        Task<bool> DeletePackageAsync(int packageId);
        Task<(bool Success, string Message, int DeletedCount)> DeleteMultiplePackagesAsync(List<int> packageIds);
        event EventHandler? PackagesChanged;
    }
}

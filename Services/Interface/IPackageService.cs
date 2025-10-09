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
        Task AddPackageAsync(string packageName, decimal price);
        Task AddPackageAsync(string packageName, decimal price, string description, string duration, string features1, string features2, string features3, string features4, string features5, decimal discount, string discountType, string discountFor, DateTime validFrom, DateTime validTo);
        Task AddPackageAsync(PackageModel package);
        Task<List<Package>> GetPackagesAsync();
        Task<bool> UpdatePackageAsync(PackageModel package);
        Task<bool> DeletePackageAsync(int packageId);
    }
}

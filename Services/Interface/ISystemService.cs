using AHON_TRACK.Models;
using AHON_TRACK.Services;
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
    }
}

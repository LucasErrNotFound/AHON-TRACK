using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface IProductPurchaseService
    {
        Task<List<CustomerModel>> GetAllCustomersAsync();
        Task<List<SellingModel>> GetAllProductsAsync();
        Task<List<SellingModel>> GetAllGymPackagesAsync();
    }
}

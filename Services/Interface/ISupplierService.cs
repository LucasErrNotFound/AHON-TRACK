using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface ISupplierService
    {
        Task<(bool Success, string Message, int? SupplierId)> AddSupplierAsync(SupplierManagementModel supplier);
        Task<(bool Success, string Message, List<SupplierManagementModel>? Suppliers)> GetAllSuppliersAsync();
        Task<(bool Success, string Message, SupplierManagementModel? Supplier)> GetSupplierByIdAsync(int supplierId);
        Task<(bool Success, string Message, List<SupplierManagementModel>? Suppliers)> GetSuppliersByProductTypeAsync(string productType);
        Task<(bool Success, string Message)> UpdateSupplierAsync(SupplierManagementModel supplier);
        Task<(bool Success, string Message)> UpdateSupplierStatusAsync(int supplierId, string newStatus);
        Task<(bool Success, string Message)> DeleteSupplierAsync(int supplierId);
        Task<(bool Success, string Message, int DeletedCount)> DeleteMultipleSuppliersAsync(List<int> supplierIds);
        Task<(bool Success, string Message, SupplierManagementModel Supplier)> GetSupplierByNameAsync(string supplierName);
    }
}

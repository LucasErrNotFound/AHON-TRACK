using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface IProductService
    {
        Task<List<ProductModel>> GetProductsAsync();
        Task<bool> AddProductAsync(ProductModel product);
        Task<bool> UpdateProductAsync(ProductModel product);
        Task<bool> DeleteProductAsync(int productId);
        Task<List<ProductModel>> GetProductsByCategoryAsync(string category);
        Task<List<ProductModel>> GetProductsByStatusAsync(string status);
        Task<List<ProductModel>> GetExpiredProductsAsync();
        Task<List<ProductModel>> GetProductsExpiringSoonAsync(int daysThreshold = 30);
        Task<ProductModel?> GetProductByIdAsync(int productId);
        Task<ProductModel?> GetProductBySKUAsync(string sku);
    }
}

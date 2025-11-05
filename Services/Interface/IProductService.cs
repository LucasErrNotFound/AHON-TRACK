using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AHON_TRACK.Services.ProductService;

namespace AHON_TRACK.Services.Interface
{
    public interface IProductService
    {
        Task<(bool Success, string Message, int? ProductId)> AddProductAsync(ProductModel product);
        Task<(bool Success, string Message, List<ProductModel>? Products)> GetAllProductsAsync();
        Task<(bool Success, string Message, ProductModel? Product)> GetProductByIdAsync(int productId);
        Task<(bool Success, string Message, ProductModel? Product)> GetProductByBatchCodeAsync(string sku);
        Task<(bool Success, string Message, List<ProductModel>? Products)> GetProductsByCategoryAsync(string category);
        Task<(bool Success, string Message, List<ProductModel>? Products)> GetProductsByStatusAsync(string status);
        Task<(bool Success, string Message, List<ProductModel>? Products)> GetExpiredProductsAsync();
        Task<(bool Success, string Message, List<ProductModel>? Products)> GetProductsExpiringSoonAsync(int daysThreshold = 30);
        Task<(bool Success, string Message)> UpdateProductAsync(ProductModel product);
        Task<(bool Success, string Message)> UpdateProductStockAsync(int productId, int newStock);
        Task<(bool Success, string Message)> DeleteProductAsync(int productId);
        Task<(bool Success, string Message, int DeletedCount)> DeleteMultipleProductsAsync(List<int> productIds);

        // NOTIFICATIONS
        void RegisterNotificationCallback(Action<Notification> callback);
        void UnRegisterNotificationCallback();
        Task ShowProductAlertsAsync(Action<Notification>? addNotificationCallback = null);
        Task<ProductAlertSummary> GetProductAlertSummaryAsync();
    }
}

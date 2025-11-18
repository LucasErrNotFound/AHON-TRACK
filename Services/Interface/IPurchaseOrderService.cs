using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    /// <summary>
    /// Service interface for Purchase Order operations
    /// </summary>
    public interface IPurchaseOrderService
    {
        // CREATE
        Task<(bool Success, string Message, int? POId)> CreatePurchaseOrderAsync(PurchaseOrderModel purchaseOrder);

        // READ
        Task<(bool Success, string Message, List<PurchaseOrderModel>? PurchaseOrders)> GetAllPurchaseOrdersAsync();
        Task<(bool Success, string Message, PurchaseOrderModel? PurchaseOrder)> GetPurchaseOrderByIdAsync(int poId);
        Task<(bool Success, string Message, PurchaseOrderModel? PurchaseOrder)> GetPurchaseOrderByPONumberAsync(string poNumber);
        Task<(bool Success, string Message, List<PurchaseOrderModel>? PurchaseOrders)> GetPurchaseOrdersBySupplierAsync(int supplierId);
        Task<(bool Success, string Message, List<PurchaseOrderModel>? PurchaseOrders)> GetPurchaseOrdersByStatusAsync(string status);

        // UPDATE
        Task<(bool Success, string Message)> UpdatePurchaseOrderAsync(PurchaseOrderModel purchaseOrder);
        Task<(bool Success, string Message)> UpdatePurchaseOrderStatusAsync(
            int poId,
            string? shippingStatus,
            string? paymentStatus,
            string? invoiceNumber);
        
        Task<(bool Success, string Message)> UpdatePurchaseOrderQuantitiesAsync(
            int purchaseOrderId, 
            List<PurchaseOrderItemModel> items);

        // DELETE
        Task<(bool Success, string Message)> DeletePurchaseOrderAsync(int poId);

        // SPECIAL OPERATIONS
        Task<(bool Success, string Message)> SendToInventoryAsync(int poId);
        Task<string> GeneratePONumberAsync();

        // STATISTICS
        Task<(bool Success, int TotalPOs, int PendingPOs, int DeliveredPOs)> GetPurchaseOrderStatisticsAsync();
        Task<(bool Success, string Message)> MarkAsDeliveredAsync(int purchaseOrderId);

    }
}

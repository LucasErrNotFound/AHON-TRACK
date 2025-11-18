using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    /// <summary>
    /// Represents a Purchase Order in the system
    /// </summary>
    public class PurchaseOrderModel
    {
        // ... existing properties ...

        public int PurchaseOrderID { get; set; }
        public string PONumber { get; set; }
        public int? SupplierID { get; set; }
        public string? SupplierName { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime ExpectedDeliveryDate { get; set; }
        public string ShippingStatus { get; set; }
        public string PaymentStatus { get; set; }
        public string? InvoiceNumber { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public int? CreatedByEmployeeID { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        public string Category { get; set; }

        // ⭐ NEW: Sent to inventory tracking fields
        public bool SentToInventory { get; set; }
        public DateTime? SentToInventoryDate { get; set; }
        public int? SentToInventoryBy { get; set; }

        public List<PurchaseOrderItemModel> Items { get; set; } = new();

        // ⭐ UPDATED: Include SentToInventory check
        public bool CanSendToInventory =>
            !SentToInventory && // ⭐ Prevent duplicate sends
            ShippingStatus?.Equals("Delivered", StringComparison.OrdinalIgnoreCase) == true &&
            PaymentStatus?.Equals("Paid", StringComparison.OrdinalIgnoreCase) == true;
    }

    public class PurchaseOrderItemModel
    {
        // Primary fields
        public int POItemID { get; set; }
        public int PurchaseOrderID { get; set; }
        public string? ItemID { get; set; }  // ⭐ Make nullable
        public string ItemName { get; set; }
        public string Unit { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string? Category { get; set; }  // ⭐ Make nullable
        public string? BatchCode { get; set; }  // ⭐ Make nullable
        
        // Additional fields for products
        public decimal SupplierPrice { get; set; }
        public decimal MarkupPrice { get; set; }
        public decimal SellingPrice { get; set; }
        
        // Receiving tracking
        public int QuantityReceived { get; set; }
        
        // Computed property
        public decimal LineTotal => Quantity * Price;
    }
}

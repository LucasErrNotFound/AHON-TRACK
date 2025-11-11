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
        public int PurchaseOrderID { get; set; }
        public string PONumber { get; set; } = string.Empty;
        public int? SupplierID { get; set; }
        public string? SupplierName { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Today;
        public DateTime ExpectedDeliveryDate { get; set; }
        public string ShippingStatus { get; set; } = "Pending";
        public string PaymentStatus { get; set; } = "Unpaid";
        public string? InvoiceNumber { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxRate { get; set; } = 0.12m; // 12% VAT
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public bool IsDeleted { get; set; }
        public int? CreatedByEmployeeID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public List<PurchaseOrderItemModel> Items { get; set; } = new();

        // Business logic properties
        public bool CanSendToInventory =>
            ShippingStatus == "Delivered" && PaymentStatus == "Paid";

        public bool IsCompleted => ShippingStatus == "Delivered" && PaymentStatus == "Paid";

        public bool IsCancelled => ShippingStatus == "Cancelled" || PaymentStatus == "Cancelled";
    }

    /// <summary>
    /// Represents an item in a Purchase Order
    /// </summary>
    public class PurchaseOrderItemModel
    {
        public int POItemID { get; set; }
        public int PurchaseOrderID { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Unit { get; set; } = "pcs";
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal LineTotal => Quantity * Price;
    }
}

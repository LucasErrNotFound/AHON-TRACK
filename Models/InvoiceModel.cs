using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class InvoiceModel
    {
        public int ID { get; set; }
        public string InvoiceId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string PurchasedItem { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Amount { get; set; }
        public decimal? TenderedPrice { get; set; }
        public decimal? Change { get; set; }
        public DateTime DatePurchased { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ReferenceNumber { get; set; }
    }
}

using ShimSkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class RecentPurchaseModel
    {
        public int SaleID { get; set; }
        public DateTime PurchaseDate { get; set; }
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerType { get; set; } = string.Empty;
        public byte[]? AvatarSource { get; set; }
    }
}

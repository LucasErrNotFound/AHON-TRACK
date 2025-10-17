using ShimSkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class MBRecentActivity
    {
        public int? CustomerID { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerType { get; set; }
        public string? ProductName { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public DateTime? PurchaseTime { get; set; }
        public decimal Amount { get; set; }
        public byte[]? ProfilePicture { get; set; }
    }
}

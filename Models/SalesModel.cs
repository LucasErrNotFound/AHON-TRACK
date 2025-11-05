using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class SalesModel
    {
        public int SaleID { get; set; }
        public DateTime SaleDate { get; set; }
        public int? PackageID { get; set; }
        public int? ProductID { get; set; }
        public int? CustomerID { get; set; }
        public int? MemberID { get; set; }
        public int Quantity { get; set; }
        public decimal Amount { get; set; }
        public int RecordedBy { get; set; }
        public string? ItemType => PackageID.HasValue ? "Gym Package" : "Product";
        public string? BuyerType => MemberID.HasValue ? "Gym Member" : "Walk-in";
    }
}

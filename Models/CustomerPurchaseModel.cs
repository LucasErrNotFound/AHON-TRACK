using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class CustomerPurchaseModel
    {
        public int PurchaseID { get; set; }
        public int CustomerID { get; set; }
        public string? CustomerType { get; set; }
        public int SellingID { get; set; }
        public string? Category { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.Now;
    }
}

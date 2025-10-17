using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class DailySalesModel
    {
        public int DailySalesID { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal TotalSales { get; set; }
        public int TotalTransactions { get; set; }
        public DateTime TransactionCreatedDate { get; set; }
        public DateTime? TransactionUpdatedDate { get; set; }
        public int TransactionByEmployeeID { get; set; }
    }
}

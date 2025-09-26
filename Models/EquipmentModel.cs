using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class EquipmentModel
    {
        public string EquipmentID { get; set; }
        public string EquipmentName { get; set; }
        public string Category { get; set; }
        public int CurrentStock { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchasePrice { get; set; }
        public string Supplier { get; set; }
        public DateTime? WarrantyExpiry { get; set; }
        public string Condition { get; set; }
        public string Status { get; set; }
        public DateTime? LastMaintenance { get; set; }
        public DateTime? NextMaintenance { get; set; }
    }
}

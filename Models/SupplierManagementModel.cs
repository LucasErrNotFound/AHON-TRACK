using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class SupplierManagementModel
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; }
        public string ContactPerson { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Products { get; set; }
        public string Status { get; set; }
        public bool IsSelected { get; set; }
        private int AddedByEmployeeID { get; set; }
    }
}

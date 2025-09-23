using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class PackageModel
    {
        public int packageID { get; set; }
        public string packageName { get; set; }
        public decimal price { get; set; }
        public string description { get; set; }
        public string duration { get; set; }
        public string features1 { get; set; }
        public string features2 { get; set; }
        public string features3 { get; set; }
        public string features4 { get; set; }
        public string features5 { get; set; }
        public decimal discount { get; set; }
        public string discountType { get; set; }
        public string discountFor { get; set; }
        public decimal discountedPrice { get; set; }
        public DateTime validFrom { get; set; }
        public DateTime validTo { get; set; }
    }
}

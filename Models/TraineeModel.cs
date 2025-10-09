using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class TraineeModel
    {
        public int ID { get; set; }
        public string Picture { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string PackageType { get; set; } = string.Empty;
        public int SessionLeft { get; set; }
        public bool IsSelected { get; set; }
        public string PicturePath { get; set; } = string.Empty;
        public int AddedByEmployeeID { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class WalkInSession
    {
        public int SessionID { get; set; }
        public int CustomerID { get; set; }
        public int PackageID { get; set; }
        public int SessionsLeft { get; set; }
        public DateTime StartDate { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class MemberModel
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string ContactNumber { get; set; }
        public string MembershipType { get; set; }
        public string Status { get; set; }
        public string Validity { get; set; }
        public object? AvatarSource { get; set; } = "avares://AHON_TRACK/Assets/MainWindowView/user.png";
    }
}

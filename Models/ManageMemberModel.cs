using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class ManageMemberModel
    {
        public int MemberID { get; set; }
        public string Name { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string MiddleInitial { get; set; }

        public string LastName { get; set; } = string.Empty;
        public string Gender { get; set; }        // must be "Male" or "Female"
        public object? ProfilePicture { get; set; } = "avares://AHON_TRACK/Assets/MainWindowView/user.png";
        public string? ContactNumber { get; set; }
        public string MembershipType { get; set; }
        public int? Age { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Validity { get; set; }
        public string Status { get; set; } = "Active";      // must be "Active" | "Inactive" | "Terminated"
        public string? PaymentMethod { get; set; }
        public int AddedByEmployeeID { get; set; }
    }
}

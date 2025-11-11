using AHON_TRACK.Converters;
using Avalonia.Media.Imaging;
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
        public byte[] AvatarBytes { get; set; }
        public Bitmap AvatarSource { get; set; }
        public string? Name { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleInitial { get; set; }
        public DateTime? DateJoined { get; set; }
        public string? LastName { get; set; } = string.Empty;
        public string? Gender { get; set; }        // must be "Male" or "Female"
        public byte[]? ProfilePicture { get; set; }
        public Bitmap? ProfileImageSource { get; set; }
        public static Bitmap DefaultAvatarSource => ImageHelper.GetDefaultAvatar();
        public string? ConsentLetter { get; set; }
        public string? ContactNumber { get; set; }
        public int? PackageID { get; set; }
        public string? MembershipType { get; set; }
        public string? CustomerType { get; set; }
        public int? Age { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? ValidUntil { get; set; }
        public string Status { get; set; } = "Active";      // must be "Active" | "Inactive" | "Terminated"
        public string? PaymentMethod { get; set; }
        public string? ReferenceNumber { get; set; }
        public int RegisteredByEmployeeID { get; set; }

        public DateTime? LastCheckIn { get; set; }
        public DateTime? LastCheckOut { get; set; }
        public string? RecentPurchaseItem { get; set; }
        public DateTime? RecentPurchaseDate { get; set; }
        public int? RecentPurchaseQuantity { get; set; }

    }
}

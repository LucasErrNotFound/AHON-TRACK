using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using AHON_TRACK.Converters;
using System.IO;

namespace AHON_TRACK.Models
{
    public class ManageEmployeeModel
    {
        public string ID { get; set; }
        public byte[] AvatarBytes { get; set; }
        public Bitmap AvatarSource { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string ContactNumber { get; set; }
        public string Position { get; set; }
        public string Status { get; set; }
        public DateTime DateJoined { get; set; }

        // Default avatar property (can be a file path, URI, or resource)
        public static Bitmap DefaultAvatarSource => ImageHelper.GetDefaultAvatar();

        public string Age { get; set; }
        public string Birthdate { get; set; }
        public string CityProvince { get; set; }
        public string Gender { get; set; }
        public string HouseAddress { get; set; }
        public string HouseNumber { get; set; }
        public string Street { get; set; }
        public string Barangay { get; set; }
        public string ZipCode { get; set; }

        public string LastLogin { get; set; } = "Never logged in";
    }
}

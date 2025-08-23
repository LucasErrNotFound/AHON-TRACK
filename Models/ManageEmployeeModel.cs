using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using AHON_TRACK.Converters;

namespace AHON_TRACK.Models
{
    public class ManageEmployeeModel
    {
        public string ID { get; set; }
        public Bitmap? AvatarSource { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string ContactNumber { get; set; }
        public string Position { get; set; }
        public string Status { get; set; }
        public DateTime DateJoined { get; set; }

        // Default avatar property (can be a file path, URI, or resource)
        public static Bitmap DefaultAvatarSource => ImageHelper.GetDefaultAvatar();

        public Bitmap GetAvatarOrDefault()
        {
            return AvatarSource ?? DefaultAvatarSource;
        }
    }
}

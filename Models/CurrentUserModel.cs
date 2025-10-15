using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class CurrentUserModel
    {
        public static int? UserId { get; set; }
        public static string Username { get; set; }
        public static string Role { get; set; }
        public static int LoginCount { get; set; }
    }
}

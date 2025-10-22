using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class CurrentUserModel : ManageEmployeeModel
    {
        public static int? UserId { get; set; }
        public static string? Username { get; set; }
        public static string? Role { get; set; }
        public static int LoginCount { get; set; }
        public static DateTime? LastLogin { get; set; }
        public static int? employeeID { get; set; }
        
        public new static byte[]? AvatarBytes { get; set; }
    }
}

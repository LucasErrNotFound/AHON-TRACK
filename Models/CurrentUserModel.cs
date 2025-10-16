using AHON_TRACK.Converters;
using Avalonia.Media.Imaging;

namespace AHON_TRACK.Models;

public class CurrentUserModel : ManageEmployeeModel
{
    public static int? UserId { get; set; }
    public static string Name { get; set; }
    public static string Username { get; set; }
    public static string Role { get; set; }
    public static int LoginCount { get; set; }
    
    public static int Age { get; set; }
    public static string DateOfBirth { get; set; } = string.Empty;
    public static string ContactNumber { get; set; } = string.Empty;
    public static string Gender { get; set; } = string.Empty;
    public static string LastLogin { get; set; } = "Never logged in";
    public static string HouseAddress { get; set; } = string.Empty;
    public static string HouseNumber { get; set; } = string.Empty;
    public static string Street { get; set; } = string.Empty;
    public static string Barangay { get; set; } = string.Empty;
    public static string CityTown { get; set; } = string.Empty;
    public static string ZipCode { get; set; } = string.Empty;
    public static System.DateTime? DateJoined { get; set; } = null;

    // Optional: avatar bytes/source if you want to surface profile picture globally
    public new static byte[]? AvatarBytes { get; set; }
}
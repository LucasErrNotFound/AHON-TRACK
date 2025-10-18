using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace AHON_TRACK.Models
{
    public class TrainingModel
    {
        public int trainingID { get; set; }
        public int customerID { get; set; }  // Changed from memberID
        public string customerType { get; set; } = string.Empty;  // "Member" or "WalkIn"
        public string firstName { get; set; } = string.Empty;
        public string lastName { get; set; } = string.Empty;
        public string contactNumber { get; set; } = string.Empty;
        public string? picture { get; set; }
        public int packageID { get; set; }  // Added
        public string packageType { get; set; } = string.Empty;  // Package name
        public string assignedCoach { get; set; } = string.Empty;
        public DateTime scheduledDate { get; set; }
        public DateTime scheduledTimeStart { get; set; }
        public DateTime scheduledTimeEnd { get; set; }
        public string attendance { get; set; } = string.Empty;
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
        public int AddedByEmployeeID { get; set; }
    }
}

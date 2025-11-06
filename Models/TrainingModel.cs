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
        public int customerID { get; set; }
        public string customerType { get; set; } = string.Empty;
        public string firstName { get; set; } = string.Empty;
        public string lastName { get; set; } = string.Empty;
        public string contactNumber { get; set; } = string.Empty;
        public int packageID { get; set; }
        public string packageType { get; set; } = string.Empty;
        public string assignedCoach { get; set; } = string.Empty;
        public int coachID { get; set; }
        public DateTime scheduledDate { get; set; }
        public DateTime scheduledTimeStart { get; set; }
        public DateTime scheduledTimeEnd { get; set; }
        public string attendance { get; set; } = string.Empty;
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
        public int addedByEmployeeID { get; set; }
        public int ScheduleID { get; set; }
    }
}

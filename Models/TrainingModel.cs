using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class TrainingModel
    {
        public int trainingID { get; set; }
        public int memberID { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }
        public string contactNumber { get; set; }
        public string picture { get; set; }
        public string packageType { get; set; }
        public string assignedCoach { get; set; }
        public DateTime scheduledDate { get; set; }
        public DateTime scheduledTimeStart { get; set; }
        public DateTime scheduledTimeEnd { get; set; }
        public string attendance { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }
}

using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class ManageWalkInModel
    {
        public int WalkInID { get; set; }
        public string? Name { get; set; } // For display if fullname is required.
        public string? FirstName { get; set; }
        public string? MiddleInitial { get; set; }
        public string? LastName { get; set; }
        public string? ContactNumber { get; set; }
        public int Age { get; set; }
        public string? Gender { get; set; }
        public string? WalkInType { get; set; }
        public string? WalkInPackage { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? ConsentLetter { get; set; }
        public int? Quantity { get; set; } // If the Walk In Package choosen is Boxing, Muay Thai, Cross Fit
        public decimal? TotalAmount { get; set; }

        // For WalkInRecords
        public int? RecordID { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public string? Attendance { get; set; }

        //Foreign key
        public int? RegisteredByEmployeeID { get; set; }

        public decimal Amount { get; set; }
    }
}

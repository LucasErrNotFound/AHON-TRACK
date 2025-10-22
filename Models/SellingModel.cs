using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class SellingModel
    {
        public int SellingID { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int Quantity { get; set; } = 1;
        public byte[]? ImagePath { get; set; }
        public decimal TotalPrice => Price * Quantity;
        public string? Features { get; set; }
    }
}

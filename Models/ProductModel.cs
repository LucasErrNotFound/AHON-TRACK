using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Models
{
    public class ProductModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string SKU { get; set; }
        public string ProductSupplier { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public bool IsPercentageDiscount { get; set; }
        public string ProductImagePath { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Status { get; set; }
        public string Category { get; set; }

        public int CurrentStock { get; set; }

        // Computed property for final price
        public decimal FinalPrice
        {
            get
            {
                if (DiscountedPrice.HasValue && DiscountedPrice.Value > 0)
                {
                    if (IsPercentageDiscount)
                    {
                        return Price - (Price * (DiscountedPrice.Value / 100));
                    }
                    return DiscountedPrice.Value;
                }
                return Price;
            }
        }

        // Check if product is expired
        public bool IsExpired
        {
            get
            {
                return ExpiryDate.HasValue && ExpiryDate.Value < DateTime.Today;
            }
        }

        // Check if discount is active
        public bool HasDiscount
        {
            get
            {
                return DiscountedPrice.HasValue && DiscountedPrice.Value > 0;
            }
        }

        // Get discount amount
        public decimal DiscountAmount
        {
            get
            {
                if (!HasDiscount) return 0;

                if (IsPercentageDiscount)
                {
                    return Price * (DiscountedPrice.Value / 100);
                }
                return Price - DiscountedPrice.Value;
            }
        }
    }
}
using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Converters
{
    public static class CartItemsExtensions
    {
        public static SellingModel ToSellingModel(this CartItemModel item)
        {
            return new SellingModel
            {
                SellingID = item.SellingID,
                Title = item.Title,
                Category = item.Category,
                Price = item.Price,
                Quantity = item.Quantity
            };
        }

        public static List<SellingModel> ToSellingModelList(this IEnumerable<CartItemModel> items)
        {
            return items.Select(item => item.ToSellingModel()).ToList();
        }
    }
}

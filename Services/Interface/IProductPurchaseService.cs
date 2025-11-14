using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AHON_TRACK.Services.ProductPurchaseService;

namespace AHON_TRACK.Services.Interface
{
    public interface IProductPurchaseService
    {
        Task<bool> ProcessPaymentAsync(List<SellingModel> cartItems, CustomerModel customer, int employeeId, string paymentMethod, string? referenceNumber = null);
        Task<List<CustomerModel>> GetAllCustomersAsync();
        Task<List<SellingModel>> GetAllProductsAsync();
        Task<List<SellingModel>> GetAllGymPackagesAsync();
        Task<List<InvoiceModel>> GetInvoicesByDateAsync(DateTime date);
        Task<List<RecentPurchaseModel>> GetRecentPurchasesAsync(int limit = 50);
        Task<string> GenerateInvoiceNumberAsync();
        Task<bool> InvoiceNumberExistsAsync(string invoiceNumber);

        (bool IsValid, string ErrorMessage) ValidatePaymentReferenceNumber(string paymentMethod, string? referenceNumber);
    }
}

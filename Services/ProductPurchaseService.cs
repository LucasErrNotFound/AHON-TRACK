using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services
{
    public class ProductPurchaseService : IProductPurchaseService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public ProductPurchaseService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        #region Role-Based Access Control

        private bool CanCreate()
        {
            // Both Admin and Staff can create
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanUpdate()
        {
            // Only Admin can update
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanDelete()
        {
            // Only Admin can delete
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanView()
        {
            // Both Admin and Staff can view
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true;
        }

        #endregion

        #region CREATE

        #endregion


        #region READ

        public async Task<List<SellingModel>> GetAllGymPackagesAsync()
        {
            var packages = new List<SellingModel>();

            try
            {
                if (!CanView())
                {
                    _toastManager.CreateToast("Access Denied")
                        .WithContent("You do not have permission to view gym packages.")
                        .ShowError();
                    return packages;
                }

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
            SELECT 
                PackageID,
                PackageName,
                Description,
                Price,
                Duration,
                Features,
                Discount,
                DiscountType,
                DiscountFor,
                DiscountedPrice,
                ValidFrom,
                ValidTo
            FROM Packages
            WHERE GETDATE() BETWEEN ValidFrom AND ValidTo;";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    packages.Add(new SellingModel
                    {
                        SellingID = reader.GetInt32(reader.GetOrdinal("PackageID")),
                        Title = reader["PackageName"].ToString(),
                        Description = reader["Description"]?.ToString(),
                        Category = "Gym Package",
                        Price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0,
                        Stock = 1, // packages aren’t physical stock
                        ImagePath = null
                    });
                }

                return packages;
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Load Packages Failed")
                    .WithContent(ex.Message)
                    .ShowError();
                return packages;
            }
        }

        public async Task<List<SellingModel>> GetAllProductsAsync()
        {
            var products = new List<SellingModel>();

            try
            {
                if (!CanView())
                {
                    _toastManager.CreateToast("Access Denied")
                        .WithContent("You do not have permission to view products.")
                        .ShowError();
                    return products;
                }

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
            SELECT 
                ProductID,
                ProductName,
                Description,
                Category,
                Price,
                CurrentStock,
                ProductImagePath
            FROM Products
            WHERE Status = 'In Stock';";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    products.Add(new SellingModel
                    {
                        SellingID = reader.GetInt32(reader.GetOrdinal("ProductID")),
                        Title = reader["ProductName"].ToString(),
                        Description = reader["Description"]?.ToString(),
                        Category = reader["Category"]?.ToString(),
                        Price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0,
                        Stock = reader["CurrentStock"] != DBNull.Value ? Convert.ToInt32(reader["CurrentStock"]) : 0,
                        ImagePath = reader["ProductImagePath"] != DBNull.Value ? (byte[])reader["ProductImagePath"] : null
                    });
                }

                return products;
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Load Products Failed")
                    .WithContent(ex.Message)
                    .ShowError();
                return products;
            }
        }


        public async Task<List<CustomerModel>> GetAllCustomersAsync()
        {
            var customers = new List<CustomerModel>();

            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to view customer data.")
                    .ShowError();
                return customers;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        MemberID AS ID,
                        FirstName,
                        LastName,
                        CustomerType
                    FROM Members

                    UNION ALL

                    SELECT 
                        CustomerID AS ID,
                        FirstName,
                        LastName,
                        CustomerType
                    FROM WalkInCustomers;
                ";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var customer = new CustomerModel
                    {
                        CustomerID = reader["ID"] is int id ? id : 0,
                        FirstName = reader["FirstName"]?.ToString() ?? "",
                        LastName = reader["LastName"]?.ToString() ?? "",
                        CustomerType = reader["CustomerType"]?.ToString() ?? ""
                    };
                    customers.Add(customer);
                }
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent(ex.Message)
                    .ShowError();
            }

            return customers;
        }

        #endregion


        #region UPDATE



        #endregion


        #region DELETE



        #endregion


        #region UTILITY



        #endregion
    }
}

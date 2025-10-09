using AHON_TRACK.Models;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AHON_TRACK.Services.Interface;

namespace AHON_TRACK.Services
{
    public class ProductService : IProductService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public ProductService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                using var logCmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime) 
              VALUES (@username, @role, @actionType, @description, @success, GETDATE())", conn);

                logCmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@success", success);

                await logCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        public async Task<List<ProductModel>> GetProductsAsync()
        {
            var products = new List<ProductModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                ORDER BY ProductName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load products: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }
            return products;
        }

        public async Task<bool> AddProductAsync(ProductModel product)
        {
            product.Status = product.CurrentStock > 0 ? "In Stock" : "Out Of Stock";

            const string query = @"
        INSERT INTO Products (ProductName, SKU, ProductSupplier, Description, 
                             Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                             ExpiryDate, Status, Category, CurrentStock)
        OUTPUT INSERTED.ProductID
        VALUES (@ProductName, @SKU, @ProductSupplier, @Description,
                @Price, @DiscountedPrice, @IsPercentageDiscount, @ProductImagePath,
                @ExpiryDate, @Status, @Category, @CurrentStock)";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductName", product.ProductName);
                        command.Parameters.AddWithValue("@SKU", product.SKU);
                        command.Parameters.AddWithValue("@ProductSupplier", (object)product.ProductSupplier ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Description", (object)product.Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Price", product.Price);
                        command.Parameters.AddWithValue("@DiscountedPrice", (object)product.DiscountedPrice ?? DBNull.Value);
                        command.Parameters.AddWithValue("@IsPercentageDiscount", product.IsPercentageDiscount);
                        command.Parameters.AddWithValue("@ProductImagePath", (object)product.ProductImagePath ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ExpiryDate", (object)product.ExpiryDate ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Status", product.Status);
                        command.Parameters.AddWithValue("@Category", product.Category);
                        command.Parameters.AddWithValue("@CurrentStock", product.CurrentStock);

                        var newId = await command.ExecuteScalarAsync();

                        if (newId != null && newId != DBNull.Value)
                        {
                            product.ProductID = Convert.ToInt32(newId);

                            string logDescription = $"Added product: '{product.ProductName}' (SKU: {product.SKU}) - Price: ₱{product.Price:N2}, Stock: {product.CurrentStock}";
                            if (product.HasDiscount)
                            {
                                logDescription += $", Discount: {product.DiscountedPrice}{(product.IsPercentageDiscount ? "%" : " fixed")}, Final Price: ₱{product.FinalPrice:N2}";
                            }

                            await LogActionAsync(connection, "Add Product", logDescription, true);

                            _toastManager?.CreateToast("Product Added")
                                .WithContent($"Product '{product.ProductName}' added successfully!")
                                .ShowSuccess();
                            return true;
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to add product",
                        $"Failed to add product '{product.ProductName}' - SQL Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to add product: {ex.Message}")
                    .ShowError();
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to add product",
                        $"Failed to add product '{product.ProductName}' - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error adding product: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> UpdateProductAsync(ProductModel product)
        {

            product.Status = product.CurrentStock > 0 ? "In Stock" : "Out Of Stock";

            const string query = @"
        UPDATE Products SET 
            ProductName = @ProductName,
            SKU = @SKU,
            ProductSupplier = @ProductSupplier,
            Description = @Description,
            Price = @Price,
            DiscountedPrice = @DiscountedPrice,
            IsPercentageDiscount = @IsPercentageDiscount,
            ProductImagePath = @ProductImagePath,
            ExpiryDate = @ExpiryDate,
            Status = @Status,
            Category = @Category,
            CurrentStock = @CurrentStock
        WHERE ProductID = @ProductID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductID", product.ProductID);
                        command.Parameters.AddWithValue("@ProductName", product.ProductName);
                        command.Parameters.AddWithValue("@SKU", product.SKU);
                        command.Parameters.AddWithValue("@ProductSupplier", (object)product.ProductSupplier ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Description", (object)product.Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Price", product.Price);
                        command.Parameters.AddWithValue("@DiscountedPrice", (object)product.DiscountedPrice ?? DBNull.Value);
                        command.Parameters.AddWithValue("@IsPercentageDiscount", product.IsPercentageDiscount);
                        command.Parameters.AddWithValue("@ProductImagePath", (object)product.ProductImagePath ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ExpiryDate", (object)product.ExpiryDate ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Status", product.Status);
                        command.Parameters.AddWithValue("@Category", product.Category);
                        command.Parameters.AddWithValue("@CurrentStock", product.CurrentStock);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Update Product",
                                $"Updated product: '{product.ProductName}' (ID: {product.ProductID})", true);

                            _toastManager?.CreateToast("Product Updated")
                                .WithContent($"Product '{product.ProductName}' updated successfully!")
                                .ShowSuccess();
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to update product",
                        $"Failed to update product '{product.ProductName}' - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error updating product: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> DeleteProductAsync(int productID)
        {
            const string query = "DELETE FROM Products WHERE ProductID = @ProductID";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var productName = await GetProductNameByIdAsync(connection, productID);

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductID", productID);
                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            await LogActionAsync(connection, "Delete Product",
                                $"Deleted product: '{productName}' (ID: {productID})", true);

                            _toastManager?.CreateToast("Product Deleted")
                                .WithContent($"Product '{productName}' deleted successfully!")
                                .ShowSuccess();
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await LogActionAsync(connection, "Failed to delete product",
                        $"Failed to delete product ID {productID} - Error: {ex.Message}", false);
                }
                _toastManager?.CreateToast("Error")
                    .WithContent($"Error deleting product: {ex.Message}")
                    .ShowError();
            }
            return false;
        }

        public async Task<List<ProductModel>> GetProductsByCategoryAsync(string category)
        {
            var products = new List<ProductModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE Category = @Category
                ORDER BY ProductName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Category", category);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load products by category: {ex.Message}")
                    .ShowError();
            }
            return products;
        }

        public async Task<List<ProductModel>> GetProductsByStatusAsync(string status)
        {
            var products = new List<ProductModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE Status = @Status
                ORDER BY ProductName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Status", status);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load products by status: {ex.Message}")
                    .ShowError();
            }
            return products;
        }

        public async Task<List<ProductModel>> GetExpiredProductsAsync()
        {
            var products = new List<ProductModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE ExpiryDate IS NOT NULL 
                  AND ExpiryDate < CAST(GETDATE() AS DATE)
                ORDER BY ExpiryDate DESC";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load expired products: {ex.Message}")
                    .ShowError();
            }
            return products;
        }

        public async Task<List<ProductModel>> GetProductsExpiringSoonAsync(int daysThreshold = 30)
        {
            var products = new List<ProductModel>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE ExpiryDate IS NOT NULL 
                  AND ExpiryDate >= CAST(GETDATE() AS DATE)
                  AND ExpiryDate <= DATEADD(day, @DaysThreshold, CAST(GETDATE() AS DATE))
                ORDER BY ExpiryDate";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@DaysThreshold", daysThreshold);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load expiring products: {ex.Message}")
                    .ShowError();
            }
            return products;
        }

        public async Task<ProductModel?> GetProductByIdAsync(int productID)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE ProductID = @ProductID";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ProductID", productID);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load product: {ex.Message}")
                    .ShowError();
            }
            return null;
        }

        public async Task<ProductModel?> GetProductBySKUAsync(string sku)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    const string query = @"
                SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                       Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                       ExpiryDate, Status, Category, CurrentStock
                FROM Products 
                WHERE SKU = @SKU";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SKU", sku);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new ProductModel
                                {
                                    ProductID = reader.GetInt32("ProductID"),
                                    ProductName = reader["ProductName"]?.ToString() ?? "",
                                    SKU = reader["SKU"]?.ToString() ?? "",
                                    ProductSupplier = reader["ProductSupplier"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    Price = reader.GetDecimal("Price"),
                                    DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value ? reader.GetDecimal("DiscountedPrice") : null,
                                    IsPercentageDiscount = reader.GetBoolean("IsPercentageDiscount"),
                                    ProductImagePath = reader["ProductImagePath"]?.ToString() ?? "",
                                    ExpiryDate = reader["ExpiryDate"] != DBNull.Value ? reader.GetDateTime("ExpiryDate") : null,
                                    Status = reader["Status"]?.ToString() ?? "",
                                    Category = reader["Category"]?.ToString() ?? "",
                                    CurrentStock = reader.GetInt32("CurrentStock")
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load product by SKU: {ex.Message}")
                    .ShowError();
            }
            return null;
        }

        private async Task<string> GetProductNameByIdAsync(SqlConnection connection, int productID)
        {
            const string query = "SELECT ProductName FROM Products WHERE ProductID = @ProductID";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@ProductID", productID);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? "Unknown Product";
            }
        }
    }
}

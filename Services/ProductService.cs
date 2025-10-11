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

        public async Task<(bool Success, string Message, int? ProductId)> AddProductAsync(ProductModel product)
        {
            if (!CanCreate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You don't have permission to add products.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to add products.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                product.Status = product.CurrentStock > 0 ? "In Stock" : "Out Of Stock";

                // Check for duplicate SKU
                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Products WHERE SKU = @sku", conn);
                checkCmd.Parameters.AddWithValue("@sku", product.SKU ?? (object)DBNull.Value);

                var count = (int)await checkCmd.ExecuteScalarAsync();
                if (count > 0)
                {
                    _toastManager.CreateToast("Duplicate SKU")
                        .WithContent($"Product with SKU '{product.SKU}' already exists.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Product SKU already exists.", null);
                }

                // ✅ Handle image properly
                byte[]? imageBytes = null;
                if (!string.IsNullOrEmpty(product.ProductImageFilePath))
                {
                    try
                    {
                        if (System.IO.File.Exists(product.ProductImageFilePath))
                        {
                            imageBytes = await System.IO.File.ReadAllBytesAsync(product.ProductImageFilePath);
                            Console.WriteLine($"Successfully read image: {imageBytes.Length} bytes");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to read image: {ex.Message}");
                    }
                }

                // ✅ UPDATED: Use SupplierID instead of ProductSupplier
                using var cmd = new SqlCommand(
                    @"INSERT INTO Products (ProductName, SKU, SupplierID, Description, 
                     Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                     ExpiryDate, Status, Category, CurrentStock, AddedByEmployeeID)
              OUTPUT INSERTED.ProductID
              VALUES (@productName, @sku, @supplierID, @description,
                      @price, @discountedPrice, @isPercentageDiscount, @imagePath,
                      @expiryDate, @status, @category, @currentStock, @employeeID)", conn);

                cmd.Parameters.AddWithValue("@productName", product.ProductName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@sku", product.SKU ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@supplierID", product.SupplierID ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@description", product.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@price", product.Price);
                cmd.Parameters.AddWithValue("@discountedPrice", product.DiscountedPrice ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@isPercentageDiscount", product.IsPercentageDiscount);

                // ✅ Handle image parameter
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    cmd.Parameters.Add("@imagePath", SqlDbType.VarBinary).Value = imageBytes;
                }
                else
                {
                    cmd.Parameters.Add("@imagePath", SqlDbType.VarBinary).Value = DBNull.Value;
                }

                cmd.Parameters.AddWithValue("@expiryDate", product.ExpiryDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", product.Status);
                cmd.Parameters.AddWithValue("@category", product.Category ?? "None");
                cmd.Parameters.AddWithValue("@currentStock", product.CurrentStock);
                cmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                var productId = (int)await cmd.ExecuteScalarAsync();

                string logDescription = $"Added product: '{product.ProductName}' (SKU: {product.SKU}) - Price: ₱{product.Price:N2}, Stock: {product.CurrentStock}";
                if (product.HasDiscount)
                {
                    logDescription += $", Discount: {product.DiscountedPrice}{(product.IsPercentageDiscount ? "%" : " fixed")}, Final Price: ₱{product.FinalPrice:N2}";
                }

                await LogActionAsync(conn, "Added new product.", logDescription, true);

                _toastManager.CreateToast("Product Added")
                    .WithContent($"Successfully added product '{product.ProductName}'.")
                    .DismissOnClick()
                    .ShowSuccess();

                return (true, "Product added successfully.", productId);
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to add product: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                Console.WriteLine($"SQL Error: {ex.Message}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                Console.WriteLine($"General Error: {ex.Message}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region READ

        public async Task<(bool Success, string Message, List<ProductModel>? Products)> GetAllProductsAsync()
        {
            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You don't have permission to view products.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to view products.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // ✅ UPDATED: JOIN with Suppliers table to get supplier name
                using var cmd = new SqlCommand(
                    @"SELECT p.ProductID, p.ProductName, p.SKU, p.SupplierID, s.SupplierName, p.Description,
                             p.Price, p.DiscountedPrice, p.IsPercentageDiscount, p.ProductImagePath,
                             p.ExpiryDate, p.Status, p.Category, p.CurrentStock, p.AddedByEmployeeID
                      FROM Products p
                      LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
                      ORDER BY p.ProductName", conn);

                var products = new List<ProductModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    products.Add(MapProductFromReader(reader));
                }

                return (true, "Products retrieved successfully.", products);
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to retrieve products: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, ProductModel? Product)> GetProductByIdAsync(int productId)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view products.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // ✅ UPDATED: JOIN with Suppliers
                using var cmd = new SqlCommand(
                    @"SELECT p.ProductID, p.ProductName, p.SKU, p.SupplierID, s.SupplierName, p.Description,
                             p.Price, p.DiscountedPrice, p.IsPercentageDiscount, p.ProductImagePath,
                             p.ExpiryDate, p.Status, p.Category, p.CurrentStock, p.AddedByEmployeeID
                      FROM Products p
                      LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID
                      WHERE p.ProductID = @productId", conn);

                cmd.Parameters.AddWithValue("@productId", productId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var product = MapProductFromReader(reader);
                    return (true, "Product retrieved successfully.", product);
                }

                return (false, "Product not found.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, ProductModel? Product)> GetProductBySKUAsync(string sku)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view products.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                             Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                             ExpiryDate, Status, Category, CurrentStock, AddedByEmployeeID
                      FROM Products 
                      WHERE SKU = @sku", conn);

                cmd.Parameters.AddWithValue("@sku", sku ?? (object)DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var product = MapProductFromReader(reader);
                    return (true, "Product retrieved successfully.", product);
                }

                return (false, "Product not found.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<ProductModel>? Products)> GetProductsByCategoryAsync(string category)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view products.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                             Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                             ExpiryDate, Status, Category, CurrentStock, AddedByEmployeeID
                      FROM Products 
                      WHERE Category = @category
                      ORDER BY ProductName", conn);

                cmd.Parameters.AddWithValue("@category", category ?? (object)DBNull.Value);

                var products = new List<ProductModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    products.Add(MapProductFromReader(reader));
                }

                return (true, "Products retrieved successfully.", products);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<ProductModel>? Products)> GetProductsByStatusAsync(string status)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view products.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                             Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                             ExpiryDate, Status, Category, CurrentStock, AddedByEmployeeID
                      FROM Products 
                      WHERE Status = @status
                      ORDER BY ProductName", conn);

                cmd.Parameters.AddWithValue("@status", status ?? (object)DBNull.Value);

                var products = new List<ProductModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    products.Add(MapProductFromReader(reader));
                }

                return (true, "Products retrieved successfully.", products);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<ProductModel>? Products)> GetExpiredProductsAsync()
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view products.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                             Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                             ExpiryDate, Status, Category, CurrentStock, AddedByEmployeeID
                      FROM Products 
                      WHERE ExpiryDate IS NOT NULL 
                        AND ExpiryDate < CAST(GETDATE() AS DATE)
                      ORDER BY ExpiryDate DESC", conn);

                var products = new List<ProductModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    products.Add(MapProductFromReader(reader));
                }

                return (true, "Expired products retrieved successfully.", products);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<ProductModel>? Products)> GetProductsExpiringSoonAsync(int daysThreshold = 30)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view products.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT ProductID, ProductName, SKU, ProductSupplier, Description,
                             Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                             ExpiryDate, Status, Category, CurrentStock, AddedByEmployeeID
                      FROM Products 
                      WHERE ExpiryDate IS NOT NULL 
                        AND ExpiryDate >= CAST(GETDATE() AS DATE)
                        AND ExpiryDate <= DATEADD(day, @daysThreshold, CAST(GETDATE() AS DATE))
                      ORDER BY ExpiryDate", conn);

                cmd.Parameters.AddWithValue("@daysThreshold", daysThreshold);

                var products = new List<ProductModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    products.Add(MapProductFromReader(reader));
                }

                return (true, "Expiring products retrieved successfully.", products);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region UPDATE

        public async Task<(bool Success, string Message)> UpdateProductAsync(ProductModel product)
        {
            if (!CanUpdate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can update product information.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to update products.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                product.Status = product.CurrentStock > 0 ? "In Stock" : "Out Of Stock";

                // Check if product exists and get existing image
                byte[]? existingImage = null;
                using var checkCmd = new SqlCommand(
                    "SELECT ProductImagePath FROM Products WHERE ProductID = @productId", conn);
                checkCmd.Parameters.AddWithValue("@productId", product.ProductID);

                var result = await checkCmd.ExecuteScalarAsync();
                if (result == null)
                {
                    _toastManager.CreateToast("Product Not Found")
                        .WithContent("The product you're trying to update doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Product not found.");
                }

                if (result != DBNull.Value)
                {
                    existingImage = (byte[])result;
                }

                // Handle image - only update if new file is provided
                byte[]? imageBytes = existingImage;

                if (!string.IsNullOrEmpty(product.ProductImageFilePath))
                {
                    try
                    {
                        if (System.IO.File.Exists(product.ProductImageFilePath))
                        {
                            imageBytes = await System.IO.File.ReadAllBytesAsync(product.ProductImageFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to read image: {ex.Message}");
                    }
                }

                // ✅ UPDATED: Use SupplierID
                using var cmd = new SqlCommand(
                    @"UPDATE Products 
              SET ProductName = @productName,
                  SKU = @sku,
                  SupplierID = @supplierID,
                  Description = @description,
                  Price = @price,
                  DiscountedPrice = @discountedPrice,
                  IsPercentageDiscount = @isPercentageDiscount,
                  ProductImagePath = @imagePath,
                  ExpiryDate = @expiryDate,
                  Status = @status,
                  Category = @category,
                  CurrentStock = @currentStock
              WHERE ProductID = @productId", conn);

                cmd.Parameters.AddWithValue("@productId", product.ProductID);
                cmd.Parameters.AddWithValue("@productName", product.ProductName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@sku", product.SKU ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@supplierID", product.SupplierID ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@description", product.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@price", product.Price);
                cmd.Parameters.AddWithValue("@discountedPrice", product.DiscountedPrice ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@isPercentageDiscount", product.IsPercentageDiscount);

                if (imageBytes != null)
                {
                    cmd.Parameters.Add("@imagePath", SqlDbType.VarBinary).Value = imageBytes;
                }
                else
                {
                    cmd.Parameters.Add("@imagePath", SqlDbType.VarBinary).Value = DBNull.Value;
                }

                cmd.Parameters.AddWithValue("@expiryDate", product.ExpiryDate ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", product.Status);
                cmd.Parameters.AddWithValue("@category", product.Category ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@currentStock", product.CurrentStock);

                await cmd.ExecuteNonQueryAsync();

                await LogActionAsync(conn, "Updated a product.", $"Updated product: {product.ProductName} (ID: {product.ProductID})", true);

                _toastManager.CreateToast("Product Updated")
                    .WithContent($"Successfully updated product '{product.ProductName}'.")
                    .DismissOnClick()
                    .ShowSuccess();

                return (true, "Product updated successfully.");
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to update product: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateProductStockAsync(int productId, int newStock)
        {
            if (!CanUpdate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can update product stock.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to update product stock.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string newStatus = newStock > 0 ? "In Stock" : "Out Of Stock";

                using var cmd = new SqlCommand(
                    @"UPDATE Products 
                      SET CurrentStock = @newStock,
                          Status = @status
                      WHERE ProductID = @productId", conn);

                cmd.Parameters.AddWithValue("@productId", productId);
                cmd.Parameters.AddWithValue("@newStock", newStock);
                cmd.Parameters.AddWithValue("@status", newStatus);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "Stock updated.", $"Updated product stock to: {newStock}", true);

                    _toastManager.CreateToast("Stock Updated")
                        .WithContent($"Product stock updated to {newStock}.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Stock updated successfully.");
                }

                return (false, "Product not found.");
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"Failed to update stock: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region DELETE

        public async Task<(bool Success, string Message)> DeleteProductAsync(int productId)
        {
            if (!CanDelete())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete products.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to delete products.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Get product name for logging
                using var getNameCmd = new SqlCommand(
                    "SELECT ProductName FROM Products WHERE ProductID = @productId", conn);
                getNameCmd.Parameters.AddWithValue("@productId", productId);
                var productName = await getNameCmd.ExecuteScalarAsync() as string;

                if (string.IsNullOrEmpty(productName))
                {
                    _toastManager.CreateToast("Product Not Found")
                        .WithContent("The product you're trying to delete doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Product not found.");
                }

                // Delete product
                using var cmd = new SqlCommand(
                    "DELETE FROM Products WHERE ProductID = @productId", conn);
                cmd.Parameters.AddWithValue("@productId", productId);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "Deleted a product.", $"Deleted product: {productName} (ID: {productId})", true);

                    _toastManager.CreateToast("Product Deleted")
                        .WithContent($"Successfully deleted product '{productName}'.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Product deleted successfully.");
                }

                return (false, "Failed to delete product.");
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to delete product: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, int DeletedCount)> DeleteMultipleProductsAsync(List<int> productIds)
        {
            if (!CanDelete())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete products.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to delete products.", 0);
            }

            if (productIds == null || productIds.Count == 0)
            {
                return (false, "No products selected for deletion.", 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var deletedCount = 0;
                var productNames = new List<string>();

                using var transaction = conn.BeginTransaction();
                try
                {
                    foreach (var productId in productIds)
                    {
                        // Get product name
                        using var getNameCmd = new SqlCommand(
                            "SELECT ProductName FROM Products WHERE ProductID = @productId", conn, transaction);
                        getNameCmd.Parameters.AddWithValue("@productId", productId);
                        var name = await getNameCmd.ExecuteScalarAsync() as string;

                        if (!string.IsNullOrEmpty(name))
                        {
                            productNames.Add(name);
                        }

                        // Delete product
                        using var deleteCmd = new SqlCommand(
                            "DELETE FROM Products WHERE ProductID = @productId", conn, transaction);
                        deleteCmd.Parameters.AddWithValue("@productId", productId);
                        deletedCount += await deleteCmd.ExecuteNonQueryAsync();
                    }

                    await LogActionAsync(conn, "Deleted multiple product.", $"Deleted {deletedCount} products: {string.Join(", ", productNames)}", true);

                    transaction.Commit();

                    _toastManager.CreateToast("Products Deleted")
                        .WithContent($"Successfully deleted {deletedCount} product(s).")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, $"Successfully deleted {deletedCount} product(s).", deletedCount);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to delete products: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Database error: {ex.Message}", 0);
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();
                return (false, $"Error: {ex.Message}", 0);
            }
        }

        #endregion

        #region UTILITY METHODS

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                using var logCmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                      VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)", conn);

                logCmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@success", success);
                logCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                await logCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        // ✅ UPDATED: MapProductFromReader to handle SupplierID and SupplierName
        private ProductModel MapProductFromReader(SqlDataReader reader)
        {
            string? imageBase64 = null;
            byte[]? imageBytes = null;

            if (!reader.IsDBNull(reader.GetOrdinal("ProductImagePath")))
            {
                imageBytes = (byte[])reader["ProductImagePath"];
                imageBase64 = Convert.ToBase64String(imageBytes);
            }

            return new ProductModel
            {
                ProductID = reader.GetInt32(reader.GetOrdinal("ProductID")),
                ProductName = reader["ProductName"]?.ToString() ?? "",
                SKU = reader["SKU"]?.ToString() ?? "",
                SupplierID = reader["SupplierID"] != DBNull.Value
                    ? reader.GetInt32(reader.GetOrdinal("SupplierID"))
                    : null,
                SupplierName = reader["SupplierName"]?.ToString(),
                Description = reader["Description"]?.ToString() ?? "",
                Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                DiscountedPrice = reader["DiscountedPrice"] != DBNull.Value
                    ? reader.GetDecimal(reader.GetOrdinal("DiscountedPrice"))
                    : null,
                IsPercentageDiscount = reader.GetBoolean(reader.GetOrdinal("IsPercentageDiscount")),
                ProductImageBase64 = imageBase64,
                ProductImageBytes = imageBytes,
                ExpiryDate = reader["ExpiryDate"] != DBNull.Value
                    ? reader.GetDateTime(reader.GetOrdinal("ExpiryDate"))
                    : null,
                Status = reader["Status"]?.ToString() ?? "",
                Category = reader["Category"]?.ToString() ?? "",
                CurrentStock = reader.GetInt32(reader.GetOrdinal("CurrentStock")),
                AddedByEmployeeID = reader["AddedByEmployeeID"] != DBNull.Value
                    ? reader.GetInt32(reader.GetOrdinal("AddedByEmployeeID"))
                    : 0
            };
        }


        public async Task<(bool Success, int TotalProducts, int InStock, int OutOfStock, int Expired)> GetProductStatisticsAsync()
        {
            if (!CanView())
            {
                return (false, 0, 0, 0, 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT 
                        COUNT(*) as TotalProducts,
                        SUM(CASE WHEN Status = 'In Stock' THEN 1 ELSE 0 END) as InStock,
                        SUM(CASE WHEN Status = 'Out Of Stock' THEN 1 ELSE 0 END) as OutOfStock,
                        SUM(CASE WHEN ExpiryDate IS NOT NULL AND ExpiryDate < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) as Expired
                      FROM Products", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (true,
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetInt32(2),
                        reader.GetInt32(3));
                }

                return (false, 0, 0, 0, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetProductStatisticsAsync] {ex.Message}");
                return (false, 0, 0, 0, 0);
            }
        }

        public async Task<(bool Success, string Message, byte[]? ImageBytes)> GetProductImageAsync(int productId)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view product images.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    "SELECT ProductImagePath FROM Products WHERE ProductID = @productId", conn);
                cmd.Parameters.AddWithValue("@productId", productId);

                var result = await cmd.ExecuteScalarAsync();

                if (result != null && result != DBNull.Value)
                {
                    byte[] imageBytes = (byte[])result;
                    return (true, "Image retrieved successfully.", imageBytes);
                }

                return (false, "No image found for this product.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion
    }
}
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
using AHON_TRACK.Services.Events;
using Notification = AHON_TRACK.Models.Notification;

namespace AHON_TRACK.Services
{
    public class ProductService : IProductService, IDisposable
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;
        private Action<Notification>? _notificationCallback;
        private bool _disposed;

        public ProductService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        public void RegisterNotificationCallback(Action<Notification> callback)
        {
            _notificationCallback = callback;
        }

        public void UnRegisterNotificationCallback()
        {
            _notificationCallback = null;
        }

        #region Role-Based Access Control

        private bool CanCreate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;


        private bool CanUpdate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanDelete() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanView() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;

        #endregion

        #region CREATE (Add + Restore)

        public async Task<(bool Success, string Message, int? ProductId)> AddProductAsync(ProductModel product)
        {
            if (!CanCreate())
            {
                ShowToast("Access Denied", "You don't have permission to add products.", ToastType.Error);
                return (false, "Insufficient permissions to add products.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                if (product.ExpiryDate.HasValue && product.ExpiryDate.Value.Date <= DateTime.Today)
                {
                    product.Status = "Expired";
                }
                else
                {
                    product.Status = product.CurrentStock > 0 ? "In Stock" : "Out Of Stock";
                }

                // Check for duplicate SKU (including deleted ones)
                var (existingProductId, existingIsDeleted) = await CheckDuplicateSKUAsync(conn, product.SKU);

                // If exists and deleted -> restore and update fields
                if (existingProductId.HasValue && existingIsDeleted)
                {
                    return await RestoreProductAsync(conn, product, existingProductId.Value);
                }

                // If exists and NOT deleted -> duplicate SKU warning
                if (existingProductId.HasValue)
                {
                    ShowToast("Duplicate SKU", $"Product with SKU '{product.SKU}' already exists.", ToastType.Warning);
                    return (false, "Product SKU already exists.", null);
                }

                // Insert new product
                return await InsertNewProductAsync(conn, product);
            }
            catch (SqlException ex)
            {
                ShowToast("Database Error", $"Failed to add product: {ex.Message}", ToastType.Error);
                Console.WriteLine($"SQL Error: {ex.Message}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"An unexpected error occurred: {ex.Message}", ToastType.Error);
                Console.WriteLine($"General Error: {ex.Message}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        private async Task<(int? ProductId, bool IsDeleted)> CheckDuplicateSKUAsync(SqlConnection conn, string? sku)
        {
            using var cmd = new SqlCommand(
                "SELECT ProductID, IsDeleted FROM Products WHERE SKU = @sku", conn);
            cmd.Parameters.AddWithValue("@sku", sku ?? (object)DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var productId = reader["ProductID"] != DBNull.Value ? Convert.ToInt32(reader["ProductID"]) : (int?)null;
                var isDeleted = reader["IsDeleted"] != DBNull.Value && Convert.ToBoolean(reader["IsDeleted"]);
                return (productId, isDeleted);
            }

            return (null, false);
        }

        private async Task<(bool Success, string Message, int? ProductId)> RestoreProductAsync(
            SqlConnection conn, ProductModel product, int productId)
        {
            var imageBytes = await ReadImageBytesAsync(product.ProductImageFilePath);

            const string restoreQuery = @"
                UPDATE Products SET
                    ProductName = @productName,
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
                    CurrentStock = @currentStock,
                    AddedByEmployeeID = @employeeID,
                    IsDeleted = 0
                WHERE ProductID = @productId";

            using var cmd = new SqlCommand(restoreQuery, conn);
            AddProductParameters(cmd, product);
            cmd.Parameters.AddWithValue("@productId", productId);
            AddImageParameter(cmd, imageBytes);

            await cmd.ExecuteNonQueryAsync();

            var logDescription = BuildLogDescription(product, "Restored", productId);
            await LogActionAsync(conn, "RESTORE", logDescription, true);

            var expiryText = product.ExpiryDate?.ToString("yyyy-MM-dd") ?? "N/A";
            ShowToast("Product Restored",
                $"Product: {product.ProductName}\nStock: {product.CurrentStock}\nExpiry: {expiryText}\nStatus: {product.Status}",
                ToastType.Success);

            return (true, "Product restored successfully.", productId);
        }

        private async Task<(bool Success, string Message, int? ProductId)> InsertNewProductAsync(
            SqlConnection conn, ProductModel product)
        {
            var imageBytes = await ReadImageBytesAsync(product.ProductImageFilePath);

            using var cmd = new SqlCommand(
                @"INSERT INTO Products (ProductName, SKU, SupplierID, Description, 
                 Price, DiscountedPrice, IsPercentageDiscount, ProductImagePath,
                 ExpiryDate, Status, Category, CurrentStock, AddedByEmployeeID, IsDeleted)
                  OUTPUT INSERTED.ProductID
                  VALUES (@productName, @sku, @supplierID, @description,
                          @price, @discountedPrice, @isPercentageDiscount, @imagePath,
                          @expiryDate, @status, @category, @currentStock, @employeeID, 0)", conn);

            AddProductParameters(cmd, product);
            AddImageParameter(cmd, imageBytes);

            var productId = (int)await cmd.ExecuteScalarAsync();

            var logDescription = BuildLogDescription(product, "Added");
            await LogActionAsync(conn, "CREATE", logDescription, true);

            DashboardEventService.Instance.NotifyProductAdded();
            return (true, "Product added successfully.", productId);
        }

        #endregion

        #region READ

        public async Task<(bool Success, string Message, List<ProductModel>? Products)> GetAllProductsAsync()
        {
            if (!CanView())
            {
                ShowToast("Access Denied", "You don't have permission to view products.", ToastType.Error);
                return (false, "Insufficient permissions to view products.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(GetProductSelectQuery() + " WHERE p.IsDeleted = 0 ORDER BY p.ProductName", conn);

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
                ShowToast("Database Error", $"Failed to retrieve products: {ex.Message}", ToastType.Error);
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"An unexpected error occurred: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, ProductModel? Product)> GetProductByIdAsync(int productId)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view products.", null);

            return await GetSingleProductAsync("p.ProductID = @param AND p.IsDeleted = 0", productId);
        }

        public async Task<(bool Success, string Message, ProductModel? Product)> GetProductBySKUAsync(string sku)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view products.", null);

            return await GetSingleProductAsync("p.SKU = @param AND p.IsDeleted = 0", sku ?? (object)DBNull.Value);
        }

        public async Task<(bool Success, string Message, List<ProductModel>? Products)> GetProductsByCategoryAsync(string category)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view products.", null);

            return await GetProductsListAsync("p.Category = @param AND p.IsDeleted = 0", category ?? (object)DBNull.Value);
        }

        public async Task<(bool Success, string Message, List<ProductModel>? Products)> GetProductsByStatusAsync(string status)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view products.", null);

            return await GetProductsListAsync("p.Status = @param AND p.IsDeleted = 0", status ?? (object)DBNull.Value);
        }

        public async Task<(bool Success, string Message, List<ProductModel>? Products)> GetExpiredProductsAsync()
        {
            if (!CanView())
                return (false, "Insufficient permissions to view products.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    GetProductSelectQuery() +
                    " WHERE p.ExpiryDate IS NOT NULL AND p.ExpiryDate <= CAST(GETDATE() AS DATE) AND p.IsDeleted = 0 ORDER BY p.ExpiryDate DESC", conn);

                var products = await ReadProductsAsync(cmd);
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
                return (false, "Insufficient permissions to view products.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    GetProductSelectQuery() +
                    @" WHERE p.ExpiryDate IS NOT NULL 
                       AND p.ExpiryDate >= CAST(GETDATE() AS DATE)
                       AND p.ExpiryDate <= DATEADD(day, @daysThreshold, CAST(GETDATE() AS DATE))
                       AND p.IsDeleted = 0
                       ORDER BY p.ExpiryDate", conn);

                cmd.Parameters.AddWithValue("@daysThreshold", daysThreshold);

                var products = await ReadProductsAsync(cmd);
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
                ShowToast("Access Denied", "Only administrators can update product information.", ToastType.Error);
                return (false, "Insufficient permissions to update products.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                if (product.ExpiryDate.HasValue && product.ExpiryDate.Value.Date <= DateTime.Today)
                {
                    product.Status = "Expired";
                }
                else
                {
                    product.Status = product.CurrentStock > 0 ? "In Stock" : "Out Of Stock";
                }

                // Get existing image
                var existingImage = await GetExistingProductImageAsync(conn, product.ProductID);
                if (existingImage == null)
                {
                    ShowToast("Product Not Found", "The product you're trying to update doesn't exist.", ToastType.Warning);
                    return (false, "Product not found.");
                }

                // Handle image - only update if new file is provided
                var imageBytes = !string.IsNullOrEmpty(product.ProductImageFilePath)
                    ? await ReadImageBytesAsync(product.ProductImageFilePath) ?? existingImage
                    : existingImage;

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
                      WHERE ProductID = @productId AND IsDeleted = 0", conn);

                cmd.Parameters.AddWithValue("@productId", product.ProductID);
                AddProductParameters(cmd, product);
                AddImageParameter(cmd, imageBytes);

                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows <= 0)
                    return (false, "Failed to update product (maybe it was deleted).");

                await LogActionAsync(conn, "UPDATE", $"Updated product: {product.ProductName} (ID: {product.ProductID})", true);
                DashboardEventService.Instance.NotifyProductUpdated();

                return (true, "Product updated successfully.");
            }
            catch (SqlException ex)
            {
                ShowToast("Database Error", $"Failed to update product: {ex.Message}", ToastType.Error);
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"An unexpected error occurred: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateProductStockAsync(int productId, int newStock)
        {
            if (!CanUpdate())
            {
                ShowToast("Access Denied", "Only administrators can update product stock.", ToastType.Error);
                return (false, "Insufficient permissions to update product stock.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string newStatus = newStock > 0 ? "In Stock" : "Out Of Stock";

                using var cmd = new SqlCommand(
                    @"UPDATE Products 
                      SET CurrentStock = @newStock, Status = @status
                      WHERE ProductID = @productId AND IsDeleted = 0", conn);

                cmd.Parameters.AddWithValue("@productId", productId);
                cmd.Parameters.AddWithValue("@newStock", newStock);
                cmd.Parameters.AddWithValue("@status", newStatus);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "Stock updated.", $"Updated product stock to: {newStock}", true);
                    return (true, "Stock updated successfully.");
                }

                return (false, "Product not found.");
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"Failed to update stock: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region DELETE (Soft Delete)

        public async Task<(bool Success, string Message)> DeleteProductAsync(int productId)
        {
            if (!CanDelete())
            {
                ShowToast("Access Denied", "Only administrators can delete products.", ToastType.Error);
                return (false, "Insufficient permissions to delete products.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Get product name for logging
                var productName = await GetProductNameAsync(conn, productId);
                if (string.IsNullOrEmpty(productName))
                {
                    ShowToast("Product Not Found", "The product you're trying to delete doesn't exist.", ToastType.Warning);
                    return (false, "Product not found.");
                }

                // Soft delete product
                using var cmd = new SqlCommand(
                    "UPDATE Products SET IsDeleted = 1 WHERE ProductID = @productId AND IsDeleted = 0", conn);
                cmd.Parameters.AddWithValue("@productId", productId);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "DELETE", $"Deleted product: {productName} (ID: {productId})", true);
                    DashboardEventService.Instance.NotifyProductDeleted();
                    return (true, "Product deleted successfully.");
                }

                return (false, "Failed to delete product.");
            }
            catch (SqlException ex)
            {
                ShowToast("Database Error", $"Failed to delete product: {ex.Message}", ToastType.Error);
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"An unexpected error occurred: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, int DeletedCount)> DeleteMultipleProductsAsync(List<int> productIds)
        {
            if (!CanDelete())
            {
                ShowToast("Access Denied", "Only administrators can delete products.", ToastType.Error);
                return (false, "Insufficient permissions to delete products.", 0);
            }

            if (productIds == null || productIds.Count == 0)
                return (false, "No products selected for deletion.", 0);

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
                        var name = await GetProductNameAsync(conn, productId, transaction);
                        if (!string.IsNullOrEmpty(name))
                            productNames.Add(name);

                        // Soft delete product
                        using var deleteCmd = new SqlCommand(
                            "UPDATE Products SET IsDeleted = 1 WHERE ProductID = @productId AND IsDeleted = 0",
                            conn, transaction);
                        deleteCmd.Parameters.AddWithValue("@productId", productId);
                        deletedCount += await deleteCmd.ExecuteNonQueryAsync();
                    }

                    await LogActionAsync(conn, "DELETE",
                        $"Deleted {deletedCount} products: {string.Join(", ", productNames)}", true);

                    transaction.Commit();
                    DashboardEventService.Instance.NotifyProductDeleted();

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
                ShowToast("Database Error", $"Failed to delete products: {ex.Message}", ToastType.Error);
                return (false, $"Database error: {ex.Message}", 0);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"An unexpected error occurred: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}", 0);
            }
        }

        #endregion

        #region NOTIFICATIONS

        public async Task ShowProductAlertsAsync(Action<Notification>? addNotificationCallback = null)
        {
            try
            {
                var summary = await GetProductAlertSummaryAsync();
                var notifyCallback = addNotificationCallback ?? _notificationCallback;

                // Expired products - highest priority
                if (summary.ExpiredCount > 0)
                {
                    SendNotification("Expired Products", $"{summary.ExpiredCount} product(s) have expired!",
                        NotificationType.Alert, ToastType.Error, notifyCallback);
                }

                // Expiring soon
                if (summary.ExpiringSoonCount > 0)
                {
                    SendNotification("Expiring Soon", $"{summary.ExpiringSoonCount} product(s) will expire within 30 days!",
                        NotificationType.Warning, ToastType.Warning, notifyCallback);
                }

                // Out of stock
                if (summary.OutOfStockCount > 0)
                {
                    SendNotification("Out of Stock", $"{summary.OutOfStockCount} product(s) are currently out of stock!",
                        NotificationType.Warning, ToastType.Warning, notifyCallback);
                }

                // Low stock
                if (summary.LowStockCount > 0)
                {
                    SendNotification("Low Stock Alert", $"{summary.LowStockCount} product(s) have low stock (≤5 units)!",
                        NotificationType.Reminder, ToastType.Info, notifyCallback);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ShowProductAlertsAsync] Error: {ex.Message}");
            }
        }

        public async Task<ProductAlertSummary> GetProductAlertSummaryAsync()
        {
            var summary = new ProductAlertSummary();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                summary.LowStockItems = await GetLowStockItemsAsync(conn);
                summary.LowStockCount = summary.LowStockItems.Count;

                summary.ExpiringSoonItems = await GetExpiringSoonItemsAsync(conn);
                summary.ExpiringSoonCount = summary.ExpiringSoonItems.Count;

                summary.ExpiredItems = await GetExpiredItemsAsync(conn);
                summary.ExpiredCount = summary.ExpiredItems.Count;

                summary.OutOfStockItems = await GetOutOfStockItemsAsync(conn);
                summary.OutOfStockCount = summary.OutOfStockItems.Count;

                summary.TotalAlerts = summary.LowStockCount + summary.ExpiringSoonCount +
                                      summary.ExpiredCount + summary.OutOfStockCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetProductAlertSummaryAsync] Error: {ex.Message}");
            }

            return summary;
        }

        #endregion

        #region UTILITY METHODS

        private enum ToastType { Success, Error, Warning, Info }

        private void ShowToast(string title, string message, ToastType type)
        {
            var toast = _toastManager.CreateToast(title)
                .WithContent(message)
                .DismissOnClick();

            switch (type)
            {
                case ToastType.Success: toast.ShowSuccess(); break;
                case ToastType.Error: toast.ShowError(); break;
                case ToastType.Warning: toast.ShowWarning(); break;
                case ToastType.Info: toast.ShowInfo(); break;
            }
        }

        private void SendNotification(string title, string message, NotificationType notifType,
            ToastType toastType, Action<Notification>? callback)
        {
            _toastManager?.CreateToast(title)
                .WithContent(message)
                .DismissOnClick()
                .ShowInfo();

            callback?.Invoke(new Notification
            {
                Type = notifType,
                Title = title,
                Message = message,
                DateAndTime = DateTime.Now
            });
        }

        private async Task<byte[]?> ReadImageBytesAsync(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
                    Console.WriteLine($"Successfully read image: {bytes.Length} bytes");
                    return bytes;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read image: {ex.Message}");
            }

            return null;
        }

        private void AddProductParameters(SqlCommand cmd, ProductModel product)
        {
            cmd.Parameters.AddWithValue("@productName", product.ProductName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sku", product.SKU ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@supplierID", product.SupplierID ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@description", product.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@price", product.Price);
            cmd.Parameters.AddWithValue("@discountedPrice", product.DiscountedPrice ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@isPercentageDiscount", product.IsPercentageDiscount);
            cmd.Parameters.AddWithValue("@expiryDate", product.ExpiryDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", product.Status);
            cmd.Parameters.AddWithValue("@category", product.Category ?? "None");
            cmd.Parameters.AddWithValue("@currentStock", product.CurrentStock);
            cmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);
        }

        private void AddImageParameter(SqlCommand cmd, byte[]? imageBytes)
        {
            if (imageBytes != null && imageBytes.Length > 0)
                cmd.Parameters.Add("@imagePath", SqlDbType.VarBinary).Value = imageBytes;
            else
                cmd.Parameters.Add("@imagePath", SqlDbType.VarBinary).Value = DBNull.Value;
        }

        private string BuildLogDescription(ProductModel product, string action, int? productId = null)
        {
            var sb = new StringBuilder($"{action} product: '{product.ProductName}' (SKU: {product.SKU}");
            if (productId.HasValue)
                sb.Append($", ID: {productId.Value}");
            sb.Append($") - Price: ₱{product.Price:N2}, Stock: {product.CurrentStock}");

            if (product.HasDiscount)
            {
                sb.Append($", Discount: {product.DiscountedPrice}{(product.IsPercentageDiscount ? "%" : " fixed")}");
                sb.Append($", Final Price: ₱{product.FinalPrice:N2}");
            }

            return sb.ToString();
        }

        private string GetProductSelectQuery() =>
            @"SELECT p.ProductID, p.ProductName, p.SKU, p.SupplierID, s.SupplierName, p.Description,
                     p.Price, p.DiscountedPrice, p.IsPercentageDiscount, p.ProductImagePath,
                     p.ExpiryDate, p.Status, p.Category, p.CurrentStock, p.AddedByEmployeeID
              FROM Products p
              LEFT JOIN Suppliers s ON p.SupplierID = s.SupplierID";

        private async Task<(bool Success, string Message, ProductModel? Product)> GetSingleProductAsync(
            string whereClause, object paramValue)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand($"{GetProductSelectQuery()} WHERE {whereClause}", conn);
                cmd.Parameters.AddWithValue("@param", paramValue);

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

        private async Task<(bool Success, string Message, List<ProductModel>? Products)> GetProductsListAsync(
            string whereClause, object paramValue)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    $"{GetProductSelectQuery()} WHERE {whereClause} ORDER BY p.ProductName", conn);
                cmd.Parameters.AddWithValue("@param", paramValue);

                var products = await ReadProductsAsync(cmd);
                return (true, "Products retrieved successfully.", products);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        private async Task<List<ProductModel>> ReadProductsAsync(SqlCommand cmd)
        {
            var products = new List<ProductModel>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                products.Add(MapProductFromReader(reader));
            }
            return products;
        }

        private async Task<byte[]?> GetExistingProductImageAsync(SqlConnection conn, int productId)
        {
            using var cmd = new SqlCommand(
                "SELECT ProductImagePath FROM Products WHERE ProductID = @productId AND IsDeleted = 0", conn);
            cmd.Parameters.AddWithValue("@productId", productId);

            var result = await cmd.ExecuteScalarAsync();
            return result == null ? null : result == DBNull.Value ? Array.Empty<byte>() : (byte[])result;
        }

        private async Task<string?> GetProductNameAsync(SqlConnection conn, int productId, SqlTransaction? transaction = null)
        {
            using var cmd = new SqlCommand(
                "SELECT ProductName FROM Products WHERE ProductID = @productId AND IsDeleted = 0", conn, transaction);
            cmd.Parameters.AddWithValue("@productId", productId);
            return await cmd.ExecuteScalarAsync() as string;
        }

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                using var logCmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                      VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)", conn);

                logCmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@actionType", actionType);
                logCmd.Parameters.AddWithValue("@description", description);
                logCmd.Parameters.AddWithValue("@success", success);
                logCmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                await logCmd.ExecuteNonQueryAsync();
                DashboardEventService.Instance.NotifyRecentLogsUpdated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

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
                IsPercentageDiscount = reader["IsPercentageDiscount"] != DBNull.Value
                    && reader.GetBoolean(reader.GetOrdinal("IsPercentageDiscount")),
                ProductImageBase64 = imageBase64,
                ProductImageBytes = imageBytes,
                ExpiryDate = reader["ExpiryDate"] != DBNull.Value
                    ? reader.GetDateTime(reader.GetOrdinal("ExpiryDate"))
                    : null,
                Status = reader["Status"]?.ToString() ?? "",
                Category = reader["Category"]?.ToString() ?? "",
                CurrentStock = reader["CurrentStock"] != DBNull.Value
                    ? reader.GetInt32(reader.GetOrdinal("CurrentStock"))
                    : 0,
                AddedByEmployeeID = reader["AddedByEmployeeID"] != DBNull.Value
                    ? reader.GetInt32(reader.GetOrdinal("AddedByEmployeeID"))
                    : 0
            };
        }

        public async Task<(bool Success, int TotalProducts, int InStock, int OutOfStock, int Expired)> GetProductStatisticsAsync()
        {
            if (!CanView())
                return (false, 0, 0, 0, 0);

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
                      FROM Products
                      WHERE IsDeleted = 0", conn);

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
                return (false, "Insufficient permissions to view product images.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    "SELECT ProductImagePath FROM Products WHERE ProductID = @productId AND IsDeleted = 0", conn);
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

        #region Private Helper Methods (Notifications)

        private async Task<List<ProductAlertItem>> GetLowStockItemsAsync(SqlConnection conn)
        {
            var items = new List<ProductAlertItem>();
            try
            {
                using var cmd = new SqlCommand(
                    @"SELECT ProductID, ProductName, CurrentStock
                      FROM Products
                      WHERE CurrentStock <= 5 AND Status = 'In Stock' AND IsDeleted = 0
                      ORDER BY CurrentStock ASC", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new ProductAlertItem
                    {
                        ProductID = reader.GetInt32(0),
                        ProductName = reader.GetString(1),
                        AlertType = "Low Stock",
                        AlertSeverity = "Warning",
                        Details = $"Only {reader.GetInt32(2)} unit(s) left"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetLowStockItemsAsync] Error: {ex.Message}");
            }
            return items;
        }

        private async Task<List<ProductAlertItem>> GetOutOfStockItemsAsync(SqlConnection conn)
        {
            var items = new List<ProductAlertItem>();
            try
            {
                using var cmd = new SqlCommand(
                    @"SELECT ProductID, ProductName
                      FROM Products
                      WHERE CurrentStock = 0 AND IsDeleted = 0
                      ORDER BY ProductName ASC", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new ProductAlertItem
                    {
                        ProductID = reader.GetInt32(0),
                        ProductName = reader.GetString(1),
                        AlertType = "Out of Stock",
                        AlertSeverity = "Error",
                        Details = "No stock available"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetOutOfStockItemsAsync] Error: {ex.Message}");
            }
            return items;
        }

        private async Task<List<ProductAlertItem>> GetExpiringSoonItemsAsync(SqlConnection conn)
        {
            var items = new List<ProductAlertItem>();
            try
            {
                using var cmd = new SqlCommand(
                    @"SELECT ProductID, ProductName, ExpiryDate
                      FROM Products
                      WHERE ExpiryDate IS NOT NULL
                        AND ExpiryDate > GETDATE()
                        AND ExpiryDate <= DATEADD(day, 30, GETDATE())
                        AND IsDeleted = 0
                      ORDER BY ExpiryDate ASC", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var expiryDate = reader.GetDateTime(2);
                    var daysLeft = (expiryDate - DateTime.Now).Days;
                    items.Add(new ProductAlertItem
                    {
                        ProductID = reader.GetInt32(0),
                        ProductName = reader.GetString(1),
                        AlertType = "Expiring Soon",
                        AlertSeverity = "Warning",
                        Details = $"Expires in {daysLeft} day(s) ({expiryDate:MMM dd, yyyy})"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetExpiringSoonItemsAsync] Error: {ex.Message}");
            }
            return items;
        }

        private async Task<List<ProductAlertItem>> GetExpiredItemsAsync(SqlConnection conn)
        {
            var items = new List<ProductAlertItem>();
            try
            {
                using var cmd = new SqlCommand(
                    @"SELECT ProductID, ProductName, ExpiryDate
                      FROM Products
                      WHERE ExpiryDate IS NOT NULL
                        AND ExpiryDate <= GETDATE()
                        AND IsDeleted = 0
                      ORDER BY ExpiryDate DESC", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var expiryDate = reader.GetDateTime(2);
                    items.Add(new ProductAlertItem
                    {
                        ProductID = reader.GetInt32(0),
                        ProductName = reader.GetString(1),
                        AlertType = "Expired",
                        AlertSeverity = "Error",
                        Details = $"Expired on {expiryDate:MMM dd, yyyy}"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetExpiredItemsAsync] Error: {ex.Message}");
            }
            return items;
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Clear callback reference
                _notificationCallback = null;
            
                // Dispose ToastManager if it's disposable
                _toastManager.DismissAll();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region SUPPORTING CLASSES

        public class ProductAlertSummary
        {
            public int LowStockCount { get; set; }
            public int OutOfStockCount { get; set; }
            public int ExpiringSoonCount { get; set; }
            public int ExpiredCount { get; set; }
            public int TotalAlerts { get; set; }

            public List<ProductAlertItem> LowStockItems { get; set; } = new();
            public List<ProductAlertItem> OutOfStockItems { get; set; } = new();
            public List<ProductAlertItem> ExpiringSoonItems { get; set; } = new();
            public List<ProductAlertItem> ExpiredItems { get; set; } = new();
        }

        public class ProductAlertItem
        {
            public int ProductID { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string AlertType { get; set; } = string.Empty;
            public string AlertSeverity { get; set; } = string.Empty;
            public string Details { get; set; } = string.Empty;
        }

        #endregion
    }
}
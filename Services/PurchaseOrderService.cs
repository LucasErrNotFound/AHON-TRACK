using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.Services.Events;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;
        private readonly IProductService? _productService;

        public PurchaseOrderService(string connectionString, ToastManager toastManager, IProductService? productService = null)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
            _productService = productService;
        }

        #region Role-Based Access Control

        private bool CanCreate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanUpdate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanDelete() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanView() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;

        #endregion

        #region CREATE

        public async Task<(bool Success, string Message, int? POId)> CreatePurchaseOrderAsync(PurchaseOrderModel purchaseOrder)
        {
            if (!CanCreate())
            {
                ShowToast("Access Denied", "You don't have permission to create purchase orders.", ToastType.Error);
                return (false, "Insufficient permissions to create purchase orders.", null);
            }

            if (purchaseOrder.Items == null || purchaseOrder.Items.Count == 0)
            {
                ShowToast("Validation Error", "Purchase order must have at least one item.", ToastType.Warning);
                return (false, "Purchase order must have at least one item.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();
                try
                {
                    // Calculate totals
                    CalculateTotals(purchaseOrder);

                    // Insert Purchase Order
                    var poId = await InsertPurchaseOrderAsync(conn, transaction, purchaseOrder);

                    // Insert Purchase Order Items
                    await InsertPurchaseOrderItemsAsync(conn, transaction, poId, purchaseOrder.Items);

                    // Log action
                    await LogActionAsync(conn, transaction, "CREATE",
                        $"Created Purchase Order: {purchaseOrder.PONumber} with {purchaseOrder.Items.Count} items - Total: ₱{purchaseOrder.Total:N2}", true);

                    transaction.Commit();

                    ShowToast("Purchase Order Created",
                        $"PO Number: {purchaseOrder.PONumber}\nTotal: ₱{purchaseOrder.Total:N2}",
                        ToastType.Success);

                    return (true, "Purchase order created successfully.", poId);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                ShowToast("Database Error", $"Failed to create purchase order: {ex.Message}", ToastType.Error);
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"An unexpected error occurred: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}", null);
            }
        }

        private async Task<int> InsertPurchaseOrderAsync(SqlConnection conn, SqlTransaction transaction, PurchaseOrderModel po)
        {
            Console.WriteLine($"[InsertPurchaseOrderAsync] SupplierID being saved: {po.SupplierID}");

            const string query = @"
                INSERT INTO PurchaseOrders (
                    PONumber, SupplierID, OrderDate, ExpectedDeliveryDate,
                    ShippingStatus, PaymentStatus, InvoiceNumber,
                    Subtotal, TaxRate, TaxAmount, Total,
                    CreatedByEmployeeID, IsDeleted
                )
                OUTPUT INSERTED.PurchaseOrderID
                VALUES (
                    @poNumber, @supplierId, @orderDate, @expectedDelivery,
                    @shippingStatus, @paymentStatus, @invoiceNumber,
                    @subtotal, @taxRate, @taxAmount, @total,
                    @employeeId, 0
                )";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@poNumber", po.PONumber);

            if (po.SupplierID.HasValue && po.SupplierID.Value > 0)
            {
                cmd.Parameters.AddWithValue("@supplierId", po.SupplierID.Value);
                Console.WriteLine($"[SQL] Saving SupplierID: {po.SupplierID.Value}");
            }
            else
            {
                cmd.Parameters.AddWithValue("@supplierId", DBNull.Value);
                Console.WriteLine("[SQL WARNING] SupplierID is NULL!");
            }

            cmd.Parameters.AddWithValue("@orderDate", po.OrderDate);
            cmd.Parameters.AddWithValue("@expectedDelivery", po.ExpectedDeliveryDate);
            cmd.Parameters.AddWithValue("@shippingStatus", po.ShippingStatus);
            cmd.Parameters.AddWithValue("@paymentStatus", po.PaymentStatus);
            cmd.Parameters.AddWithValue("@invoiceNumber", po.InvoiceNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@subtotal", po.Subtotal);
            cmd.Parameters.AddWithValue("@taxRate", po.TaxRate);
            cmd.Parameters.AddWithValue("@taxAmount", po.TaxAmount);
            cmd.Parameters.AddWithValue("@total", po.Total);
            cmd.Parameters.AddWithValue("@employeeId", CurrentUserModel.UserId ?? (object)DBNull.Value);

            return (int)await cmd.ExecuteScalarAsync();
        }

        private async Task InsertPurchaseOrderItemsAsync(SqlConnection conn, SqlTransaction transaction,
            int poId, List<PurchaseOrderItemModel> items)
        {
            const string query = @"
                INSERT INTO PurchaseOrderItems (PurchaseOrderID, ItemName, Unit, Quantity, Price)
                VALUES (@poId, @itemName, @unit, @quantity, @price)";

            foreach (var item in items)
            {
                using var cmd = new SqlCommand(query, conn, transaction);
                cmd.Parameters.AddWithValue("@poId", poId);
                cmd.Parameters.AddWithValue("@itemName", item.ItemName);
                cmd.Parameters.AddWithValue("@unit", item.Unit);
                cmd.Parameters.AddWithValue("@quantity", item.Quantity);
                cmd.Parameters.AddWithValue("@price", item.Price);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        #endregion

        #region READ

        public async Task<(bool Success, string Message, List<PurchaseOrderModel>? PurchaseOrders)> GetAllPurchaseOrdersAsync()
        {
            if (!CanView())
            {
                ShowToast("Access Denied", "You don't have permission to view purchase orders.", ToastType.Error);
                return (false, "Insufficient permissions to view purchase orders.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT po.*, s.SupplierName
                    FROM PurchaseOrders po
                    LEFT JOIN Suppliers s ON po.SupplierID = s.SupplierID
                    WHERE po.IsDeleted = 0
                    ORDER BY po.OrderDate DESC";

                using var cmd = new SqlCommand(query, conn);
                var purchaseOrders = new List<PurchaseOrderModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var po = MapPurchaseOrderFromReader(reader);
                    purchaseOrders.Add(po);
                }
                reader.Close();

                // Load items for each PO
                foreach (var po in purchaseOrders)
                {
                    po.Items = await GetPurchaseOrderItemsAsync(conn, po.PurchaseOrderID);
                }

                return (true, "Purchase orders retrieved successfully.", purchaseOrders);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"Failed to retrieve purchase orders: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, PurchaseOrderModel? PurchaseOrder)> GetPurchaseOrderByIdAsync(int poId)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view purchase orders.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT po.*, s.SupplierName
                    FROM PurchaseOrders po
                    LEFT JOIN Suppliers s ON po.SupplierID = s.SupplierID
                    WHERE po.PurchaseOrderID = @poId AND po.IsDeleted = 0";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@poId", poId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var po = MapPurchaseOrderFromReader(reader);
                    reader.Close();

                    po.Items = await GetPurchaseOrderItemsAsync(conn, po.PurchaseOrderID);
                    return (true, "Purchase order retrieved successfully.", po);
                }

                return (false, "Purchase order not found.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, PurchaseOrderModel? PurchaseOrder)> GetPurchaseOrderByPONumberAsync(string poNumber)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view purchase orders.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT po.*, s.SupplierName
                    FROM PurchaseOrders po
                    LEFT JOIN Suppliers s ON po.SupplierID = s.SupplierID
                    WHERE po.PONumber = @poNumber AND po.IsDeleted = 0";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@poNumber", poNumber ?? (object)DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var po = MapPurchaseOrderFromReader(reader);
                    reader.Close();

                    po.Items = await GetPurchaseOrderItemsAsync(conn, po.PurchaseOrderID);
                    return (true, "Purchase order retrieved successfully.", po);
                }

                return (false, "Purchase order not found.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<PurchaseOrderModel>? PurchaseOrders)> GetPurchaseOrdersBySupplierAsync(int supplierId)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view purchase orders.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT po.*, s.SupplierName
                    FROM PurchaseOrders po
                    LEFT JOIN Suppliers s ON po.SupplierID = s.SupplierID
                    WHERE po.SupplierID = @supplierId AND po.IsDeleted = 0
                    ORDER BY po.OrderDate DESC";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@supplierId", supplierId);

                var purchaseOrders = new List<PurchaseOrderModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var po = MapPurchaseOrderFromReader(reader);
                    purchaseOrders.Add(po);
                }
                reader.Close();

                foreach (var po in purchaseOrders)
                {
                    po.Items = await GetPurchaseOrderItemsAsync(conn, po.PurchaseOrderID);
                }

                return (true, "Purchase orders retrieved successfully.", purchaseOrders);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, List<PurchaseOrderModel>? PurchaseOrders)> GetPurchaseOrdersByStatusAsync(string status)
        {
            if (!CanView())
                return (false, "Insufficient permissions to view purchase orders.", null);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT po.*, s.SupplierName
                    FROM PurchaseOrders po
                    LEFT JOIN Suppliers s ON po.SupplierID = s.SupplierID
                    WHERE po.ShippingStatus = @status AND po.IsDeleted = 0
                    ORDER BY po.OrderDate DESC";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@status", status);

                var purchaseOrders = new List<PurchaseOrderModel>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var po = MapPurchaseOrderFromReader(reader);
                    purchaseOrders.Add(po);
                }
                reader.Close();

                foreach (var po in purchaseOrders)
                {
                    po.Items = await GetPurchaseOrderItemsAsync(conn, po.PurchaseOrderID);
                }

                return (true, "Purchase orders retrieved successfully.", purchaseOrders);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", null);
            }
        }

        private async Task<List<PurchaseOrderItemModel>> GetPurchaseOrderItemsAsync(SqlConnection conn, int poId)
        {
            const string query = @"
                SELECT POItemID, PurchaseOrderID, ItemName, Unit, Quantity, Price
                FROM PurchaseOrderItems
                WHERE PurchaseOrderID = @poId";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@poId", poId);

            var items = new List<PurchaseOrderItemModel>();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new PurchaseOrderItemModel
                {
                    POItemID = reader.GetInt32(0),
                    PurchaseOrderID = reader.GetInt32(1),
                    ItemName = reader.GetString(2),
                    Unit = reader.GetString(3),
                    Quantity = reader.GetDecimal(4),
                    Price = reader.GetDecimal(5)
                });
            }

            return items;
        }

        #endregion

        #region UPDATE

        public async Task<(bool Success, string Message)> UpdatePurchaseOrderAsync(PurchaseOrderModel purchaseOrder)
        {
            if (!CanUpdate())
            {
                ShowToast("Access Denied", "You don't have permission to update purchase orders.", ToastType.Error);
                return (false, "Insufficient permissions to update purchase orders.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();
                try
                {
                    // Calculate totals
                    CalculateTotals(purchaseOrder);

                    // Update Purchase Order
                    await UpdatePurchaseOrderMainAsync(conn, transaction, purchaseOrder);

                    // Delete existing items
                    await DeletePurchaseOrderItemsAsync(conn, transaction, purchaseOrder.PurchaseOrderID);

                    // Insert updated items
                    await InsertPurchaseOrderItemsAsync(conn, transaction, purchaseOrder.PurchaseOrderID, purchaseOrder.Items);

                    // Log action
                    await LogActionAsync(conn, transaction, "UPDATE",
                        $"Updated Purchase Order: {purchaseOrder.PONumber}", true);

                    transaction.Commit();

                    ShowToast("Purchase Order Updated",
                        $"PO Number: {purchaseOrder.PONumber} updated successfully",
                        ToastType.Success);

                    return (true, "Purchase order updated successfully.");
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                ShowToast("Database Error", $"Failed to update purchase order: {ex.Message}", ToastType.Error);
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"An unexpected error occurred: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}");
            }
        }

        private async Task UpdatePurchaseOrderMainAsync(SqlConnection conn, SqlTransaction transaction, PurchaseOrderModel po)
        {
            const string query = @"
                UPDATE PurchaseOrders
                SET ExpectedDeliveryDate = @expectedDelivery,
                    ShippingStatus = @shippingStatus,
                    PaymentStatus = @paymentStatus,
                    InvoiceNumber = @invoiceNumber,
                    Subtotal = @subtotal,
                    TaxAmount = @taxAmount,
                    Total = @total,
                    UpdatedAt = GETDATE()
                WHERE PurchaseOrderID = @poId";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@poId", po.PurchaseOrderID);
            cmd.Parameters.AddWithValue("@expectedDelivery", po.ExpectedDeliveryDate);
            cmd.Parameters.AddWithValue("@shippingStatus", po.ShippingStatus);
            cmd.Parameters.AddWithValue("@paymentStatus", po.PaymentStatus);
            cmd.Parameters.AddWithValue("@invoiceNumber", po.InvoiceNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@subtotal", po.Subtotal);
            cmd.Parameters.AddWithValue("@taxAmount", po.TaxAmount);
            cmd.Parameters.AddWithValue("@total", po.Total);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task DeletePurchaseOrderItemsAsync(SqlConnection conn, SqlTransaction transaction, int poId)
        {
            const string query = "DELETE FROM PurchaseOrderItems WHERE PurchaseOrderID = @poId";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@poId", poId);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<(bool Success, string Message)> UpdatePurchaseOrderStatusAsync(
            int poId, string? shippingStatus, string? paymentStatus, string? invoiceNumber)
        {
            if (!CanUpdate())
            {
                ShowToast("Access Denied", "You don't have permission to update purchase order status.", ToastType.Error);
                return (false, "Insufficient permissions to update purchase order status.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var queryBuilder = new StringBuilder("UPDATE PurchaseOrders SET UpdatedAt = GETDATE()");
                var parameters = new List<SqlParameter>();

                if (!string.IsNullOrEmpty(shippingStatus))
                {
                    queryBuilder.Append(", ShippingStatus = @shippingStatus");
                    parameters.Add(new SqlParameter("@shippingStatus", shippingStatus));
                }

                if (!string.IsNullOrEmpty(paymentStatus))
                {
                    queryBuilder.Append(", PaymentStatus = @paymentStatus");
                    parameters.Add(new SqlParameter("@paymentStatus", paymentStatus));
                }

                if (!string.IsNullOrEmpty(invoiceNumber))
                {
                    queryBuilder.Append(", InvoiceNumber = @invoiceNumber");
                    parameters.Add(new SqlParameter("@invoiceNumber", invoiceNumber));
                }

                queryBuilder.Append(" WHERE PurchaseOrderID = @poId");
                parameters.Add(new SqlParameter("@poId", poId));

                using var cmd = new SqlCommand(queryBuilder.ToString(), conn);
                cmd.Parameters.AddRange(parameters.ToArray());

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, null, "UPDATE",
                        $"Updated status for PO ID: {poId}", true);

                    return (true, "Purchase order status updated successfully.");
                }

                return (false, "Purchase order not found.");
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"Failed to update status: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region DELETE

        public async Task<(bool Success, string Message)> DeletePurchaseOrderAsync(int poId)
        {
            if (!CanDelete())
            {
                ShowToast("Access Denied", "Only administrators can delete purchase orders.", ToastType.Error);
                return (false, "Insufficient permissions to delete purchase orders.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Get PO number for logging
                var poResult = await GetPurchaseOrderByIdAsync(poId);
                if (!poResult.Success || poResult.PurchaseOrder == null)
                {
                    return (false, "Purchase order not found.");
                }

                const string query = @"
                    UPDATE PurchaseOrders 
                    SET IsDeleted = 1, UpdatedAt = GETDATE() 
                    WHERE PurchaseOrderID = @poId";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@poId", poId);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, null, "DELETE",
                        $"Deleted Purchase Order: {poResult.PurchaseOrder.PONumber}", true);

                    ShowToast("Purchase Order Deleted",
                        $"PO {poResult.PurchaseOrder.PONumber} has been deleted",
                        ToastType.Success);

                    return (true, "Purchase order deleted successfully.");
                }

                return (false, "Failed to delete purchase order.");
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"Failed to delete purchase order: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region SEND TO INVENTORY

        public async Task<(bool Success, string Message)> SendToInventoryAsync(int poId)
        {
            Console.WriteLine($"[SendToInventoryAsync] Starting - PO ID: {poId}");

            if (!CanUpdate())
            {
                ShowToast("Access Denied", "You don't have permission to send items to inventory.", ToastType.Error);
                return (false, "Insufficient permissions to send items to inventory.");
            }

            if (_productService == null)
            {
                Console.WriteLine("[ERROR] Product service is null");
                ShowToast("Service Error", "Product service is not available.", ToastType.Error);
                return (false, "Product service is not available.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Get PO details
                var poResult = await GetPurchaseOrderByIdAsync(poId);
                if (!poResult.Success || poResult.PurchaseOrder == null)
                {
                    Console.WriteLine($"[ERROR] PO not found: {poId}");
                    return (false, "Purchase order not found.");
                }

                var po = poResult.PurchaseOrder;
                Console.WriteLine($"[SendToInventoryAsync] PO loaded: {po.PONumber}");
                Console.WriteLine($"  - Shipping Status: {po.ShippingStatus}");
                Console.WriteLine($"  - Payment Status: {po.PaymentStatus}");
                Console.WriteLine($"  - Already Sent: {po.SentToInventory}");
                Console.WriteLine($"  - Items: {po.Items?.Count ?? 0}");

                // ⭐ CRITICAL CHECK: Prevent duplicate sends
                if (po.SentToInventory)
                {
                    var sentDate = po.SentToInventoryDate?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown";
                    var sentBy = po.SentToInventoryBy?.ToString() ?? "Unknown";

                    ShowToast("Already Sent",
                        $"This purchase order was already sent to inventory on {sentDate}.",
                        ToastType.Warning);

                    Console.WriteLine($"[BLOCKED] PO already sent on {sentDate} by user {sentBy}");
                    return (false, $"Purchase order already sent to inventory on {sentDate}");
                }

                // Validate status
                if (!po.CanSendToInventory)
                {
                    var message = $"Invalid status - Shipping: {po.ShippingStatus}, Payment: {po.PaymentStatus}";
                    Console.WriteLine($"[ERROR] {message}");
                    ShowToast("Invalid Status",
                        "Purchase order must have 'Delivered' shipping status and 'Paid' payment status.",
                        ToastType.Warning);
                    return (false, message);
                }

                if (po.Items == null || po.Items.Count == 0)
                {
                    Console.WriteLine("[ERROR] No items in PO");
                    ShowToast("No Items", "Purchase order has no items to send to inventory.", ToastType.Warning);
                    return (false, "No items in purchase order.");
                }

                using var transaction = conn.BeginTransaction();
                try
                {
                    int updatedCount = 0;
                    int notFoundCount = 0;
                    var updateDetails = new StringBuilder();
                    updateDetails.AppendLine($"Purchase Order: {po.PONumber}");

                    // ⭐ Process each item and update stock
                    foreach (var item in po.Items)
                    {
                        Console.WriteLine($"[Processing] {item.ItemName} - Qty: {item.Quantity} {item.Unit}");

                        var (updated, oldStock, newStock) = await UpdateProductStockFromPOAsync(conn, transaction, item);

                        if (updated)
                        {
                            updatedCount++;
                            updateDetails.AppendLine($"  ✓ {item.ItemName}: {oldStock} → {newStock} (+{item.Quantity} {item.Unit})");
                            Console.WriteLine($"  ✓ Stock updated: {oldStock} → {newStock}");
                        }
                        else
                        {
                            notFoundCount++;
                            updateDetails.AppendLine($"  ✗ {item.ItemName}: Not found in inventory");
                            Console.WriteLine($"  ✗ Product not found");
                        }
                    }

                    // ⭐ CRITICAL: Mark PO as sent to inventory (prevents re-sending)
                    const string markSentQuery = @"
                        UPDATE PurchaseOrders 
                        SET SentToInventory = 1,
                            SentToInventoryDate = GETDATE(),
                            SentToInventoryBy = @employeeId,
                            UpdatedAt = GETDATE()
                        WHERE PurchaseOrderID = @poId";

                    using (var cmd = new SqlCommand(markSentQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@poId", poId);
                        cmd.Parameters.AddWithValue("@employeeId", CurrentUserModel.UserId ?? (object)DBNull.Value);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        Console.WriteLine($"[Database] Marked PO as sent: {rowsAffected} rows affected");
                    }

                    // Update supplier's products list
                    if (po.SupplierID.HasValue)
                    {
                        await UpdateSupplierProductsAsync(conn, transaction, po.SupplierID.Value, po.Items);
                    }

                    // Log action with full details
                    await LogActionAsync(conn, transaction, "INVENTORY",
                        $"Sent PO {po.PONumber} to inventory\n{updateDetails}", true);

                    transaction.Commit();

                    // Prepare success message
                    var message = notFoundCount > 0
                        ? $"{updatedCount} items sent to inventory. {notFoundCount} items not found in products."
                        : $"All {updatedCount} items successfully sent to inventory.";

                    ShowToast("Sent to Inventory", message, ToastType.Success);
                    Console.WriteLine($"[SUCCESS] {message}");

                    // Notify UI to refresh
                    DashboardEventService.Instance.NotifyProductUpdated();

                    return (true, message);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"[ERROR] Transaction rolled back: {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception: {ex.Message}\n{ex.StackTrace}");
                ShowToast("Error", $"Failed to send to inventory: {ex.Message}", ToastType.Error);
                return (false, $"Error: {ex.Message}");
            }
        }

        private async Task<(bool Success, int OldStock, int NewStock)> UpdateProductStockFromPOAsync(
            SqlConnection conn, SqlTransaction transaction, PurchaseOrderItemModel item)
        {
            // Try to find existing product by name
            const string findQuery = @"
                SELECT ProductID, CurrentStock, ProductName 
                FROM Products 
                WHERE ProductName = @itemName AND IsDeleted = 0";

            using var findCmd = new SqlCommand(findQuery, conn, transaction);
            findCmd.Parameters.AddWithValue("@itemName", item.ItemName);

            using var reader = await findCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var productId = reader.GetInt32(0);
                var currentStock = reader.GetInt32(1);
                var productName = reader.GetString(2);
                reader.Close();

                // ⭐ CONVERT: Handle unit conversions properly
                int quantityToAdd = ConvertPOQuantityToStock(item.Quantity, item.Unit);

                // ⭐ ACCUMULATE: Add to existing stock (this allows multiple POs with same item)
                var newStock = currentStock + quantityToAdd;

                Console.WriteLine($"📦 Stock Update: {productName}");
                Console.WriteLine($"   - Current Stock: {currentStock}");
                Console.WriteLine($"   - Adding: {quantityToAdd} (from {item.Quantity} {item.Unit})");
                Console.WriteLine($"   - New Total: {newStock}");

                // Update product stock
                const string updateQuery = @"
                    UPDATE Products 
                    SET CurrentStock = @newStock,
                        Status = CASE 
                            WHEN @newStock > 0 THEN 'In Stock' 
                            ELSE 'Out Of Stock' 
                        END,
                        UpdatedAt = GETDATE()
                    WHERE ProductID = @productId";

                using var updateCmd = new SqlCommand(updateQuery, conn, transaction);
                updateCmd.Parameters.AddWithValue("@productId", productId);
                updateCmd.Parameters.AddWithValue("@newStock", newStock);

                await updateCmd.ExecuteNonQueryAsync();
                return (true, currentStock, newStock);
            }
            else
            {
                reader.Close();
                Console.WriteLine($"⚠️ Warning: Product '{item.ItemName}' not found in inventory");
                return (false, 0, 0);
            }
        }

        private async Task UpdateSupplierProductsAsync(SqlConnection conn, SqlTransaction transaction,
            int supplierId, List<PurchaseOrderItemModel> items)
        {
            try
            {
                // Get current supplier data
                const string getSupplierQuery = @"
                    SELECT Products, DeliverySchedule 
                    FROM Suppliers 
                    WHERE SupplierID = @supplierId";

                string? currentProducts = null;
                string? currentDeliverySchedule = null;

                using (var cmd = new SqlCommand(getSupplierQuery, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@supplierId", supplierId);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        currentProducts = reader.IsDBNull(0) ? null : reader.GetString(0);
                        currentDeliverySchedule = reader.IsDBNull(1) ? null : reader.GetString(1);
                    }
                }

                // Build new products list (combine existing + new items)
                var existingProducts = string.IsNullOrWhiteSpace(currentProducts)
                    ? new List<string>()
                    : currentProducts.Split(',').Select(p => p.Trim()).ToList();

                var newProducts = items.Select(i => i.ItemName).Where(name => !string.IsNullOrEmpty(name)).ToList();

                // Merge products (avoid duplicates)
                var allProducts = existingProducts.Union(newProducts).Distinct().OrderBy(p => p).ToList();
                var updatedProducts = string.Join(", ", allProducts);

                // Update supplier
                const string updateQuery = @"
                    UPDATE Suppliers 
                    SET Products = @products,
                        DeliverySchedule = @deliverySchedule,
                        UpdatedAt = GETDATE()
                    WHERE SupplierID = @supplierId";

                using var updateCmd = new SqlCommand(updateQuery, conn, transaction);
                updateCmd.Parameters.AddWithValue("@supplierId", supplierId);
                updateCmd.Parameters.AddWithValue("@products", updatedProducts);
                updateCmd.Parameters.AddWithValue("@deliverySchedule", currentDeliverySchedule ?? "Every 7 days");

                await updateCmd.ExecuteNonQueryAsync();

                Console.WriteLine($"[UpdateSupplierProducts] Updated supplier {supplierId}");
                Console.WriteLine($"  - Products: {updatedProducts}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpdateSupplierProducts] Error: {ex.Message}");
                // Don't throw - we don't want to rollback the entire transaction if supplier update fails
            }
        }

        #endregion

        #region UTILITY METHODS

        public async Task<string> GeneratePONumberAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var year = DateTime.Now.Year;
                var query = @"
                    SELECT COUNT(*) 
                    FROM PurchaseOrders 
                    WHERE PONumber LIKE @pattern AND YEAR(OrderDate) = @year";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@pattern", $"PO-{year}-%");
                cmd.Parameters.AddWithValue("@year", year);

                var count = (int)await cmd.ExecuteScalarAsync();
                var sequence = (count + 1).ToString("D5");

                return $"PO-{year}-{sequence}";
            }
            catch
            {
                // Fallback to random number if database query fails
                var random = new Random();
                var randomNumber = random.Next(100000, 999999);
                return $"PO-{DateTime.Now.Year}-{randomNumber}";
            }
        }

        public async Task<(bool Success, int TotalPOs, int PendingPOs, int DeliveredPOs, int CancelledPOs)> GetPurchaseOrderStatisticsAsync()
        {
            if (!CanView())
                return (false, 0, 0, 0, 0);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    @"SELECT 
                        COUNT(*) as TotalPOs,
                        SUM(CASE WHEN ShippingStatus = 'Pending' THEN 1 ELSE 0 END) as PendingPOs,
                        SUM(CASE WHEN ShippingStatus = 'Delivered' THEN 1 ELSE 0 END) as DeliveredPOs,
                        SUM(CASE WHEN ShippingStatus = 'Cancelled' THEN 1 ELSE 0 END) as CancelledPOs
                      FROM PurchaseOrders
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
                Console.WriteLine($"[GetPurchaseOrderStatisticsAsync] {ex.Message}");
                return (false, 0, 0, 0, 0);
            }
        }

        private void CalculateTotals(PurchaseOrderModel po)
        {
            po.Subtotal = po.Items.Sum(i => i.LineTotal);
            po.TaxAmount = po.Subtotal * po.TaxRate;
            po.Total = po.Subtotal + po.TaxAmount;
        }

        private PurchaseOrderModel MapPurchaseOrderFromReader(SqlDataReader reader)
        {
            return new PurchaseOrderModel
            {
                PurchaseOrderID = reader.GetInt32(reader.GetOrdinal("PurchaseOrderID")),
                PONumber = reader.GetString(reader.GetOrdinal("PONumber")),

                // ⭐ FIX: Proper null handling for SupplierID
                SupplierID = reader.IsDBNull(reader.GetOrdinal("SupplierID"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("SupplierID")),

                // ⭐ FIX: Proper null handling for SupplierName
                SupplierName = reader.IsDBNull(reader.GetOrdinal("SupplierName"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("SupplierName")),

                OrderDate = reader.GetDateTime(reader.GetOrdinal("OrderDate")),
                ExpectedDeliveryDate = reader.GetDateTime(reader.GetOrdinal("ExpectedDeliveryDate")),
                ShippingStatus = reader.GetString(reader.GetOrdinal("ShippingStatus")),
                PaymentStatus = reader.GetString(reader.GetOrdinal("PaymentStatus")),

                // ⭐ FIX: Proper null handling for InvoiceNumber
                InvoiceNumber = reader.IsDBNull(reader.GetOrdinal("InvoiceNumber"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("InvoiceNumber")),

                Subtotal = reader.GetDecimal(reader.GetOrdinal("Subtotal")),
                TaxRate = reader.GetDecimal(reader.GetOrdinal("TaxRate")),
                TaxAmount = reader.GetDecimal(reader.GetOrdinal("TaxAmount")),
                Total = reader.GetDecimal(reader.GetOrdinal("Total")),

                // ⭐ FIX: Proper null handling for CreatedByEmployeeID
                CreatedByEmployeeID = reader.IsDBNull(reader.GetOrdinal("CreatedByEmployeeID"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("CreatedByEmployeeID")),

                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),

                // ⭐ FIX: Proper null handling for UpdatedAt
                UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),

                // ⭐ CRITICAL: Handle SentToInventory tracking fields
                SentToInventory = reader.IsDBNull(reader.GetOrdinal("SentToInventory"))
                    ? false
                    : reader.GetBoolean(reader.GetOrdinal("SentToInventory")),

                SentToInventoryDate = reader.IsDBNull(reader.GetOrdinal("SentToInventoryDate"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("SentToInventoryDate")),

                SentToInventoryBy = reader.IsDBNull(reader.GetOrdinal("SentToInventoryBy"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("SentToInventoryBy"))
            };
        }

        private async Task LogActionAsync(SqlConnection conn, SqlTransaction? transaction,
            string actionType, string description, bool success)
        {
            try
            {
                const string query = @"
                    INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                    VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)";

                using var cmd = new SqlCommand(query, conn, transaction);
                cmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@actionType", actionType);
                cmd.Parameters.AddWithValue("@description", description);
                cmd.Parameters.AddWithValue("@success", success);
                cmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                DashboardEventService.Instance.NotifyRecentLogsUpdated();
                DashboardEventService.Instance.NotifyPurchaseOrderAdded();
                DashboardEventService.Instance.NotifyPurchaseOrderDeleted();
                DashboardEventService.Instance.NotifyPurchaseOrderUpdated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        private int ConvertPOQuantityToStock(decimal quantity, string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                return (int)Math.Floor(quantity);
            }

            string unitLower = unit.ToLowerInvariant();

            return unitLower switch
            {
                "pcs" or "unit" or "piece" or "pieces" => (int)Math.Floor(quantity),
                "box" or "pack" or "case" => (int)Math.Floor(quantity * 12), // Assume 12 per box
                "kg" or "kilogram" or "kilograms" => (int)Math.Floor(quantity),
                "lbs" or "pound" or "pounds" => (int)Math.Floor(quantity),
                "liter" or "liters" or "l" => (int)Math.Floor(quantity),
                _ => (int)Math.Floor(quantity)
            };
        }

        private void ShowToast(string title, string content, ToastType type)
        {
            var toast = _toastManager.CreateToast(title)
                .WithContent(content)
                .DismissOnClick();

            switch (type)
            {
                case ToastType.Success:
                    toast.ShowSuccess();
                    break;
                case ToastType.Warning:
                    toast.ShowWarning();
                    break;
                case ToastType.Error:
                    toast.ShowError();
                    break;
                case ToastType.Info:
                    toast.ShowInfo();
                    break;
            }
        }

        private enum ToastType
        {
            Success,
            Warning,
            Error,
            Info
        }

        #endregion
    }
}
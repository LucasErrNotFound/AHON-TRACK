using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private bool CanCreate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanView() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;

        #endregion

        #region CREATE

        public async Task<bool> ProcessPaymentAsync(List<SellingModel> cartItems, CustomerModel customer, int employeeId, string paymentMethod)
        {
            if (!CanCreate())
            {
                ShowError("Access Denied", "You do not have permission to record sales.");
                return false;
            }

            if (cartItems == null || cartItems.Count == 0)
            {
                ShowError("Empty Cart", "No items to process.");
                return false;
            }

            if (customer == null)
            {
                ShowError("No Customer", "Please select a customer.");
                return false;
            }

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);

            try
            {
                decimal totalAmount = 0;
                int totalTransactions = 0;

                foreach (var item in cartItems)
                {
                    decimal itemTotal = await CalculateItemTotal(item, customer, conn, transaction);
                    totalAmount += itemTotal;
                    totalTransactions++;

                    await RecordSaleAsync(item, customer, employeeId, itemTotal, conn, transaction);
                    await RecordPurchaseAsync(item, customer, itemTotal, conn, transaction);
                    await HandleInventoryAndSessionsAsync(item, customer, conn, transaction);
                }

                await UpdateDailySalesAsync(employeeId, totalAmount, totalTransactions, conn, transaction);

                transaction.Commit();

                await LogSuccessfulPayment(conn, customer, totalAmount, paymentMethod, cartItems);
                ShowPaymentSuccess(totalAmount, paymentMethod, cartItems);

                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                await LogFailedPayment(conn, customer, ex.Message);
                ShowError("Payment Failed", $"Transaction failed: {ex.Message}");
                return false;
            }
        }

        private async Task<decimal> CalculateItemTotal(SellingModel item, CustomerModel customer, SqlConnection conn, SqlTransaction transaction)
        {
            decimal itemTotal = item.Price * item.Quantity;

            if (item.Category == CategoryConstants.GymPackage)
            {
                itemTotal = await ApplyPackageDiscount(item, customer, conn, transaction);
            }
            else if (item.Category == CategoryConstants.Product)
            {
                itemTotal = await ApplyProductDiscount(item, conn, transaction);
            }

            return itemTotal;
        }

        private async Task<decimal> ApplyPackageDiscount(SellingModel item, CustomerModel customer, SqlConnection conn, SqlTransaction transaction)
        {
            string query = @"SELECT Discount, DiscountType, DiscountFor, ValidFrom, ValidTo FROM Packages WHERE PackageID = @PackageID";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@PackageID", item.SellingID);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                decimal discountValue = reader["Discount"] != DBNull.Value ? Convert.ToDecimal(reader["Discount"]) : 0;
                string discountType = reader["DiscountType"]?.ToString()?.Trim() ?? "none";
                string discountFor = reader["DiscountFor"]?.ToString()?.Trim() ?? "All";
                DateTime? validFrom = reader["ValidFrom"] != DBNull.Value ? Convert.ToDateTime(reader["ValidFrom"]) : null;
                DateTime? validTo = reader["ValidTo"] != DBNull.Value ? Convert.ToDateTime(reader["ValidTo"]) : null;

                bool isValidDate = (!validFrom.HasValue || DateTime.Now >= validFrom) &&
                                   (!validTo.HasValue || DateTime.Now <= validTo);

                bool isEligibleCustomer =
                    discountFor.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                    (customer.CustomerType.Equals("Member", StringComparison.OrdinalIgnoreCase) &&
                     discountFor.Equals("Gym Members", StringComparison.OrdinalIgnoreCase));

                if (isValidDate && isEligibleCustomer && discountValue > 0 && discountType != "none")
                {
                    decimal discountAmount = discountType.Equals("percentage", StringComparison.OrdinalIgnoreCase)
                        ? item.Price * (discountValue / 100m)
                        : discountValue;

                    return (item.Price - discountAmount) * item.Quantity;
                }
            }

            return item.Price * item.Quantity;
        }

        private async Task<decimal> ApplyProductDiscount(SellingModel item, SqlConnection conn, SqlTransaction transaction)
        {
            string query = @"SELECT Price, DiscountedPrice, IsPercentageDiscount FROM Products WHERE ProductID = @ProductID";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@ProductID", item.SellingID);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                decimal price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0;
                decimal discountValue = reader["DiscountedPrice"] != DBNull.Value ? Convert.ToDecimal(reader["DiscountedPrice"]) : 0;
                bool isPercentage = reader["IsPercentageDiscount"] != DBNull.Value && Convert.ToBoolean(reader["IsPercentageDiscount"]);

                if (discountValue > 0 && isPercentage)
                {
                    decimal finalPrice = price - (price * (discountValue / 100m));
                    return Math.Max(0, finalPrice * item.Quantity);
                }
            }

            return item.Price * item.Quantity;
        }

        private async Task RecordSaleAsync(SellingModel item, CustomerModel customer, int employeeId, decimal itemTotal, SqlConnection conn, SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO Sales (SaleDate, PackageID, ProductID, CustomerID, MemberID, Quantity, Amount, RecordedBy)
                VALUES (@SaleDate, @PackageID, @ProductID, @CustomerID, @MemberID, @Quantity, @Amount, @RecordedBy);
                SELECT SCOPE_IDENTITY();";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@SaleDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@PackageID", item.Category == CategoryConstants.GymPackage ? (object)item.SellingID : DBNull.Value);
            cmd.Parameters.AddWithValue("@ProductID", item.Category == CategoryConstants.Product ? (object)item.SellingID : DBNull.Value);
            cmd.Parameters.AddWithValue("@CustomerID", customer.CustomerType == CategoryConstants.WalkIn ? (object)customer.CustomerID : DBNull.Value);
            cmd.Parameters.AddWithValue("@MemberID", customer.CustomerType == CategoryConstants.Member ? (object)customer.CustomerID : DBNull.Value);
            cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
            cmd.Parameters.AddWithValue("@Amount", itemTotal);
            cmd.Parameters.AddWithValue("@RecordedBy", employeeId);

            await cmd.ExecuteScalarAsync();
        }

        private async Task RecordPurchaseAsync(SellingModel item, CustomerModel customer, decimal itemTotal, SqlConnection conn, SqlTransaction transaction)
        {
            string insertPurchase = @"
                INSERT INTO CustomerPurchases (CustomerID, CustomerType, SellingID, Category, Quantity, TotalAmount, PurchaseDate)
                VALUES (@CustomerID, @CustomerType, @SellingID, @Category, @Quantity, @TotalAmount, GETDATE());
                SELECT SCOPE_IDENTITY();";

            using var purchaseCmd = new SqlCommand(insertPurchase, conn, transaction);
            purchaseCmd.Parameters.AddWithValue("@CustomerID", customer.CustomerID);
            purchaseCmd.Parameters.AddWithValue("@CustomerType", customer.CustomerType);
            purchaseCmd.Parameters.AddWithValue("@SellingID", item.SellingID);
            purchaseCmd.Parameters.AddWithValue("@Category", item.Category);
            purchaseCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
            purchaseCmd.Parameters.AddWithValue("@TotalAmount", itemTotal);

            var purchaseId = Convert.ToInt32(await purchaseCmd.ExecuteScalarAsync());

            await RecordPurchaseDetailsAsync(purchaseId, item, conn, transaction);
        }

        private async Task RecordPurchaseDetailsAsync(int purchaseId, SellingModel item, SqlConnection conn, SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO PurchaseDetails (PurchaseID, SellingID, Category, Quantity, UnitPrice)
                VALUES (@PurchaseID, @SellingID, @Category, @Quantity, @UnitPrice);";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@PurchaseID", purchaseId);
            cmd.Parameters.AddWithValue("@SellingID", item.SellingID);
            cmd.Parameters.AddWithValue("@Category", item.Category);
            cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
            cmd.Parameters.AddWithValue("@UnitPrice", item.Price);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task HandleInventoryAndSessionsAsync(SellingModel item, CustomerModel customer, SqlConnection conn, SqlTransaction transaction)
        {
            if (item.Category == CategoryConstants.Product)
            {
                await UpdateProductStockAsync(item, conn, transaction);
            }
            else if (item.Category == CategoryConstants.GymPackage && customer.CustomerType == CategoryConstants.Member)
            {
                await HandleMemberSessionsAsync(item, customer, conn, transaction);
            }
        }

        private async Task UpdateProductStockAsync(SellingModel item, SqlConnection conn, SqlTransaction transaction)
        {
            string checkStock = @"SELECT CurrentStock FROM Products WHERE ProductID = @ProductID;";

            using var checkCmd = new SqlCommand(checkStock, conn, transaction);
            checkCmd.Parameters.AddWithValue("@ProductID", item.SellingID);
            var currentStock = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (currentStock < item.Quantity)
            {
                throw new InvalidOperationException($"Insufficient stock for {item.Title}. Available: {currentStock}, Required: {item.Quantity}");
            }

            string updateStock = @"
                UPDATE Products 
                SET CurrentStock = CurrentStock - @Qty,
                    Status = CASE 
                        WHEN (CurrentStock - @Qty) <= 0 THEN 'Out of Stock'
                        ELSE Status
                    END
                WHERE ProductID = @ProductID;";

            using var stockCmd = new SqlCommand(updateStock, conn, transaction);
            stockCmd.Parameters.AddWithValue("@Qty", item.Quantity);
            stockCmd.Parameters.AddWithValue("@ProductID", item.SellingID);
            await stockCmd.ExecuteNonQueryAsync();
        }

        private async Task HandleMemberSessionsAsync(SellingModel item, CustomerModel customer, SqlConnection conn, SqlTransaction transaction)
        {
            string getDuration = @"SELECT Duration FROM Packages WHERE PackageID = @PackageID;";
            string durationStr = "";

            using (var cmd = new SqlCommand(getDuration, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@PackageID", item.SellingID);
                var result = await cmd.ExecuteScalarAsync();
                durationStr = result?.ToString()?.Trim()?.ToLower() ?? "";
            }

            if (durationStr != "session" && durationStr != "one-time only")
                return;

            int sessionsToAdd = (durationStr == "one-time only" ? 1 : 1) * item.Quantity;

            string checkExisting = @"
                SELECT SessionID, SessionsLeft 
                FROM MemberSessions 
                WHERE CustomerID = @CustomerID AND PackageID = @PackageID;";

            int? existingSessionId = null;

            using (var checkCmd = new SqlCommand(checkExisting, conn, transaction))
            {
                checkCmd.Parameters.AddWithValue("@CustomerID", customer.CustomerID);
                checkCmd.Parameters.AddWithValue("@PackageID", item.SellingID);

                using var reader = await checkCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    existingSessionId = reader.GetInt32(0);
                }
            }

            if (existingSessionId.HasValue)
            {
                await UpdateExistingSessionAsync(existingSessionId.Value, sessionsToAdd, conn, transaction);
            }
            else
            {
                await CreateNewSessionAsync(customer.CustomerID, item.SellingID, sessionsToAdd, conn, transaction);
            }
        }

        private async Task UpdateExistingSessionAsync(int sessionId, int sessionsToAdd, SqlConnection conn, SqlTransaction transaction)
        {
            string query = @"
                UPDATE MemberSessions 
                SET SessionsLeft = SessionsLeft + @NewSessions
                WHERE SessionID = @SessionID;";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@SessionID", sessionId);
            cmd.Parameters.AddWithValue("@NewSessions", sessionsToAdd);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task CreateNewSessionAsync(int customerId, int packageId, int sessions, SqlConnection conn, SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO MemberSessions (CustomerID, PackageID, SessionsLeft, StartDate)
                VALUES (@CustomerID, @PackageID, @SessionsLeft, GETDATE());";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@CustomerID", customerId);
            cmd.Parameters.AddWithValue("@PackageID", packageId);
            cmd.Parameters.AddWithValue("@SessionsLeft", sessions);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateDailySalesAsync(int employeeId, decimal totalAmount, int totalTransactions, SqlConnection conn, SqlTransaction transaction)
        {
            string query = @"
                MERGE DailySales AS target
                USING (SELECT CAST(GETDATE() AS DATE) AS SaleDate, @EmployeeID AS EmployeeID) AS source
                ON target.SaleDate = source.SaleDate AND target.TransactionByEmployeeID = source.EmployeeID
                WHEN MATCHED THEN
                    UPDATE SET 
                        TotalSales = target.TotalSales + @TotalAmount,
                        TotalTransactions = target.TotalTransactions + @TotalTransactions,
                        TransactionUpdatedDate = SYSDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (SaleDate, TotalSales, TotalTransactions, TransactionByEmployeeID)
                    VALUES (source.SaleDate, @TotalAmount, @TotalTransactions, source.EmployeeID);";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
            cmd.Parameters.AddWithValue("@TotalTransactions", totalTransactions);
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region READ

        public async Task<List<SellingModel>> GetAllGymPackagesAsync()
        {
            var packages = new List<SellingModel>();

            if (!CanView())
            {
                ShowError("Access Denied", "You do not have permission to view gym packages.");
                return packages;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT PackageID, PackageName, Description, Price, Duration, Features
                    FROM Packages
                    WHERE GETDATE() BETWEEN ValidFrom AND ValidTo 
                        AND IsDeleted = 0
                        AND Duration LIKE '%Session%'";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    packages.Add(new SellingModel
                    {
                        SellingID = reader.GetInt32(reader.GetOrdinal("PackageID")),
                        Title = reader["PackageName"]?.ToString() ?? string.Empty,
                        Description = reader["Description"]?.ToString() ?? string.Empty,
                        Category = CategoryConstants.GymPackage,
                        Price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0,
                        Stock = 999,
                        ImagePath = null,
                        Features = reader["Features"]?.ToString() ?? string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                ShowError("Load Packages Failed", $"Error loading packages: {ex.Message}");
            }

            return packages;
        }

        public async Task<List<SellingModel>> GetAllProductsAsync()
        {
            var products = new List<SellingModel>();

            if (!CanView())
            {
                ShowError("Access Denied", "You do not have permission to view products.");
                return products;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT ProductID, ProductName, Description, Category, Price, CurrentStock, ProductImagePath
                    FROM Products 
                    WHERE Status = 'In Stock' 
                        AND CurrentStock > 0 
                        AND IsDeleted = 0;";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    products.Add(new SellingModel
                    {
                        SellingID = reader.GetInt32(reader.GetOrdinal("ProductID")),
                        Title = reader["ProductName"]?.ToString() ?? string.Empty,
                        Description = reader["Description"]?.ToString() ?? string.Empty,
                        Category = reader["Category"]?.ToString() ?? CategoryConstants.Product,
                        Price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0,
                        Stock = reader["CurrentStock"] != DBNull.Value ? Convert.ToInt32(reader["CurrentStock"]) : 0,
                        ImagePath = reader["ProductImagePath"] != DBNull.Value ? (byte[])reader["ProductImagePath"] : null
                    });
                }
            }
            catch (Exception ex)
            {
                ShowError("Load Products Failed", $"Error loading products: {ex.Message}");
            }

            return products;
        }

        public async Task<List<CustomerModel>> GetAllCustomersAsync()
        {
            var customers = new List<CustomerModel>();

            if (!CanView())
            {
                ShowError("Access Denied", "You do not have permission to view customer data.");
                return customers;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        m.MemberID AS ID,
                        m.FirstName,
                        m.LastName,
                        @MemberType AS CustomerType,
                        mc.CheckIn
                    FROM Members m
                    INNER JOIN MemberCheckIns mc ON m.MemberID = mc.MemberID
                    WHERE CAST(mc.DateAttendance AS DATE) = CAST(GETDATE() AS DATE)
                        AND mc.CheckOut IS NULL
                        AND mc.IsDeleted = 0
                        AND m.Status = 'Active'
                    
                    UNION ALL
                    
                    SELECT 
                        wc.CustomerID AS ID,
                        wc.FirstName,
                        wc.LastName,
                        @WalkInType AS CustomerType,
                        wr.CheckIn
                    FROM WalkInCustomers wc
                    INNER JOIN WalkInRecords wr ON wc.CustomerID = wr.CustomerID
                    WHERE CAST(wr.Attendance AS DATE) = CAST(GETDATE() AS DATE)
                        AND wr.CheckOut IS NULL
                        AND wr.IsDeleted = 0
                    
                    ORDER BY CheckIn DESC;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@MemberType", CategoryConstants.Member);
                cmd.Parameters.AddWithValue("@WalkInType", CategoryConstants.WalkIn);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    customers.Add(new CustomerModel
                    {
                        CustomerID = reader["ID"] is int id ? id : 0,
                        FirstName = reader["FirstName"]?.ToString() ?? string.Empty,
                        LastName = reader["LastName"]?.ToString() ?? string.Empty,
                        CustomerType = reader["CustomerType"]?.ToString() ?? string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                ShowError("Database Error", $"Error loading customers: {ex.Message}");
            }

            return customers;
        }

        public async Task<List<RecentPurchaseModel>> GetRecentPurchasesAsync(int limit = 50)
        {
            var recentPurchases = new List<RecentPurchaseModel>();

            if (!CanView())
            {
                ShowError("Access Denied", "You do not have permission to view purchase data.");
                return recentPurchases;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT TOP (@Limit)
                        s.SaleID,
                        s.SaleDate,
                        s.Amount,
                        s.Quantity,
                        CASE 
                            WHEN s.ProductID IS NOT NULL THEN p.ProductName
                            WHEN s.PackageID IS NOT NULL THEN pkg.PackageName
                            ELSE 'Unknown Item'
                        END AS ItemName,
                        CASE 
                            WHEN s.ProductID IS NOT NULL THEN 'Product'
                            WHEN s.PackageID IS NOT NULL THEN 'Gym Package'
                            ELSE 'Unknown'
                        END AS ItemType,
                        CASE 
                            WHEN s.MemberID IS NOT NULL THEN CONCAT(m.FirstName, ' ', m.LastName)
                            WHEN s.CustomerID IS NOT NULL THEN CONCAT(wc.FirstName, ' ', wc.LastName)
                            ELSE 'Unknown Customer'
                        END AS CustomerName,
                        CASE 
                            WHEN s.MemberID IS NOT NULL THEN 'Gym Member'
                            WHEN s.CustomerID IS NOT NULL THEN 'Walk-in'
                            ELSE 'Unknown'
                        END AS CustomerType,
                        ProfilePicture AS AvatarSource
                    FROM Sales s
                    LEFT JOIN Products p ON s.ProductID = p.ProductID
                    LEFT JOIN Packages pkg ON s.PackageID = pkg.PackageID
                    LEFT JOIN Members m ON s.MemberID = m.MemberID
                    LEFT JOIN WalkInCustomers wc ON s.CustomerID = wc.CustomerID
                    ORDER BY s.SaleDate DESC;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Limit", limit);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    recentPurchases.Add(new RecentPurchaseModel
                    {
                        SaleID = reader.GetInt32(reader.GetOrdinal("SaleID")),
                        PurchaseDate = reader.GetDateTime(reader.GetOrdinal("SaleDate")),
                        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                        Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                        ItemName = reader["ItemName"]?.ToString() ?? "Unknown Item",
                        ItemType = reader["ItemType"]?.ToString() ?? "Unknown",
                        CustomerName = reader["CustomerName"]?.ToString() ?? "Unknown Customer",
                        CustomerType = reader["CustomerType"]?.ToString() ?? "Unknown",
                        AvatarSource = reader["AvatarSource"] != DBNull.Value ? (byte[])reader["AvatarSource"] : null
                    });
                }
            }
            catch (Exception ex)
            {
                ShowError("Database Error", $"Error loading recent purchases: {ex.Message}");
            }

            return recentPurchases;
        }

        public async Task<List<InvoiceModel>> GetInvoicesByDateAsync(DateTime date)
        {
            var invoices = new List<InvoiceModel>();

            if (!CanView())
            {
                ShowError("Access Denied", "You do not have permission to view invoice data.");
                return invoices;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        s.SaleID,
                        CASE 
                            WHEN s.MemberID IS NOT NULL THEN CONCAT(m.FirstName, ' ', m.LastName)
                            WHEN s.CustomerID IS NOT NULL THEN CONCAT(wc.FirstName, ' ', wc.LastName)
                            ELSE 'Unknown Customer'
                        END AS CustomerName,
                        CASE 
                            WHEN s.ProductID IS NOT NULL THEN p.ProductName
                            WHEN s.PackageID IS NOT NULL THEN pkg.PackageName
                            ELSE 'Unknown Item'
                        END AS PurchasedItem,
                        s.Quantity,
                        s.Amount,
                        s.SaleDate
                    FROM Sales s
                    LEFT JOIN Products p ON s.ProductID = p.ProductID
                    LEFT JOIN Packages pkg ON s.PackageID = pkg.PackageID
                    LEFT JOIN Members m ON s.MemberID = m.MemberID
                    LEFT JOIN WalkInCustomers wc ON s.CustomerID = wc.CustomerID
                    WHERE CAST(s.SaleDate AS DATE) = @SelectedDate
                    ORDER BY s.SaleDate DESC;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@SelectedDate", date.Date);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    invoices.Add(new InvoiceModel
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("SaleID")),
                        CustomerName = reader["CustomerName"]?.ToString() ?? "Unknown",
                        PurchasedItem = reader["PurchasedItem"]?.ToString() ?? "Unknown",
                        Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                        DatePurchased = reader.GetDateTime(reader.GetOrdinal("SaleDate"))
                    });
                }
            }
            catch (Exception ex)
            {
                ShowError("Database Error", $"Error loading invoices: {ex.Message}");
            }

            return invoices;
        }

        #endregion

        #region UPDATE

        // No update operations required for this service
        // All business logic is handled through the CREATE operations

        #endregion

        #region DELETE

        // No delete operations required for this service
        // Sales records are immutable for audit trail purposes

        #endregion

        #region UTILITY

        private void ShowError(string title, string message)
        {
            _toastManager.CreateToast(title)
                .WithContent(message)
                .ShowError();
        }

        private void ShowSuccess(string title, string message)
        {
            _toastManager.CreateToast(title)
                .WithContent(message)
                .ShowSuccess();
        }

        private void ShowPaymentSuccess(decimal totalAmount, string paymentMethod, List<SellingModel> cartItems)
        {
            ShowSuccess("Payment Successful", $"Transaction completed. Total: ₱{totalAmount:N2} via {paymentMethod}");

            bool hasPackageDiscount = cartItems.Any(i => i.Category == CategoryConstants.GymPackage);
            bool hasProductDiscount = cartItems.Any(i => i.Category == CategoryConstants.Product);

            if (hasPackageDiscount || hasProductDiscount)
            {
                string message = hasPackageDiscount && hasProductDiscount
                    ? "Package and product discounts applied successfully."
                    : hasPackageDiscount
                        ? "Package discounts applied successfully."
                        : "Product discounts applied successfully.";

                ShowSuccess("Discount Applied", message);
            }
        }

        private async Task LogSuccessfulPayment(SqlConnection conn, CustomerModel customer, decimal totalAmount, string paymentMethod, List<SellingModel> cartItems)
        {
            string itemsList = string.Join(", ", cartItems.Select(i => $"{i.Title} x{i.Quantity}"));
            string description = $"Payment processed for {customer.FirstName} {customer.LastName} ({customer.CustomerType}). Total: ₱{totalAmount:N2} via {paymentMethod}. Items: {itemsList}";
            await LogActionAsync(conn, "Purchase", description, true);
        }

        private async Task LogFailedPayment(SqlConnection conn, CustomerModel customer, string errorMessage)
        {
            try
            {
                DashboardEventService.Instance.NotifyProductUpdated();
                string description = $"Failed to process payment for {customer?.FirstName} {customer?.LastName}. Error: {errorMessage}";
                await LogActionAsync(conn, "Purchase", description, false);
            }
            catch
            {
                // Silently ignore logging errors to prevent masking the original exception
            }
        }

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                string query = @"
                    INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                    VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@success", success);
                cmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                NotifyDashboardEvents();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] Failed to log action: {ex.Message}");
            }
        }

        private void NotifyDashboardEvents()
        {
            DashboardEventService.Instance.NotifyRecentLogsUpdated();
            DashboardEventService.Instance.NotifySalesUpdated();
            DashboardEventService.Instance.NotifyChartDataUpdated();
            DashboardEventService.Instance.NotifyProductPurchased();
        }

        #endregion
    }
}
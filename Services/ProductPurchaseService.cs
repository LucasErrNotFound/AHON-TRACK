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
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;
        }

        #endregion

        #region CREATE

        public async Task<bool> ProcessPaymentAsync(List<SellingModel> cartItems, CustomerModel customer, int employeeId, string paymentMethod)
        {
            if (!CanCreate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to record sales.")
                    .ShowError();
                return false;
            }

            if (cartItems == null || cartItems.Count == 0)
            {
                _toastManager.CreateToast("Empty Cart")
                    .WithContent("No items to process.")
                    .ShowError();
                return false;
            }

            if (customer == null)
            {
                _toastManager.CreateToast("No Customer")
                    .WithContent("Please select a customer.")
                    .ShowError();
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
                    decimal itemTotal = item.Price * item.Quantity;
                    if (item.Category == CategoryConstants.GymPackage)
                    {
                        string getDiscountQuery = @"SELECT Discount, DiscountType, DiscountFor, ValidFrom, ValidTo FROM Packages WHERE PackageID = @PackageID";

                        using var discountCmd = new SqlCommand(getDiscountQuery, conn, transaction);
                        discountCmd.Parameters.AddWithValue("@PackageID", item.SellingID);

                        using var reader = await discountCmd.ExecuteReaderAsync();
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
                                decimal discountAmount = 0;
                                if (discountType.Equals("percentage", StringComparison.OrdinalIgnoreCase))
                                    discountAmount = item.Price * (discountValue / 100m);
                                else if (discountType.Equals("fixed", StringComparison.OrdinalIgnoreCase))
                                    discountAmount = discountValue;

                                // Apply discount
                                itemTotal = (item.Price - discountAmount) * item.Quantity;
                            }
                        }
                        await reader.CloseAsync();
                    }
                    else if (item.Category == CategoryConstants.Product)
                    {
                        // --- ✅ Product Discount Logic (new) ---
                        string getProductDiscountQuery = @"
    SELECT Price, DiscountedPrice, IsPercentageDiscount
    FROM Products
    WHERE ProductID = @ProductID";

                        using var prodCmd = new SqlCommand(getProductDiscountQuery, conn, transaction);
                        prodCmd.Parameters.AddWithValue("@ProductID", item.SellingID);

                        using var prodReader = await prodCmd.ExecuteReaderAsync();
                        if (await prodReader.ReadAsync())
                        {
                            decimal price = prodReader["Price"] != DBNull.Value ? Convert.ToDecimal(prodReader["Price"]) : 0;
                            decimal discountValue = prodReader["DiscountedPrice"] != DBNull.Value ? Convert.ToDecimal(prodReader["DiscountedPrice"]) : 0;
                            bool isPercentage = prodReader["IsPercentageDiscount"] != DBNull.Value && Convert.ToBoolean(prodReader["IsPercentageDiscount"]);

                            if (discountValue > 0)
                            {
                                decimal finalPrice = price;

                                if (isPercentage)
                                {
                                    finalPrice = price - (price * (discountValue / 100m));
                                }
                                else
                                {
                                    // if IsPercentageDiscount = false, then discount is inactive
                                    finalPrice = price;
                                }

                                // Make sure price doesn't go below zero
                                if (finalPrice < 0) finalPrice = 0;

                                itemTotal = finalPrice * item.Quantity;
                            }
                        }
                        await prodReader.CloseAsync();
                    }

                    totalAmount += itemTotal;
                    totalTransactions++;

                    // Step 1: Record in Sales table
                    string insertSale = @"
                INSERT INTO Sales (SaleDate, PackageID, ProductID, CustomerID, MemberID, Quantity, Amount, RecordedBy)
                VALUES (@SaleDate, @PackageID, @ProductID, @CustomerID, @MemberID, @Quantity, @Amount, @RecordedBy);
                SELECT SCOPE_IDENTITY();";

                    using var cmd = new SqlCommand(insertSale, conn, transaction);
                    cmd.Parameters.AddWithValue("@SaleDate", DateTime.Now);
                    cmd.Parameters.AddWithValue("@PackageID", item.Category == CategoryConstants.GymPackage ? (object)item.SellingID : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ProductID", item.Category == CategoryConstants.Product ? (object)item.SellingID : DBNull.Value);
                    cmd.Parameters.AddWithValue("@CustomerID", customer.CustomerType == CategoryConstants.WalkIn ? (object)customer.CustomerID : DBNull.Value);
                    cmd.Parameters.AddWithValue("@MemberID", customer.CustomerType == CategoryConstants.Member ? (object)customer.CustomerID : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                    cmd.Parameters.AddWithValue("@Amount", itemTotal);
                    cmd.Parameters.AddWithValue("@RecordedBy", employeeId);

                    var saleId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                    // Step 2: Record in CustomerPurchases
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

                    // Step 3: Record in PurchaseDetails
                    string insertDetails = @"
                INSERT INTO PurchaseDetails (PurchaseID, SellingID, Category, Quantity, UnitPrice)
                VALUES (@PurchaseID, @SellingID, @Category, @Quantity, @UnitPrice);";

                    using var detailsCmd = new SqlCommand(insertDetails, conn, transaction);
                    detailsCmd.Parameters.AddWithValue("@PurchaseID", purchaseId);
                    detailsCmd.Parameters.AddWithValue("@SellingID", item.SellingID);
                    detailsCmd.Parameters.AddWithValue("@Category", item.Category);
                    detailsCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                    detailsCmd.Parameters.AddWithValue("@UnitPrice", item.Price);
                    await detailsCmd.ExecuteNonQueryAsync();

                    // Step 4: Handle logic depending on type
                    if (item.Category == CategoryConstants.Product)
                    {
                        // Check if sufficient stock exists
                        string checkStock = @"
                    SELECT CurrentStock FROM Products WHERE ProductID = @ProductID;";

                        using var checkCmd = new SqlCommand(checkStock, conn, transaction);
                        checkCmd.Parameters.AddWithValue("@ProductID", item.SellingID);
                        var currentStock = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                        if (currentStock < item.Quantity)
                        {
                            throw new InvalidOperationException($"Insufficient stock for {item.Title}. Available: {currentStock}, Required: {item.Quantity}");
                        }

                        // Update stock
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
                    else if (item.Category == CategoryConstants.GymPackage)
                    {
                        // Only handle Member sessions for per-session packages
                        if (customer.CustomerType == CategoryConstants.Member)
                        {
                            // Get package duration to determine if it's per-session
                            string getPackageDuration = @"
                        SELECT Duration FROM Packages WHERE PackageID = @PackageID;";

                            string durationStr = "";
                            using (var pkgCmd = new SqlCommand(getPackageDuration, conn, transaction))
                            {
                                pkgCmd.Parameters.AddWithValue("@PackageID", item.SellingID);
                                var result = await pkgCmd.ExecuteScalarAsync();

                                if (result != null && result != DBNull.Value)
                                {
                                    durationStr = result.ToString()?.Trim()?.ToLower() ?? "";
                                }
                            }

                            // Only track sessions for "session" and "one-time only" packages
                            // Skip monthly packages - they're just recorded as sales
                            if (durationStr == "session" || durationStr == "one-time only")
                            {
                                int sessionsLeft = durationStr == "one-time only" ? 1 : 1;

                                // Member Sessions Logic
                                string checkExisting = @"
                            SELECT SessionID, SessionsLeft 
                            FROM MemberSessions 
                            WHERE CustomerID = @CustomerID AND PackageID = @PackageID;";

                                int? existingSessionId = null;
                                int existingSessionsLeft = 0;

                                using (var checkCmd = new SqlCommand(checkExisting, conn, transaction))
                                {
                                    checkCmd.Parameters.AddWithValue("@CustomerID", customer.CustomerID);
                                    checkCmd.Parameters.AddWithValue("@PackageID", item.SellingID);

                                    using var reader = await checkCmd.ExecuteReaderAsync();
                                    if (await reader.ReadAsync())
                                    {
                                        existingSessionId = reader.GetInt32(0);
                                        existingSessionsLeft = reader.GetInt32(1);
                                    }
                                }

                                if (existingSessionId.HasValue)
                                {
                                    // Update existing session
                                    string updateSession = @"
                                UPDATE MemberSessions 
                                SET SessionsLeft = SessionsLeft + @NewSessions
                                WHERE SessionID = @SessionID;";

                                    using var updateCmd = new SqlCommand(updateSession, conn, transaction);
                                    updateCmd.Parameters.AddWithValue("@SessionID", existingSessionId.Value);
                                    updateCmd.Parameters.AddWithValue("@NewSessions", sessionsLeft * item.Quantity);
                                    await updateCmd.ExecuteNonQueryAsync();
                                }
                                else
                                {
                                    // Insert new session
                                    string insertSession = @"
                                INSERT INTO MemberSessions (CustomerID, PackageID, SessionsLeft, StartDate)
                                VALUES (@CustomerID, @PackageID, @SessionsLeft, GETDATE());";

                                    using var insertCmd = new SqlCommand(insertSession, conn, transaction);
                                    insertCmd.Parameters.AddWithValue("@CustomerID", customer.CustomerID);
                                    insertCmd.Parameters.AddWithValue("@PackageID", item.SellingID);
                                    insertCmd.Parameters.AddWithValue("@SessionsLeft", sessionsLeft * item.Quantity);
                                    await insertCmd.ExecuteNonQueryAsync();
                                }
                            }
                            // Monthly packages: No session tracking, sale is already recorded above
                        }
                        // Walk-in customers: No session tracking, sale is already recorded above
                    }
                }

                // Step 5: Update DailySales
                string upsertDaily = @"
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

                using var dailyCmd = new SqlCommand(upsertDaily, conn, transaction);
                dailyCmd.Parameters.AddWithValue("@EmployeeID", employeeId);
                dailyCmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
                dailyCmd.Parameters.AddWithValue("@TotalTransactions", totalTransactions);
                await dailyCmd.ExecuteNonQueryAsync();

                transaction.Commit();
                string itemsList = string.Join(", ", cartItems.Select(i => $"{i.Title} x{i.Quantity}"));
                await LogActionAsync(conn, "Purchase",
                    $"Payment processed for {customer.FirstName} {customer.LastName} ({customer.CustomerType}). Total: ₱{totalAmount:N2} via {paymentMethod}. Items: {itemsList}",
                    true);

                _toastManager.CreateToast("Payment Successful")
                    .WithContent($"Transaction completed. Total: ₱{totalAmount:N2} via {paymentMethod}")
                    .ShowSuccess();

                bool anyPackageDiscount = cartItems.Any(i => i.Category == CategoryConstants.GymPackage);
                bool anyProductDiscount = cartItems.Any(i => i.Category == CategoryConstants.Product);

                if (anyPackageDiscount || anyProductDiscount)
                {
                    var message = anyPackageDiscount && anyProductDiscount
                        ? "Package and product discounts applied successfully."
                        : anyPackageDiscount
                            ? "Package discounts applied successfully."
                            : "Product discounts applied successfully.";

                    _toastManager.CreateToast("Discount Applied")
                        .WithContent(message)
                        .ShowSuccess();
                }
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                // Log failed transaction
                try
                {
                    DashboardEventService.Instance.NotifyProductUpdated();
                    await LogActionAsync(conn, "Purchase",
                        $"Failed to process payment for {customer?.FirstName} {customer?.LastName}. Error: {ex.Message}",
                        false);
                }
                catch { /* Ignore logging errors */ }

                _toastManager.CreateToast("Payment Failed")
                    .WithContent($"Transaction failed: {ex.Message}")
                    .ShowError();
                return false;
            }
        }

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
            WHERE GETDATE() BETWEEN ValidFrom AND ValidTo AND IsDeleted = 0
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
                        Stock = 999, // Packages don't have physical stock
                        ImagePath = null,
                        Features = reader["Features"]?.ToString() ?? string.Empty
                    });
                }
                return packages;
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Load Packages Failed")
                    .WithContent($"Error loading packages: {ex.Message}")
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

                string query = @" SELECT ProductID, ProductName, Description, Category, Price, CurrentStock, DiscountedPrice, IsPercentageDiscount, ProductImagePath FROM Products WHERE Status = 'In Stock' AND CurrentStock > 0 AND IsDeleted = 0;";

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

                return products;
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Load Products Failed")
                    .WithContent($"Error loading products: {ex.Message}")
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
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Error loading customers: {ex.Message}")
                    .ShowError();
            }

            return customers;
        } // Done

        public async Task<List<RecentPurchaseModel>> GetRecentPurchasesAsync(int limit = 50)
        {
            var recentPurchases = new List<RecentPurchaseModel>();

            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to view purchase data.")
                    .ShowError();
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
                        AvatarSource = reader["AvatarSource"] != DBNull.Value
                            ? (byte[])reader["AvatarSource"]
                            : null
                    });
                }
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Error loading recent purchases: {ex.Message}")
                    .ShowError();
            }

            return recentPurchases;
        }

        public async Task<List<InvoiceModel>> GetInvoicesByDateAsync(DateTime date)
        {
            var invoices = new List<InvoiceModel>();

            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to view invoice data.")
                    .ShowError();
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
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Error loading invoices: {ex.Message}")
                    .ShowError();
            }

            return invoices;
        }

        #endregion

        #region UPDATE



        #endregion

        #region DELETE



        #endregion

        #region UTILITY

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
                DashboardEventService.Instance.NotifyRecentLogsUpdated();
                DashboardEventService.Instance.NotifySalesUpdated();
                DashboardEventService.Instance.NotifyChartDataUpdated();
                DashboardEventService.Instance.NotifyProductPurchased();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        private async Task<decimal> GetDiscountedPackagePriceAsync(SqlConnection conn, SqlTransaction transaction, int packageId, string customerType, decimal basePrice)
        {
            string query = @"
        SELECT Discount, DiscountType, DiscountFor
        FROM Packages
        WHERE PackageID = @PackageID;";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@PackageID", packageId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                decimal discountValue = reader["Discount"] != DBNull.Value ? Convert.ToDecimal(reader["Discount"]) : 0;
                string discountType = reader["DiscountType"]?.ToString() ?? string.Empty;
                string discountFor = reader["DiscountFor"]?.ToString() ?? string.Empty;

                // Check if discount applies
                bool appliesToCustomer =
                    discountFor.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                    (customerType.Equals("Member", StringComparison.OrdinalIgnoreCase) &&
                     discountFor.Equals("Gym Members", StringComparison.OrdinalIgnoreCase)) ||
                    (customerType.Equals("WalkIn", StringComparison.OrdinalIgnoreCase) &&
                     discountFor.Equals("WalkIn", StringComparison.OrdinalIgnoreCase));

                if (appliesToCustomer)
                {
                    if (discountType.Equals("Percentage", StringComparison.OrdinalIgnoreCase))
                    {
                        basePrice -= basePrice * (discountValue / 100);
                    }
                    else if (discountType.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
                    {
                        basePrice -= discountValue;
                    }

                    if (basePrice < 0)
                        basePrice = 0;
                }
            }

            return basePrice;
        }


        #endregion
    }
}

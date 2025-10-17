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
using AHON_TRACK.Converters;

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
                    else if (item.Category == CategoryConstants.GymPackage && customer.CustomerType == CategoryConstants.Member)
                    {
                        // Get package duration (number of sessions)
                        string getPackageDuration = @"
                            SELECT Duration FROM Packages WHERE PackageID = @PackageID;";

                        using var pkgCmd = new SqlCommand(getPackageDuration, conn, transaction);
                        pkgCmd.Parameters.AddWithValue("@PackageID", item.SellingID);
                        var packageDuration = await pkgCmd.ExecuteScalarAsync();

                        int sessionsLeft = packageDuration != DBNull.Value ? Convert.ToInt32(packageDuration) : 1;

                        // Insert or update member sessions
                        string upsertSession = @"
                            MERGE INTO MemberSessions AS target
                            USING (SELECT @CustomerID AS CustomerID, @PackageID AS PackageID) AS source
                            ON target.CustomerID = source.CustomerID AND target.PackageID = source.PackageID
                            WHEN MATCHED THEN
                                UPDATE SET SessionsLeft = target.SessionsLeft + @SessionsLeft
                            WHEN NOT MATCHED THEN
                                INSERT (CustomerID, PackageID, SessionsLeft, StartDate)
                                VALUES (@CustomerID, @PackageID, @SessionsLeft, GETDATE());";

                        using var sessionCmd = new SqlCommand(upsertSession, conn, transaction);
                        sessionCmd.Parameters.AddWithValue("@CustomerID", customer.CustomerID);
                        sessionCmd.Parameters.AddWithValue("@PackageID", item.SellingID);
                        sessionCmd.Parameters.AddWithValue("@SessionsLeft", sessionsLeft * item.Quantity);
                        await sessionCmd.ExecuteNonQueryAsync();
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

                _toastManager.CreateToast("Payment Successful")
                    .WithContent($"Transaction completed. Total: ₱{totalAmount:N2} via {paymentMethod}")
                    .ShowSuccess();

                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
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
                    WHERE GETDATE() BETWEEN ValidFrom AND ValidTo;";

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
                        ImagePath = null
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
                    WHERE Status = 'In Stock' AND CurrentStock > 0;";

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
                        MemberID AS ID,
                        FirstName,
                        LastName,
                        @MemberType AS CustomerType
                    FROM Members
                    WHERE Status = 'Active'

                    UNION ALL

                    SELECT 
                        CustomerID AS ID,
                        FirstName,
                        LastName,
                        @WalkInType AS CustomerType
                    FROM WalkInCustomers;";

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

using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Notification = AHON_TRACK.Models.Notification;

namespace AHON_TRACK.Services
{
    public class MemberService : IMemberService, IDisposable
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;
        private Action<Notification>? _notificationCallback;
        private bool _disposed;

        private const int EXPIRATION_THRESHOLD_DAYS = 7;
        private const string MEMBER_NOT_DELETED_FILTER = "(IsDeleted = 0 OR IsDeleted IS NULL)";

        public MemberService(string connectionString, ToastManager toastManager)
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

        #region PAYMENT & REFERENCE NUMBER HANDLING

        /// <summary>
        /// Processes reference number based on payment method:
        /// - Cash → "PAID"
        /// - GCash/Maya → User-provided reference number
        /// - Other → Reference number as-is or empty
        /// </summary>
        private string ProcessReferenceNumber(ManageMemberModel member)
        {
            // Normalize payment method to uppercase for comparison
            string paymentMethod = member.PaymentMethod?.Trim().ToUpper() ?? "";

            // If payment method is CASH, store "PAID"
            if (paymentMethod == "CASH")
            {
                Debug.WriteLine("[ProcessReferenceNumber] Cash payment detected - storing 'PAID'");
                return "PAID";
            }

            // If payment method is GCash or Maya, use the user-provided reference number
            if (paymentMethod == "GCASH" || paymentMethod == "MAYA")
            {
                string refNumber = member.ReferenceNumber?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(refNumber))
                {
                    Debug.WriteLine($"[ProcessReferenceNumber] Warning: {paymentMethod} payment but no reference number provided");
                    return $"{paymentMethod}_NO_REF";
                }

                Debug.WriteLine($"[ProcessReferenceNumber] {paymentMethod} payment - Reference: {refNumber}");
                return refNumber;
            }

            // For other payment methods, return the reference number as-is or empty
            Debug.WriteLine($"[ProcessReferenceNumber] Other payment method: {paymentMethod}");
            return member.ReferenceNumber?.Trim() ?? "";
        }

        /// <summary>
        /// Validates payment method and reference number before saving member.
        /// Call this from your ViewModel before saving.
        /// </summary>
        public (bool IsValid, string ErrorMessage) ValidatePaymentReferenceNumber(ManageMemberModel member)
        {
            if (string.IsNullOrWhiteSpace(member.PaymentMethod))
            {
                return (false, "Payment method is required.");
            }

            string paymentMethod = member.PaymentMethod.Trim().ToUpper();

            // Cash doesn't need reference number
            if (paymentMethod == "CASH")
            {
                return (true, "");
            }

            // GCash: 13 digits only
            if (paymentMethod == "GCASH")
            {
                if (string.IsNullOrWhiteSpace(member.ReferenceNumber))
                {
                    return (false, "GCash payment requires a reference number.");
                }

                string refNum = member.ReferenceNumber.Trim();
                if (refNum.Length != 13 || !refNum.All(char.IsDigit))
                {
                    return (false, "GCash reference number must be exactly 13 digits.");
                }

                return (true, "");
            }

            // Maya: 6 digits
            if (paymentMethod == "MAYA")
            {
                if (string.IsNullOrWhiteSpace(member.ReferenceNumber))
                {
                    return (false, "Maya payment requires a reference number.");
                }

                string refNum = member.ReferenceNumber.Trim();
                if (refNum.Length != 6 || !refNum.All(char.IsDigit))
                {
                    return (false, "Maya reference number must be exactly 6 digits.");
                }

                return (true, "");
            }

            return (true, "");
        }

        #endregion

        #region CREATE

        public async Task<(bool Success, string Message, int? MemberId)> AddMemberAsync(
            ManageMemberModel member, 
            string? invoiceNumber = null)
        {
            if (!CanCreate())
            {
                ShowAccessDeniedToast("add members");
                return (false, "Insufficient permissions to add members.", null);
            }

            // ✅ USE PROVIDED INVOICE NUMBER OR GENERATE NEW ONE
            string finalInvoiceNumber = !string.IsNullOrWhiteSpace(invoiceNumber) 
                ? invoiceNumber 
                : await GenerateInvoiceNumberAsync();

            Debug.WriteLine($"[AddMemberAsync] Using Invoice: {finalInvoiceNumber}");

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    var (existingMemberId, isDeleted) = await CheckMemberExistsAsync(conn, transaction, member);

                    if (existingMemberId.HasValue && isDeleted)
                    {
                        var result = await RestoreMemberAsync(conn, transaction, existingMemberId.Value, member, finalInvoiceNumber);
                        return (result.Success, result.Message, result.MemberId);
                    }

                    if (existingMemberId.HasValue)
                    {
                        ShowMemberExistsToast(member);
                        return (false, "Member already exists.", null);
                    }

                    var migratedWalkInId = await CheckAndMigrateWalkInCustomerEnhancedAsync(conn, transaction, member);
                    if (migratedWalkInId.HasValue)
                    {
                        Debug.WriteLine($"[AddMemberAsync] Successfully migrated walk-in customer {migratedWalkInId.Value} to member");
                        DashboardEventService.Instance.NotifyPopulationDataChanged();
                    }

                    int memberId = await InsertNewMemberAsync(conn, transaction, member);

                    if (member.PackageID.HasValue && member.PackageID.Value > 0)
                    {
                        await RecordPackageSaleAsync(conn, transaction, member, memberId, null, finalInvoiceNumber);
                    }

                    await LogActionAsync(conn, transaction, "CREATE", 
                        $"Added new member: {member.FirstName} {member.LastName} - Invoice: {finalInvoiceNumber}", true);
                    transaction.Commit();

                    DashboardEventService.Instance.NotifyMemberAdded();

                    if (migratedWalkInId.HasValue)
                    {
                        _toastManager?.CreateToast("Member Added & Migrated")
                            .WithContent($"{member.FirstName} {member.LastName} registered as member (walk-in record archived).\nInvoice: {finalInvoiceNumber}")
                            .DismissOnClick()
                            .ShowSuccess();
                    }

                    return (true, "Member added successfully.", memberId);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                ShowDatabaseErrorToast("add member", ex.Message);
                Debug.WriteLine($"AddMemberAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowErrorToast(ex.Message);
                Debug.WriteLine($"AddMemberAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        private async Task<(int? MemberId, bool IsDeleted)> CheckMemberExistsAsync(
            SqlConnection conn, SqlTransaction transaction, ManageMemberModel member)
        {
            const string query = @"
                SELECT MemberID, IsDeleted 
                FROM Members 
                WHERE Firstname = @Firstname 
                AND Lastname = @Lastname 
                AND BirthYear = @BirthYear";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@Firstname", member.FirstName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Lastname", member.LastName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@BirthYear", member.BirthYear ?? (object)DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int memberId = reader.GetInt32(0);
                bool isDeleted = !reader.IsDBNull(1) && reader.GetBoolean(1);
                return (memberId, isDeleted);
            }

            return (null, false);
        }

        private async Task<(bool Success, string Message, int? MemberId)> RestoreMemberAsync(
            SqlConnection conn, SqlTransaction transaction, int memberId, ManageMemberModel member, string invoiceNumber)
        {
            const string query = @"
            UPDATE Members 
            SET IsDeleted = 0,
                MiddleInitial = @MiddleInitial,
                Gender = @Gender,
                ProfilePicture = @ProfilePicture,
                ContactNumber = @ContactNumber,
                Age = @Age,
                BirthYear = @BirthYear,
                ValidUntil = @ValidUntil,
                PackageID = @PackageID,
                Status = @Status,
                PaymentMethod = @PaymentMethod,
                ReferenceNumber = @ReferenceNumber,
                ConsentLetter = @ConsentLetter,
                RegisteredByEmployeeID = @RegisteredByEmployeeID
            WHERE MemberID = @MemberID";

            using var cmd = new SqlCommand(query, conn, transaction);
            AddMemberParameters(cmd, member);
            cmd.Parameters.AddWithValue("@MemberID", memberId);

            await cmd.ExecuteNonQueryAsync();
        
            if (member.PackageID.HasValue && member.PackageID.Value > 0)
            {
                await RecordPackageSaleAsync(conn, transaction, member, memberId, null, invoiceNumber);
            }
        
            await LogActionAsync(conn, transaction, "RESTORE", 
                $"Restored member: {member.FirstName} {member.LastName} - Invoice: {invoiceNumber}", true);
            transaction.Commit();

            _toastManager?.CreateToast("Member Restored")
                .WithContent($"Successfully restored {member.FirstName} {member.LastName}.\nInvoice: {invoiceNumber}")
                .DismissOnClick()
                .ShowSuccess();

            return (true, "Member restored successfully.", memberId);
        }

        private async Task<int> InsertNewMemberAsync(
            SqlConnection conn, SqlTransaction transaction, ManageMemberModel member)
        {
            const string query = @"
            INSERT INTO Members 
            (Firstname, MiddleInitial, Lastname, Gender, ProfilePicture, ContactNumber, Age, BirthYear, 
             ValidUntil, PackageID, Status, PaymentMethod, ReferenceNumber, ConsentLetter, RegisteredByEmployeeID, IsDeleted)
            OUTPUT INSERTED.MemberID
            VALUES 
            (@Firstname, @MiddleInitial, @Lastname, @Gender, @ProfilePicture, @ContactNumber, @Age, @BirthYear, 
             @ValidUntil, @PackageID, @Status, @PaymentMethod, @ReferenceNumber, @ConsentLetter, @RegisteredByEmployeeID, 0)";

            using var cmd = new SqlCommand(query, conn, transaction);
            AddMemberParameters(cmd, member);

            return (int)await cmd.ExecuteScalarAsync();
        }

        private async Task RecordPackageSaleAsync(
    SqlConnection conn, SqlTransaction transaction,
    ManageMemberModel member, int memberId, ManageMemberModel? originalMember = null, string? invoiceNumber = null)
        {
            var (packagePrice, packageName, duration) = await GetPackageDetailsAsync(
                conn, transaction, member.PackageID!.Value);

            if (packagePrice <= 0) return;

            int quantity;

            if (originalMember?.ValidUntil != null)
            {
                // ✅ RENEWAL/UPGRADE: Calculate months ADDED (difference between old and new ValidUntil)
                quantity = CalculateMonthsAdded(originalMember.ValidUntil, member.ValidUntil);

                Debug.WriteLine($"[RecordPackageSaleAsync] RENEWAL/UPGRADE");
                Debug.WriteLine($"  Invoice: {invoiceNumber ?? "N/A"}");
                Debug.WriteLine($"  Old ValidUntil: {originalMember.ValidUntil}");
                Debug.WriteLine($"  New ValidUntil: {member.ValidUntil}");
                Debug.WriteLine($"  Months ADDED: {quantity}");
            }
            else
            {
                // ✅ NEW MEMBER: Calculate from today to ValidUntil
                quantity = CalculateQuantityFromValidUntil(member.ValidUntil);

                Debug.WriteLine($"[RecordPackageSaleAsync] NEW MEMBER");
                Debug.WriteLine($"  Invoice: {invoiceNumber ?? "N/A"}");
                Debug.WriteLine($"  ValidUntil: {member.ValidUntil}");
                Debug.WriteLine($"  Months from today: {quantity}");
            }

            decimal totalAmount = packagePrice * quantity;
            
            decimal? change = null;
            if (member.TenderedPrice.HasValue && member.TenderedPrice.Value >= totalAmount)
            {
                change = member.TenderedPrice.Value - totalAmount;
            }

            // ✅ PROCESS REFERENCE NUMBER BASED ON CURRENT PAYMENT METHOD
            string processedRefNumber = ProcessReferenceNumber(member);

            // ✅ PASS PAYMENT METHOD, REFERENCE NUMBER, AND INVOICE NUMBER TO SALES RECORD
            await InsertSaleRecordAsync(
                conn, transaction, member.PackageID!.Value, memberId,
                quantity, totalAmount, member.PaymentMethod, processedRefNumber, invoiceNumber,
                member.TenderedPrice, change);

            await UpdateDailySalesAsync(conn, transaction, totalAmount);

            Debug.WriteLine($"[RecordPackageSaleAsync] Sale recorded: {packageName} x{quantity} months = ₱{totalAmount:N2}");
            Debug.WriteLine($"  Payment Method: {member.PaymentMethod}");
            Debug.WriteLine($"  Reference Number: {processedRefNumber}");
            Debug.WriteLine($"  Invoice Number: {invoiceNumber ?? "N/A"}");
            Debug.WriteLine($"  Tendered: ₱{member.TenderedPrice:N2}, Change: ₱{change:N2}");
        }

        private int CalculateMonthsAdded(string? oldValidUntil, string? newValidUntil)
        {
            if (string.IsNullOrEmpty(oldValidUntil) || string.IsNullOrEmpty(newValidUntil))
                return 1;

            if (!DateTime.TryParse(oldValidUntil, out DateTime oldDate) ||
                !DateTime.TryParse(newValidUntil, out DateTime newDate))
                return 1;

            int monthsAdded = ((newDate.Year - oldDate.Year) * 12) + (newDate.Month - oldDate.Month);

            Debug.WriteLine($"[CalculateMonthsAdded] {oldDate:MMM dd, yyyy} → {newDate:MMM dd, yyyy} = {monthsAdded} months");

            return Math.Max(1, monthsAdded);
        }

        private async Task<(decimal Price, string Name, string Duration)> GetPackageDetailsAsync(
            SqlConnection conn, SqlTransaction transaction, int packageId)
        {
            const string query = @"
                SELECT PackageID, PackageName, Price, Duration 
                FROM Packages 
                WHERE PackageID = @packageId AND IsDeleted = 0";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@packageId", packageId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetDecimal(2), reader.GetString(1), reader.GetString(3));
            }

            return (0, "", "");
        }

        private int CalculateQuantityFromValidUntil(string? validUntil)
        {
            if (string.IsNullOrEmpty(validUntil) || !DateTime.TryParse(validUntil, out DateTime validDate))
                return 1;

            var monthsDiff = ((validDate.Year - DateTime.Now.Year) * 12) + validDate.Month - DateTime.Now.Month;
            return Math.Max(1, monthsDiff);
        }

        private async Task InsertSaleRecordAsync(
            SqlConnection conn, SqlTransaction transaction, int packageId, int memberId,
            int quantity, decimal amount, string? paymentMethod = null, string? referenceNumber = null, 
            string? invoiceNumber = null, decimal? tenderedPrice = null, decimal? change = null)
        {
            const string query = @"
        INSERT INTO Sales (SaleDate, PackageID, MemberID, Quantity, Amount, RecordedBy, 
                          PaymentMethod, ReferenceNumber, InvoiceNumber, TenderedPrice, Change)
        VALUES (GETDATE(), @packageId, @memberId, @quantity, @amount, @employeeId, 
                @paymentMethod, @referenceNumber, @invoiceNumber, @tenderedPrice, @change)";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@packageId", packageId);
            cmd.Parameters.AddWithValue("@memberId", memberId);
            cmd.Parameters.AddWithValue("@quantity", quantity);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.Parameters.AddWithValue("@employeeId", CurrentUserModel.UserId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@referenceNumber", referenceNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@tenderedPrice", tenderedPrice ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@change", change ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateDailySalesAsync(SqlConnection conn, SqlTransaction transaction, decimal totalAmount)
        {
            const string query = @"
                MERGE DailySales AS target
                USING (SELECT CAST(GETDATE() AS DATE) AS SaleDate, @EmployeeID AS EmployeeID) AS source
                ON target.SaleDate = source.SaleDate AND target.TransactionByEmployeeID = source.EmployeeID
                WHEN MATCHED THEN
                    UPDATE SET 
                        TotalSales = target.TotalSales + @TotalAmount,
                        TotalTransactions = target.TotalTransactions + 1,
                        TransactionUpdatedDate = SYSDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (SaleDate, TotalSales, TotalTransactions, TransactionByEmployeeID)
                    VALUES (source.SaleDate, @TotalAmount, 1, source.EmployeeID);";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@EmployeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TotalAmount", totalAmount);

            await cmd.ExecuteNonQueryAsync();
            Debug.WriteLine($"[AddMemberAsync] DailySales updated with ₱{totalAmount:N2}");
        }

        private void AddMemberParameters(SqlCommand cmd, ManageMemberModel member)
        {
            cmd.Parameters.AddWithValue("@Firstname", member.FirstName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MiddleInitial",
                string.IsNullOrWhiteSpace(member.MiddleInitial)
                    ? (object)DBNull.Value
                    : member.MiddleInitial.Substring(0, 1).ToUpper());
            cmd.Parameters.AddWithValue("@Lastname", member.LastName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Gender", member.Gender ?? (object)DBNull.Value);
            cmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary, -1).Value = member.ProfilePicture ?? (object)DBNull.Value;
            cmd.Parameters.AddWithValue("@ContactNumber", member.ContactNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Age", member.Age > 0 ? member.Age : (object)DBNull.Value);
        
            cmd.Parameters.AddWithValue("@BirthYear", member.BirthYear ?? (object)DBNull.Value);
        
            cmd.Parameters.AddWithValue("@ValidUntil", member.ValidUntil ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PackageID", member.PackageID ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", member.Status ?? "Active");
            cmd.Parameters.AddWithValue("@PaymentMethod", member.PaymentMethod ?? (object)DBNull.Value);

            string processedRefNumber = ProcessReferenceNumber(member);
            cmd.Parameters.AddWithValue("@ReferenceNumber",
                string.IsNullOrWhiteSpace(processedRefNumber) ? (object)DBNull.Value : processedRefNumber);

            cmd.Parameters.AddWithValue("@ConsentLetter",
                string.IsNullOrWhiteSpace(member.ConsentLetter) ? (object)DBNull.Value : member.ConsentLetter);

            cmd.Parameters.AddWithValue("@RegisteredByEmployeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);
        }

        #region MEMBER NOTIFICATION WITH DASHBOARD DISPLAY

        /// <summary>
        /// Records member notification and creates a dashboard notification
        /// This displays "You have notified [Member Name]" in the dashboard
        /// </summary>
        public async Task<(bool Success, string Message)> RecordMemberNotificationAsync(
            int memberId, string notificationMessage)
        {
            if (!CanUpdate())
            {
                ShowAccessDeniedToast("record notifications");
                return (false, "Insufficient permissions to record notifications.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    // Update member notification tracking
                    const string updateQuery = @"
                UPDATE Members 
                SET LastNotificationDate = GETDATE(),
                    NotificationCount = ISNULL(NotificationCount, 0) + 1,
                    IsNotified = 1
                WHERE MemberID = @MemberId";

                    using var cmd = new SqlCommand(updateQuery, conn, transaction);
                    cmd.Parameters.AddWithValue("@MemberId", memberId);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        // Get member details for notification
                        const string nameQuery = @"
                    SELECT Firstname, Lastname, Status, ValidUntil
                    FROM Members 
                    WHERE MemberID = @MemberId";

                        using var nameCmd = new SqlCommand(nameQuery, conn, transaction);
                        nameCmd.Parameters.AddWithValue("@MemberId", memberId);

                        using var reader = await nameCmd.ExecuteReaderAsync();

                        string memberName = "";
                        string status = "";
                        DateTime? validUntil = null;

                        if (await reader.ReadAsync())
                        {
                            memberName = $"{reader.GetString(0)} {reader.GetString(1)}";
                            status = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2);
                            validUntil = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                        }
                        reader.Close();

                        // Log the action
                        await LogActionAsync(conn, transaction, "MEMBER_NOTIFIED",
                            $"Notified {memberName} - {notificationMessage}", true);

                        transaction.Commit();

                        // ✅ CREATE DASHBOARD NOTIFICATION
                        CreateDashboardNotification(memberName, status, validUntil, notificationMessage);

                        DashboardEventService.Instance.NotifyMemberUpdated();

                        return (true, "Notification recorded successfully.");
                    }

                    return (false, "Failed to record notification.");
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                ShowDatabaseErrorToast("record notification", ex.Message);
                Debug.WriteLine($"RecordMemberNotificationAsync Error: {ex}");
                return (false, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a notification that displays in the dashboard
        /// </summary>
        private void CreateDashboardNotification(
            string memberName,
            string status,
            DateTime? validUntil,
            string notificationMessage)
        {
            try
            {
                // Determine notification type based on status
                NotificationType notifType = status.ToLower() switch
                {
                    "expired" => NotificationType.Alert,
                    "near expiry" => NotificationType.Warning,
                    _ => NotificationType.Alert
                };

                // Create detailed message for dashboard
                string dashboardMessage = validUntil.HasValue
                    ? $"Successfully notified {memberName} about their membership status. " +
                      $"Status: {status}, Valid Until: {validUntil.Value:MMM dd, yyyy}"
                    : $"Successfully notified {memberName} about their membership status. Status: {status}";

                // Trigger the notification callback to display in dashboard
                _notificationCallback?.Invoke(new Notification
                {
                    Type = notifType,
                    Title = "Member Notification Sent",
                    Message = dashboardMessage,
                    DateAndTime = DateTime.Now
                });

                Debug.WriteLine($"[CreateDashboardNotification] Dashboard notification created for {memberName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CreateDashboardNotification] Error: {ex.Message}");
            }
        }

        #endregion

        #endregion

        #region READ

        public async Task<(bool Success, string Message, List<ManageMemberModel>? Members)> GetMembersAsync()
        {
            if (!CanView())
            {
                ShowAccessDeniedToast("view members");
                return (false, "Insufficient permissions to view members.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
    SELECT 
        m.MemberID, m.Firstname, m.MiddleInitial, m.Lastname,
        LTRIM(RTRIM(m.Firstname + ISNULL(' ' + m.MiddleInitial + '.', '') + ' ' + m.Lastname)) AS Name,
        m.Gender, m.ContactNumber, m.Age, m.BirthYear, m.ValidUntil, 
        m.LastNotificationDate, m.NotificationCount, m.IsNotified,
        m.PackageID, 
        
        p.PackageName AS GymPackageName,
        
        STUFF((
            SELECT ', ' + pkg.PackageName
            FROM MemberSessions ms
            INNER JOIN Packages pkg ON ms.PackageID = pkg.PackageID
            WHERE ms.CustomerID = m.MemberID 
                AND ms.SessionsLeft > 0
                AND (ms.IsDeleted = 0 OR ms.IsDeleted IS NULL)
                AND (pkg.IsDeleted = 0 OR pkg.IsDeleted IS NULL)
            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS SessionPackages,
        
        CASE 
            WHEN m.ValidUntil < CAST(GETDATE() AS DATE) THEN 'Expired'
            WHEN DATEDIFF(DAY, GETDATE(), m.ValidUntil) BETWEEN 0 AND @ExpirationThreshold THEN 'Near Expiry'
            ELSE m.Status
        END AS Status,
        
        m.PaymentMethod, m.ProfilePicture, m.ConsentLetter, m.DateJoined,
        
        (SELECT TOP 1 CheckIn FROM MemberCheckIns 
         WHERE MemberID = m.MemberID AND (IsDeleted = 0 OR IsDeleted IS NULL)
         ORDER BY CheckIn DESC) AS LastCheckIn,
        
        (SELECT TOP 1 CheckOut FROM MemberCheckIns 
         WHERE MemberID = m.MemberID AND CheckOut IS NOT NULL AND (IsDeleted = 0 OR IsDeleted IS NULL)
         ORDER BY CheckOut DESC) AS LastCheckOut,
        
        (SELECT TOP 1 
            CASE 
                WHEN s.ProductID IS NOT NULL THEN pr.ProductName
                WHEN s.PackageID IS NOT NULL THEN pk.PackageName
                ELSE 'Unknown'
            END
         FROM Sales s
         LEFT JOIN Products pr ON s.ProductID = pr.ProductID
         LEFT JOIN Packages pk ON s.PackageID = pk.PackageID
         WHERE s.MemberID = m.MemberID AND (s.IsDeleted = 0 OR s.IsDeleted IS NULL)
         ORDER BY s.SaleDate DESC) AS RecentPurchaseItem,
        
        (SELECT TOP 1 SaleDate FROM Sales 
         WHERE MemberID = m.MemberID AND (IsDeleted = 0 OR IsDeleted IS NULL)
         ORDER BY SaleDate DESC) AS RecentPurchaseDate,
        
        (SELECT TOP 1 Quantity FROM Sales 
         WHERE MemberID = m.MemberID AND (IsDeleted = 0 OR IsDeleted IS NULL)
         ORDER BY SaleDate DESC) AS RecentPurchaseQuantity
    
    FROM Members m
    LEFT JOIN Packages p ON m.PackageID = p.PackageID
    WHERE (m.IsDeleted = 0 OR m.IsDeleted IS NULL)
    ORDER BY Name";

                var members = new List<ManageMemberModel>();

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ExpirationThreshold", EXPIRATION_THRESHOLD_DAYS);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    members.Add(MapMemberFromReader(reader));
                }

                return (true, "Members retrieved successfully.", members);
            }
            catch (SqlException ex)
            {
                ShowDatabaseErrorToast("load members", ex.Message);
                Debug.WriteLine($"GetMembersAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                ShowErrorToast(ex.Message);
                Debug.WriteLine($"GetMembersAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, ManageMemberModel? Member)> GetMemberByIdAsync(int memberId)
        {
            if (!CanView())
            {
                return (false, "Insufficient permissions to view member.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
    SELECT 
        m.MemberID, m.Firstname, m.MiddleInitial, m.Lastname, m.Gender, m.ProfilePicture,
        m.ContactNumber, m.Age, m.BirthYear, m.ValidUntil, m.PackageID, 
        
        p.PackageName AS GymPackageName,
        
        STUFF((
            SELECT ', ' + pkg.PackageName
            FROM MemberSessions ms
            INNER JOIN Packages pkg ON ms.PackageID = pkg.PackageID
            WHERE ms.CustomerID = m.MemberID 
                AND ms.SessionsLeft > 0
                AND (ms.IsDeleted = 0 OR ms.IsDeleted IS NULL)
                AND (pkg.IsDeleted = 0 OR pkg.IsDeleted IS NULL)
            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS SessionPackages,
        
        CASE 
            WHEN m.ValidUntil < CAST(GETDATE() AS DATE) THEN 'Expired'
            WHEN DATEDIFF(DAY, GETDATE(), m.ValidUntil) BETWEEN 0 AND @ExpirationThreshold THEN 'Near Expiry'
            ELSE m.Status
        END AS Status,
        
        m.PaymentMethod, m.ConsentLetter, m.RegisteredByEmployeeID, m.DateJoined,
        m.LastNotificationDate, m.NotificationCount, m.IsNotified,
        
        (SELECT TOP 1 CheckIn FROM MemberCheckIns 
         WHERE MemberID = m.MemberID AND (IsDeleted = 0 OR IsDeleted IS NULL)
         ORDER BY CheckIn DESC) AS LastCheckIn,
        
        (SELECT TOP 1 CheckOut FROM MemberCheckIns 
         WHERE MemberID = m.MemberID AND CheckOut IS NOT NULL AND (IsDeleted = 0 OR IsDeleted IS NULL)
         ORDER BY CheckOut DESC) AS LastCheckOut,
        
        (SELECT TOP 1 
            CASE 
                WHEN s.ProductID IS NOT NULL THEN pr.ProductName
                WHEN s.PackageID IS NOT NULL THEN pk.PackageName
                ELSE 'Unknown'
            END
         FROM Sales s
         LEFT JOIN Products pr ON s.ProductID = pr.ProductID
         LEFT JOIN Packages pk ON s.PackageID = pk.PackageID
         WHERE s.MemberID = m.MemberID AND (s.IsDeleted = 0 OR s.IsDeleted IS NULL)
         ORDER BY s.SaleDate DESC) AS RecentPurchaseItem,
        
        (SELECT TOP 1 SaleDate FROM Sales 
         WHERE MemberID = m.MemberID AND (IsDeleted = 0 OR IsDeleted IS NULL)
         ORDER BY SaleDate DESC) AS RecentPurchaseDate,
        
        (SELECT TOP 1 Quantity FROM Sales 
         WHERE MemberID = m.MemberID AND (IsDeleted = 0 OR IsDeleted IS NULL)
         ORDER BY SaleDate DESC) AS RecentPurchaseQuantity
    
    FROM Members m
    LEFT JOIN Packages p ON m.PackageID = p.PackageID
    WHERE m.MemberID = @Id AND (m.IsDeleted = 0 OR m.IsDeleted IS NULL)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", memberId);
                cmd.Parameters.AddWithValue("@ExpirationThreshold", EXPIRATION_THRESHOLD_DAYS);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var member = MapMemberFromReader(reader, includeRegisteredBy: true);
                    member.Name = $"{member.FirstName} {(string.IsNullOrWhiteSpace(member.MiddleInitial) ? "" : member.MiddleInitial + ". ")}{member.LastName}";
                    return (true, "Member retrieved successfully.", member);
                }

                return (false, "Member not found.", null);
            }
            catch (Exception ex)
            {
                ShowDatabaseErrorToast("retrieve member", ex.Message);
                Debug.WriteLine($"GetMemberByIdAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        public async Task<List<SellingModel>> GetAvailablePackagesForMembersAsync()
        {
            Debug.WriteLine("🔹 GetAvailablePackagesForMembersAsync called");
            var packages = new List<SellingModel>();

            if (!CanView())
            {
                Debug.WriteLine("❌ Access denied - CanView() returned false");
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to view gym packages.")
                    .ShowError();
                return packages;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT 
                        PackageID, PackageName, Description, Price, Duration, Features,
                        Discount, DiscountType, DiscountFor, DiscountedPrice, ValidFrom, ValidTo
                    FROM Packages
                    WHERE GETDATE() BETWEEN ValidFrom AND ValidTo 
                    AND IsDeleted = 0
                    AND DURATION LIKE '%Month%'
                    ORDER BY Price ASC";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                int count = 0;
                while (await reader.ReadAsync())
                {
                    count++;
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

                Debug.WriteLine($"✅ Total packages loaded: {count}");
                return packages;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error in GetAvailablePackagesForMembersAsync: {ex.Message}");
                _toastManager.CreateToast("Load Packages Failed")
                    .WithContent($"Error loading packages: {ex.Message}")
                    .ShowError();
                return packages;
            }
        }

        private ManageMemberModel MapMemberFromReader(SqlDataReader reader, bool includeRegisteredBy = false)
        {
            // Column indices for GetMembersAsync (includeRegisteredBy = false)
            // 0: MemberID, 1: Firstname, 2: MiddleInitial, 3: Lastname, 4: Name,
            // 5: Gender, 6: ContactNumber, 7: Age, 8: DateOfBirth, 9: ValidUntil,
            // 10: LastNotificationDate, 11: NotificationCount, 12: IsNotified,
            // 13: PackageID, 14: GymPackageName, 15: SessionPackages, 16: Status,
            // 17: PaymentMethod, 18: ProfilePicture, 19: ConsentLetter, 20: DateJoined,
            // 21: LastCheckIn, 22: LastCheckOut, 23: RecentPurchaseItem,
            // 24: RecentPurchaseDate, 25: RecentPurchaseQuantity

            // Column indices for GetMemberByIdAsync (includeRegisteredBy = true)
            // 0: MemberID, 1: Firstname, 2: MiddleInitial, 3: Lastname, 4: Gender, 5: ProfilePicture,
            // 6: ContactNumber, 7: Age, 8: DateOfBirth, 9: ValidUntil,
            // 10: LastNotificationDate, 11: NotificationCount, 12: IsNotified,
            // 13: PackageID, 14: GymPackageName, 15: SessionPackages, 16: Status,
            // 17: PaymentMethod, 18: ConsentLetter, 19: RegisteredByEmployeeID, 20: DateJoined,
            // 21: LastCheckIn, 22: LastCheckOut, 23: RecentPurchaseItem,
            // 24: RecentPurchaseDate, 25: RecentPurchaseQuantity

            int memberIdIdx = 0;
            int firstNameIdx = 1;
            int middleInitialIdx = 2;
            int lastNameIdx = 3;
            int nameIdx = includeRegisteredBy ? -1 : 4;
            int genderIdx = includeRegisteredBy ? 4 : 5;
            int profilePicIdx = includeRegisteredBy ? 5 : 18;
            int contactIdx = includeRegisteredBy ? 6 : 6;
            int ageIdx = includeRegisteredBy ? 7 : 7;
            int birthYearIdx = includeRegisteredBy ? 8 : 8; // ✅ CHANGED from dobIdx to birthYearIdx
            int validUntilIdx = includeRegisteredBy ? 9 : 9;
            int lastNotificationDateIdx = includeRegisteredBy ? 18 : 10;
            int notificationCountIdx = includeRegisteredBy ? 19 : 11;
            int isNotifiedIdx = includeRegisteredBy ? 20 : 12;
            int packageIdIdx = includeRegisteredBy ? 10 : 13;
            int gymPackageNameIdx = includeRegisteredBy ? 11 : 14;
            int sessionPackagesIdx = includeRegisteredBy ? 12 : 15;
            int statusIdx = includeRegisteredBy ? 13 : 16;
            int paymentMethodIdx = includeRegisteredBy ? 14 : 17;
            int consentLetterIdx = includeRegisteredBy ? 15 : 19;
            int registeredByEmployeeIdIdx = includeRegisteredBy ? 16 : -1;
            int dateJoinedIdx = includeRegisteredBy ? 17 : 20;
            int lastCheckInIdx = includeRegisteredBy ? 21 : 21;
            int lastCheckOutIdx = includeRegisteredBy ? 22 : 22;
            int recentPurchaseItemIdx = includeRegisteredBy ? 23 : 23;
            int recentPurchaseDateIdx = includeRegisteredBy ? 24 : 24;
            int recentPurchaseQuantityIdx = includeRegisteredBy ? 25 : 25;

            string gymPackageName = reader.IsDBNull(gymPackageNameIdx) ? null : reader.GetString(gymPackageNameIdx);
            string sessionPackages = reader.IsDBNull(sessionPackagesIdx) ? null : reader.GetString(sessionPackagesIdx);

            string combinedPackages;
            if (!string.IsNullOrEmpty(gymPackageName) && !string.IsNullOrEmpty(sessionPackages))
            {
                combinedPackages = $"{gymPackageName}, {sessionPackages}";
            }
            else if (!string.IsNullOrEmpty(gymPackageName))
            {
                combinedPackages = gymPackageName;
            }
            else if (!string.IsNullOrEmpty(sessionPackages))
            {
                combinedPackages = sessionPackages;
            }
            else
            {
                combinedPackages = "None";
            }

            var member = new ManageMemberModel
            {
                MemberID = reader.GetInt32(memberIdIdx),
                FirstName = reader.IsDBNull(firstNameIdx) ? string.Empty : reader.GetString(firstNameIdx),
                MiddleInitial = reader.IsDBNull(middleInitialIdx) ? null : reader.GetString(middleInitialIdx),
                LastName = reader.IsDBNull(lastNameIdx) ? string.Empty : reader.GetString(lastNameIdx),
                Name = includeRegisteredBy ? null : (reader.IsDBNull(nameIdx) ? string.Empty : reader.GetString(nameIdx)),
                Gender = reader.IsDBNull(genderIdx) ? string.Empty : reader.GetString(genderIdx),
                ContactNumber = reader.IsDBNull(contactIdx) ? string.Empty : reader.GetString(contactIdx),
                Age = reader.IsDBNull(ageIdx) ? null : reader.GetInt32(ageIdx),
            
                // ✅ CHANGED: Read BirthYear instead of DateOfBirth
                BirthYear = reader.IsDBNull(birthYearIdx) ? null : reader.GetInt32(birthYearIdx),
            
                ValidUntil = reader.IsDBNull(validUntilIdx) ? null : reader.GetDateTime(validUntilIdx).ToString("MMM dd, yyyy"),
                LastNotificationDate = reader.IsDBNull(lastNotificationDateIdx) ? null : reader.GetDateTime(lastNotificationDateIdx),
                NotificationCount = reader.IsDBNull(notificationCountIdx) ? 0 : reader.GetInt32(notificationCountIdx),
                IsNotified = reader.IsDBNull(isNotifiedIdx) ? false : reader.GetBoolean(isNotifiedIdx),
                PackageID = reader.IsDBNull(packageIdIdx) ? null : reader.GetInt32(packageIdIdx),
                MembershipType = combinedPackages,
                Status = reader.IsDBNull(statusIdx) ? "Active" : reader.GetString(statusIdx),
                PaymentMethod = reader.IsDBNull(paymentMethodIdx) ? string.Empty : reader.GetString(paymentMethodIdx),
                ConsentLetter = reader.IsDBNull(consentLetterIdx) ? null : reader.GetString(consentLetterIdx),
                AvatarBytes = reader.IsDBNull(profilePicIdx) ? null : (byte[])reader[profilePicIdx],
                AvatarSource = reader.IsDBNull(profilePicIdx)
                    ? ImageHelper.GetDefaultAvatar()
                    : ImageHelper.BytesToBitmap((byte[])reader[profilePicIdx]),
                DateJoined = reader.IsDBNull(dateJoinedIdx) ? null : reader.GetDateTime(dateJoinedIdx),
                LastCheckIn = reader.IsDBNull(lastCheckInIdx) ? null : reader.GetDateTime(lastCheckInIdx),
                LastCheckOut = reader.IsDBNull(lastCheckOutIdx) ? null : reader.GetDateTime(lastCheckOutIdx),
                RecentPurchaseItem = reader.IsDBNull(recentPurchaseItemIdx) ? null : reader.GetString(recentPurchaseItemIdx),
                RecentPurchaseDate = reader.IsDBNull(recentPurchaseDateIdx) ? null : reader.GetDateTime(recentPurchaseDateIdx),
                RecentPurchaseQuantity = reader.IsDBNull(recentPurchaseQuantityIdx) ? null : reader.GetInt32(recentPurchaseQuantityIdx)
            };

            if (includeRegisteredBy && registeredByEmployeeIdIdx >= 0)
            {
                member.RegisteredByEmployeeID = reader.IsDBNull(registeredByEmployeeIdIdx) ? 0 : reader.GetInt32(registeredByEmployeeIdIdx);
            }

            return member;
        }

        #endregion

        #region UPDATE

        public async Task<(bool Success, string Message)> UpdateMemberAsync(
            ManageMemberModel member, 
            string? invoiceNumber = null)
        {
            if (!CanUpdate())
            {
                ShowAccessDeniedToast("update member data");
                return (false, "Insufficient permissions to update members.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    if (!await MemberExistsAsync(conn, member.MemberID, transaction))
                    {
                        _toastManager?.CreateToast("Member Not Found")
                            .WithContent("The member you're trying to update doesn't exist.")
                            .DismissOnClick()
                            .ShowWarning();
                        return (false, "Member not found.");
                    }

                    var originalMember = await GetOriginalMemberDataAsync(conn, member.MemberID, transaction);
                    byte[]? imageToSave = await GetImageToSaveAsync(conn, member, transaction);

                    const string query = @"
                UPDATE Members 
                SET Firstname = @Firstname, 
                    MiddleInitial = @MiddleInitial, 
                    Lastname = @Lastname, 
                    Gender = @Gender,
                    ContactNumber = @ContactNumber, 
                    Age = @Age,
                    BirthYear = @BirthYear,
                    ValidUntil = @ValidUntil,
                    PackageID = @PackageID,
                    Status = @Status,
                    PaymentMethod = @PaymentMethod,
                    ReferenceNumber = @ReferenceNumber,
                    ConsentLetter = @ConsentLetter,
                    ProfilePicture = @ProfilePicture,
                    IsNotified = 0,
                    LastNotificationDate = NULL
                WHERE MemberID = @MemberID";

                    using var cmd = new SqlCommand(query, conn, transaction);
                    member.ProfilePicture = imageToSave;

                    AddMemberParameters(cmd, member);
                    cmd.Parameters.AddWithValue("@MemberID", member.MemberID);

                    int rows = await cmd.ExecuteNonQueryAsync();

                    if (rows > 0)
                    {
                        bool isGymMembershipRenewal = await IsGymMembershipRenewalAsync(
                            conn, transaction, originalMember, member);

                        if (isGymMembershipRenewal && member.PackageID.HasValue && member.PackageID.Value > 0)
                        {
                            string finalInvoiceNumber = !string.IsNullOrWhiteSpace(invoiceNumber) 
                                ? invoiceNumber 
                                : await GenerateInvoiceNumberAsync();
                    
                            Debug.WriteLine($"[UpdateMemberAsync] Generated Invoice for renewal/upgrade: {finalInvoiceNumber}");

                            await RecordPackageSaleAsync(conn, transaction, member, member.MemberID, originalMember, finalInvoiceNumber);

                            string actionType = originalMember?.PackageID == member.PackageID ? "RENEWAL" : "UPGRADE";
                            await LogActionAsync(conn, transaction, actionType,
                                $"{actionType}: {member.FirstName} {member.LastName} - Package ID: {member.PackageID} - Invoice: {finalInvoiceNumber}", true);

                            _toastManager?.CreateToast($"Package {actionType}")
                                .WithContent($"Successfully {actionType.ToLower()}ed gym membership for {member.FirstName} {member.LastName}\nInvoice: {finalInvoiceNumber}")
                                .DismissOnClick()
                                .ShowSuccess();
                        }
                        else
                        {
                            await LogActionAsync(conn, transaction, "UPDATE",
                                $"Updated member: {member.FirstName} {member.LastName}", true);
                        }

                        transaction.Commit();
                        DashboardEventService.Instance.NotifyMemberUpdated();
                        return (true, "Member updated successfully.");
                    }

                    return (false, "Failed to update member.");
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                ShowDatabaseErrorToast("update member", ex.Message);
                Debug.WriteLine($"UpdateMemberAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorToast(ex.Message);
                Debug.WriteLine($"UpdateMemberAsync Error: {ex}");
                return (false, $"Error: {ex.Message}");
            }
        }

        private async Task<bool> MemberExistsAsync(SqlConnection conn, int memberId, SqlTransaction? transaction = null)
        {
            using var cmd = new SqlCommand(
                $"SELECT COUNT(*) FROM Members WHERE MemberID = @memberId AND {MEMBER_NOT_DELETED_FILTER}",
                conn, transaction);
            cmd.Parameters.AddWithValue("@memberId", memberId);
            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private async Task<byte[]?> GetImageToSaveAsync(SqlConnection conn, ManageMemberModel member, SqlTransaction? transaction = null)
        {
            byte[]? existingImage = null;
            using var getImageCmd = new SqlCommand(
                "SELECT ProfilePicture FROM Members WHERE MemberID = @memberId", conn, transaction);
            getImageCmd.Parameters.AddWithValue("@memberId", member.MemberID);

            var imageResult = await getImageCmd.ExecuteScalarAsync();
            if (imageResult != null && imageResult != DBNull.Value)
            {
                existingImage = (byte[])imageResult;
            }

            if (member.ProfilePicture != null && member.ProfilePicture.Length > 0)
                return member.ProfilePicture;

            if (member.AvatarBytes != null && member.AvatarBytes.Length > 0)
                return member.AvatarBytes;

            return existingImage;
        }

        #endregion

        #region DELETE (SOFT DELETE)

        public async Task<(bool Success, string Message)> DeleteMemberAsync(int memberId)
        {
            if (!CanDelete())
            {
                ShowAccessDeniedToast("delete members", "Only administrators can delete members.");
                return (false, "Insufficient permissions to delete members.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string memberName = await GetMemberNameAsync(conn, memberId);

                if (string.IsNullOrEmpty(memberName))
                {
                    _toastManager?.CreateToast("Member Not Found")
                        .WithContent("The member you're trying to delete doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Member not found.");
                }

                using var cmd = new SqlCommand(
                    "UPDATE Members SET IsDeleted = 1 WHERE MemberID = @memberId", conn);
                cmd.Parameters.AddWithValue("@memberId", memberId);
                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "DELETE", $"Soft deleted member: {memberName} (ID: {memberId})", true);
                    NotifyDashboardOfDeletion();
                    return (true, "Member deleted successfully.");
                }

                return (false, "Failed to delete member.");
            }
            catch (SqlException ex)
            {
                ShowDatabaseErrorToast("delete member", ex.Message);
                Debug.WriteLine($"DeleteMemberAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorToast(ex.Message);
                Debug.WriteLine($"DeleteMemberAsync Error: {ex}");
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteMultipleMembersAsync(List<int> memberIds)
        {
            if (!CanDelete())
            {
                ShowAccessDeniedToast("delete members", "Only administrators can delete members.");
                return (false, "Insufficient permissions to delete members.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var idsParam = string.Join(",", memberIds);
                var deleteQuery = $"UPDATE Members SET IsDeleted = 1 WHERE MemberID IN ({idsParam})";

                using var command = new SqlCommand(deleteQuery, conn);
                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "DELETE", $"Soft deleted {rowsAffected} members", true);
                    NotifyDashboardOfDeletion();
                    return (true, "Members deleted successfully.");
                }

                return (false, "No members were deleted.");
            }
            catch (SqlException ex)
            {
                ShowDatabaseErrorToast("delete members", ex.Message);
                Debug.WriteLine($"DeleteMultipleMembersAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorToast(ex.Message);
                Debug.WriteLine($"DeleteMultipleMembersAsync Error: {ex}");
                return (false, $"Error: {ex.Message}");
            }
        }

        private async Task<string> GetMemberNameAsync(SqlConnection conn, int memberId)
        {
            using var cmd = new SqlCommand(
                $"SELECT Firstname, Lastname FROM Members WHERE MemberID = @memberId AND {MEMBER_NOT_DELETED_FILTER}", conn);
            cmd.Parameters.AddWithValue("@memberId", memberId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return $"{reader[0]} {reader[1]}";
            }

            return string.Empty;
        }

        private void NotifyDashboardOfDeletion()
        {
            DashboardEventService.Instance.NotifyMemberDeleted();
            DashboardEventService.Instance.NotifyPopulationDataChanged();
        }

        #endregion

        #region EXPIRATION NOTIFICATIONS

        private async Task<List<MemberExpirationAlert>> GetExpiringMembersAsync(SqlConnection conn, int daysThreshold = EXPIRATION_THRESHOLD_DAYS)
        {
            var alerts = new List<MemberExpirationAlert>();

            try
            {
                const string query = @"
                    SELECT 
                        m.MemberID, m.Firstname, m.MiddleInitial, m.Lastname, m.ValidUntil,
                        m.ContactNumber, p.PackageName,
                        DATEDIFF(DAY, GETDATE(), m.ValidUntil) AS DaysRemaining
                    FROM Members m
                    LEFT JOIN Packages p ON m.PackageID = p.PackageID
                    WHERE m.ValidUntil IS NOT NULL
                    AND m.Status = 'Active'
                    AND (m.IsDeleted = 0 OR m.IsDeleted IS NULL)
                    AND DATEDIFF(DAY, GETDATE(), m.ValidUntil) BETWEEN 0 AND @DaysThreshold
                    ORDER BY m.ValidUntil ASC";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@DaysThreshold", daysThreshold);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    alerts.Add(new MemberExpirationAlert
                    {
                        MemberID = reader.GetInt32(0),
                        FirstName = reader.GetString(1),
                        MiddleInitial = reader.IsDBNull(2) ? null : reader.GetString(2),
                        LastName = reader.GetString(3),
                        ValidUntil = reader.GetDateTime(4),
                        ContactNumber = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        PackageName = reader.IsDBNull(6) ? "None" : reader.GetString(6),
                        DaysRemaining = reader.GetInt32(7)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetExpiringMembersAsync Error: {ex}");
            }

            return alerts;
        }

        private async Task<int> GetExpiringMembersCountAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT COUNT(*)
                    FROM Members
                    WHERE ValidUntil IS NOT NULL
                    AND Status = 'Active'
                    AND (IsDeleted = 0 OR IsDeleted IS NULL)
                    AND DATEDIFF(DAY, GETDATE(), ValidUntil) BETWEEN 0 AND @DaysThreshold";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@DaysThreshold", EXPIRATION_THRESHOLD_DAYS);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetExpiringMembersCountAsync Error: {ex}");
                return 0;
            }
        }

        public async Task AutoInactivateExpiredMembersAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    UPDATE Members 
                    SET Status = 'Inactive'
                    WHERE ValidUntil < CAST(GETDATE() AS DATE)
                    AND Status = 'Active'
                    AND (IsDeleted = 0 OR IsDeleted IS NULL)";

                using var cmd = new SqlCommand(query, conn);
                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "AUTO_UPDATE", $"Auto-inactivated {rowsAffected} expired members", true);
                    DashboardEventService.Instance.NotifyMemberUpdated();
                    Debug.WriteLine($"[AutoInactivateExpiredMembers] Inactivated {rowsAffected} members");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutoInactivateExpiredMembersAsync Error: {ex}");
            }
        }

        public async Task ShowMemberExpirationAlertsAsync(Action<Notification>? addNotificationCallback = null)
        {
            if (!CanView()) return;

            try
            {
                var expiringCount = await GetExpiringMembersCountAsync();
                await AutoInactivateExpiredMembersAsync();

                var notifyCallback = addNotificationCallback ?? _notificationCallback;

                if (expiringCount > 0)
                {
                    var title = "Membership Expiring Soon";
                    var message = $"{expiringCount} member(s) will expire within {EXPIRATION_THRESHOLD_DAYS} days!";

                    _toastManager?.CreateToast(title)
                        .WithContent(message)
                        .DismissOnClick()
                        .ShowWarning();

                    notifyCallback?.Invoke(new Notification
                    {
                        Type = NotificationType.Warning,
                        Title = title,
                        Message = message,
                        DateAndTime = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShowMemberExpirationAlertsAsync] Error: {ex.Message}");
            }
        }

        public async Task<MemberExpirationSummary> GetMemberExpirationSummaryAsync()
        {
            var summary = new MemberExpirationSummary();

            if (!CanView()) return summary;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                summary.ExpiringMembers = await GetExpiringMembersAsync(conn, EXPIRATION_THRESHOLD_DAYS);
                summary.ExpiringCount = summary.ExpiringMembers.Count;
                summary.TotalAlerts = summary.ExpiringCount;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetMemberExpirationSummaryAsync] Error: {ex.Message}");
            }

            return summary;
        }

        #endregion

        #region UTILITY METHODS

        private async Task LogActionAsync(SqlConnection conn, SqlTransaction transaction, string actionType, string description, bool success)
        {
            try
            {
                const string query = @"
                    INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                    VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)";

                using var cmd = new SqlCommand(query, conn, transaction);
                AddLogParameters(cmd, actionType, description, success);
                await cmd.ExecuteNonQueryAsync();
                NotifyDashboardOfLogUpdate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                const string query = @"
                    INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                    VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @employeeID)";

                using var cmd = new SqlCommand(query, conn);
                AddLogParameters(cmd, actionType, description, success);
                await cmd.ExecuteNonQueryAsync();
                NotifyDashboardOfLogUpdate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        private void AddLogParameters(SqlCommand cmd, string actionType, string description, bool success)
        {
            cmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@success", success);
            cmd.Parameters.AddWithValue("@employeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);
        }

        private void NotifyDashboardOfLogUpdate()
        {
            DashboardEventService.Instance.NotifyRecentLogsUpdated();
            DashboardEventService.Instance.NotifyPopulationDataChanged();
            DashboardEventService.Instance.NotifyChartDataUpdated();
            DashboardEventService.Instance.NotifySalesUpdated();
        }

        public async Task<int> GetTotalMemberCountAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    $"SELECT COUNT(*) FROM Members WHERE {MEMBER_NOT_DELETED_FILTER}", conn);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetTotalMemberCountAsync Error: {ex}");
                return 0;
            }
        }

        public async Task<int> GetMemberCountByStatusAsync(string status)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    $"SELECT COUNT(*) FROM Members WHERE Status = @Status AND {MEMBER_NOT_DELETED_FILTER}", conn);
                cmd.Parameters.AddWithValue("@Status", status);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetMemberCountByStatusAsync Error: {ex}");
                return 0;
            }
        }

        public async Task<(bool Success, int ActiveCount, int InactiveCount, int TerminatedCount)> GetMemberStatisticsAsync()
        {
            if (!CanView())
            {
                return (false, 0, 0, 0);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT 
                        SUM(CASE WHEN Status = 'Active' THEN 1 ELSE 0 END) as ActiveCount,
                        SUM(CASE WHEN Status = 'Inactive' THEN 1 ELSE 0 END) as InactiveCount,
                        SUM(CASE WHEN Status = 'Terminated' THEN 1 ELSE 0 END) as TerminatedCount
                    FROM Members
                    WHERE (IsDeleted = 0 OR IsDeleted IS NULL)";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return (true,
                        reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        reader.IsDBNull(2) ? 0 : reader.GetInt32(2));
                }

                return (false, 0, 0, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetMemberStatisticsAsync] {ex.Message}");
                return (false, 0, 0, 0);
            }
        }

        #endregion

        #region TOAST HELPERS

        private void ShowAccessDeniedToast(string action, string? customMessage = null)
        {
            _toastManager?.CreateToast("Access Denied")
                .WithContent(customMessage ?? $"You don't have permission to {action}.")
                .DismissOnClick()
                .ShowError();
        }

        private void ShowDatabaseErrorToast(string action, string errorMessage)
        {
            _toastManager?.CreateToast("Database Error")
                .WithContent($"Failed to {action}: {errorMessage}")
                .DismissOnClick()
                .ShowError();
        }

        private void ShowErrorToast(string errorMessage)
        {
            _toastManager?.CreateToast("Error")
                .WithContent($"An unexpected error occurred: {errorMessage}")
                .DismissOnClick()
                .ShowError();
        }

        private void ShowMemberExistsToast(ManageMemberModel member)
        {
            _toastManager?.CreateToast("Member Already Exists")
                .WithContent($"{member.FirstName} {member.LastName} already exists in the system.")
                .DismissOnClick()
                .ShowWarning();
        }

        #endregion

        #region PERSISTENT MEMBER NOTIFICATIONS

        /// <summary>
        /// Gets all members who have been notified (IsNotified = 1)
        /// These will be displayed in dashboard on startup
        /// </summary>
        public async Task<List<NotifiedMemberInfo>> GetNotifiedMembersAsync()
        {
            var notifiedMembers = new List<NotifiedMemberInfo>();

            if (!CanView())
            {
                return notifiedMembers;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
            SELECT 
                MemberID,
                LTRIM(RTRIM(Firstname + ISNULL(' ' + MiddleInitial + '.', '') + ' ' + Lastname)) AS Name,
                Status,
                ValidUntil,
                LastNotificationDate,
                NotificationCount
            FROM Members
            WHERE (IsDeleted = 0 OR IsDeleted IS NULL)
                AND IsNotified = 1
                AND LastNotificationDate IS NOT NULL
            ORDER BY LastNotificationDate DESC";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    notifiedMembers.Add(new NotifiedMemberInfo
                    {
                        MemberID = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Status = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2),
                        ValidUntil = reader.IsDBNull(3) ? "N/A" : reader.GetDateTime(3).ToString("MMM dd, yyyy"),
                        LastNotificationDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                        NotificationCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
                    });
                }

                Debug.WriteLine($"[GetNotifiedMembersAsync] Found {notifiedMembers.Count} notified members");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetNotifiedMembersAsync] Error: {ex.Message}");
            }

            return notifiedMembers;
        }

        /// <summary>
        /// Clears notification flag when member is renewed/upgraded
        /// </summary>
        private async Task ClearMemberNotificationFlagAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int memberId)
        {
            try
            {
                const string query = @"
            UPDATE Members 
            SET IsNotified = 0,
                LastNotificationDate = NULL
            WHERE MemberID = @MemberId";

                using var cmd = new SqlCommand(query, conn, transaction);
                cmd.Parameters.AddWithValue("@MemberId", memberId);

                await cmd.ExecuteNonQueryAsync();

                Debug.WriteLine($"[ClearMemberNotificationFlagAsync] Cleared notification for member {memberId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClearMemberNotificationFlagAsync] Error: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Supporting class for notified member information
        /// </summary>
        public class NotifiedMemberInfo
        {
            public int MemberID { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string ValidUntil { get; set; } = string.Empty;
            public DateTime? LastNotificationDate { get; set; }
            public int NotificationCount { get; set; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _notificationCallback = null;
                _toastManager.DismissAll();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #region SUPPORTING CLASSES

        public class MemberExpirationAlert
        {
            public int MemberID { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string? MiddleInitial { get; set; }
            public string LastName { get; set; } = string.Empty;
            public string FullName
            {
                get => $"{FirstName} {(string.IsNullOrWhiteSpace(MiddleInitial) ? "" : MiddleInitial + ". ")}{LastName}";
                set { }
            }
            public DateTime ValidUntil { get; set; }
            public string ContactNumber { get; set; } = string.Empty;
            public string PackageName { get; set; } = string.Empty;
            public int DaysRemaining { get; set; }

            public string AlertSeverity =>
                DaysRemaining == 1 ? "Critical" :
                DaysRemaining <= 2 ? "High" :
                DaysRemaining <= 4 ? "Warning" : "Normal";

            public string FormattedValidUntil => ValidUntil.ToString("MMM dd, yyyy");

            public string Details =>
                DaysRemaining == 1
                    ? $"Expires tomorrow ({FormattedValidUntil})"
                    : $"Expires in {DaysRemaining} days ({FormattedValidUntil})";
        }

        public class MemberExpirationSummary
        {
            public List<MemberExpirationAlert> ExpiringMembers { get; set; } = new();
            public int ExpiringCount { get; set; }
            public int TotalAlerts { get; set; }
            public bool HasAlerts => TotalAlerts > 0;
        }

        #endregion

        #region HELPER METHODS FOR RENEWAL/UPGRADE

        private async Task<ManageMemberModel?> GetOriginalMemberDataAsync(
            SqlConnection conn, int memberId, SqlTransaction transaction)
        {
            const string query = @"
                SELECT PackageID, ValidUntil, PaymentMethod
                FROM Members 
                WHERE MemberID = @MemberId";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@MemberId", memberId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ManageMemberModel
                {
                    MemberID = memberId,
                    PackageID = reader.IsDBNull(0) ? null : reader.GetInt32(0),
                    ValidUntil = reader.IsDBNull(1) ? null : reader.GetDateTime(1).ToString("MMM dd, yyyy"),
                    PaymentMethod = reader.IsDBNull(2) ? null : reader.GetString(2)
                };
            }

            return null;
        }

        private async Task<bool> IsGymMembershipRenewalAsync(
            SqlConnection conn, SqlTransaction transaction,
            ManageMemberModel? originalMember, ManageMemberModel updatedMember)
        {
            if (originalMember == null || !updatedMember.PackageID.HasValue)
                return false;

            bool isGymMembershipPackage = await IsGymMembershipPackageAsync(
                conn, transaction, updatedMember.PackageID.Value);

            if (!isGymMembershipPackage)
                return false;

            bool packageChanged = originalMember.PackageID != updatedMember.PackageID;

            bool validityExtended = originalMember.PackageID == updatedMember.PackageID &&
                                   !string.IsNullOrEmpty(updatedMember.ValidUntil) &&
                                   !string.IsNullOrEmpty(originalMember.ValidUntil) &&
                                   DateTime.TryParse(updatedMember.ValidUntil, out DateTime newDate) &&
                                   DateTime.TryParse(originalMember.ValidUntil, out DateTime oldDate) &&
                                   newDate > oldDate;

            return packageChanged || validityExtended;
        }

        private async Task<bool> IsGymMembershipPackageAsync(
            SqlConnection conn, SqlTransaction transaction, int packageId)
        {
            const string query = @"
                SELECT Duration 
                FROM Packages 
                WHERE PackageID = @PackageID 
                    AND IsDeleted = 0";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@PackageID", packageId);

            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
                return false;

            string duration = result.ToString()?.Trim().ToLower() ?? "";

            bool isSessionBased = duration.Contains("session") ||
                                 duration.Contains("one-time only") ||
                                 duration.Contains("one time only");

            return !isSessionBased;
        }

        #endregion

        #region WALK-IN TO MEMBER MIGRATION

        private async Task<int?> CheckAndMigrateWalkInCustomerEnhancedAsync(
            SqlConnection conn, SqlTransaction transaction, ManageMemberModel member)
        {
            try
            {
                // ✅ UPDATED: Match by name only (since we're not storing full birth dates anymore)
                const string checkQuery = @"
                SELECT CustomerID, FirstName, LastName, Age, ContactNumber, WalkinType 
                FROM WalkInCustomers 
                WHERE FirstName = @FirstName 
                    AND LastName = @LastName 
                    AND (IsDeleted = 0 OR IsDeleted IS NULL)
                ORDER BY CustomerID DESC";

                using var checkCmd = new SqlCommand(checkQuery, conn, transaction);
                checkCmd.Parameters.AddWithValue("@FirstName", member.FirstName ?? (object)DBNull.Value);
                checkCmd.Parameters.AddWithValue("@LastName", member.LastName ?? (object)DBNull.Value);

                var walkInMatches = new List<(int CustomerId, string FirstName, string LastName, int Age, string Contact, string Type)>();

                using var reader = await checkCmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int customerId = reader.GetInt32(0);
                    string firstName = reader.GetString(1);
                    string lastName = reader.GetString(2);
                    int age = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    string contact = reader.IsDBNull(4) ? "N/A" : reader.GetString(4);
                    string type = reader.IsDBNull(5) ? "Unknown" : reader.GetString(5);

                    walkInMatches.Add((customerId, firstName, lastName, age, contact, type));

                    Debug.WriteLine($"[CheckAndMigrateWalkInCustomerEnhancedAsync] Found walk-in match:");
                    Debug.WriteLine($"  CustomerID: {customerId}");
                    Debug.WriteLine($"  Name: {firstName} {lastName}");
                    Debug.WriteLine($"  Age: {age}, Contact: {contact}, Type: {type}");
                }

                reader.Close();

                if (walkInMatches.Count == 0)
                {
                    Debug.WriteLine("[CheckAndMigrateWalkInCustomerEnhancedAsync] No matching walk-in customers found");
                    return null;
                }

                int totalMigrated = 0;
                var migratedIds = new List<int>();

                foreach (var match in walkInMatches)
                {
                    const string deleteQuery = @"
                    UPDATE WalkInCustomers 
                    SET IsDeleted = 1 
                    WHERE CustomerID = @CustomerID";

                    using var deleteCmd = new SqlCommand(deleteQuery, conn, transaction);
                    deleteCmd.Parameters.AddWithValue("@CustomerID", match.CustomerId);

                    int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        totalMigrated++;
                        migratedIds.Add(match.CustomerId);

                        Debug.WriteLine($"[CheckAndMigrateWalkInCustomerEnhancedAsync] ✅ Migrated walk-in {match.CustomerId}");
                    }
                }

                if (totalMigrated > 0)
                {
                    string migrationDetails = totalMigrated == 1
                        ? $"Migrated walk-in customer {member.FirstName} {member.LastName} (ID: {migratedIds[0]}) to member"
                        : $"Migrated {totalMigrated} walk-in records for {member.FirstName} {member.LastName} (IDs: {string.Join(", ", migratedIds)}) to member";

                    await LogActionAsync(conn, transaction, "MIGRATION", migrationDetails, true);

                    string toastMessage = totalMigrated == 1
                        ? $"{member.FirstName} {member.LastName} has been successfully converted from walk-in to member."
                        : $"{member.FirstName} {member.LastName} - {totalMigrated} walk-in record(s) have been archived and converted to member.";

                    _toastManager?.CreateToast("Walk-in Customer Migrated")
                        .WithContent(toastMessage)
                        .DismissOnClick()
                        .ShowSuccess();

                    Debug.WriteLine($"[CheckAndMigrateWalkInCustomerEnhancedAsync] ✅ Successfully migrated {totalMigrated} walk-in record(s)");

                    return migratedIds.FirstOrDefault();
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CheckAndMigrateWalkInCustomerEnhancedAsync] Error: {ex.Message}");
                return null;
            }
        }

        #endregion
        
        #region INVOICE NUMBER GENERATION

        /// <summary>
        /// Generates a unique invoice number in format: INV-YYYYMMDD-XXXXX
        /// Where XXXXX is a random alphanumeric code
        /// </summary>
        public async Task<string> GenerateInvoiceNumberAsync()
        {
            const int maxAttempts = 10;
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                string invoiceNumber = GenerateInvoiceNumberFormat();
        
                bool exists = await InvoiceNumberExistsAsync(invoiceNumber);
        
                if (!exists)
                {
                    Debug.WriteLine($"[GenerateInvoiceNumberAsync] Generated unique invoice: {invoiceNumber}");
                    return invoiceNumber;
                }

                attempts++;
                Debug.WriteLine($"[GenerateInvoiceNumberAsync] Invoice collision, retry {attempts}/{maxAttempts}");
            }

            // Fallback: use timestamp to ensure uniqueness
            string fallbackInvoice = $"INV-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Ticks % 100000:D5}";
            Debug.WriteLine($"[GenerateInvoiceNumberAsync] Using fallback invoice: {fallbackInvoice}");
            return fallbackInvoice;
        }

        /// <summary>
        /// Generates invoice number in format: INV-YYYYMMDD-XXXXX
        /// </summary>
        private string GenerateInvoiceNumberFormat()
        {
            string datePrefix = DateTime.Now.ToString("yyyyMMdd");
            string randomSuffix = GenerateRandomAlphanumeric(5);
    
            return $"INV-{datePrefix}-{randomSuffix}";
        }

        /// <summary>
        /// Generates a random alphanumeric string of specified length
        /// </summary>
        private string GenerateRandomAlphanumeric(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var result = new char[length];
    
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }
    
            return new string(result);
        }

        /// <summary>
        /// Checks if an invoice number already exists in the database
        /// </summary>
        public async Task<bool> InvoiceNumberExistsAsync(string invoiceNumber)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
            SELECT COUNT(1) 
            FROM Sales 
            WHERE InvoiceNumber = @InvoiceNumber";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber ?? (object)DBNull.Value);

                int count = (int)await cmd.ExecuteScalarAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InvoiceNumberExistsAsync] Error: {ex.Message}");
                return false; // Assume doesn't exist on error
            }
        }

        #endregion
    }
}
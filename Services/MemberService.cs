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
using System.Threading.Tasks;
using Notification = AHON_TRACK.Models.Notification;

namespace AHON_TRACK.Services
{
    public class MemberService : IMemberService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;
        private Action<Notification>? _notificationCallback;

        private const int EXPIRATION_THRESHOLD_DAYS = 4;
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

        #region CREATE

        public async Task<(bool Success, string Message, int? MemberId)> AddMemberAsync(ManageMemberModel member)
        {
            if (!CanCreate())
            {
                ShowAccessDeniedToast("add members");
                return (false, "Insufficient permissions to add members.", null);
            }

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
                        return await RestoreMemberAsync(conn, transaction, existingMemberId.Value, member);
                    }

                    if (existingMemberId.HasValue)
                    {
                        ShowMemberExistsToast(member);
                        return (false, "Member already exists.", null);
                    }

                    int memberId = await InsertNewMemberAsync(conn, transaction, member);

                    if (member.PackageID.HasValue && member.PackageID.Value > 0)
                    {
                        await RecordPackageSaleAsync(conn, transaction, member, memberId);
                    }

                    await LogActionAsync(conn, transaction, "CREATE", $"Added new member: {member.FirstName} {member.LastName}", true);
                    transaction.Commit();

                    DashboardEventService.Instance.NotifyMemberAdded();
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
                AND DateOfBirth = @DateOfBirth";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@Firstname", member.FirstName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Lastname", member.LastName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateOfBirth", member.DateOfBirth ?? (object)DBNull.Value);

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
            SqlConnection conn, SqlTransaction transaction, int memberId, ManageMemberModel member)
        {
            const string query = @"
                UPDATE Members 
                SET IsDeleted = 0,
                    MiddleInitial = @MiddleInitial,
                    Gender = @Gender,
                    ProfilePicture = @ProfilePicture,
                    ContactNumber = @ContactNumber,
                    Age = @Age,
                    ValidUntil = @ValidUntil,
                    PackageID = @PackageID,
                    Status = @Status,
                    PaymentMethod = @PaymentMethod,
                    RegisteredByEmployeeID = @RegisteredByEmployeeID
                WHERE MemberID = @MemberID";

            using var cmd = new SqlCommand(query, conn, transaction);
            AddMemberParameters(cmd, member);
            cmd.Parameters.AddWithValue("@MemberID", memberId);

            await cmd.ExecuteNonQueryAsync();
            await LogActionAsync(conn, transaction, "RESTORE", $"Restored member: {member.FirstName} {member.LastName}", true);
            transaction.Commit();

            _toastManager?.CreateToast("Member Restored")
                .WithContent($"Successfully restored {member.FirstName} {member.LastName}.")
                .DismissOnClick()
                .ShowSuccess();

            return (true, "Member restored successfully.", memberId);
        }

        private async Task<int> InsertNewMemberAsync(
            SqlConnection conn, SqlTransaction transaction, ManageMemberModel member)
        {
            const string query = @"
                INSERT INTO Members 
                (Firstname, MiddleInitial, Lastname, Gender, ProfilePicture, ContactNumber, Age, DateOfBirth, 
                 ValidUntil, PackageID, Status, PaymentMethod, RegisteredByEmployeeID, IsDeleted)
                OUTPUT INSERTED.MemberID
                VALUES 
                (@Firstname, @MiddleInitial, @Lastname, @Gender, @ProfilePicture, @ContactNumber, @Age, @DateOfBirth, 
                 @ValidUntil, @PackageID, @Status, @PaymentMethod, @RegisteredByEmployeeID, 0)";

            using var cmd = new SqlCommand(query, conn, transaction);
            AddMemberParameters(cmd, member);
            cmd.Parameters.AddWithValue("@DateOfBirth", member.DateOfBirth ?? (object)DBNull.Value);

            return (int)await cmd.ExecuteScalarAsync();
        }

        private async Task RecordPackageSaleAsync(
            SqlConnection conn, SqlTransaction transaction, ManageMemberModel member, int memberId)
        {
            var (packagePrice, packageName, duration) = await GetPackageDetailsAsync(conn, transaction, member.PackageID!.Value);

            if (packagePrice <= 0) return;

            int quantity = CalculateQuantityFromValidUntil(member.ValidUntil);
            decimal totalAmount = packagePrice * quantity;

            await InsertSaleRecordAsync(conn, transaction, member.PackageID!.Value, memberId, quantity, totalAmount);
            await UpdateDailySalesAsync(conn, transaction, totalAmount);

            Debug.WriteLine($"[AddMemberAsync] Sale recorded: {packageName} x{quantity} = ₱{totalAmount:N2}");
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
            SqlConnection conn, SqlTransaction transaction, int packageId, int memberId, int quantity, decimal amount)
        {
            const string query = @"
                INSERT INTO Sales (SaleDate, PackageID, MemberID, Quantity, Amount, RecordedBy)
                VALUES (GETDATE(), @packageId, @memberId, @quantity, @amount, @employeeId)";

            using var cmd = new SqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@packageId", packageId);
            cmd.Parameters.AddWithValue("@memberId", memberId);
            cmd.Parameters.AddWithValue("@quantity", quantity);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.Parameters.AddWithValue("@employeeId", CurrentUserModel.UserId ?? (object)DBNull.Value);

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
            cmd.Parameters.AddWithValue("@ValidUntil", member.ValidUntil ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PackageID", member.PackageID ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", member.Status ?? "Active");
            cmd.Parameters.AddWithValue("@PaymentMethod", member.PaymentMethod ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RegisteredByEmployeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);
        }

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
                        m.Gender, m.ContactNumber, m.Age, m.DateOfBirth, m.ValidUntil,
                        m.PackageID, p.PackageName, m.Status, m.PaymentMethod, m.ProfilePicture, m.DateJoined,
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
                        m.ContactNumber, m.Age, m.DateOfBirth, m.ValidUntil, m.PackageID, p.PackageName,
                        m.Status, m.PaymentMethod, m.RegisteredByEmployeeID, m.DateJoined,
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
                    AND Duration NOT LIKE '%One-time Only%' 
                    AND Duration NOT LIKE '%One time Only%' 
                    AND Duration NOT LIKE '%Session%'
                    AND Duration NOT LIKE '%session%'
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
            var member = new ManageMemberModel
            {
                MemberID = reader.GetInt32(0),
                FirstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                MiddleInitial = reader.IsDBNull(2) ? null : reader.GetString(2),
                LastName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Name = includeRegisteredBy ? null : (reader.IsDBNull(4) ? string.Empty : reader.GetString(4)),
                Gender = reader.IsDBNull(includeRegisteredBy ? 4 : 5) ? string.Empty : reader.GetString(includeRegisteredBy ? 4 : 5),
                ContactNumber = reader.IsDBNull(includeRegisteredBy ? 6 : 6) ? string.Empty : reader.GetString(includeRegisteredBy ? 6 : 6),
                Age = reader.IsDBNull(includeRegisteredBy ? 7 : 7) ? null : reader.GetInt32(includeRegisteredBy ? 7 : 7),
                DateOfBirth = reader.IsDBNull(includeRegisteredBy ? 8 : 8) ? null : reader.GetDateTime(includeRegisteredBy ? 8 : 8),
                ValidUntil = reader.IsDBNull(includeRegisteredBy ? 9 : 9) ? null : reader.GetDateTime(includeRegisteredBy ? 9 : 9).ToString("MMM dd, yyyy"),
                PackageID = reader.IsDBNull(includeRegisteredBy ? 10 : 10) ? null : reader.GetInt32(includeRegisteredBy ? 10 : 10),
                MembershipType = reader.IsDBNull(includeRegisteredBy ? 11 : 11) ? "None" : reader.GetString(includeRegisteredBy ? 11 : 11),
                Status = reader.IsDBNull(includeRegisteredBy ? 12 : 12) ? "Active" : reader.GetString(includeRegisteredBy ? 12 : 12),
                PaymentMethod = reader.IsDBNull(includeRegisteredBy ? 13 : 13) ? string.Empty : reader.GetString(includeRegisteredBy ? 13 : 13),
                AvatarBytes = reader.IsDBNull(includeRegisteredBy ? 5 : 14) ? null : (byte[])reader[includeRegisteredBy ? 5 : 14],
                AvatarSource = reader.IsDBNull(includeRegisteredBy ? 5 : 14)
                    ? ImageHelper.GetDefaultAvatar()
                    : ImageHelper.BytesToBitmap((byte[])reader[includeRegisteredBy ? 5 : 14]),
                DateJoined = reader.IsDBNull(includeRegisteredBy ? 15 : 15) ? null : reader.GetDateTime(includeRegisteredBy ? 15 : 15),
                LastCheckIn = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
                LastCheckOut = reader.IsDBNull(17) ? null : reader.GetDateTime(17),
                RecentPurchaseItem = reader.IsDBNull(18) ? null : reader.GetString(18),
                RecentPurchaseDate = reader.IsDBNull(19) ? null : reader.GetDateTime(19),
                RecentPurchaseQuantity = reader.IsDBNull(20) ? null : reader.GetInt32(20)
            };

            if (includeRegisteredBy)
            {
                member.RegisteredByEmployeeID = reader.IsDBNull(14) ? 0 : reader.GetInt32(14);
            }

            return member;
        }

        #endregion

        #region UPDATE

        public async Task<(bool Success, string Message)> UpdateMemberAsync(ManageMemberModel member)
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

                if (!await MemberExistsAsync(conn, member.MemberID))
                {
                    _toastManager?.CreateToast("Member Not Found")
                        .WithContent("The member you're trying to update doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Member not found.");
                }

                byte[]? imageToSave = await GetImageToSaveAsync(conn, member);

                const string query = @"
                    UPDATE Members 
                    SET Firstname = @Firstname, 
                        MiddleInitial = @MiddleInitial, 
                        Lastname = @Lastname, 
                        Gender = @Gender,
                        ContactNumber = @ContactNumber, 
                        Age = @Age,
                        DateOfBirth = @DateOfBirth,
                        ValidUntil = @ValidUntil,
                        PackageID = @PackageID,
                        Status = @Status,
                        PaymentMethod = @PaymentMethod,
                        ProfilePicture = @ProfilePicture
                    WHERE MemberID = @MemberID";

                using var cmd = new SqlCommand(query, conn);
                member.ProfilePicture = imageToSave;

                AddMemberParameters(cmd, member);
                cmd.Parameters.AddWithValue("@MemberID", member.MemberID);
                cmd.Parameters.AddWithValue("@DateOfBirth", member.DateOfBirth ?? (object)DBNull.Value);

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                {
                    await LogActionAsync(conn, "UPDATE", $"Updated member: {member.FirstName} {member.LastName}", true);
                    DashboardEventService.Instance.NotifyMemberUpdated();
                    return (true, "Member updated successfully.");
                }

                return (false, "Failed to update member.");
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

        private async Task<bool> MemberExistsAsync(SqlConnection conn, int memberId)
        {
            using var cmd = new SqlCommand(
                $"SELECT COUNT(*) FROM Members WHERE MemberID = @memberId AND {MEMBER_NOT_DELETED_FILTER}", conn);
            cmd.Parameters.AddWithValue("@memberId", memberId);
            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private async Task<byte[]?> GetImageToSaveAsync(SqlConnection conn, ManageMemberModel member)
        {
            byte[]? existingImage = null;
            using var getImageCmd = new SqlCommand(
                "SELECT ProfilePicture FROM Members WHERE MemberID = @memberId", conn);
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
                    AND DATEDIFF(DAY, GETDATE(), m.ValidUntil) BETWEEN 1 AND @DaysThreshold
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
                    AND DATEDIFF(DAY, GETDATE(), ValidUntil) BETWEEN 1 AND @DaysThreshold";

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
    }
}
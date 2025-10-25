using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Events;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AHON_TRACK.Services
{
    public class CheckInOutService : ICheckInOutService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public CheckInOutService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        #region ROLE-BASED PERMISSIONS

        private bool CanCreate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanUpdate() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanDelete() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        private bool CanView() =>
            CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;

        private bool CheckPermission(Func<bool> permissionCheck, string action)
        {
            if (!permissionCheck())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent($"You do not have permission to {action}.")
                    .ShowError();
                return false;
            }
            return true;
        }

        #endregion

        #region AUTO CHECKOUT

        /// <summary>
        /// Automatically checks out all members and walk-ins who didn't check out from previous days.
        /// This should be called when loading data or on application startup.
        /// </summary>
        public async Task AutoCheckoutPreviousDaysAsync()
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Auto checkout members from previous days
                var memberCount = await AutoCheckoutMembersAsync(connection);

                // Auto checkout walk-ins from previous days
                var walkInCount = await AutoCheckoutWalkInsAsync(connection);

                if (memberCount > 0 || walkInCount > 0)
                {
                    Debug.WriteLine($"Auto checkout completed: {memberCount} members, {walkInCount} walk-ins");

                    await LogActionAsync(connection, "Auto Check-Out",
                        $"Automatically checked out {memberCount} members and {walkInCount} walk-ins from previous days", true);

                    DashboardEventService.Instance.NotifyCheckoutAdded();
                }
            }
            catch (Exception ex)
            {
                LogError("AutoCheckoutPreviousDaysAsync", ex);
            }
        }

        private async Task<int> AutoCheckoutMembersAsync(SqlConnection connection)
        {
            const string query = @"
                UPDATE MemberCheckIns 
                SET CheckOut = DATEADD(MINUTE, 59, DATEADD(HOUR, 23, DATEADD(DAY, 0, CAST(DateAttendance AS DATETIME))))
                WHERE CheckOut IS NULL 
                AND CAST(DateAttendance AS DATE) < CAST(GETDATE() AS DATE)
                AND IsDeleted = 0";

            await using var cmd = new SqlCommand(query, connection);
            return await cmd.ExecuteNonQueryAsync();
        }

        private async Task<int> AutoCheckoutWalkInsAsync(SqlConnection connection)
        {
            const string query = @"
                UPDATE WalkInRecords 
                SET CheckOut = DATEADD(MINUTE, 59, DATEADD(HOUR, 23, DATEADD(DAY, 0, CAST(CAST(CheckIn AS DATE) AS DATETIME))))
                WHERE CheckOut IS NULL 
                AND CAST(CheckIn AS DATE) < CAST(GETDATE() AS DATE)";

            await using var cmd = new SqlCommand(query, connection);
            return await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region CREATE (Check-In)

        public async Task<bool> CheckInMemberAsync(int memberId)
        {
            if (!CheckPermission(CanCreate, "perform this action"))
                return false;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Auto checkout previous days before checking in
                await AutoCheckoutPreviousDaysAsync();

                if (await IsMemberAlreadyCheckedInAsync(connection, memberId))
                    return false;

                if (await InsertMemberCheckInAsync(connection, memberId))
                {
                    var memberInfo = await GetMemberInfoAsync(connection, memberId);
                    DashboardEventService.Instance.NotifyCheckinAdded();
                    await LogActionAsync(connection, "Member Check-In",
                        $"Member {memberInfo?.Name ?? memberId.ToString()} checked in", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError("CheckInMemberAsync", ex);
                ShowErrorToast("Check-In Error", $"Failed to check in member: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> CheckInWalkInAsync(int customerID)
        {
            if (!CheckPermission(CanCreate, "perform this action"))
                return false;

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Auto checkout previous days before checking in
                await AutoCheckoutPreviousDaysAsync();

                if (await IsWalkInAlreadyCheckedInAsync(conn, customerID))
                    return false;

                return await InsertWalkInCheckInAsync(conn, customerID);
            }
            catch (Exception ex)
            {
                LogError("CheckInWalkInAsync", ex);
                return false;
            }
        }

        private async Task<bool> IsMemberAlreadyCheckedInAsync(SqlConnection connection, int memberId)
        {
            const string query = @"
                SELECT COUNT(*) FROM MemberCheckIns 
                WHERE MemberID = @MemberID 
                AND CAST(DateAttendance AS DATE) = CAST(GETDATE() AS DATE)
                AND CheckOut IS NULL 
                AND IsDeleted = 0";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@MemberID", memberId);
            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private async Task<bool> InsertMemberCheckInAsync(SqlConnection connection, int memberId)
        {
            const string query = @"
                INSERT INTO MemberCheckIns (MemberID, CheckIn, DateAttendance)
                VALUES (@MemberId, GETDATE(), CAST(GETDATE() AS DATE))";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@MemberId", memberId);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private async Task<bool> IsWalkInAlreadyCheckedInAsync(SqlConnection connection, int customerID)
        {
            const string query = @"
                SELECT COUNT(*) FROM WalkInRecords 
                WHERE CustomerID = @customerID 
                AND CAST(CheckIn AS DATE) = CAST(GETDATE() AS DATE)";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@customerID", customerID);
            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private async Task<bool> InsertWalkInCheckInAsync(SqlConnection connection, int customerID)
        {
            const string query = @"
                INSERT INTO WalkInRecords (CustomerID, CheckIn, CheckOut)
                VALUES (@customerID, GETDATE(), NULL)";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@customerID", customerID);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        #endregion

        #region READ (View)

        public async Task<List<MemberPerson>> GetMemberCheckInsAsync(DateTime date)
        {
            if (!CheckPermission(CanView, "view this data"))
                return new List<MemberPerson>();

            // Auto checkout previous days when viewing data
            await AutoCheckoutPreviousDaysAsync();

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        mci.RecordID,
                        m.Firstname,
                        m.Lastname,
                        m.ContactNumber,
                        m.Status,
                        m.ProfilePicture,
                        mci.CheckIn,
                        mci.CheckOut,
                        mci.DateAttendance
                    FROM MemberCheckIns mci
                    INNER JOIN Members m ON mci.MemberId = m.MemberId
                    WHERE CAST(mci.DateAttendance AS DATE) = @Date
                    ORDER BY mci.CheckIn ASC";

                await using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Date", date.Date);
                await using var reader = await cmd.ExecuteReaderAsync();

                return await ReadMemberCheckInsAsync(reader);
            }
            catch (Exception ex)
            {
                LogError("GetMemberCheckInsAsync", ex);
                ShowErrorToast("Database Error", $"Failed to load member check-ins: {ex.Message}");
                return new List<MemberPerson>();
            }
        }

        public async Task<List<WalkInPerson>> GetWalkInCheckInsAsync(DateTime date)
        {
            if (!CheckPermission(CanView, "view this data"))
                return new List<WalkInPerson>();

            // Auto checkout previous days when viewing data
            await AutoCheckoutPreviousDaysAsync();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string query = @"
                    SELECT 
                        r.RecordID,
                        c.CustomerID,
                        c.FirstName,
                        c.LastName,
                        c.Age,
                        c.ContactNumber,
                        c.WalkinPackage AS Package,
                        r.CheckIn,
                        r.CheckOut
                    FROM WalkInRecords r
                    INNER JOIN WalkInCustomers c ON r.CustomerID = c.CustomerID
                    WHERE CAST(r.CheckIn AS DATE) = CAST(@date AS DATE)
                    ORDER BY r.RecordID ASC";

                await using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@date", date);
                await using var reader = await cmd.ExecuteReaderAsync();

                return await ReadWalkInCheckInsAsync(reader);
            }
            catch (Exception ex)
            {
                LogError("GetWalkInCheckInsAsync", ex);
                return new List<WalkInPerson>();
            }
        }

        public async Task<List<ManageMemberModel>> GetAvailableMembersForCheckInAsync()
        {
            if (!CheckPermission(CanView, "view this data"))
                return new List<ManageMemberModel>();

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        m.MemberID,
                        m.Firstname,
                        m.Lastname,
                        m.ContactNumber,
                        m.Status,
                        m.ValidUntil,
                        m.ProfilePicture
                    FROM Members m
                    WHERE m.Status = 'Active' AND IsDeleted = 0
                    ORDER BY MemberID";

                await using var cmd = new SqlCommand(query, connection);
                await using var reader = await cmd.ExecuteReaderAsync();

                return await ReadAvailableMembersAsync(reader);
            }
            catch (Exception ex)
            {
                LogError("GetAvailableMembersForCheckInAsync", ex);
                ShowErrorToast("Database Error", $"Failed to load available members: {ex.Message}");
                return new List<ManageMemberModel>();
            }
        }

        private async Task<List<MemberPerson>> ReadMemberCheckInsAsync(SqlDataReader reader)
        {
            var members = new List<MemberPerson>();

            while (await reader.ReadAsync())
            {
                byte[]? profilePictureBytes = reader.IsDBNull("ProfilePicture")
                    ? null
                    : (byte[])reader["ProfilePicture"];

                members.Add(new MemberPerson
                {
                    ID = reader.GetInt32("RecordID"),
                    FirstName = reader["Firstname"]?.ToString() ?? "",
                    LastName = reader["Lastname"]?.ToString() ?? "",
                    ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                    MembershipType = "Gym Member",
                    Status = reader["Status"]?.ToString() ?? "",
                    DateAttendance = reader.GetDateTime("DateAttendance"),
                    CheckInTime = reader.GetDateTime("CheckIn"),
                    CheckOutTime = reader.IsDBNull("CheckOut") ? null : reader.GetDateTime("CheckOut"),
                    AvatarSource = ImageHelper.BytesToBitmap(profilePictureBytes)
                });
            }

            return members;
        }

        private async Task<List<WalkInPerson>> ReadWalkInCheckInsAsync(SqlDataReader reader)
        {
            var walkIns = new List<WalkInPerson>();

            while (await reader.ReadAsync())
            {
                var checkIn = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7);

                walkIns.Add(new WalkInPerson
                {
                    ID = reader.GetInt32(0),
                    FirstName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    LastName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Age = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    ContactNumber = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    PackageType = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    DateAttendance = checkIn?.Date,
                    CheckInTime = checkIn,
                    CheckOutTime = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                });
            }

            return walkIns;
        }

        private async Task<List<ManageMemberModel>> ReadAvailableMembersAsync(SqlDataReader reader)
        {
            var members = new List<ManageMemberModel>();

            while (await reader.ReadAsync())
            {
                byte[]? bytes = reader.IsDBNull("ProfilePicture")
                    ? null
                    : (byte[])reader["ProfilePicture"];

                string validityDisplay = "";
                if (!reader.IsDBNull("ValidUntil"))
                {
                    validityDisplay = reader.GetDateTime("ValidUntil").ToString("MMM dd, yyyy");
                }

                string firstName = reader["Firstname"]?.ToString() ?? "";
                string lastName = reader["Lastname"]?.ToString() ?? "";

                members.Add(new ManageMemberModel
                {
                    MemberID = reader.GetInt32("MemberId"),
                    Name = $"{firstName} {lastName}".Trim(),
                    ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                    Status = reader["Status"]?.ToString() ?? "",
                    ValidUntil = validityDisplay,
                    AvatarBytes = bytes,
                    AvatarSource = bytes != null
                        ? ImageHelper.BytesToBitmap(bytes)
                        : ImageHelper.GetDefaultAvatar()
                });
            }

            return members;
        }

        #endregion

        #region UPDATE (Check-Out)

        public async Task<bool> CheckOutMemberAsync(int memberCheckInId)
        {
            if (!CheckPermission(CanUpdate, "perform this action"))
                return false;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                if (await UpdateMemberCheckOutAsync(connection, memberCheckInId))
                {
                    DashboardEventService.Instance.NotifyCheckoutAdded();
                    await LogActionAsync(connection, "Member Check-Out",
                        $"Member checked out (Record ID: {memberCheckInId})", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError("CheckOutMemberAsync", ex);
                ShowErrorToast("Check-Out Error", $"Failed to check out member: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> CheckOutWalkInAsync(int checkInID)
        {
            if (!CheckPermission(CanUpdate, "perform this action"))
                return false;

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                if (await UpdateWalkInCheckOutAsync(conn, checkInID))
                {
                    DashboardEventService.Instance.NotifyCheckoutAdded();
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError("CheckOutWalkInAsync", ex);
            }
            return false;
        }

        private async Task<bool> UpdateMemberCheckOutAsync(SqlConnection connection, int recordId)
        {
            const string query = @"
                UPDATE MemberCheckIns 
                SET CheckOut = GETDATE() 
                WHERE RecordID = @RecordID AND CheckOut IS NULL";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@RecordID", recordId);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private async Task<bool> UpdateWalkInCheckOutAsync(SqlConnection connection, int recordId)
        {
            const string query = @"
                UPDATE WalkInRecords 
                SET CheckOut = GETDATE() 
                WHERE RecordID = @recordID";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@recordID", recordId);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        #endregion

        #region DELETE

        public async Task<bool> DeleteMemberCheckInAsync(int memberCheckInId)
        {
            if (!CheckPermission(CanDelete, "perform this action"))
                return false;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                if (await ExecuteDeleteAsync(connection, "MemberCheckIns", "RecordID", memberCheckInId))
                {
                    DashboardEventService.Instance.NotifyCheckInOutDeleted();
                    await LogActionAsync(connection, "Delete Member Check-In",
                        $"Deleted member check-in record (RecordID: {memberCheckInId})", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError("DeleteMemberCheckInAsync", ex);
                ShowErrorToast("Delete Error", $"Failed to delete member check-in: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> DeleteWalkInCheckInAsync(int checkInID)
        {
            if (!CheckPermission(CanDelete, "perform this action"))
                return false;

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                if (await ExecuteDeleteAsync(conn, "WalkInRecords", "RecordID", checkInID))
                {
                    DashboardEventService.Instance.NotifyCheckInOutDeleted();
                    await LogActionAsync(conn, "Delete Walk-In Check-In",
                        $"Deleted walk-in check-in record (RecordID: {checkInID})", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError("DeleteWalkInCheckInAsync", ex);
                ShowErrorToast("Delete Error", $"Failed to delete walk-in check-in: {ex.Message}");
            }
            return false;
        }

        private async Task<bool> ExecuteDeleteAsync(SqlConnection connection, string tableName,
            string idColumn, int id)
        {
            string query = $"DELETE FROM {tableName} WHERE {idColumn} = @Id";
            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        #endregion

        #region HELPER METHODS

        private async Task LogActionAsync(SqlConnection conn, string actionType,
            string description, bool success)
        {
            try
            {
                const string query = @"
                    INSERT INTO SystemLogs 
                        (Username, Role, ActionType, ActionDescription, IsSuccessful, 
                         LogDateTime, PerformedByEmployeeID) 
                    VALUES (@username, @role, @actionType, @description, @success, 
                            GETDATE(), @performed)";

                await using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@success", success);
                cmd.Parameters.AddWithValue("@performed", CurrentUserModel.UserId ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                DashboardEventService.Instance.NotifyRecentLogsUpdated();
                DashboardEventService.Instance.NotifyPopulationDataChanged();
            }
            catch (Exception ex)
            {
                LogError("LogActionAsync", ex);
            }
        }

        private async Task<ManageMemberModel?> GetMemberInfoAsync(SqlConnection connection, int memberId)
        {
            const string query = @"
                SELECT 
                    MemberId,
                    (Firstname + ' ' + ISNULL(MiddleInitial + '. ', '') + Lastname) AS Name,
                    ContactNumber,
                    Status
                FROM Members 
                WHERE MemberId = @MemberId";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@MemberId", memberId);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new ManageMemberModel
                {
                    MemberID = reader.GetInt32("MemberId"),
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? string.Empty
                };
            }
            return null;
        }

        public async Task<MemberPerson?> GetMemberCheckInByIdAsync(int memberCheckInId)
        {
            if (!CheckPermission(CanView, "view this data"))
                return null;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        mci.CheckInId,
                        m.Firstname + ' ' + ISNULL(m.MiddleInitial + '. ', '') + m.Lastname AS FullName,
                        m.ContactNumber,
                        m.Status,
                        m.ProfilePicture,
                        mci.CheckInTime,
                        mci.CheckOutTime,
                        mci.DateAttendance
                    FROM MemberCheckIns mci
                    INNER JOIN Members m ON mci.MemberId = m.MemberId
                    WHERE mci.CheckInId = @CheckInId";

                await using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@CheckInId", memberCheckInId);
                await using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapToMemberPerson(reader);
                }
            }
            catch (Exception ex)
            {
                LogError("GetMemberCheckInByIdAsync", ex);
            }
            return null;
        }

        public async Task<WalkInPerson?> GetWalkInCheckInByIdAsync(int walkInCheckInId)
        {
            if (!CheckPermission(CanView, "view this data"))
                return null;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        CheckInId,
                        FirstName,
                        LastName,
                        Age,
                        ContactNumber,
                        PackageType,
                        CheckInTime,
                        CheckOutTime,
                        DateAttendance
                    FROM WalkInCheckIns
                    WHERE CheckInId = @CheckInId";

                await using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@CheckInId", walkInCheckInId);
                await using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapToWalkInPerson(reader);
                }
            }
            catch (Exception ex)
            {
                LogError("GetWalkInCheckInByIdAsync", ex);
            }
            return null;
        }

        private MemberPerson MapToMemberPerson(SqlDataReader reader)
        {
            var nameParts = (reader["FullName"]?.ToString() ?? "")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            string firstName = nameParts.Length > 0 ? nameParts[0] : "";
            string lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";

            byte[]? profilePictureBytes = reader.IsDBNull("ProfilePicture")
                ? null
                : (byte[])reader["ProfilePicture"];

            return new MemberPerson
            {
                ID = reader.GetInt32("CheckInId"),
                FirstName = firstName,
                LastName = lastName,
                ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                MembershipType = "Member",
                Status = reader["Status"]?.ToString() ?? "",
                DateAttendance = reader.GetDateTime("DateAttendance"),
                CheckInTime = reader.GetDateTime("CheckInTime"),
                CheckOutTime = reader.IsDBNull("CheckOutTime") ? null : reader.GetDateTime("CheckOutTime"),
                AvatarSource = ImageHelper.BytesToBitmap(profilePictureBytes)
            };
        }

        private WalkInPerson MapToWalkInPerson(SqlDataReader reader)
        {
            return new WalkInPerson
            {
                ID = reader.GetInt32("CheckInId"),
                FirstName = reader["FirstName"]?.ToString() ?? "",
                LastName = reader["LastName"]?.ToString() ?? "",
                Age = reader.GetInt32("Age"),
                ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                PackageType = reader["PackageType"]?.ToString() ?? "",
                DateAttendance = reader.GetDateTime("DateAttendance"),
                CheckInTime = reader.GetDateTime("CheckInTime"),
                CheckOutTime = reader.IsDBNull("CheckOutTime") ? null : reader.GetDateTime("CheckOutTime")
            };
        }

        private void LogError(string methodName, Exception ex)
        {
            Console.WriteLine($"[{methodName}] {ex.Message}");
            Debug.WriteLine($"[{methodName}] {ex.Message}");
        }

        private void ShowErrorToast(string title, string message)
        {
            _toastManager.CreateToast(title)
                .WithContent(message)
                .WithDelay(5)
                .ShowError();
        }

        #endregion
    }
}
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

        private bool CanCreate()
        {
            // Both Admin and Staff can create
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;
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
            // Admin, Staff, and Coach can view
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;
        }

        #endregion

        #region CREATE (Check-In)

        public async Task<bool> CheckInMemberAsync(int memberId)
        {
            if (!CanCreate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to perform this action.")
                    .ShowError();
                return false;
            }

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string checkQuery = @"
                    SELECT COUNT(*) FROM MemberCheckIns 
                    WHERE MemberID = @MemberID AND CAST(DateAttendance AS DATE) = CAST(GETDATE() AS DATE)
                    AND CheckOut IS NULL AND IsDeleted = 0";

                await using var checkCommand = new SqlCommand(checkQuery, connection);
                checkCommand.Parameters.AddWithValue("@MemberID", memberId);
                var existingCount = (int)await checkCommand.ExecuteScalarAsync();

                if (existingCount > 0)
                {
                    /*    _toastManager.CreateToast("Already Checked In")
                            .WithContent("Member is already checked in today")
                            .ShowWarning();*/
                    return false;
                }

                const string insertQuery = @"
                    INSERT INTO MemberCheckIns (MemberID, CheckIn, DateAttendance)
                    VALUES (@MemberId, GETDATE(), CAST(GETDATE() AS DATE))";

                await using var command = new SqlCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@MemberId", memberId);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
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
                Console.WriteLine($"[CheckInMemberAsync] {ex.Message}");
                _toastManager.CreateToast("Check-In Error")
                    .WithContent($"Failed to check in member: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> CheckInWalkInAsync(int customerID)
        {
            if (!CanCreate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to perform this action.")
                    .ShowError();
                return false;
            }

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var checkCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM WalkInRecords 
                    WHERE CustomerID = @customerID 
                    AND CAST(CheckIn AS DATE) = CAST(GETDATE() AS DATE)", conn);

                checkCmd.Parameters.AddWithValue("@customerID", customerID);
                var alreadyCheckedIn = (int)await checkCmd.ExecuteScalarAsync() > 0;

                if (alreadyCheckedIn)
                {
                    /*   _toastManager.CreateToast("Already Checked In")
                           .WithContent("This walk-in customer is already checked in today")
                           .ShowWarning(); */
                    return false;
                }

                await using var cmd = new SqlCommand(@"
                    INSERT INTO WalkInRecords (CustomerID, CheckIn, CheckOut)
                    VALUES (@customerID, GETDATE(), NULL)", conn);

                cmd.Parameters.AddWithValue("@customerID", customerID);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking in walk-in: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region READ (View)

        public async Task<List<MemberPerson>> GetMemberCheckInsAsync(DateTime date)
        {
            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to view this data.")
                    .ShowError();
                return new List<MemberPerson>();
            }

            var memberCheckIns = new List<MemberPerson>();
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

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Date", date.Date);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    byte[]? profilePictureBytes = null;
                    if (!reader.IsDBNull("ProfilePicture"))
                        profilePictureBytes = (byte[])reader["ProfilePicture"];

                    memberCheckIns.Add(new MemberPerson
                    {
                        ID = reader.GetInt32("RecordID"),
                        FirstName = reader["Firstname"]?.ToString() ?? "",
                        LastName = reader["Lastname"]?.ToString() ?? "",
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                        MembershipType = "Gym Member",
                        Status = reader["Status"].ToString() ?? "",
                        DateAttendance = reader.GetDateTime("DateAttendance"),
                        CheckInTime = reader.GetDateTime("CheckIn"),
                        CheckOutTime = await reader.IsDBNullAsync("CheckOut") ? null : reader.GetDateTime("CheckOut"),
                        AvatarSource = ImageHelper.BytesToBitmap(profilePictureBytes)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMemberCheckInsAsync] {ex.Message}");
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to load member check-ins: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }

            return memberCheckIns;
        }

        public async Task<List<WalkInPerson>> GetWalkInCheckInsAsync(DateTime date)
        {
            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to view this data.")
                    .ShowError();
                return new List<WalkInPerson>();
            }

            var walkIns = new List<WalkInPerson>();

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
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
                    ORDER BY r.RecordID ASC", conn);

                cmd.Parameters.AddWithValue("@date", date);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var checkIn = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7);

                    walkIns.Add(new WalkInPerson
                    {
                        ID = reader.GetInt32("RecordID"),
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting walk-in check-ins: {ex.Message}");
            }

            return walkIns;
        }

        public async Task<List<ManageMemberModel>> GetAvailableMembersForCheckInAsync()
        {
            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to view this data.")
                    .ShowError();
                return new List<ManageMemberModel>();
            }

            var members = new List<ManageMemberModel>();
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
                    ORDER BY MemberID;";

                await using var command = new SqlCommand(query, connection);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    byte[]? bytes = null;
                    if (!reader.IsDBNull("ProfilePicture"))
                        bytes = (byte[])reader["ProfilePicture"];

                    string validityDisplay = "";
                    if (!reader.IsDBNull("ValidUntil"))
                    {
                        var validityDate = reader.GetDateTime("ValidUntil");
                        validityDisplay = validityDate.ToString("MMM dd, yyyy");
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetAvailableMembersForCheckInAsync] {ex.Message}");
                _toastManager.CreateToast("Database Error")
                    .WithContent($"Failed to load available members: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }
            return members;
        }

        #endregion

        #region UPDATE (Check-Out)

        public async Task<bool> CheckOutMemberAsync(int memberCheckInId)
        {
            if (!CanUpdate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to perform this action.")
                    .ShowError();
                return false;
            }

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string updateQuery = @"
                    UPDATE MemberCheckIns 
                    SET CheckOut = GETDATE() 
                    WHERE RecordID = @RecordID AND CheckOut IS NULL";

                await using var command = new SqlCommand(updateQuery, connection);
                command.Parameters.AddWithValue("@RecordID", memberCheckInId);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    DashboardEventService.Instance.NotifyCheckoutAdded();
                    await LogActionAsync(connection, "Member Check-Out",
                        $"Member checked out (Record ID: {memberCheckInId})", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckOutMemberAsync] {ex.Message}");
                _toastManager.CreateToast("Check-Out Error")
                    .WithContent($"Failed to check out member: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> CheckOutWalkInAsync(int checkInID)
        {
            if (!CanUpdate())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to perform this action.")
                    .ShowError();
                return false;
            }

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    UPDATE WalkInRecords 
                    SET CheckOut = GETDATE() 
                    WHERE RecordID = @recordID", conn);

                cmd.Parameters.AddWithValue("@recordID", checkInID);
                DashboardEventService.Instance.NotifyCheckoutAdded();
                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking out walk-in: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region DELETE

        public async Task<bool> DeleteMemberCheckInAsync(int memberCheckInId)
        {
            if (!CanDelete())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to perform this action.")
                    .ShowError();
                return false;
            }

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string deleteQuery = "DELETE FROM MemberCheckIns WHERE RecordID = @RecordId";
                await using var command = new SqlCommand(deleteQuery, connection);
                command.Parameters.AddWithValue("@RecordId", memberCheckInId);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    DashboardEventService.Instance.NotifyCheckInOutDeleted();
                    await LogActionAsync(connection, "Delete Member Check-In",
                        $"Deleted member check-in record (RecordID: {memberCheckInId})", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteMemberCheckInAsync] {ex.Message}");
                _toastManager.CreateToast("Delete Error")
                    .WithContent($"Failed to delete member check-in: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }
            return false;
        }

        public async Task<bool> DeleteWalkInCheckInAsync(int checkInID)
        {
            if (!CanDelete())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to perform this action.")
                    .ShowError();
                return false;
            }

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
            DELETE FROM WalkInRecords 
            WHERE RecordID = @recordID", conn);

                cmd.Parameters.AddWithValue("@recordID", checkInID);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    // 🔹 Notify UI about deletion (just like MemberCheckIn)
                    DashboardEventService.Instance.NotifyCheckInOutDeleted();

                    // 🔹 Log the deletion action for auditing
                    await LogActionAsync(conn, "Delete Walk-In Check-In",
                        $"Deleted walk-in check-in record (RecordID: {checkInID})", true);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeleteWalkInCheckInAsync] {ex.Message}");
                _toastManager.CreateToast("Delete Error")
                    .WithContent($"Failed to delete walk-in check-in: {ex.Message}")
                    .WithDelay(5)
                    .ShowError();
            }

            return false;
        }

        #endregion

        #region HELPER METHODS

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                var logCmd = new SqlCommand(@"
                    INSERT INTO SystemLogs 
                        (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime, PerformedByEmployeeID) 
                    VALUES (@username, @role, @actionType, @description, @success, GETDATE(), @performed)", conn);

                logCmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@success", success);
                logCmd.Parameters.AddWithValue("@performed", CurrentUserModel.UserId ?? (object)DBNull.Value);

                await logCmd.ExecuteNonQueryAsync();
                DashboardEventService.Instance.NotifyRecentLogsUpdated();
                DashboardEventService.Instance.NotifyPopulationDataChanged();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
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
                FROM Members WHERE MemberId = @MemberId";

            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@MemberId", memberId);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new ManageMemberModel
                {
                    MemberID = reader.GetInt32("MemberId"),
                    Name = reader["Name"].ToString() ?? string.Empty,
                    ContactNumber = reader["ContactNumber"].ToString() ?? string.Empty,
                    Status = reader["Status"].ToString() ?? string.Empty
                };
            }
            return null;
        }

        public async Task<MemberPerson?> GetMemberCheckInByIdAsync(int memberCheckInId)
        {
            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to view this data.")
                    .ShowError();
                return null;
            }

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

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CheckInId", memberCheckInId);
                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var nameParts = (reader["FullName"]?.ToString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string firstName = nameParts.Length > 0 ? nameParts[0] : "";
                    string lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";

                    byte[]? profilePictureBytes = null;
                    if (!await reader.IsDBNullAsync("ProfilePicture"))
                        profilePictureBytes = (byte[])reader["ProfilePicture"];

                    return new MemberPerson
                    {
                        ID = reader.GetInt32("CheckInId"),
                        FirstName = firstName,
                        LastName = lastName,
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                        MembershipType = "Member",
                        Status = reader["Status"].ToString() ?? "",
                        DateAttendance = reader.GetDateTime("DateAttendance"),
                        CheckInTime = reader.GetDateTime("CheckInTime"),
                        CheckOutTime = await reader.IsDBNullAsync("CheckOutTime") ? null : reader.GetDateTime("CheckOutTime"),
                        AvatarSource = ImageHelper.BytesToBitmap(profilePictureBytes)
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMemberCheckInByIdAsync] {ex.Message}");
            }
            return null;
        }

        public async Task<WalkInPerson?> GetWalkInCheckInByIdAsync(int walkInCheckInId)
        {
            if (!CanView())
            {
                _toastManager.CreateToast("Access Denied")
                    .WithContent("You do not have permission to view this data.")
                    .ShowError();
                return null;
            }

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

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CheckInId", walkInCheckInId);
                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new WalkInPerson
                    {
                        ID = reader.GetInt32("CheckInId"),
                        FirstName = reader["FirstName"].ToString() ?? "",
                        LastName = reader["LastName"].ToString() ?? "",
                        Age = reader.GetInt32("Age"),
                        ContactNumber = reader["ContactNumber"].ToString() ?? "",
                        PackageType = reader["PackageType"].ToString() ?? "",
                        DateAttendance = reader.GetDateTime("DateAttendance"),
                        CheckInTime = reader.GetDateTime("CheckInTime"),
                        CheckOutTime = await reader.IsDBNullAsync("CheckOutTime") ? null : reader.GetDateTime("CheckOutTime")
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetWalkInCheckInByIdAsync] {ex.Message}");
            }
            return null;
        }

        #endregion
    }
}

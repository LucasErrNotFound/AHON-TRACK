using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
using AHON_TRACK.ViewModels;
using Microsoft.Data.SqlClient;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

        // Logs user actions to SystemLogs table
        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool success)
        {
            try
            {
                using var logCmd = new SqlCommand(
                    @"INSERT INTO SystemLogs (Username, Role, ActionType, ActionDescription, IsSuccessful, LogDateTime) 
                      VALUES (@username, @role, @actionType, @description, @success, GETDATE())", conn);

                logCmd.Parameters.AddWithValue("@username", CurrentUserModel.Username ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@role", CurrentUserModel.Role ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@actionType", actionType ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
                logCmd.Parameters.AddWithValue("@success", success);

                await logCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        #region Member Check-in/out Operations
        public async Task<List<MemberPerson>> GetMemberCheckInsAsync(DateTime date)
        {
            var memberCheckIns = new List<MemberPerson>();
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT 
                        mci.RecordID,
                        m.Firstname + ' ' + m.Lastname AS FullName,
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
                    var nameParts = (reader["FullName"]?.ToString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string firstName = nameParts.Length > 0 ? nameParts[0] : "";
                    string lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";

                    byte[]? profilePictureBytes = null;
                    if (!reader.IsDBNull("ProfilePicture"))
                        profilePictureBytes = (byte[])reader["ProfilePicture"];

                    memberCheckIns.Add(new MemberPerson
                    {
                        ID = reader.GetInt32("RecordID"), // Use CheckInId for operations
                        FirstName = firstName,
                        LastName = lastName,
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? "",
                        MembershipType = "Gym Member", // You might want to add this to your Members table
                        Status = reader["Status"]?.ToString() ?? "",
                        DateAttendance = reader.GetDateTime("DateAttendance"),
                        CheckInTime = reader.GetDateTime("CheckIn"),
                        CheckOutTime = reader.IsDBNull("CheckOut") ? null : reader.GetDateTime("CheckOut"),
                        MemberPicture = GetMemberPicturePath(profilePictureBytes)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMemberCheckInsAsync] {ex.Message}");
                _toastManager?.CreateToast("Database Error")
                              .WithContent($"Failed to load member check-ins: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return memberCheckIns;
        }

        public async Task<bool> CheckInMemberAsync(int memberId)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check if member already checked in today
                const string checkQuery = @"
                    SELECT COUNT(*) FROM MemberCheckIns 
                    WHERE MemberID = @MemberID AND CAST(DateAttendance AS DATE) = CAST(GETDATE() AS DATE)
                    AND CheckOut IS NULL";

                await using var checkCommand = new SqlCommand(checkQuery, connection);
                checkCommand.Parameters.AddWithValue("@MemberID", memberId);
                var existingCount = (int)await checkCommand.ExecuteScalarAsync();

                if (existingCount > 0)
                {
                    _toastManager?.CreateToast("Already Checked In")
                                 .WithContent("Member is already checked in today")
                                 .ShowWarning();
                    return false;
                }

                // Insert new check-in record
                const string insertQuery = @"
                    INSERT INTO MemberCheckIns (MemberID, CheckIn, DateAttendance)
                    VALUES (@MemberId, GETDATE(), CAST(GETDATE() AS DATE))";

                await using var command = new SqlCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@MemberId", memberId);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    // Get member info for logging
                    var memberInfo = await GetMemberInfoAsync(connection, memberId);

                    await LogActionAsync(connection, "Member Check-In",
                        $"Member {memberInfo?.Name ?? memberId.ToString()} checked in", true);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckInMemberAsync] {ex.Message}");
                _toastManager?.CreateToast("Check-In Error")
                              .WithContent($"Failed to check in member: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return false;
        }

        public async Task<bool> CheckOutMemberAsync(int memberCheckInId)
        {
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
                    await LogActionAsync(connection, "Member Check-Out",
                        $"Member checked out (Record ID: {memberCheckInId})", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckOutMemberAsync] {ex.Message}");
                _toastManager?.CreateToast("Check-Out Error")
                              .WithContent($"Failed to check out member: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return false;
        }
        #endregion

        #region Walk-in Check-in/out Operations

        public async Task<bool> CheckInWalkInAsync(int customerID)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check if already checked in today
                using var checkCmd = new SqlCommand(@"
            SELECT COUNT(*) FROM WalkInRecords 
            WHERE CustomerID = @customerID 
            AND CAST(CheckIn AS DATE) = CAST(GETDATE() AS DATE)", conn);

                checkCmd.Parameters.AddWithValue("@customerID", customerID);
                var alreadyCheckedIn = (int)await checkCmd.ExecuteScalarAsync() > 0;

                if (alreadyCheckedIn)
                {
                    _toastManager.CreateToast("Already Checked In")
                        .WithContent("This walk-in customer is already checked in today")
                        .ShowWarning();
                    return false;
                }

                // Insert check-in record
                using var cmd = new SqlCommand(@"
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

        public async Task<bool> CheckOutWalkInAsync(int checkInID)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
            UPDATE WalkInRecords 
            SET CheckOut = GETDATE() 
            WHERE RecordID = @recordID", conn);

                cmd.Parameters.AddWithValue("@recordID", checkInID);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking out walk-in: {ex.Message}");
                return false;
            }
        }

        public async Task<List<WalkInPerson>> GetWalkInCheckInsAsync(DateTime date)
        {
            var walkIns = new List<WalkInPerson>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
    SELECT 
        r.RecordID,
        c.CustomerID,
        c.FirstName,
        c.LastName,
        c.Age,
        c.ContactNumber,
        c.WalkinPackage AS Package,
        r.CheckIn,
        r.CheckOut,
        r.RecordDate
    FROM WalkInRecords r
    INNER JOIN WalkInCustomers c ON r.CustomerID = c.CustomerID
    WHERE r.RecordDate = CAST(@date AS DATE)
    ORDER BY r.RecordID ASC", conn);


                cmd.Parameters.AddWithValue("@date", date);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var checkIn = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7);
                    var recordDate = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9);

                    walkIns.Add(new WalkInPerson
                    {
                        ID = reader.GetInt32(0),
                        FirstName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        LastName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Age = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        ContactNumber = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        PackageType = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        DateAttendance = recordDate,
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

        #endregion

        #region Delete Operations

        public async Task<bool> DeleteMemberCheckInAsync(int memberCheckInId)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string deleteQuery = "DELETE FROM MemberCheckIns WHERE RecordID = RecordId";
                await using var command = new SqlCommand(deleteQuery, connection);
                command.Parameters.AddWithValue("@RecordId", memberCheckInId);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(connection, "Delete Member Check-In",
                        $"Deleted member check-in record (RecordID: {memberCheckInId})", true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteMemberCheckInAsync] {ex.Message}");
                _toastManager?.CreateToast("Delete Error")
                              .WithContent($"Failed to delete member check-in: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return false;
        }

        public async Task<bool> DeleteWalkInCheckInAsync(int checkInID)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
            DELETE FROM WalkInRecords 
            WHERE RecordID = @recordID", conn);

                cmd.Parameters.AddWithValue("@recordID", checkInID);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting walk-in check-in: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Available Members

        public async Task<List<ManageMemberModel>> GetAvailableMembersForCheckInAsync()
        {
            var members = new List<ManageMemberModel>();
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"SELECT m.MemberID, (m.Firstname + ' ' + m.Lastname) AS Name,
                        m.ContactNumber,
                        m.Status,
                        m.ValidUntil,
                        m.ProfilePicture
                        FROM Members m
                        WHERE m.Status = 'Active'
                        ORDER BY MemberID;";

                await using var command = new SqlCommand(query, connection);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    byte[]? bytes = null;
                    if (!reader.IsDBNull("ProfilePicture"))
                        bytes = (byte[])reader["ProfilePicture"];

                    string validityDisplay = string.Empty;
                    if (!reader.IsDBNull("ValidUntil"))
                    {
                        var validityDate = reader.GetDateTime("ValidUntil");
                        validityDisplay = validityDate.ToString("MMM dd, yyyy");
                    }

                    members.Add(new ManageMemberModel
                    {
                        MemberID = reader.GetInt32("MemberId"),
                        Name = reader["Name"]?.ToString() ?? string.Empty,
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                        Status = reader["Status"]?.ToString() ?? string.Empty,
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
                _toastManager?.CreateToast("Database Error")
                              .WithContent($"Failed to load available members: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return members;
        }

        #endregion

        #region Helper Methods

        public async Task<MemberPerson?> GetMemberCheckInByIdAsync(int memberCheckInId)
        {
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
                    if (!reader.IsDBNull("ProfilePicture"))
                        profilePictureBytes = (byte[])reader["ProfilePicture"];

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
                        MemberPicture = GetMemberPicturePath(profilePictureBytes)
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetWalkInCheckInByIdAsync] {ex.Message}");
            }
            return null;
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
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? string.Empty
                };
            }
            return null;
        }

        private string GetMemberPicturePath(byte[]? profilePictureBytes)
        {
            if (profilePictureBytes != null && profilePictureBytes.Length > 0)
            {
                // You might want to convert bytes to image path or return a default
                return "avares://AHON_TRACK/Assets/MainWindowView/user-admin.png";
            }
            return "avares://AHON_TRACK/Assets/MainWindowView/user.png";
        }
        #endregion
    }
}
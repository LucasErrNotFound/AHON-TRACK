using AHON_TRACK.Converters;
using AHON_TRACK.Models;
using AHON_TRACK.Services.Interface;
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
    public class MemberService : IMemberService
    {
        private readonly string _connectionString;
        private readonly ToastManager _toastManager;

        public MemberService(string connectionString, ToastManager toastManager)
        {
            _connectionString = connectionString;
            _toastManager = toastManager;
        }

        public async Task<List<ManageMemberModel>> GetMemberAsync()
        {
            var members = new List<ManageMemberModel>();
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                const string query = @"
        SELECT 
            MemberId,
            (Firstname + ' ' + ISNULL(MiddleInitial + '. ', '') + Lastname) AS Name,
            ContactNumber,
            Status,
            Validity,
            ProfilePicture
        FROM Members";

                await using var command = new SqlCommand(query, connection);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    byte[]? bytes = null;
                    if (!reader.IsDBNull(reader.GetOrdinal("ProfilePicture")))
                        bytes = (byte[])reader["ProfilePicture"]; // Get bytes from DB (VARBINARY)

                    string validityDisplay = string.Empty;
                    if (!reader.IsDBNull(reader.GetOrdinal("Validity")))
                    {
                        var validityDate = reader.GetDateTime(reader.GetOrdinal("Validity"));
                        validityDisplay = validityDate.ToString("MMM dd, yyyy");
                    }

                    members.Add(new ManageMemberModel
                    {
                        MemberID = reader.GetInt32(reader.GetOrdinal("MemberId")),
                        Name = reader["Name"]?.ToString() ?? string.Empty,
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                        MembershipType = null,
                        Status = reader["Status"].ToString() ?? string.Empty,
                        Validity = validityDisplay,
                        ProfilePicture = ImageHelper.GetAvatarOrDefault(bytes) // Convert bytes to Bitmap for UI
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMemberAsync] {ex.Message}");
                _toastManager?.CreateToast("Database Error")
                              .WithContent($"Failed to load members: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
            }
            return members;
        }

        public async Task<bool> DeleteMemberAsync(string memberId)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // First, get member info for logging
                var memberInfo = await GetMemberByIdAsync(memberId);

                const string deleteQuery = "DELETE FROM Members WHERE MemberID = @MemberID";
                await using var command = new SqlCommand(deleteQuery, connection);
                command.Parameters.AddWithValue("@MemberID", int.Parse(memberId));

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    // Log successful deletion
                    await LogActionAsync(
                        connection,
                        actionType: "Delete Member",
                        description: $"Deleted member: {memberInfo?.Name ?? memberId}",
                        success: true
                    );

                    _toastManager?.CreateToast("Member Deleted")
                                 .WithContent($"Successfully deleted member")
                                 .ShowSuccess();
                    return true;
                }
                else
                {
                    _toastManager?.CreateToast("Delete Failed")
                                 .WithContent("Member not found")
                                 .ShowWarning();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteMemberAsync] {ex.Message}");
                _toastManager?.CreateToast("Database Error")
                              .WithContent($"Failed to delete member: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();

                // Log failed deletion
                try
                {
                    await using var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync();
                    await LogActionAsync(conn, "Delete Member", $"Failed to delete member {memberId}: {ex.Message}", success: false);
                }
                catch { /* swallow secondary errors */ }

                return false;
            }
        }

        public async Task<bool> DeleteMultipleMembersAsync(List<string> memberIds)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var idsParam = string.Join(",", memberIds.Select(id => int.Parse(id)));
                var deleteQuery = $"DELETE FROM Members WHERE MemberID IN ({idsParam})";

                await using var command = new SqlCommand(deleteQuery, connection);
                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(
                        connection,
                        actionType: "Delete Multiple Members",
                        description: $"Deleted {rowsAffected} members",
                        success: true
                    );

                    _toastManager?.CreateToast("Members Deleted")
                                 .WithContent($"Successfully deleted {rowsAffected} members")
                                 .ShowSuccess();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteMultipleMembersAsync] {ex.Message}");
                _toastManager?.CreateToast("Database Error")
                              .WithContent($"Failed to delete members: {ex.Message}")
                              .WithDelay(5)
                              .ShowError();
                return false;
            }
        }

        private async Task<ManageMemberModel?> GetMemberByIdAsync(string memberId)
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                SELECT 
                    MemberId,
                    (Firstname + ' ' + ISNULL(MiddleInitial + '. ', '') + Lastname) AS Name,
                    ContactNumber,
                    MembershipType,
                    Status,
                    MembershipDuration AS Validity,
                    ProfilePicture
                FROM Members WHERE MemberId = @MemberId";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@MemberId", int.Parse(memberId));
                await using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    object profilePictureObj = ImageHelper.GetDefaultAvatar();
                    if (!reader.IsDBNull(reader.GetOrdinal("ProfilePicture")))
                    {
                        profilePictureObj = (byte[])reader["ProfilePicture"];
                    }

                    return new ManageMemberModel
                    {
                        MemberID = reader.GetInt32(reader.GetOrdinal("MemberId")),
                        Name = reader["Name"]?.ToString() ?? string.Empty,
                        ContactNumber = reader["ContactNumber"]?.ToString() ?? string.Empty,
                        MembershipType = reader["MembershipType"]?.ToString() ?? string.Empty,
                        Status = reader["Status"]?.ToString() ?? string.Empty,
                        Validity = reader["Validity"]?.ToString() ?? string.Empty,
                        ProfilePicture = profilePictureObj
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMemberByIdAsync] {ex.Message}");
            }
            return null;
        }


        public async Task AddMemberAsync(ManageMemberModel member)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string sql = @"
INSERT INTO Members(Firstname, MiddleInitial, Lastname, Gender, ProfilePicture, ContactNumber, Age, DateOfBirth, Validity, Status, PaymentMethod) VALUES(@Firstname, @MiddleInitial, @Lastname, @Gender, @ProfilePicture, @ContactNumber, @Age, @DateOfBirth, @Validity, @Status, @PaymentMethod)";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Firstname", member.FirstName);
                cmd.Parameters.AddWithValue("@MiddleInitial", member.MiddleInitial);
                cmd.Parameters.AddWithValue("@Lastname", member.LastName);
                cmd.Parameters.AddWithValue("@Gender", member.Gender);

                byte[]? dbBytes = null;
                switch (member.ProfilePicture)
                {
                    case byte[] bytes:
                        dbBytes = bytes;
                        break;
                    case Avalonia.Media.Imaging.Bitmap bmp:
                        dbBytes = ImageHelper.BitmapToBytes(bmp);
                        break;
                    case string s when s.StartsWith("avares://", StringComparison.OrdinalIgnoreCase):
                        dbBytes = ImageHelper.BitmapToBytes(ImageHelper.GetDefaultAvatar());
                        break;
                    default:
                        dbBytes = null;
                        break;
                }

                cmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary).Value = (object?)dbBytes ?? DBNull.Value;
                cmd.Parameters.AddWithValue("@ContactNumber", (object?)member.ContactNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Age", (object?)member.Age ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DateOfBirth", (object?)member.DateOfBirth ?? DBNull.Value);

                // FIX: Convert validity months to actual date
                DateTime? validityDate = null;
                if (member.Validity != null)
                {
                    // Assuming member.Validity contains the number of months as string
                    if (int.TryParse(member.Validity, out int months) && months > 0)
                    {
                        validityDate = DateTime.Now.AddMonths(months);
                    }
                }
                cmd.Parameters.AddWithValue("@Validity", (object?)validityDate ?? DBNull.Value);
                Console.WriteLine($"Adding member with Status: '{member.Status}'");
                cmd.Parameters.AddWithValue("@Status", member.Status);
                cmd.Parameters.AddWithValue("@PaymentMethod", (object?)member.PaymentMethod ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();


                string desc = $"Added new member: {member.FirstName} {member.MiddleInitial}. {member.LastName}";
                await LogActionAsync(conn, actionType: "Add Member", description: desc, success: true);

                _toastManager?.CreateToast("Member Added")
                             .WithContent($"Successfully added {member.FirstName}")
                             .ShowSuccess();
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"[AddMemberAsync] SQL Error: {ex.Message}");
                _toastManager?.CreateToast("Database Error")
                              .WithContent(ex.Message)
                              .WithDelay(5)
                              .ShowError();

                try
                {
                    await using var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync();
                    await LogActionAsync(conn, "Add Member", $"Failed to add {member.FirstName}: {ex.Message}", success: false);
                }
                catch { /* swallow secondary errors */ }

                throw;
            }
        }

        private async Task LogActionAsync(SqlConnection conn, string actionType, string description, bool? success = null)
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
                logCmd.Parameters.AddWithValue("@success", success.HasValue ? success.Value : (object)DBNull.Value);

                await logCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Log Error")
                    .WithContent($"Failed to save log: {ex.Message}")
                    .WithDelay(10)
                    .ShowError();
            }
        }
    }
}

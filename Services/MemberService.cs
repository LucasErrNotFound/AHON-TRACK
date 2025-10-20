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

        #region Role-Based Access Control

        private bool CanCreate()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanUpdate()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanDelete()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool CanView()
        {
            return CurrentUserModel.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
                   CurrentUserModel.Role?.Equals("Coach", StringComparison.OrdinalIgnoreCase) == true;
        }

        #endregion

        #region CREATE

        public async Task<(bool Success, string Message, int? MemberId)> AddMemberAsync(ManageMemberModel member)
        {
            if (!CanCreate())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to add members.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to add members.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check if member already exists (including soft deleted)
                string checkQuery = @"
                    SELECT MemberID, IsDeleted 
                    FROM Members 
                    WHERE Firstname = @Firstname 
                    AND Lastname = @Lastname 
                    AND DateOfBirth = @DateOfBirth";

                using var checkCmd = new SqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@Firstname", member.FirstName ?? (object)DBNull.Value);
                checkCmd.Parameters.AddWithValue("@Lastname", member.LastName ?? (object)DBNull.Value);
                checkCmd.Parameters.AddWithValue("@DateOfBirth", member.DateOfBirth ?? (object)DBNull.Value);

                int? existingMemberId = null;
                bool isDeleted = false;

                using var reader = await checkCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    existingMemberId = reader.GetInt32(0);
                    isDeleted = !reader.IsDBNull(1) && reader.GetBoolean(1);
                }
                reader.Close();

                // If member exists and is soft deleted, restore them
                if (existingMemberId.HasValue && isDeleted)
                {
                    string restoreQuery = @"
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

                    using var restoreCmd = new SqlCommand(restoreQuery, conn);
                    restoreCmd.Parameters.AddWithValue("@MemberID", existingMemberId.Value);
                    restoreCmd.Parameters.AddWithValue("@MiddleInitial", string.IsNullOrWhiteSpace(member.MiddleInitial) ? (object)DBNull.Value : member.MiddleInitial.Substring(0, 1).ToUpper());
                    restoreCmd.Parameters.AddWithValue("@Gender", member.Gender ?? (object)DBNull.Value);
                    restoreCmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary, -1).Value = member.ProfilePicture ?? (object)DBNull.Value;
                    restoreCmd.Parameters.AddWithValue("@ContactNumber", member.ContactNumber ?? (object)DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@Age", member.Age > 0 ? member.Age : (object)DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@ValidUntil", member.ValidUntil ?? (object)DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@PackageID", member.PackageID ?? (object)DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@Status", member.Status ?? "Active");
                    restoreCmd.Parameters.AddWithValue("@PaymentMethod", member.PaymentMethod ?? (object)DBNull.Value);
                    restoreCmd.Parameters.AddWithValue("@RegisteredByEmployeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                    await restoreCmd.ExecuteNonQueryAsync();

                    await LogActionAsync(conn, "RESTORE", $"Restored member: {member.FirstName} {member.LastName}", true);

                    _toastManager?.CreateToast("Member Restored")
                        .WithContent($"Successfully restored {member.FirstName} {member.LastName}.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Member restored successfully.", existingMemberId.Value);
                }

                // If member exists but is not deleted, return error
                if (existingMemberId.HasValue && !isDeleted)
                {
                    _toastManager?.CreateToast("Member Already Exists")
                        .WithContent($"{member.FirstName} {member.LastName} already exists in the system.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Member already exists.", null);
                }

                // Insert new member with IsDeleted = 0
                string memberQuery = @"
                    INSERT INTO Members 
                    (Firstname, MiddleInitial, Lastname, Gender, ProfilePicture, ContactNumber, Age, DateOfBirth, 
                     ValidUntil, PackageID, Status, PaymentMethod, RegisteredByEmployeeID, IsDeleted)
                    OUTPUT INSERTED.MemberID
                    VALUES 
                    (@Firstname, @MiddleInitial, @Lastname, @Gender, @ProfilePicture, @ContactNumber, @Age, @DateOfBirth, 
                     @ValidUntil, @PackageID, @Status, @PaymentMethod, @RegisteredByEmployeeID, 0)";

                using var cmd = new SqlCommand(memberQuery, conn);
                cmd.Parameters.AddWithValue("@Firstname", member.FirstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@MiddleInitial", string.IsNullOrWhiteSpace(member.MiddleInitial) ? (object)DBNull.Value : member.MiddleInitial.Substring(0, 1).ToUpper());
                cmd.Parameters.AddWithValue("@Lastname", member.LastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Gender", member.Gender ?? (object)DBNull.Value);
                cmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary, -1).Value = member.ProfilePicture ?? (object)DBNull.Value;
                cmd.Parameters.AddWithValue("@ContactNumber", member.ContactNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Age", member.Age > 0 ? member.Age : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateOfBirth", member.DateOfBirth ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ValidUntil", member.ValidUntil ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@PackageID", member.PackageID ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", member.Status ?? "Active");
                cmd.Parameters.AddWithValue("@PaymentMethod", member.PaymentMethod ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@RegisteredByEmployeeID", CurrentUserModel.UserId ?? (object)DBNull.Value);

                int memberId = (int)await cmd.ExecuteScalarAsync();

                await LogActionAsync(conn, "CREATE", $"Added new member: {member.FirstName} {member.LastName}", true);

                _toastManager?.CreateToast("Member Added")
                    .WithContent($"Successfully added {member.FirstName} {member.LastName}.")
                    .DismissOnClick()
                    .ShowSuccess();

                return (true, "Member added successfully.", memberId);
            }
            catch (SqlException ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to add member: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"AddMemberAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"AddMemberAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region READ

        public async Task<(bool Success, string Message, List<ManageMemberModel>? Members)> GetMembersAsync()
        {
            if (!CanView())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to view members.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to view members.", null);
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        m.MemberID,
                        m.Firstname,
                        m.MiddleInitial,
                        m.Lastname,
                        LTRIM(RTRIM(m.Firstname + ISNULL(' ' + m.MiddleInitial + '.', '') + ' ' + m.Lastname)) AS Name,
                        m.Gender,
                        m.ContactNumber,
                        m.Age,
                        m.DateOfBirth,
                        m.ValidUntil,
                        m.PackageID,
                        p.PackageName,
                        m.Status,
                        m.PaymentMethod,
                        m.ProfilePicture
                    FROM Members m
                    LEFT JOIN Packages p ON m.PackageID = p.PackageID
                    WHERE (m.IsDeleted = 0 OR m.IsDeleted IS NULL)
                    ORDER BY Name";

                var members = new List<ManageMemberModel>();

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    members.Add(new ManageMemberModel
                    {
                        MemberID = reader.GetInt32(0),
                        FirstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        MiddleInitial = reader.IsDBNull(2) ? null : reader.GetString(2),
                        LastName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Name = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        Gender = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        ContactNumber = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        Age = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                        DateOfBirth = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        ValidUntil = reader.IsDBNull(9) ? null : reader.GetDateTime(9).ToString("MMM dd, yyyy"),
                        PackageID = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                        MembershipType = reader.IsDBNull(11) ? "None" : reader.GetString(11),
                        Status = reader.IsDBNull(12) ? "Active" : reader.GetString(12),
                        PaymentMethod = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                        AvatarBytes = reader.IsDBNull(14) ? null : (byte[])reader[14],
                        AvatarSource = reader.IsDBNull(14)
                            ? ImageHelper.GetDefaultAvatar()
                            : ImageHelper.BytesToBitmap((byte[])reader[14])
                    });
                }

                return (true, "Members retrieved successfully.", members);
            }
            catch (SqlException ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to load members: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"GetMembersAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}", null);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"GetMembersAsync Error: {ex}");
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

                var query = @"
                    SELECT 
                        m.MemberID,
                        m.Firstname,
                        m.MiddleInitial,
                        m.Lastname,
                        m.Gender,
                        m.ProfilePicture,
                        m.ContactNumber,
                        m.Age,
                        m.DateOfBirth,
                        m.ValidUntil,
                        m.PackageID,
                        p.PackageName,
                        m.Status,
                        m.PaymentMethod,
                        m.RegisteredByEmployeeID,
                        m.DateJoined
                    FROM Members m
                    LEFT JOIN Packages p ON m.PackageID = p.PackageID
                    WHERE m.MemberID = @Id 
                    AND (m.IsDeleted = 0 OR m.IsDeleted IS NULL)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Id", memberId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var member = new ManageMemberModel
                    {
                        MemberID = reader.GetInt32(0),
                        FirstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        MiddleInitial = reader.IsDBNull(2) ? null : reader.GetString(2),
                        LastName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Gender = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        AvatarBytes = reader.IsDBNull(5) ? null : (byte[])reader[5],
                        AvatarSource = reader.IsDBNull(5) ? ImageHelper.GetDefaultAvatar() : ImageHelper.BytesToBitmap((byte[])reader[5]),
                        ContactNumber = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        Age = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                        DateOfBirth = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        ValidUntil = reader.IsDBNull(9) ? null : reader.GetDateTime(9).ToString("MMM dd, yyyy"),
                        PackageID = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                        MembershipType = reader.IsDBNull(11) ? "None" : reader.GetString(11),
                        Status = reader.IsDBNull(12) ? "Active" : reader.GetString(12),
                        PaymentMethod = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                        RegisteredByEmployeeID = reader.IsDBNull(14) ? 0 : reader.GetInt32(14),
                        DateJoined = reader.IsDBNull(15) ? DateTime.MinValue : reader.GetDateTime(15)
                    };

                    member.Name = $"{member.FirstName} {(string.IsNullOrWhiteSpace(member.MiddleInitial) ? "" : member.MiddleInitial + ". ")}{member.LastName}";

                    return (true, "Member retrieved successfully.", member);
                }

                return (false, "Member not found.", null);
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Error retrieving member: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"GetMemberByIdAsync Error: {ex}");
                return (false, $"Error: {ex.Message}", null);
            }
        }

        #endregion

        #region UPDATE

        public async Task<(bool Success, string Message)> UpdateMemberAsync(ManageMemberModel member)
        {
            if (!CanUpdate())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("You don't have permission to update member data.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to update members.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Members WHERE MemberID = @memberId AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);
                checkCmd.Parameters.AddWithValue("@memberId", member.MemberID);

                var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;
                if (!exists)
                {
                    _toastManager?.CreateToast("Member Not Found")
                        .WithContent("The member you're trying to update doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Member not found.");
                }

                byte[]? existingImage = null;
                using var getImageCmd = new SqlCommand(
                    "SELECT ProfilePicture FROM Members WHERE MemberID = @memberId", conn);
                getImageCmd.Parameters.AddWithValue("@memberId", member.MemberID);

                var imageResult = await getImageCmd.ExecuteScalarAsync();
                if (imageResult != null && imageResult != DBNull.Value)
                {
                    existingImage = (byte[])imageResult;
                }

                byte[]? imageToSave = existingImage;

                if (member.ProfilePicture != null && member.ProfilePicture.Length > 0)
                {
                    imageToSave = member.ProfilePicture;
                }
                else if (member.AvatarBytes != null && member.AvatarBytes.Length > 0)
                {
                    imageToSave = member.AvatarBytes;
                }

                string query = @"UPDATE Members 
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
                cmd.Parameters.AddWithValue("@MemberID", member.MemberID);
                cmd.Parameters.AddWithValue("@Firstname", member.FirstName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@MiddleInitial", string.IsNullOrWhiteSpace(member.MiddleInitial) ? (object)DBNull.Value : member.MiddleInitial.Substring(0, 1).ToUpper());
                cmd.Parameters.AddWithValue("@Lastname", member.LastName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Gender", member.Gender ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ContactNumber", member.ContactNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Age", member.Age > 0 ? member.Age : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateOfBirth", member.DateOfBirth ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ValidUntil", member.ValidUntil ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@PackageID", member.PackageID ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", member.Status ?? "Active");
                cmd.Parameters.AddWithValue("@PaymentMethod", member.PaymentMethod ?? (object)DBNull.Value);

                if (imageToSave != null && imageToSave.Length > 0)
                {
                    cmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary, -1).Value = imageToSave;
                }
                else
                {
                    cmd.Parameters.Add("@ProfilePicture", SqlDbType.VarBinary, -1).Value = DBNull.Value;
                }

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                {
                    await LogActionAsync(conn, "UPDATE", $"Updated member: {member.FirstName} {member.LastName}", true);

                    _toastManager?.CreateToast("Member Updated")
                        .WithContent($"Successfully updated {member.FirstName} {member.LastName}.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Member updated successfully.");
                }

                return (false, "Failed to update member.");
            }
            catch (SqlException ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to update member: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"UpdateMemberAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"UpdateMemberAsync Error: {ex}");
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region DELETE (SOFT DELETE)

        public async Task<(bool Success, string Message)> DeleteMemberAsync(int memberId)
        {
            if (!CanDelete())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete members.")
                    .DismissOnClick()
                    .ShowError();
                return (false, "Insufficient permissions to delete members.");
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string memberName = string.Empty;
                using var getNameCmd = new SqlCommand(
                    "SELECT Firstname, Lastname FROM Members WHERE MemberID = @memberId AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);
                getNameCmd.Parameters.AddWithValue("@memberId", memberId);

                using var nameReader = await getNameCmd.ExecuteReaderAsync();
                if (await nameReader.ReadAsync())
                {
                    memberName = $"{nameReader[0]} {nameReader[1]}";
                }
                nameReader.Close();

                if (string.IsNullOrEmpty(memberName))
                {
                    _toastManager?.CreateToast("Member Not Found")
                        .WithContent("The member you're trying to delete doesn't exist.")
                        .DismissOnClick()
                        .ShowWarning();
                    return (false, "Member not found.");
                }

                using var deleteMemberCmd = new SqlCommand(
                    "UPDATE Members SET IsDeleted = 1 WHERE MemberID = @memberId", conn);
                deleteMemberCmd.Parameters.AddWithValue("@memberId", memberId);
                int rowsAffected = await deleteMemberCmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    await LogActionAsync(conn, "DELETE", $"Soft deleted member: {memberName} (ID: {memberId})", true);

                    _toastManager?.CreateToast("Member Deleted")
                        .WithContent($"Successfully deleted {memberName}.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Member deleted successfully.");
                }

                return (false, "Failed to delete member.");
            }
            catch (SqlException ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to delete member: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"DeleteMemberAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"DeleteMemberAsync Error: {ex}");
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteMultipleMembersAsync(List<int> memberIds)
        {
            if (!CanDelete())
            {
                _toastManager?.CreateToast("Access Denied")
                    .WithContent("Only administrators can delete members.")
                    .DismissOnClick()
                    .ShowError();
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

                    _toastManager?.CreateToast("Members Deleted")
                        .WithContent($"Successfully deleted {rowsAffected} members.")
                        .DismissOnClick()
                        .ShowSuccess();

                    return (true, "Members deleted successfully.");
                }

                return (false, "No members were deleted.");
            }
            catch (SqlException ex)
            {
                _toastManager?.CreateToast("Database Error")
                    .WithContent($"Failed to delete members: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"DeleteMultipleMembersAsync Error: {ex}");
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _toastManager?.CreateToast("Error")
                    .WithContent($"An unexpected error occurred: {ex.Message}")
                    .DismissOnClick()
                    .ShowError();

                System.Diagnostics.Debug.WriteLine($"DeleteMultipleMembersAsync Error: {ex}");
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region UTILITY METHODS

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
                DashboardEventService.Instance.NotifyPopulationDataChanged();
                DashboardEventService.Instance.NotifyMemberDeleted();
                DashboardEventService.Instance.NotifyMemberUpdated();
                DashboardEventService.Instance.NotifyMemberAdded();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogActionAsync] {ex.Message}");
            }
        }

        public async Task<int> GetTotalMemberCountAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = "SELECT COUNT(*) FROM Members WHERE (IsDeleted = 0 OR IsDeleted IS NULL)";
                using var cmd = new SqlCommand(query, conn);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTotalMemberCountAsync Error: {ex}");
                return 0;
            }
        }

        public async Task<int> GetMemberCountByStatusAsync(string status)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = "SELECT COUNT(*) FROM Members WHERE Status = @Status AND (IsDeleted = 0 OR IsDeleted IS NULL)";
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Status", status);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetMemberCountByStatusAsync Error: {ex}");
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

                using var cmd = new SqlCommand(
                    @"SELECT 
                        SUM(CASE WHEN Status = 'Active' THEN 1 ELSE 0 END) as ActiveCount,
                        SUM(CASE WHEN Status = 'Inactive' THEN 1 ELSE 0 END) as InactiveCount,
                        SUM(CASE WHEN Status = 'Terminated' THEN 1 ELSE 0 END) as TerminatedCount
                      FROM Members
                      WHERE (IsDeleted = 0 OR IsDeleted IS NULL)", conn);

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
                Console.WriteLine($"[GetMemberStatisticsAsync] {ex.Message}");
                return (false, 0, 0, 0);
            }
        }

        #endregion
    }
}
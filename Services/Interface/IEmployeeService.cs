using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface IEmployeeService
    {
        // CREATE
        Task<(bool Success, string Message, int? EmployeeId)> AddEmployeeAsync(ManageEmployeeModel employee);

        // READ
        Task<(bool Success, string Message, List<ManageEmployeeModel>? Employees)> GetEmployeesAsync();
        Task<(bool Success, string Message, ManageEmployeeModel? Employee)> GetEmployeeByIdAsync(int employeeId);
        Task<(bool Success, string Message, ManageEmployeeModel? Employee)> ViewEmployeeProfileAsync(int employeeId);
        Task<byte[]?> GetEmployeeProfilePictureAsync(int employeeId);

        // UPDATE
        Task<(bool Success, string Message)> UpdateEmployeeAsync(ManageEmployeeModel employee);

        // DELETE
        Task<(bool Success, string Message)> DeleteEmployeeAsync(int employeeId);

        // UTILITY
        Task<int> GetTotalEmployeeCountAsync();
        Task<int> GetEmployeeCountByStatusAsync(string status);
        Task<(bool Success, int ActiveCount, int InactiveCount, int TerminatedCount)> GetEmployeeStatisticsAsync();

        // Hashed Password
        Task<(bool Success, string Message, int? EmployeeId, string? Role)> AuthenticateUserAsync(string username, string password);

        // FORGOT
        Task<(bool Exists, string? Role, string? EmployeeName)> VerifyUsernameExistsAsync(string username);
        Task<(bool Success, string Message)> ChangePasswordAsync(string username, string newPassword);

        #region lockout
        Task<(bool IsLocked, int AttemptsLeft, string Message)> CheckLockoutStatusAsync(string username);
        Task RecordFailedLoginAttemptAsync(string username);
        Task ResetLoginAttemptsAsync(string username);
        Task UnlockAllAccountsAsync();
        #endregion
    }
}

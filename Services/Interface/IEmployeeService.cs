using AHON_TRACK.Models;
using AHON_TRACK.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface IEmployeeService
    {
        Task<bool> AddEmployeeAsync(EmployeeModel employee);
        Task<List<ManageEmployeeModel>> GetEmployeesAsync();
        Task<bool> DeleteEmployeeAsync(int employeeId);
        Task<bool> UpdateEmployeeAsync(EmployeeModel employee);
        Task<ManageEmployeeModel?> GetEmployeeByIdAsync(int employeeId);
    }
}

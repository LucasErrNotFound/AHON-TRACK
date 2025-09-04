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
        // Add a new employee
        Task<bool> AddEmployeeAsync(EmployeeModel employee);

        // Get a single employee by ID
        Task<ManageEmployeeModel?> GetEmployeeByIdAsync(int employeeId);

        // Get all employees
        Task<List<ManageEmployeeModel>> GetEmployeesAsync();

        // Search employees
        Task<List<ManageEmployeeModel>> SearchEmployeesAsync(string searchTerm);

        // Get employees by status (Active, Inactive, Terminated)
        Task<List<ManageEmployeeModel>> GetEmployeesByStatusAsync(string status);

        // Get employees with sorting options
        Task<List<ManageEmployeeModel>> GetEmployeesSortedAsync(string sortBy, bool descending = false);

        // Get employee count by status
        Task<int> GetEmployeeCountByStatusAsync(string status);

        // Get total employee count
        Task<int> GetTotalEmployeeCountAsync();

        // Update employee info
        Task<bool> UpdateEmployeeAsync(EmployeeModel employee);

        // Delete employee
        Task<bool> DeleteEmployeeAsync(int id);

        // View full employee profile
        Task<ManageEmployeeModel?> ViewEmployeeProfileAsync(int employeeId);
    }
}

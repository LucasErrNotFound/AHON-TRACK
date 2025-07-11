using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.ViewModels;

[Page("viewEmployeeProfile")]
public sealed partial class EmployeeProfileInformationViewModel : ViewModelBase, INavigable
{
    private readonly PageManager _pageManager;

    [ObservableProperty]
    private string _employeeFullNameHeader = string.Empty;

    [ObservableProperty]
    private string _employeeID = string.Empty;

    [ObservableProperty]
    private string _employeePosition = string.Empty;

    [ObservableProperty]
    private string _employeeDateJoined = string.Empty;

    [ObservableProperty]
    private string _employeeFullName = string.Empty;

    [ObservableProperty]
    private string _employeeAge = string.Empty;

    [ObservableProperty]
    private string _employeeBirthDate = string.Empty;

    [ObservableProperty]
    private string _employeeGender = string.Empty;

    [ObservableProperty]
    private string _employeePhoneNumber = string.Empty;

    [ObservableProperty]
    private string _employeeLastLogin = string.Empty;

    [ObservableProperty]
    private string _employeeHouseAddress = string.Empty; 

    [ObservableProperty]
    private string _employeeHouseNumber = string.Empty; 

    [ObservableProperty]
    private string _employeeStreet = string.Empty;

    [ObservableProperty]
    private string _employeeBarangay = string.Empty;

    [ObservableProperty]
    private string _employeeCityProvince = string.Empty;

    [ObservableProperty]
    private string _employeeZipCode = string.Empty;

    public static ManageEmployeesItem? SelectedEmployeeData { get; set; }

    public EmployeeProfileInformationViewModel(PageManager pageManager)
    {
        _pageManager = pageManager;
    }

    public EmployeeProfileInformationViewModel()
    {
        _pageManager = new PageManager(new ServiceProvider());
    }

    public void Initialize()
    {
        if (SelectedEmployeeData != null)
        {
            // Pass the employee data to the profile properties
            EmployeeID = SelectedEmployeeData.ID;
            EmployeePosition = SelectedEmployeeData.Position;
            EmployeeDateJoined = SelectedEmployeeData.DateJoined.ToString("MMMM d, yyyy");
            EmployeeFullName = SelectedEmployeeData.Name;
            EmployeeFullNameHeader = $"{SelectedEmployeeData.Name}'s Profile";
            EmployeePhoneNumber = SelectedEmployeeData.ContactNumber;

            // Set default values for properties not available in ManageEmployeesItem
            EmployeeAge = "30"; // Default or calculate from birth date if available
            EmployeeBirthDate = "1993-05-20"; // Default
            EmployeeGender = "Male"; // Default
            EmployeeLastLogin = DateTime.Now.ToString("MMMM dd, yyyy h:mmtt");
            EmployeeHouseAddress = "123 Main"; // Default
            EmployeeHouseNumber = "123"; // Default
            EmployeeStreet = "Main Street"; // Default
            EmployeeBarangay = "Maungib"; // Default
            EmployeeCityProvince = "Cebu City, Cebu"; // Default
            EmployeeZipCode = "6000"; // Default
        }
        else
        {
            Debug.WriteLine("SelectedEmployeeData is null, setting default values.");
            SetDefaultValues();
        }
    }

    private void SetDefaultValues()
    {
        EmployeeID = "E12345";
        EmployeePosition = "Software Engineer";
        EmployeeDateJoined = "2023-01-15";
        EmployeeFullName = "John Doe";
        EmployeeFullNameHeader = "John Doe's Profile";
        EmployeeAge = "30";
        EmployeeBirthDate = "1993-05-20";
        EmployeeGender = "Male";
        EmployeePhoneNumber = "09837756473";
        EmployeeLastLogin = "July 10, 2025 4:30PM";
        EmployeeHouseAddress = "123 Main";
        EmployeeHouseNumber = "123";
        EmployeeStreet = "Main Street";
        EmployeeBarangay = "Maungib";
        EmployeeCityProvince = "Cebu City, Cebu";
        EmployeeZipCode = "6000";
    }

    [RelayCommand]
    private void BackPage() => _pageManager.Navigate<DashboardViewModel>();
}

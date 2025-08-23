using System;

public class EmployeeModel
{
    public int EmployeeId { get; set; }
    public string FirstName { get; set; } = "";
    public string? MiddleInitial { get; set; }
    public string LastName { get; set; } = "";
    public string Gender { get; set; } = "";
    public object? ProfilePicture { get; set; }
    public string? ContactNumber { get; set; }
    public int? Age { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? HouseAddress { get; set; }
    public string? HouseNumber { get; set; }
    public string? Street { get; set; }
    public string? Barangay { get; set; }
    public string? CityTown { get; set; }
    public string? Province { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = ""; // ⚠️ Store hashed
    public DateTime DateJoined { get; set; }
    public string? Status { get; set; }
    public string Position { get; set; } = "";
}
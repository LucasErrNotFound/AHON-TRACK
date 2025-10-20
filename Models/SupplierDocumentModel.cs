using System;
using System.Collections.Generic;

namespace AHON_TRACK.Models;

public class SupplierDocumentModel
{
    public DateTime GeneratedDate { get; set; }
    public string GymName { get; set; } = "AHON Fitness Gym";
    public string GymAddress { get; set; } = string.Empty;
    public string GymPhone { get; set; } = string.Empty;
    public string GymEmail { get; set; } = string.Empty;
    public List<SupplierItem> Items { get; set; } = [];
    public decimal TotalSuppliers => Items.Count;  
}

public class SupplierItem 
{
    public int ID { get; set; }
    public string? Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; } = string.Empty;
    public string? Products { get; set; } = string.Empty;
    public string? Status { get; set; } = string.Empty;
}
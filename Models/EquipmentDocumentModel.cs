using System;
using System.Collections.Generic;

namespace AHON_TRACK.Models;

public class EquipmentDocumentModel
{
    public DateTime GeneratedDate { get; set; }
    public string GymName { get; set; } = "AHON Fitness Gym";
    public string GymAddress { get; set; } = string.Empty;
    public string GymPhone { get; set; } = string.Empty;
    public string GymEmail { get; set; } = string.Empty;
    public List<EquipmentItem> Items { get; set; } = [];
    public decimal TotalEquipments => Items.Count;  
}

public class EquipmentItem
{
    public int ID { get; set; }
    public string? BrandName { get; set; } = string.Empty;
    public string? Category { get; set; } = string.Empty;
    public string? Supplier { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int PurchasedPrice { get; set; }
    public DateTime? PurchasedDate { get; set; }
    public DateTime? Warranty { get; set; }
    public string? Condition { get; set; } = string.Empty;
    public DateTime? LastMaintenance { get; set; }
    public DateTime? NextMaintenance { get; set; }
}

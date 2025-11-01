using System;
using System.Collections.Generic;

namespace AHON_TRACK.Models;

public class ProductStockDocumentModel
{
    public DateTime GeneratedDate { get; set; }
    public string GymName { get; set; } = "AHON Fitness Gym";
    public string GymAddress { get; set; } = string.Empty;
    public string GymPhone { get; set; } = string.Empty;
    public string GymEmail { get; set; } = string.Empty;
    public List<ProductItem> Items { get; set; } = [];
    public decimal TotalProductInList => Items.Count;
}

public class ProductItem 
{
    public int ID { get; set; }
    public string? ProductName { get; set; } = string.Empty;
    public string? BatchNumber { get; set; } = string.Empty;
    public string? Category { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public decimal Price { get; set; }
    public string? Supplier { get; set; } = string.Empty;
    public DateTime? Expiry { get; set; }
    public string? Status { get; set; } = string.Empty;
}
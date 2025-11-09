using System;
using System.Collections.Generic;
using System.Linq;

namespace AHON_TRACK.Models;

public class InvoiceDocumentModel
{
    public DateTime GeneratedDate { get; set; }
    public string GymName { get; set; } = "AHON Fitness Gym";
    public string GymAddress { get; set; } = string.Empty;
    public string GymPhone { get; set; } = string.Empty;
    public string GymEmail { get; set; } = string.Empty;
    public List<InvoiceItem> Items { get; set; } = [];
    public decimal TotalAmount => Items.Sum(x => x.Amount);
}

public class InvoiceItem
{
    public int ID { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PurchasedItem { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public DateTime DatePurchased { get; set; }
}
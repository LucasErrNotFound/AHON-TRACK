using System;
using System.Collections.Generic;

namespace AHON_TRACK.Models;

public class AuditLogDocumentModel
{
    public DateTime GeneratedDate { get; set; }
    public string GymName { get; set; } = "AHON Fitness Gym";
    public string GymAddress { get; set; } = string.Empty;
    public string GymPhone { get; set; } = string.Empty;
    public string GymEmail { get; set; } = string.Empty;
    public List<AuditLogItem> Items { get; set; } = [];
    public decimal TotalAuditLogs => Items.Count;
}

public class AuditLogItem
{
    public int ID { get; set; }
    public string? Name { get; set; } = string.Empty;
    public string? Position { get; set; } = string.Empty;
    public string? Action { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal LogCount { get; set; }
    public DateTime DateAndTime { get; set; }
}
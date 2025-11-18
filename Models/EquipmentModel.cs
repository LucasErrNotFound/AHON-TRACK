using System;

public class EquipmentModel
{
    public int EquipmentID { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? BatchCode { get; set; }

    // Supplier Reference - stores the ID
    public int? SupplierID { get; set; }

    // Supplier Name - populated from JOIN query (not stored in Equipment table)
    public string SupplierName { get; set; } = "N/A";

    public DateTime? WarrantyExpiry { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastMaintenance { get; set; }
    public DateTime? NextMaintenance { get; set; }

    // UI Helper Properties
    public bool IsSelected { get; set; }
    public bool NeedsMaintenance => NextMaintenance.HasValue && NextMaintenance.Value <= DateTime.Now.AddDays(7);
    public bool IsWarrantyExpiring => WarrantyExpiry.HasValue && WarrantyExpiry.Value <= DateTime.Now.AddDays(30);
    public bool IsWarrantyExpired => WarrantyExpiry.HasValue && WarrantyExpiry.Value < DateTime.Now;

    // Computed Properties
    public string ConditionBadgeColor => Condition switch
    {
        "Excellent" => "success",
        "Repairing" => "warning",
        "Broken" => "error",
        _ => "default"
    };

    public string StatusBadgeColor => Status switch
    {
        "Active" => "success",
        "Inactive" => "secondary",
        "Under Maintenance" => "warning",
        "Retired" => "error",
        "On Loan" => "info",
        _ => "default"
    };

    public int DaysUntilNextMaintenance
    {
        get
        {
            if (!NextMaintenance.HasValue) return int.MaxValue;
            return (NextMaintenance.Value - DateTime.Now).Days;
        }
    }

    public int DaysUntilWarrantyExpiry
    {
        get
        {
            if (!WarrantyExpiry.HasValue) return int.MaxValue;
            return (WarrantyExpiry.Value - DateTime.Now).Days;
        }
    }

    public string MaintenanceStatus
    {
        get
        {
            if (!NextMaintenance.HasValue) return "Not Scheduled";
            var days = DaysUntilNextMaintenance;
            if (days < 0) return "Overdue";
            if (days <= 7) return "Due Soon";
            return "Scheduled";
        }
    }

    public string WarrantyStatus
    {
        get
        {
            if (!WarrantyExpiry.HasValue) return "No Warranty";
            if (IsWarrantyExpired) return "Expired";
            if (IsWarrantyExpiring) return "Expiring Soon";
            return "Active";
        }
    }
}
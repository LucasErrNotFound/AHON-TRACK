using System;
using System.Text.Json.Serialization;

namespace AHON_TRACK.Models;

public class AppSettings
{
    [JsonPropertyName("downloadPath")]
    public string DownloadPath { get; set; } = string.Empty;

    [JsonPropertyName("isDarkMode")]
    public bool IsDarkMode { get; set; } = false;

    [JsonPropertyName("backupFrequency")]
    public string BackupFrequency { get; set; } = "Everyday";

    [JsonPropertyName("recoveryFilePath")]
    public string RecoveryFilePath { get; set; } = string.Empty;
    public DateTime? LastBackupDate { get; set; }

    public int IndexMaintenanceFrequencyDays { get; set; } = 25;
    public DateTime? LastIndexMaintenanceDate { get; set; }
}
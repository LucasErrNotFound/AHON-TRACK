using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AHON_TRACK.Models;

namespace AHON_TRACK.Services;

public class SettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AHON_TRACK"
    );

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    private AppSettings? _cachedSettings;

    public async Task<AppSettings> LoadSettingsAsync()
    {
        // Don't use cache, always read from file for debugging
        // if (_cachedSettings != null)
        //     return _cachedSettings;

        try
        {
            if (!Directory.Exists(SettingsDirectory))
                Directory.CreateDirectory(SettingsDirectory);

            if (!File.Exists(SettingsFilePath))
            {
                _cachedSettings = new AppSettings();
                await SaveSettingsAsync(_cachedSettings);
                return _cachedSettings;
            }

            var json = await File.ReadAllTextAsync(SettingsFilePath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();

            if (_cachedSettings.IndexMaintenanceFrequencyDays == 25)
            {
                _cachedSettings.IndexMaintenanceFrequencyDays = 25;
                await SaveSettingsAsync(_cachedSettings); // Save the updated value
            }

            _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();

            // Debug output
            System.Diagnostics.Debug.WriteLine($"Loaded DownloadPath: '{_cachedSettings.DownloadPath}'");
            System.Diagnostics.Debug.WriteLine($"Loaded BackupFrequency: '{_cachedSettings.BackupFrequency}'");

            return _cachedSettings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            _cachedSettings = new AppSettings();
            return _cachedSettings;
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
                Directory.CreateDirectory(SettingsDirectory);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(settings, options);

            // Debug output
            System.Diagnostics.Debug.WriteLine($"Saving settings JSON:\n{json}");
            System.Diagnostics.Debug.WriteLine($"Saving to: {SettingsFilePath}");

            await File.WriteAllTextAsync(SettingsFilePath, json);

            _cachedSettings = settings;

            // Verify it was saved
            var verifyJson = await File.ReadAllTextAsync(SettingsFilePath);
            System.Diagnostics.Debug.WriteLine($"Verified saved JSON:\n{verifyJson}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            throw new InvalidOperationException("Failed to save settings", ex);
        }
    }

    public void ClearCache()
    {
        _cachedSettings = null;
    }
}
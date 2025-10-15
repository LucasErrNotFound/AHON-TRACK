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
        if (_cachedSettings != null)
            return _cachedSettings;
            
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
            _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            return _cachedSettings;
        }
        catch
        {
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
                WriteIndented = true
            };
            
            var json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(SettingsFilePath, json);
            _cachedSettings = settings;
        }
        catch (Exception ex)
        {
            // Log error or handle as needed
            throw new InvalidOperationException("Failed to save settings", ex);
        }
    }
    
    public void ClearCache()
    {
        _cachedSettings = null;
    }
}
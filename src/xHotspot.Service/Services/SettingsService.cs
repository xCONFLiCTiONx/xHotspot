using System.IO;
using System.Text.Json;
using xHotspot.Core.Interfaces;
using xHotspot.Core.Models;

namespace xHotspot.Service.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private AppSettings _settings;
    private readonly object _lock = new();

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dir = Path.Combine(appData, "xHotspot");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        _settings = LoadSettingsInternal();
    }

    public AppSettings LoadSettings()
    {
        lock (_lock)
        {
            return _settings;
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        lock (_lock)
        {
            _settings = settings;
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
            SettingsChanged?.Invoke(this, _settings);
        }
    }

    private AppSettings LoadSettingsInternal()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // fallback
        }
        return new AppSettings();
    }
}

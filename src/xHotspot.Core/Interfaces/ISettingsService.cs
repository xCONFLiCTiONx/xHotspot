using xHotspot.Core.Models;

namespace xHotspot.Core.Interfaces;

public interface ISettingsService
{
    AppSettings LoadSettings();
    void SaveSettings(AppSettings settings);
    event EventHandler<AppSettings>? SettingsChanged;
}

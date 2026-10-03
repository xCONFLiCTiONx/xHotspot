using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;
using xHotspot.Core.Models;

namespace xHotspot.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private string _wifiSsid = "Connected";

    [ObservableProperty]
    private string _phoneName = "Samsung Galaxy S23 FE";

    [ObservableProperty]
    private string _hotspotStatus = "Off";

    [ObservableProperty]
    private string _automationStatusText = "Always-On Active";

    [ObservableProperty]
    private string _toggleButtonText = "Enable Hotspot";

    public MainViewModel()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += async (s, e) => await RefreshStatusAsync();
        _timer.Start();

        _ = RefreshStatusAsync();
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        var app = App.Current;
        if (app == null) return;

        var currentStatus = await app.HotspotManager.GetStatusAsync();
        HotspotStatus = currentStatus.ToString();

        if (currentStatus == xHotspot.Core.Models.HotspotStatus.On || currentStatus == xHotspot.Core.Models.HotspotStatus.TurningOn)
        {
            ToggleButtonText = "Disable Hotspot";
            AutomationStatusText = app.IsAutoReenableEnabled ? "Always-On Active" : "Manual";
        }
        else
        {
            ToggleButtonText = "Enable Hotspot";
            AutomationStatusText = app.IsAutoReenableEnabled ? "Always-On Active (Reconnecting...)" : "Disabled";
        }
    }

    [RelayCommand]
    private async Task ToggleHotspotAsync()
    {
        var app = App.Current;
        if (app != null)
        {
            await app.ToggleHotspotAsync();
            await RefreshStatusAsync();
        }
    }
}

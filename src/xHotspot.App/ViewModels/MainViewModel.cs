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
    private string _hotspotStatus = "Checking...";

    [ObservableProperty]
    private string _automationStatusText = "Manual Mode";

    [ObservableProperty]
    private string _toggleButtonText = "Loading...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy = true; // Set busy by default so progress bar shows instantly on launch

    public bool IsNotBusy => !IsBusy;

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

        try
        {
            var currentStatus = await app.HotspotManager.GetStatusAsync();
            HotspotStatus = currentStatus.ToString();

            if (currentStatus == xHotspot.Core.Models.HotspotStatus.TurningOn)
            {
                IsBusy = true;
                ToggleButtonText = "Enabling...";
                AutomationStatusText = app.IsAutoReenableEnabled ? "Always-On Active" : "Manual Mode";
            }
            else if (currentStatus == xHotspot.Core.Models.HotspotStatus.On)
            {
                IsBusy = false;
                ToggleButtonText = "Disable Hotspot";
                AutomationStatusText = app.IsAutoReenableEnabled ? "Always-On Active" : "Manual Mode";
            }
            else
            {
                if (!app.IsBusy) IsBusy = false;
                ToggleButtonText = "Enable Hotspot";
                AutomationStatusText = app.IsAutoReenableEnabled ? "Always-On Active (Reconnecting...)" : "Manual Mode";
            }
        }
        catch
        {
            IsBusy = false;
            ToggleButtonText = "Enable Hotspot";
        }
    }

    [RelayCommand]
    private async Task ToggleHotspotAsync()
    {
        var app = App.Current;
        if (app != null)
        {
            IsBusy = true;
            try
            {
                await app.ToggleHotspotAsync();
            }
            finally
            {
                IsBusy = false;
                await RefreshStatusAsync();
            }
        }
    }
}

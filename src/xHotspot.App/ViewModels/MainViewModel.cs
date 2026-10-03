using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using xHotspot.App.Services;
using xHotspot.Core.Hotspot;
using xHotspot.Core.Models;
using xHotspot.Core.Services;

namespace xHotspot.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IpcClient _ipcClient = new();
    private readonly DispatcherTimer _timer;
    private readonly LoggerService _logger = new();
    private readonly WindowsHotspotManager _hotspotManager;

    [ObservableProperty]
    private string _serviceStatus = "Running";

    [ObservableProperty]
    private string _wifiSsid = "Connected";

    [ObservableProperty]
    private string _phoneName = "Samsung Galaxy S23 FE";

    [ObservableProperty]
    private string _hotspotStatus = "Off";

    [ObservableProperty]
    private bool _automationPaused;

    [ObservableProperty]
    private string _automationStatusText = "Always-On Active";

    [ObservableProperty]
    private string _automationButtonText = "Pause Automation";

    [ObservableProperty]
    private bool _internetConnected = true;

    [ObservableProperty]
    private ObservableCollection<BluetoothDeviceItem> _pairedDevices = new();

    public MainViewModel()
    {
        _hotspotManager = new WindowsHotspotManager(_logger);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _timer.Tick += async (s, e) => await RefreshStatusAsync();
        _timer.Start();

        _ = RefreshStatusAsync();
        _ = LoadDevicesAsync();
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        // Query status directly via WindowsHotspotManager in user session for real-time accuracy
        var status = await _hotspotManager.GetStatusAsync();
        HotspotStatus = status.ToString();

        var resp = await _ipcClient.SendCommandAsync(IpcCommandType.GetStatus);
        if (resp.Success && !string.IsNullOrEmpty(resp.DataJson))
        {
            var dto = System.Text.Json.JsonSerializer.Deserialize<StatusDto>(resp.DataJson);
            if (dto != null)
            {
                WifiSsid = string.IsNullOrEmpty(dto.WifiSsid) ? "Connected" : dto.WifiSsid;
                InternetConnected = dto.InternetConnected;
                PhoneName = string.IsNullOrEmpty(dto.PhoneName) ? "Samsung Galaxy S23 FE" : dto.PhoneName;
                AutomationPaused = dto.AutomationPaused;
                AutomationStatusText = AutomationPaused ? "Paused" : "Always-On Active";
                AutomationButtonText = AutomationPaused ? "Resume Automation" : "Pause Automation";
            }
        }
    }

    [RelayCommand]
    private async Task EnableHotspotAsync()
    {
        Console.WriteLine(">>> [GUI] Enable Hotspot clicked. Executing directly in user session...");
        await _hotspotManager.EnableAsync();
        await RefreshStatusAsync();
    }

    [RelayCommand]
    private async Task DisableHotspotAsync()
    {
        Console.WriteLine(">>> [GUI] Disable Hotspot clicked. Executing directly in user session...");
        await _hotspotManager.DisableAsync();
        await RefreshStatusAsync();
    }

    [RelayCommand]
    private async Task ToggleAutomationAsync()
    {
        if (AutomationPaused)
        {
            await _ipcClient.SendCommandAsync(IpcCommandType.ResumeAutomation);
        }
        else
        {
            await _ipcClient.SendCommandAsync(IpcCommandType.PauseAutomation);
        }
        await RefreshStatusAsync();
    }

    private async Task LoadDevicesAsync()
    {
        var resp = await _ipcClient.SendCommandAsync(IpcCommandType.GetDevices);
        if (resp.Success && !string.IsNullOrEmpty(resp.DataJson))
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<BluetoothDeviceItem>>(resp.DataJson);
            if (list != null)
            {
                PairedDevices.Clear();
                foreach (var d in list) PairedDevices.Add(d);
            }
        }
    }
}

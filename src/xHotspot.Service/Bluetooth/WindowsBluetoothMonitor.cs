using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using xHotspot.Core.Interfaces;
using xHotspot.Core.Models;

namespace xHotspot.Service.Bluetooth;

public class WindowsBluetoothMonitor : IBluetoothMonitor
{
    private readonly ILoggerService _logger;
    private DeviceWatcher? _deviceWatcher;

    public event EventHandler<BluetoothDeviceItem>? DeviceStateChanged;

    public WindowsBluetoothMonitor(ILoggerService logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<BluetoothDeviceItem>> GetPairedDevicesAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<BluetoothDeviceItem>();
        string aqs = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
        var devices = await DeviceInformation.FindAllAsync(aqs);

        foreach (var dev in devices)
        {
            list.Add(new BluetoothDeviceItem
            {
                DeviceId = dev.Id,
                Name = dev.Name,
                State = BluetoothDeviceState.Paired,
                IsConnected = dev.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var conn) && conn is bool b && b
            });
        }
        return list;
    }

    public async Task<BluetoothDeviceItem?> GetDeviceStateAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deviceId)) return null;
        var devInfo = await DeviceInformation.CreateFromIdAsync(deviceId);
        if (devInfo != null)
        {
            bool connected = devInfo.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var conn) && conn is bool b && b;
            return new BluetoothDeviceItem
            {
                DeviceId = devInfo.Id,
                Name = devInfo.Name,
                State = connected ? BluetoothDeviceState.Connected : BluetoothDeviceState.Available,
                IsConnected = connected
            };
        }
        return null;
    }

    public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        string aqs = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
        _deviceWatcher = DeviceInformation.CreateWatcher(aqs, new[] { "System.Devices.Aep.IsConnected" }, DeviceInformationKind.AssociationEndpoint);

        _deviceWatcher.Updated += async (watcher, args) =>
        {
            try
            {
                var item = await GetDeviceStateAsync(args.Id);
                if (item != null)
                {
                    DeviceStateChanged?.Invoke(this, item);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BLUETOOTH WATCHER ERROR] {ex}");
                _logger.LogError("Error in device watcher update", ex);
            }
        };

        _deviceWatcher.Start();
        _logger.LogInformation("WindowsBluetoothMonitor started.");
        return Task.CompletedTask;
    }

    public Task StopMonitoringAsync()
    {
        _deviceWatcher?.Stop();
        _deviceWatcher = null;
        _logger.LogInformation("WindowsBluetoothMonitor stopped.");
        return Task.CompletedTask;
    }
}

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
        try
        {
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
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to query paired Bluetooth devices", ex);
        }
        return list;
    }

    public async Task<BluetoothDeviceItem?> GetDeviceStateAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to get state for device {deviceId}", ex);
        }
        return null;
    }

    public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string aqs = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
            _deviceWatcher = DeviceInformation.CreateWatcher(aqs, new[] { "System.Devices.Aep.IsConnected" }, DeviceInformationKind.AssociationEndpoint);

            _deviceWatcher.Updated += async (watcher, args) =>
            {
                var item = await GetDeviceStateAsync(args.Id);
                if (item != null)
                {
                    DeviceStateChanged?.Invoke(this, item);
                }
            };

            _deviceWatcher.Start();
            _logger.LogInformation("WindowsBluetoothMonitor started.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to start WindowsBluetoothMonitor", ex);
        }
        return Task.CompletedTask;
    }

    public Task StopMonitoringAsync()
    {
        try
        {
            _deviceWatcher?.Stop();
            _deviceWatcher = null;
            _logger.LogInformation("WindowsBluetoothMonitor stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to stop WindowsBluetoothMonitor", ex);
        }
        return Task.CompletedTask;
    }
}

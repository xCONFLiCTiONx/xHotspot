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
        var seenIds = new HashSet<string>();

        try
        {
            string aqsClassic = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var devicesClassic = await DeviceInformation.FindAllAsync(aqsClassic);
            foreach (var dev in devicesClassic)
            {
                if (seenIds.Add(dev.Id))
                {
                    bool connected = dev.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var conn) && conn is bool b && b;
                    list.Add(new BluetoothDeviceItem
                    {
                        DeviceId = dev.Id,
                        Name = string.IsNullOrEmpty(dev.Name) ? "Unknown Bluetooth Device" : dev.Name,
                        State = connected ? BluetoothDeviceState.Connected : BluetoothDeviceState.Paired,
                        IsConnected = connected
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BLUETOOTH CLASSIC QUERY ERROR] {ex}");
            _logger.LogError("Failed to query classic Bluetooth devices", ex);
        }

        try
        {
            string aqsBle = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
            var devicesBle = await DeviceInformation.FindAllAsync(aqsBle);
            foreach (var dev in devicesBle)
            {
                if (seenIds.Add(dev.Id))
                {
                    bool connected = dev.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var conn) && conn is bool b && b;
                    list.Add(new BluetoothDeviceItem
                    {
                        DeviceId = dev.Id,
                        Name = string.IsNullOrEmpty(dev.Name) ? "Unknown BLE Device" : dev.Name,
                        State = connected ? BluetoothDeviceState.Connected : BluetoothDeviceState.Paired,
                        IsConnected = connected
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BLUETOOTH BLE QUERY ERROR] {ex}");
            _logger.LogError("Failed to query BLE devices", ex);
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
            Console.WriteLine($"[DEVICE STATE ERROR] {ex}");
            _logger.LogError($"Failed to get state for device {deviceId}", ex);
        }
        return null;
    }

    public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string aqs = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            _deviceWatcher = DeviceInformation.CreateWatcher(aqs);

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
                }
            };

            _deviceWatcher.Start();
            _logger.LogInformation("WindowsBluetoothMonitor started.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BLUETOOTH MONITOR START ERROR] {ex}");
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
            Console.WriteLine($"[BLUETOOTH MONITOR STOP ERROR] {ex}");
        }
        return Task.CompletedTask;
    }
}

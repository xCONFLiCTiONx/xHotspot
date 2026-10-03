using xHotspot.Core.Models;

namespace xHotspot.Core.Interfaces;

public interface IBluetoothMonitor
{
    event EventHandler<BluetoothDeviceItem>? DeviceStateChanged;
    Task<IReadOnlyList<BluetoothDeviceItem>> GetPairedDevicesAsync(CancellationToken cancellationToken = default);
    Task<BluetoothDeviceItem?> GetDeviceStateAsync(string deviceId, CancellationToken cancellationToken = default);
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);
    Task StopMonitoringAsync();
}

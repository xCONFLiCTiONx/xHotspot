namespace xHotspot.Core.Models;

public class BluetoothDeviceItem
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BluetoothDeviceState State { get; set; } = BluetoothDeviceState.Unknown;
    public bool IsConnected { get; set; }
}

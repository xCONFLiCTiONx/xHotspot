namespace xHotspot.Core.Models;

public enum ServiceState
{
    ServiceStarting,
    WaitingForNetwork,
    NetworkReady,
    WaitingForPhone,
    PhoneDetected,
    StartingHotspot,
    HotspotRunning,
    PhoneConnecting,
    PhoneConnected,
    Recovering,
    TemporarilyUnavailable,
    Error,
    Stopping
}

public enum HotspotStatus
{
    Off,
    TurningOn,
    On,
    TurningOff,
    Error,
    Unknown
}

public enum BluetoothDeviceState
{
    Paired,
    Available,
    Connected,
    Disconnected,
    Unknown
}

public enum NetworkState
{
    Disconnected,
    Connecting,
    Connected,
    InternetAvailable,
    NoInternet
}

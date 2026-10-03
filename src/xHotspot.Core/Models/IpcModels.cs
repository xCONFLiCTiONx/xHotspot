namespace xHotspot.Core.Models;

public enum IpcCommandType
{
    GetStatus,
    GetDiagnostics,
    GetSettings,
    UpdateSettings,
    GetDevices,
    EnableHotspot,
    DisableHotspot,
    PauseAutomation,
    ResumeAutomation,
    GetLogs
}

public class IpcRequest
{
    public IpcCommandType Command { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}

public class IpcResponse
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string DataJson { get; set; } = string.Empty;
}

public class StatusDto
{
    public ServiceState ServiceState { get; set; }
    public HotspotStatus HotspotStatus { get; set; }
    public NetworkState WifiState { get; set; }
    public string WifiSsid { get; set; } = string.Empty;
    public bool InternetConnected { get; set; }
    public string PhoneName { get; set; } = string.Empty;
    public string RemoteSystemId { get; set; } = string.Empty;
    public bool RemoteSystemAvailable { get; set; }
    public string IdentityConfidence { get; set; } = "Unknown";
    public string TurnOffTime { get; set; } = "23:00";
    public BluetoothDeviceState PhoneBluetoothState { get; set; }
    public bool AutomationPaused { get; set; }
    public string LastError { get; set; } = string.Empty;
}

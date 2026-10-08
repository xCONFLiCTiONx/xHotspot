namespace xHotspot.Core.Models;

public class DiagnosticsReport
{
    public string WindowsVersion { get; set; } = string.Empty;
    public string DotNetVersion { get; set; } = string.Empty;
    public string AppVersion { get; set; } = "1.0.0";
    public string WifiAdapterName { get; set; } = string.Empty;
    public NetworkState WifiState { get; set; } = NetworkState.Disconnected;
    public string WifiSsid { get; set; } = string.Empty;
    public bool InternetConnected { get; set; }
    public HotspotStatus HotspotStatus { get; set; } = HotspotStatus.Off;
    public ServiceState ServiceState { get; set; } = ServiceState.ServiceStarting;
    public TimeSpan ServiceUptime { get; set; }
    public DateTime? LastSuccessfulRecovery { get; set; }
    public DateTime? LastFailedRecovery { get; set; }
    public string LastError { get; set; } = string.Empty;
    public int RecoveryAttempts { get; set; }
    public bool HotspotApiAvailable { get; set; }
    public string RequiredCapabilities { get; set; } = string.Empty;
}

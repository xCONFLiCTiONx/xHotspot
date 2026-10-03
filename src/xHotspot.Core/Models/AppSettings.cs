namespace xHotspot.Core.Models;

public class AppSettings
{
    public bool Enabled { get; set; } = true;
    public bool AutoStartHotspot { get; set; } = true;
    public bool StartAfterBoot { get; set; } = true;
    public bool RecoverAfterSleep { get; set; } = true;
    public bool RecoverAfterNetworkChange { get; set; } = true;
    public bool RecoverAfterBluetoothReconnect { get; set; } = true;
    public string PhoneDeviceId { get; set; } = string.Empty;
    public string PhoneName { get; set; } = string.Empty;
    public int RetryIntervalSeconds { get; set; } = 30;
    public int StartupDelaySeconds { get; set; } = 10;
    public bool AutomationPaused { get; set; } = false;
    public string TurnOffTime { get; set; } = "23:00"; // Scheduled turn-off time (24-hour format)
}

namespace xHotspot.Core.Models;

public class AppSettings
{
    public bool Enabled { get; set; } = true;
    public bool AutoStartHotspot { get; set; } = true;
    public bool StartAfterBoot { get; set; } = true;
    public bool RecoverAfterSleep { get; set; } = true;
    public bool TurnOffHotspotOnSleep { get; set; } = true;
    public bool RecoverAfterNetworkChange { get; set; } = true;
    public int RetryIntervalSeconds { get; set; } = 5;
    public int StartupDelaySeconds { get; set; } = 2;
    public bool AutomationPaused { get; set; } = false;
    public string TurnOffTime { get; set; } = "23:00"; // Scheduled turn-off time (24-hour format)
    public bool DisableOnDisconnect { get; set; } = true;
    public bool ReconnectResumes { get; set; } = true;
    public bool EnableMonday { get; set; } = true;
    public bool EnableTuesday { get; set; } = true;
    public bool EnableWednesday { get; set; } = true;
    public bool EnableThursday { get; set; } = true;
    public bool EnableFriday { get; set; } = true;
    public bool EnableSaturday { get; set; } = false;
    public bool EnableSunday { get; set; } = false;
}

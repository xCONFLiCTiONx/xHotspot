namespace xHotspot.Core.Interfaces;

public interface IPhoneLinkMonitor
{
    Task<bool> IsPhoneLinkConnectedAsync(CancellationToken cancellationToken = default);
    event EventHandler<bool>? PhoneLinkConnectionChanged;
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);
    Task StopMonitoringAsync();
}

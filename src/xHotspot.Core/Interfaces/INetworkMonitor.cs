using xHotspot.Core.Models;

namespace xHotspot.Core.Interfaces;

public interface INetworkMonitor
{
    event EventHandler<NetworkState>? NetworkStateChanged;
    NetworkState CurrentState { get; }
    string CurrentSsid { get; }
    bool IsInternetAvailable { get; }
    Task<bool> CheckInternetConnectivityAsync(CancellationToken cancellationToken = default);
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);
    Task StopMonitoringAsync();
}

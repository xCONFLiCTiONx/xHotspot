using System.Net.NetworkInformation;
using xHotspot.Core.Interfaces;
using xHotspot.Core.Models;

namespace xHotspot.Service.Network;

public class WindowsNetworkMonitor : INetworkMonitor
{
    private readonly ILoggerService _logger;
    private NetworkState _currentState = NetworkState.Disconnected;
    private string _currentSsid = string.Empty;
    private bool _isInternetAvailable;

    public event EventHandler<NetworkState>? NetworkStateChanged;

    public NetworkState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState != value)
            {
                _currentState = value;
                NetworkStateChanged?.Invoke(this, _currentState);
            }
        }
    }

    public string CurrentSsid => _currentSsid;
    public bool IsInternetAvailable => _isInternetAvailable;

    public WindowsNetworkMonitor(ILoggerService logger)
    {
        _logger = logger;
    }

    public Task<bool> CheckInternetConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _isInternetAvailable = NetworkInterface.GetIsNetworkAvailable();
            CurrentState = _isInternetAvailable ? NetworkState.InternetAvailable : NetworkState.NoInternet;
            return Task.FromResult(_isInternetAvailable);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error checking internet connectivity", ex);
            _isInternetAvailable = false;
            CurrentState = NetworkState.NoInternet;
            return Task.FromResult(false);
        }
    }

    public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        _logger.LogInformation("WindowsNetworkMonitor started.");
        return CheckInternetConnectivityAsync(cancellationToken);
    }

    public Task StopMonitoringAsync()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        _logger.LogInformation("WindowsNetworkMonitor stopped.");
        return Task.CompletedTask;
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        _isInternetAvailable = e.IsAvailable;
        CurrentState = e.IsAvailable ? NetworkState.InternetAvailable : NetworkState.NoInternet;
        _logger.LogInformation($"Network availability changed: Available={e.IsAvailable}");
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        _logger.LogDebug("Network address changed.");
        _ = CheckInternetConnectivityAsync();
    }
}

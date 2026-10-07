using System.Diagnostics;
using Microsoft.Win32;
using Windows.System.RemoteSystems;
using xHotspot.Core.Interfaces;
using xHotspot.Core.Models;

namespace xHotspot.Service.Services;

public class WindowsPhoneLinkMonitor : IPhoneLinkMonitor
{
    private readonly ILoggerService _logger;
    private readonly ISettingsService _settingsService;
    private bool _lastConnectedState = false;
    private bool _isSuspended = false;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;

    public event EventHandler<bool>? PhoneLinkConnectionChanged;

    public WindowsPhoneLinkMonitor(ILoggerService logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public async Task<bool> IsPhoneLinkConnectedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = _settingsService.LoadSettings();
            string targetId = settings.PhoneDeviceId;
            string targetName = string.IsNullOrEmpty(settings.PhoneName) ? "Galaxy S23 FE" : settings.PhoneName;

            bool remoteSystemMatched = false;
            string confidence = "Unknown";

            try
            {
                var accessStatus = await RemoteSystem.RequestAccessAsync();
                if (accessStatus == RemoteSystemAccessStatus.Allowed)
                {
                    var watcher = RemoteSystem.CreateWatcher();
                    var systems = new List<RemoteSystem>();

                    watcher.RemoteSystemAdded += (s, args) => {
                        lock (systems) { systems.Add(args.RemoteSystem); }
                    };
                    watcher.Start();
                    await Task.Delay(2000, cancellationToken);
                    watcher.Stop();

                    foreach (var rs in systems)
                    {
                        bool idMatch = !string.IsNullOrEmpty(targetId) && rs.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase);
                        bool nameMatch = !string.IsNullOrEmpty(rs.DisplayName) &&
                            (rs.DisplayName.Contains(targetName, StringComparison.OrdinalIgnoreCase) ||
                             rs.DisplayName.Contains("S23", StringComparison.OrdinalIgnoreCase));

                        if (idMatch || nameMatch)
                        {
                            bool isAvailable = rs.Status == RemoteSystemStatus.Available || rs.IsAvailableByProximity;
                            if (isAvailable)
                            {
                                remoteSystemMatched = true;
                                confidence = idMatch ? "Confirmed" : "Probable";
                                _logger.LogInformation($"RemoteSystem matched phone: Name={rs.DisplayName}, Id={rs.Id}, Confidence={confidence}");
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"RemoteSystem query exception (fallback to processes): {ex.Message}");
            }

            // Secondary validation / fallback: Phone Link / CrossDeviceService processes
            bool hostRunning = Process.GetProcessesByName("PhoneExperienceHost").Length > 0;
            bool crossDeviceRunning = Process.GetProcessesByName("CrossDeviceService").Length > 0;
            bool processActive = hostRunning || crossDeviceRunning;

            bool connected = remoteSystemMatched || processActive;
            return connected;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error checking Phone Link status with RemoteSystem", ex);
            return Process.GetProcessesByName("PhoneExperienceHost").Length > 0 ||
                   Process.GetProcessesByName("CrossDeviceService").Length > 0;
        }
    }

    public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        _cts = new CancellationTokenSource();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
        _logger.LogInformation("WindowsPhoneLinkMonitor started with RemoteSystem support.");
        return Task.CompletedTask;
    }

    public Task StopMonitoringAsync()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _cts?.Cancel();
        _monitorTask?.Wait(TimeSpan.FromSeconds(2));
        _logger.LogInformation("WindowsPhoneLinkMonitor stopped.");
        return Task.CompletedTask;
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            _isSuspended = true;
        }
        else if (e.Mode == PowerModes.Resume)
        {
            _isSuspended = false;
        }
    }

    private async Task MonitorLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_isSuspended)
            {
                await Task.Delay(3000, cancellationToken);
                continue;
            }

            try
            {
                bool connected = await IsPhoneLinkConnectedAsync(cancellationToken);
                if (connected != _lastConnectedState)
                {
                    _lastConnectedState = connected;
                    Console.WriteLine($">>> [PHONE LINK / REMOTESYSTEM] Connection state changed: Connected = {connected}");
                    PhoneLinkConnectionChanged?.Invoke(this, connected);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PHONE LINK MONITOR ERROR] {ex}");
            }

            await Task.Delay(3000, cancellationToken);
        }
    }
}

using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using xHotspot.Core.Interfaces;
using xHotspot.Core.Models;
using xHotspot.Service.Ipc;

namespace xHotspot.Service;

public class Worker : BackgroundService
{
    private readonly ILoggerService _logger;
    private readonly ISettingsService _settingsService;
    private readonly IHotspotManager _hotspotManager;
    private readonly IBluetoothMonitor _bluetoothMonitor;
    private readonly INetworkMonitor _networkMonitor;
    private readonly NamedPipeServer _ipcServer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public Worker(
        ILoggerService logger,
        ISettingsService settingsService,
        IHotspotManager hotspotManager,
        IBluetoothMonitor bluetoothMonitor,
        INetworkMonitor networkMonitor,
        NamedPipeServer ipcServer)
    {
        _logger = logger;
        _settingsService = settingsService;
        _hotspotManager = hotspotManager;
        _bluetoothMonitor = bluetoothMonitor;
        _networkMonitor = networkMonitor;
        _ipcServer = ipcServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("xHotspot service starting...");

        try
        {
            _ipcServer.Start();
            await _networkMonitor.StartMonitoringAsync(stoppingToken);
            await _bluetoothMonitor.StartMonitoringAsync(stoppingToken);

            _bluetoothMonitor.DeviceStateChanged += OnDeviceStateChanged;
            _networkMonitor.NetworkStateChanged += OnNetworkStateChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            var settings = _settingsService.LoadSettings();
            await Task.Delay(TimeSpan.FromSeconds(settings.StartupDelaySeconds), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await EvaluateAndRecoverAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(settings.RetryIntervalSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogCritical("Unhandled exception in Worker background loop", ex);
        }
        finally
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            await _bluetoothMonitor.StopMonitoringAsync();
            await _networkMonitor.StopMonitoringAsync();
            _ipcServer.Stop();
            _logger.LogInformation("xHotspot service stopped.");
        }
    }

    private void OnDeviceStateChanged(object? sender, BluetoothDeviceItem e)
    {
        _logger.LogInformation($"Device state changed: {e.Name} (Connected: {e.IsConnected})");
        _ = EvaluateAndRecoverAsync();
    }

    private void OnNetworkStateChanged(object? sender, NetworkState state)
    {
        _logger.LogInformation($"Network state changed: {state}");
        _ = EvaluateAndRecoverAsync();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _logger.LogInformation("System resumed from sleep. Triggering stabilization and recovery...");
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                await EvaluateAndRecoverAsync();
            });
        }
    }

    private async Task EvaluateAndRecoverAsync(CancellationToken cancellationToken = default)
    {
        if (!await _semaphore.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var settings = _settingsService.LoadSettings();
            if (!settings.Enabled || settings.AutomationPaused)
            {
                return;
            }

            if (!string.IsNullOrEmpty(settings.PhoneDeviceId))
            {
                var phoneState = await _bluetoothMonitor.GetDeviceStateAsync(settings.PhoneDeviceId, cancellationToken);
                bool phonePresent = phoneState != null && (phoneState.IsConnected || phoneState.State == BluetoothDeviceState.Connected);

                var hotspotStatus = await _hotspotManager.GetStatusAsync(cancellationToken);

                if (phonePresent && hotspotStatus == HotspotStatus.Off)
                {
                    _logger.LogInformation("Phone present and hotspot is OFF. Enabling Mobile Hotspot...");
                    await _hotspotManager.EnableAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during recovery evaluation", ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

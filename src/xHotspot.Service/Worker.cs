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
        Console.WriteLine(">>> [DEBUG] xHotspot Service Worker starting execution loop...");

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
            Console.WriteLine(">>> [DEBUG] xHotspot Service Worker cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> [FATAL WORKER ERROR] {ex}");
            _logger.LogCritical("Unhandled exception in Worker background loop", ex);
            throw; // Do not swallow exceptions in debug mode
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
        Console.WriteLine($">>> [DEBUG] Device state changed event received: {e.Name} (Connected: {e.IsConnected})");
        _logger.LogInformation($"Device state changed: {e.Name} (Connected: {e.IsConnected})");
        _ = EvaluateAndRecoverAsync();
    }

    private void OnNetworkStateChanged(object? sender, NetworkState state)
    {
        Console.WriteLine($">>> [DEBUG] Network state changed event received: {state}");
        _logger.LogInformation($"Network state changed: {state}");
        _ = EvaluateAndRecoverAsync();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Console.WriteLine(">>> [DEBUG] Power mode resume detected.");
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
                Console.WriteLine(">>> [DEBUG] Automation is disabled or paused.");
                return;
            }

            if (!string.IsNullOrEmpty(settings.PhoneDeviceId))
            {
                Console.WriteLine($">>> [DEBUG] Checking state for configured phone ID: {settings.PhoneDeviceId}");
                var phoneState = await _bluetoothMonitor.GetDeviceStateAsync(settings.PhoneDeviceId, cancellationToken);
                bool phonePresent = phoneState != null && (phoneState.IsConnected || phoneState.State == BluetoothDeviceState.Connected);

                var hotspotStatus = await _hotspotManager.GetStatusAsync(cancellationToken);
                Console.WriteLine($">>> [DEBUG] Phone present: {phonePresent}, Hotspot status: {hotspotStatus}");

                if (phonePresent && hotspotStatus == HotspotStatus.Off)
                {
                    _logger.LogInformation("Phone present and hotspot is OFF. Enabling Mobile Hotspot...");
                    Console.WriteLine(">>> [DEBUG] Phone present and hotspot is OFF. Enabling Mobile Hotspot...");
                    await _hotspotManager.EnableAsync(cancellationToken);
                }
            }
            else
            {
                Console.WriteLine(">>> [DEBUG] No configured phone device ID in settings.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RECOVERY EVALUATION ERROR] {ex}");
            _logger.LogError("Error during recovery evaluation", ex);
            throw; // Surface errors in debug mode
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

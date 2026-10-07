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
    private readonly IPhoneLinkMonitor _phoneLinkMonitor;
    private readonly INetworkMonitor _networkMonitor;
    private readonly NamedPipeServer _ipcServer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isSuspended;

    public Worker(
        ILoggerService logger,
        ISettingsService settingsService,
        IHotspotManager hotspotManager,
        IPhoneLinkMonitor phoneLinkMonitor,
        INetworkMonitor networkMonitor,
        NamedPipeServer ipcServer)
    {
        _logger = logger;
        _settingsService = settingsService;
        _hotspotManager = hotspotManager;
        _phoneLinkMonitor = phoneLinkMonitor;
        _networkMonitor = networkMonitor;
        _ipcServer = ipcServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("xHotspot service starting (Always-On Mode)...");
        Console.WriteLine(">>> [DEBUG] xHotspot Service Worker starting execution loop (Always-On Mode)...");

        try
        {
            _ipcServer.Start();
            await _networkMonitor.StartMonitoringAsync(stoppingToken);
            await _phoneLinkMonitor.StartMonitoringAsync(stoppingToken);

            _phoneLinkMonitor.PhoneLinkConnectionChanged += OnPhoneLinkConnectionChanged;
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
            throw;
        }
        finally
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            await _phoneLinkMonitor.StopMonitoringAsync();
            await _networkMonitor.StopMonitoringAsync();
            _ipcServer.Stop();
            _logger.LogInformation("xHotspot service stopped.");
        }
    }

    private void OnPhoneLinkConnectionChanged(object? sender, bool connected)
    {
        Console.WriteLine($">>> [DEBUG] Phone Link connection changed event received: Connected = {connected}");
        _logger.LogInformation($"Phone Link connection changed: Connected = {connected}");
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
        if (e.Mode == PowerModes.Suspend)
        {
            _isSuspended = true;
            Console.WriteLine(">>> [POWER] System suspending/entering sleep. Disabling Mobile Hotspot to allow computer sleep...");
            _logger.LogInformation("System suspending/entering sleep. Disabling Mobile Hotspot to allow computer sleep.");
            var settings = _settingsService.LoadSettings();
            if (settings.TurnOffHotspotOnSleep)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _hotspotManager.DisableAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error disabling hotspot on suspend", ex);
                    }
                });
            }
        }
        else if (e.Mode == PowerModes.Resume)
        {
            _isSuspended = false;
            Console.WriteLine(">>> [POWER] System resumed from sleep. Forcing Mobile Hotspot ON...");
            _logger.LogInformation("System resumed from sleep. Forcing Mobile Hotspot ON...");
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                await EvaluateAndRecoverAsync();
            });
        }
    }

    private async Task EvaluateAndRecoverAsync(CancellationToken cancellationToken = default)
    {
        if (_isSuspended)
        {
            Console.WriteLine(">>> [DEBUG] System is currently suspended/sleeping. Skipping evaluation.");
            return;
        }

        if (!await _semaphore.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            if (_isSuspended)
            {
                return;
            }

            var settings = _settingsService.LoadSettings();
            if (!settings.Enabled || settings.AutomationPaused)
            {
                Console.WriteLine(">>> [DEBUG] Automation is disabled or paused. Hotspot will not be forced on.");
                return;
            }

            var hotspotStatus = await _hotspotManager.GetStatusAsync(cancellationToken);
            Console.WriteLine($">>> [DEBUG] Always-on evaluation: Hotspot status = {hotspotStatus}");

            if (hotspotStatus == HotspotStatus.Off)
            {
                _logger.LogInformation("Hotspot is OFF while automation is active. Forcing Mobile Hotspot ON...");
                Console.WriteLine(">>> [ALWAYS-ON] Hotspot is OFF. Forcing Mobile Hotspot ON...");
                bool success = await _hotspotManager.EnableAsync(cancellationToken);
                Console.WriteLine($">>> [ALWAYS-ON] EnableAsync result: {success}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ALWAYS-ON EVALUATION ERROR] {ex}");
            _logger.LogError("Error during always-on hotspot evaluation", ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

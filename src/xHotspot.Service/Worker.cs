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
    private bool _scheduledOffTriggeredToday = false;
    private DateTime _lastDayChecked = DateTime.Today;

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
        _logger.LogInformation("xHotspot service starting...");
        Console.WriteLine(">>> [DEBUG] xHotspot Service Worker starting execution loop (Phone Link + Schedule mode)...");

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
        if (connected)
        {
            _ = EvaluateAndRecoverAsync();
        }
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
                _scheduledOffTriggeredToday = false;
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

            // Check scheduled turn-off time
            if (DateTime.Today != _lastDayChecked)
            {
                _lastDayChecked = DateTime.Today;
                _scheduledOffTriggeredToday = false;
            }

            if (!_scheduledOffTriggeredToday && TimeSpan.TryParse(settings.TurnOffTime, out var turnOffTime))
            {
                var now = DateTime.Now.TimeOfDay;
                if (now >= turnOffTime)
                {
                    Console.WriteLine($">>> [SCHEDULE] Current time {now:hh\\:mm} has reached or passed scheduled turn-off time {settings.TurnOffTime}. Turning OFF Mobile Hotspot.");
                    _logger.LogInformation($"Scheduled turn-off time reached ({settings.TurnOffTime}). Disabling Mobile Hotspot.");
                    await _hotspotManager.DisableAsync(cancellationToken);
                    _scheduledOffTriggeredToday = true;
                    return;
                }
            }

            // Check Windows Phone Link status
            bool phoneLinkConnected = await _phoneLinkMonitor.IsPhoneLinkConnectedAsync(cancellationToken);
            var hotspotStatus = await _hotspotManager.GetStatusAsync(cancellationToken);
            Console.WriteLine($">>> [DEBUG] Phone Link connected: {phoneLinkConnected}, Hotspot status: {hotspotStatus}, Scheduled off today: {_scheduledOffTriggeredToday}");

            if (phoneLinkConnected && hotspotStatus == HotspotStatus.Off && !_scheduledOffTriggeredToday)
            {
                _logger.LogInformation("Phone Link connected and hotspot is OFF. Enabling Mobile Hotspot...");
                Console.WriteLine(">>> [DEBUG] Phone Link connected and hotspot is OFF. Enabling Mobile Hotspot...");
                bool success = await _hotspotManager.EnableAsync(cancellationToken);
                Console.WriteLine($">>> [DEBUG] EnableAsync result: {success}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RECOVERY EVALUATION ERROR] {ex}");
            _logger.LogError("Error during recovery evaluation", ex);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

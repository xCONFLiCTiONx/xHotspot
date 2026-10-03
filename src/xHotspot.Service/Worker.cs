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
        Console.WriteLine(">>> [DEBUG] xHotspot Service Worker starting execution loop (Phone Connect Start + Scheduled Stop mode)...");

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
        if (e.Mode == PowerModes.Resume)
        {
            Console.WriteLine(">>> [DEBUG] Power mode resume detected.");
            _logger.LogInformation("System resumed from sleep. Triggering stabilization and recovery...");
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
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

            // Calendar-aware day check
            var today = DateTime.Today;
            if (today != _lastDayChecked)
            {
                _lastDayChecked = today;
                _scheduledOffTriggeredToday = false;
            }

            bool isDayEnabled = today.DayOfWeek switch
            {
                DayOfWeek.Monday => settings.EnableMonday,
                DayOfWeek.Tuesday => settings.EnableTuesday,
                DayOfWeek.Wednesday => settings.EnableWednesday,
                DayOfWeek.Thursday => settings.EnableThursday,
                DayOfWeek.Friday => settings.EnableFriday,
                DayOfWeek.Saturday => settings.EnableSaturday,
                DayOfWeek.Sunday => settings.EnableSunday,
                _ => true
            };

            var now = DateTime.Now.TimeOfDay;
            bool hasStopReached = false;
            if (TimeSpan.TryParse(settings.TurnOffTime, out var turnOffTime))
            {
                if (now >= turnOffTime)
                {
                    hasStopReached = true;
                }
            }

            // 1. If scheduled stop time reached, turn off hotspot
            if (hasStopReached && !_scheduledOffTriggeredToday)
            {
                Console.WriteLine($">>> [SCHEDULE] Current time {now:hh\\:mm} reached or passed stop time {settings.TurnOffTime}. Turning OFF Mobile Hotspot.");
                _logger.LogInformation($"Scheduled stop time reached ({settings.TurnOffTime}). Disabling Mobile Hotspot.");
                await _hotspotManager.DisableAsync(cancellationToken);
                _scheduledOffTriggeredToday = true;
                return;
            }

            // 2. Check Phone Link connection state
            bool phoneConnected = await _phoneLinkMonitor.IsPhoneLinkConnectedAsync(cancellationToken);
            var hotspotStatus = await _hotspotManager.GetStatusAsync(cancellationToken);
            Console.WriteLine($">>> [DEBUG] Phone connected: {phoneConnected}, Hotspot status: {hotspotStatus}, Day enabled: {isDayEnabled}, Stop reached: {hasStopReached}, Scheduled off today: {_scheduledOffTriggeredToday}");

            if (!isDayEnabled)
            {
                Console.WriteLine(">>> [SCHEDULE] Today is not enabled in schedule. Hotspot automation inactive.");
                return;
            }

            if (hasStopReached)
            {
                Console.WriteLine(">>> [SCHEDULE] Scheduled window has ended. Hotspot remains OFF for new connections tonight.");
                return;
            }

            // Start condition: Phone connects and stop not reached and day enabled and not scheduled off
            if (phoneConnected && hotspotStatus == HotspotStatus.Off && !hasStopReached && !_scheduledOffTriggeredToday)
            {
                _logger.LogInformation("Phone connected to Phone Link during active schedule window. Enabling Mobile Hotspot...");
                Console.WriteLine(">>> [DEBUG] Phone connected and hotspot OFF. Turning hotspot ON.");
                bool success = await _hotspotManager.EnableAsync(cancellationToken);
                Console.WriteLine($">>> [DEBUG] EnableAsync result: {success}");
            }
            else if (!phoneConnected && hotspotStatus == HotspotStatus.On && settings.DisableOnDisconnect)
            {
                Console.WriteLine(">>> [DEBUG] Phone disconnected from Phone Link and DisableOnDisconnect is enabled. Turning hotspot OFF.");
                _logger.LogInformation("Phone disconnected from Phone Link. Disabling Mobile Hotspot.");
                await _hotspotManager.DisableAsync(cancellationToken);
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

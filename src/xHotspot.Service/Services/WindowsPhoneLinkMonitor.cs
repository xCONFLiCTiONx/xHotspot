using System.Diagnostics;
using xHotspot.Core.Interfaces;

namespace xHotspot.Service.Services;

public class WindowsPhoneLinkMonitor : IPhoneLinkMonitor
{
    private readonly ILoggerService _logger;
    private bool _lastConnectedState = false;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;

    public event EventHandler<bool>? PhoneLinkConnectionChanged;

    public WindowsPhoneLinkMonitor(ILoggerService logger)
    {
        _logger = logger;
    }

    public Task<bool> IsPhoneLinkConnectedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var processes = Process.GetProcessesByName("PhoneExperienceHost");
            bool connected = processes.Length > 0;
            return Task.FromResult(connected);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error checking Phone Link status", ex);
            return Task.FromResult(false);
        }
    }

    public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
        _logger.LogInformation("WindowsPhoneLinkMonitor started.");
        return Task.CompletedTask;
    }

    public Task StopMonitoringAsync()
    {
        _cts?.Cancel();
        _monitorTask?.Wait(TimeSpan.FromSeconds(2));
        _logger.LogInformation("WindowsPhoneLinkMonitor stopped.");
        return Task.CompletedTask;
    }

    private async Task MonitorLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                bool connected = await IsPhoneLinkConnectedAsync(cancellationToken);
                if (connected != _lastConnectedState)
                {
                    _lastConnectedState = connected;
                    Console.WriteLine($">>> [PHONE LINK] Connection state changed: Connected = {connected}");
                    PhoneLinkConnectionChanged?.Invoke(this, connected);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PHONE LINK MONITOR ERROR] {ex}");
            }

            await Task.Delay(5000, cancellationToken);
        }
    }
}

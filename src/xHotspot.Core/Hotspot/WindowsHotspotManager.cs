using System.Diagnostics;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;
using xHotspot.Core.Interfaces;
using xHotspot.Core.Models;

namespace xHotspot.Core.Hotspot;

public class WindowsHotspotManager : IHotspotManager
{
    private readonly ILoggerService _logger;

    public WindowsHotspotManager(ILoggerService logger)
    {
        _logger = logger;
    }

    public async Task<HotspotStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var profiles = NetworkInformation.GetConnectionProfiles();
            foreach (var profile in profiles)
            {
                try
                {
                    var manager = NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
                    if (manager != null)
                    {
                        var state = manager.TetheringOperationalState;
                        if (state == TetheringOperationalState.On)
                        {
                            return HotspotStatus.On;
                        }
                        if (state == TetheringOperationalState.InTransition)
                        {
                            return HotspotStatus.TurningOn;
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error checking hotspot status", ex);
        }

        return HotspotStatus.Off;
    }

    public async Task<bool> EnableAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Requesting Windows Mobile Hotspot activation...");
        Console.WriteLine(">>> [HOTSPOT] Attempting to enable Windows Mobile Hotspot via TetheringManager...");
        try
        {
            NetworkOperatorTetheringManager? targetManager = null;
            var profiles = NetworkInformation.GetConnectionProfiles();
            foreach (var profile in profiles)
            {
                try
                {
                    var manager = NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
                    if (manager != null)
                    {
                        targetManager = manager;
                        break;
                    }
                }
                catch { }
            }

            if (targetManager == null)
            {
                _logger.LogError("No tethering manager found on any connection profile.");
                Console.WriteLine(">>> [HOTSPOT ERROR] No tethering manager found on any connection profile.");
                return false;
            }

            if (targetManager.TetheringOperationalState == TetheringOperationalState.On)
            {
                _logger.LogInformation("Mobile Hotspot is already ON.");
                Console.WriteLine(">>> [HOTSPOT] Mobile Hotspot is already ON.");
                return true;
            }

            var result = await targetManager.StartTetheringAsync().AsTask(cancellationToken);
            _logger.LogInformation($"StartTetheringAsync result: Status = {result.Status}");
            Console.WriteLine($">>> [HOTSPOT] StartTetheringAsync result: Status = {result.Status}");

            var finalStatus = await GetStatusAsync(cancellationToken);
            return finalStatus == HotspotStatus.On || result.Status == TetheringOperationStatus.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception during StartTetheringAsync", ex);
            Console.WriteLine($"[HOTSPOT ENABLE EXCEPTION] {ex}");
            return false;
        }
    }

    public async Task<bool> DisableAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Requesting Windows Mobile Hotspot deactivation...");
        Console.WriteLine(">>> [HOTSPOT] Attempting to disable Windows Mobile Hotspot via TetheringManager...");
        try
        {
            NetworkOperatorTetheringManager? targetManager = null;
            var profiles = NetworkInformation.GetConnectionProfiles();
            foreach (var profile in profiles)
            {
                try
                {
                    var manager = NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
                    if (manager != null)
                    {
                        targetManager = manager;
                        break;
                    }
                }
                catch { }
            }

            if (targetManager == null)
            {
                return false;
            }

            var result = await targetManager.StopTetheringAsync().AsTask(cancellationToken);
            _logger.LogInformation($"StopTetheringAsync result: Status = {result.Status}");
            Console.WriteLine($">>> [HOTSPOT] StopTetheringAsync result: Status = {result.Status}");

            var finalStatus = await GetStatusAsync(cancellationToken);
            return finalStatus == HotspotStatus.Off || result.Status == TetheringOperationStatus.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception during StopTetheringAsync", ex);
            Console.WriteLine($"[HOTSPOT DISABLE EXCEPTION] {ex}");
            return false;
        }
    }

    public Task<bool> IsApiAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

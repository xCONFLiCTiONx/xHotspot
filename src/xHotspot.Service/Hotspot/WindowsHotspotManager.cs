using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;
using xHotspot.Core.Interfaces;
using xHotspot.Core.Models;

namespace xHotspot.Service.Hotspot;

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
            var manager = GetTetheringManager();
            if (manager == null) return HotspotStatus.Unknown;

            return manager.TetheringOperationalState switch
            {
                NetworkOperatorTetheringOperationalState.On => HotspotStatus.On,
                NetworkOperatorTetheringOperationalState.Off => HotspotStatus.Off,
                NetworkOperatorTetheringOperationalState.InTransition => HotspotStatus.TurningOn,
                _ => HotspotStatus.Unknown
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to get hotspot status via WinRT", ex);
            return HotspotStatus.Unknown;
        }
    }

    public async Task<bool> EnableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var manager = GetTetheringManager();
            if (manager == null) return false;

            if (manager.TetheringOperationalState == NetworkOperatorTetheringOperationalState.On)
            {
                _logger.LogInformation("Hotspot is already enabled.");
                return true;
            }

            _logger.LogInformation("Enabling Mobile Hotspot...");
            var result = await manager.StartTetheringAsync();
            bool success = result.Status == NetworkOperatorTetheringOperationStatus.Success;
            if (success)
            {
                _logger.LogInformation("Mobile Hotspot enabled successfully.");
            }
            else
            {
                _logger.LogWarning($"Failed to enable Mobile Hotspot. Status: {result.Status}");
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception while enabling Mobile Hotspot", ex);
            return false;
        }
    }

    public async Task<bool> DisableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var manager = GetTetheringManager();
            if (manager == null) return false;

            if (manager.TetheringOperationalState == NetworkOperatorTetheringOperationalState.Off)
            {
                return true;
            }

            _logger.LogInformation("Disabling Mobile Hotspot...");
            var result = await manager.StopTetheringAsync();
            bool success = result.Status == NetworkOperatorTetheringOperationStatus.Success;
            if (success)
            {
                _logger.LogInformation("Mobile Hotspot disabled successfully.");
            }
            else
            {
                _logger.LogWarning($"Failed to disable Mobile Hotspot. Status: {result.Status}");
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception while disabling Mobile Hotspot", ex);
            return false;
        }
    }

    public Task<bool> IsApiAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var manager = GetTetheringManager();
            return Task.FromResult(manager != null);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private NetworkOperatorTetheringManager? GetTetheringManager()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile != null)
            {
                return NetworkOperatorTetheringManager.CreateForConnectionProfile(profile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Could not create TetheringManager from internet profile: {ex.Message}");
        }
        return null;
    }
}

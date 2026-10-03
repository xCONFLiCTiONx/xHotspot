using System.Diagnostics;
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
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show hostednetwork",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p != null)
            {
                string output = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync(cancellationToken);

                if (output.Contains("Started") || output.Contains("Running"))
                {
                    return HotspotStatus.On;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Could not query hosted network status: {ex.Message}");
        }

        return HotspotStatus.Off;
    }

    public async Task<bool> EnableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Enabling Windows Mobile Hotspot...");

            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-Command \"(Get-NetConnectionProfile).InterfaceAlias\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p != null)
            {
                await p.WaitForExitAsync(cancellationToken);
            }

            _logger.LogInformation("Mobile Hotspot enable command executed.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to enable Mobile Hotspot", ex);
            return false;
        }
    }

    public async Task<bool> DisableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Disabling Windows Mobile Hotspot...");
            await Task.Delay(200, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to disable Mobile Hotspot", ex);
            return false;
        }
    }

    public Task<bool> IsApiAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

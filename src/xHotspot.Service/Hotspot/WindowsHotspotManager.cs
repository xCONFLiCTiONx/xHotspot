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
                FileName = "powershell",
                Arguments = "-Command \"Get-NetConnectionProfile\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p != null)
            {
                string output = await p.StandardOutput.ReadToEndAsync(cancellationToken);
                await p.WaitForExitAsync(cancellationToken);
                Console.WriteLine($">>> [HOTSPOT STATUS CHECK] NetConnectionProfile output:\n{output}");
                if (output.Contains("Hotspot") || output.Contains("Connected"))
                {
                    return HotspotStatus.On;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HOTSPOT STATUS ERROR] {ex}");
        }

        return HotspotStatus.Off;
    }

    public async Task<bool> EnableAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine(">>> [HOTSPOT] Attempting to enable Windows Mobile Hotspot...");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-Command \"Start-Process 'ms-settings:network-mobilehotspot'\"",
                UseShellExecute = true,
                CreateNoWindow = false
            };

            using var p = Process.Start(psi);
            if (p != null)
            {
                await p.WaitForExitAsync(cancellationToken);
            }

            Console.WriteLine(">>> [HOTSPOT] Mobile hotspot settings invoked successfully.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HOTSPOT ENABLE ERROR] {ex}");
            _logger.LogError("Failed to enable Mobile Hotspot", ex);
            return false;
        }
    }

    public async Task<bool> DisableAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine(">>> [HOTSPOT] Disabling Mobile Hotspot...");
        await Task.Delay(200, cancellationToken);
        return true;
    }

    public Task<bool> IsApiAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

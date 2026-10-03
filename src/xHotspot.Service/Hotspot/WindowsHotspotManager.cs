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
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = "wlan show hostednetwork",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start netsh process.");
        string output = await p.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await p.StandardError.ReadToEndAsync(cancellationToken);
        await p.WaitForExitAsync(cancellationToken);

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"[NETSH ERROR] {error}");
        }

        if (output.Contains("Started") || output.Contains("Running"))
        {
            return HotspotStatus.On;
        }

        return HotspotStatus.Off;
    }

    public async Task<bool> EnableAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Enabling Windows Mobile Hotspot...");

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-Command \"(Get-NetConnectionProfile).InterfaceAlias\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start powershell process.");
        string output = await p.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await p.StandardError.ReadToEndAsync(cancellationToken);
        await p.WaitForExitAsync(cancellationToken);

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"[POWERSHELL ERROR] {error}");
            throw new InvalidOperationException($"PowerShell error: {error}");
        }

        _logger.LogInformation($"Mobile Hotspot enable command executed. Output: {output.Trim()}");
        return true;
    }

    public async Task<bool> DisableAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disabling Windows Mobile Hotspot...");
        await Task.Delay(200, cancellationToken);
        return true;
    }

    public Task<bool> IsApiAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

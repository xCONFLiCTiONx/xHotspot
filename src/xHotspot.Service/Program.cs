using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using xHotspot.Core.Interfaces;
using xHotspot.Core.Models;
using xHotspot.Service.Hotspot;
using xHotspot.Service.Ipc;
using xHotspot.Service.Network;
using xHotspot.Service.Services;
using Windows.System.RemoteSystems;

namespace xHotspot.Service;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--diagnose"))
        {
            RunDiagnostics();
            return 0;
        }

        if (args.Contains("--phone-diagnostics"))
        {
            await RunPhoneDiagnosticsAsync();
            return 0;
        }

        bool isConsole = args.Contains("--console") || !Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService();

        var builder = Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "xHotspot";
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<ILoggerService, LoggerService>();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IHotspotManager, WindowsHotspotManager>();
                services.AddSingleton<IPhoneLinkMonitor, WindowsPhoneLinkMonitor>();
                services.AddSingleton<INetworkMonitor, WindowsNetworkMonitor>();
                services.AddSingleton<NamedPipeServer>();
                services.AddHostedService<Worker>();
            });

        if (isConsole)
        {
            Console.WriteLine("Running xHotspot Service in Console Mode. Press Ctrl+C to exit.");
            await builder.RunConsoleAsync();
        }
        else
        {
            await builder.Build().RunAsync();
        }

        return 0;
    }

    private static void RunDiagnostics()
    {
        Console.WriteLine("=== xHotspot Diagnostics ===");
        Console.WriteLine($"Windows Version: {Environment.OSVersion}");
        Console.WriteLine($".NET Runtime: {Environment.Version}");
        Console.WriteLine($"Machine Name: {Environment.MachineName}");
        Console.WriteLine($"User: {Environment.UserName}");
        Console.WriteLine("============================");
    }

    private static async Task RunPhoneDiagnosticsAsync()
    {
        Console.WriteLine("=== xHotspot Phone Identity Diagnostics (RemoteSystem API) ===");
        Console.WriteLine($"Windows Version: {Environment.OSVersion}");

        try
        {
            var accessStatus = await RemoteSystem.RequestAccessAsync();
            Console.WriteLine($"RemoteSystem access status: {accessStatus}");

            if (accessStatus == RemoteSystemAccessStatus.Allowed)
            {
                var watcher = RemoteSystem.CreateWatcher();
                var systems = new List<RemoteSystem>();

                watcher.RemoteSystemAdded += (s, args) => {
                    lock (systems) { systems.Add(args.RemoteSystem); }
                };
                watcher.Start();

                Console.WriteLine("Discovering remote systems for 3 seconds...");
                await Task.Delay(3000);
                watcher.Stop();

                Console.WriteLine($"Discovered RemoteSystems count: {systems.Count}");

                if (systems.Count == 0)
                {
                    Console.WriteLine("No remote systems found. Ensure Phone Link / Connected Devices is active and paired.");
                }

                foreach (var rs in systems)
                {
                    Console.WriteLine("----------------------------------------");
                    Console.WriteLine($"DisplayName: {rs.DisplayName}");
                    Console.WriteLine($"Id: {rs.Id}");
                    Console.WriteLine($"Kind: {rs.Kind}");
                    Console.WriteLine($"Status: {rs.Status}");
                    Console.WriteLine($"IsAvailableByProximity: {rs.IsAvailableByProximity}");
                    Console.WriteLine($"Manufacturer: {rs.ManufacturerDisplayName}");
                    Console.WriteLine($"Model: {rs.ModelDisplayName}");
                }
            }
            else
            {
                Console.WriteLine($"RemoteSystem access denied or not allowed: {accessStatus}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PHONE DIAGNOSTICS ERROR]: {ex}");
        }
        Console.WriteLine("=============================================================");
    }
}

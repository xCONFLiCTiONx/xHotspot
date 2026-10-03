using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using xHotspot.Core.Interfaces;
using xHotspot.Core.Models;
using xHotspot.Service.Hotspot;
using xHotspot.Service.Ipc;
using xHotspot.Service.Network;
using xHotspot.Service.Services;

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
}

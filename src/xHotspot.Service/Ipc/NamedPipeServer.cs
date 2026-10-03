using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using xHotspot.Core.Interfaces;
using xHotspot.Core.Models;

namespace xHotspot.Service.Ipc;

public class NamedPipeServer
{
    private readonly string PipeName = "xHotspotIpcPipe";
    private readonly ILoggerService _logger;
    private readonly ISettingsService _settingsService;
    private readonly IHotspotManager _hotspotManager;
    private readonly IBluetoothMonitor _bluetoothMonitor;
    private readonly INetworkMonitor _networkMonitor;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public NamedPipeServer(
        ILoggerService logger,
        ISettingsService settingsService,
        IHotspotManager hotspotManager,
        IBluetoothMonitor bluetoothMonitor,
        INetworkMonitor networkMonitor)
    {
        _logger = logger;
        _settingsService = settingsService;
        _hotspotManager = hotspotManager;
        _bluetoothMonitor = bluetoothMonitor;
        _networkMonitor = networkMonitor;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenLoop(_cts.Token));
        _logger.LogInformation("NamedPipeServer started.");
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listenerTask?.Wait(TimeSpan.FromSeconds(3));
        _logger.LogInformation("NamedPipeServer stopped.");
    }

    private async Task ListenLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var ps = new PipeSecurity();
                var sid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
                ps.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));

                using var server = NamedPipeServerStreamEx.Create(PipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Message, PipeOptions.Asynchronous, 4096, 4096, ps);

                await server.WaitForConnectionAsync(cancellationToken);

                _ = Task.Run(() => HandleClientAsync(server), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NAMED PIPE SERVER ERROR] {ex}");
                _logger.LogError("Error in NamedPipe server loop", ex);
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server)
    {
        try
        {
            using var reader = new StreamReader(server);
            using var writer = new StreamWriter(server) { AutoFlush = true };

            var jsonRequest = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(jsonRequest)) return;

            var request = JsonSerializer.Deserialize<IpcRequest>(jsonRequest);
            if (request == null) return;

            var response = await ProcessRequestAsync(request);
            var jsonResponse = JsonSerializer.Serialize(response);
            await writer.WriteLineAsync(jsonResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IPC CLIENT HANDLER ERROR] {ex}");
            _logger.LogError("Error handling IPC client", ex);
        }
    }

    private async Task<IpcResponse> ProcessRequestAsync(IpcRequest request)
    {
        try
        {
            switch (request.Command)
            {
                case IpcCommandType.GetStatus:
                    var status = new StatusDto
                    {
                        ServiceState = ServiceState.HotspotRunning,
                        HotspotStatus = await _hotspotManager.GetStatusAsync(),
                        WifiState = _networkMonitor.CurrentState,
                        WifiSsid = _networkMonitor.CurrentSsid,
                        InternetConnected = _networkMonitor.IsInternetAvailable,
                        PhoneName = _settingsService.LoadSettings().PhoneName,
                        AutomationPaused = _settingsService.LoadSettings().AutomationPaused
                    };
                    return new IpcResponse { Success = true, DataJson = JsonSerializer.Serialize(status) };

                case IpcCommandType.GetSettings:
                    return new IpcResponse { Success = true, DataJson = JsonSerializer.Serialize(_settingsService.LoadSettings()) };

                case IpcCommandType.UpdateSettings:
                    var newSettings = JsonSerializer.Deserialize<AppSettings>(request.PayloadJson);
                    if (newSettings != null)
                    {
                        _settingsService.SaveSettings(newSettings);
                        return new IpcResponse { Success = true };
                    }
                    return new IpcResponse { Success = false, ErrorMessage = "Invalid settings payload" };

                case IpcCommandType.GetDevices:
                    var devices = await _bluetoothMonitor.GetPairedDevicesAsync();
                    return new IpcResponse { Success = true, DataJson = JsonSerializer.Serialize(devices) };

                case IpcCommandType.EnableHotspot:
                    bool en = await _hotspotManager.EnableAsync();
                    return new IpcResponse { Success = en };

                case IpcCommandType.DisableHotspot:
                    bool dis = await _hotspotManager.DisableAsync();
                    return new IpcResponse { Success = dis };

                case IpcCommandType.PauseAutomation:
                    var s1 = _settingsService.LoadSettings();
                    s1.AutomationPaused = true;
                    _settingsService.SaveSettings(s1);
                    return new IpcResponse { Success = true };

                case IpcCommandType.ResumeAutomation:
                    var s2 = _settingsService.LoadSettings();
                    s2.AutomationPaused = false;
                    _settingsService.SaveSettings(s2);
                    return new IpcResponse { Success = true };

                default:
                    return new IpcResponse { Success = false, ErrorMessage = "Unknown command" };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IPC COMMAND ERROR] Command {request.Command}: {ex}");
            _logger.LogError($"IPC Command {request.Command} failed", ex);
            return new IpcResponse { Success = false, ErrorMessage = ex.ToString() };
        }
    }
}

internal static class NamedPipeServerStreamEx
{
    public static NamedPipeServerStream Create(string pipeName, PipeDirection direction, int maxInstances, PipeTransmissionMode transmissionMode, PipeOptions options, int inBufferSize, int outBufferSize, PipeSecurity pipeSecurity)
    {
        return NamedPipeServerStreamAcl.Create(pipeName, direction, maxInstances, transmissionMode, options, inBufferSize, outBufferSize, pipeSecurity);
    }
}

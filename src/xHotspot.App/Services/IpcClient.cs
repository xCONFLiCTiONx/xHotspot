using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using xHotspot.Core.Models;

namespace xHotspot.App.Services;

public class IpcClient
{
    private readonly string PipeName = "xHotspotIpcPipe";

    public async Task<IpcResponse> SendCommandAsync(IpcCommandType command, object? payload = null)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(2000);

            using var writer = new StreamWriter(client) { AutoFlush = true };
            using var reader = new StreamReader(client);

            var req = new IpcRequest
            {
                Command = command,
                PayloadJson = payload != null ? JsonSerializer.Serialize(payload) : string.Empty
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize(req));
            var respJson = await reader.ReadLineAsync();

            if (!string.IsNullOrEmpty(respJson))
            {
                return JsonSerializer.Deserialize<IpcResponse>(respJson) ?? new IpcResponse { Success = false, ErrorMessage = "Deserialization failed" };
            }
        }
        catch (Exception ex)
        {
            return new IpcResponse { Success = false, ErrorMessage = ex.Message };
        }
        return new IpcResponse { Success = false, ErrorMessage = "No response from service" };
    }
}

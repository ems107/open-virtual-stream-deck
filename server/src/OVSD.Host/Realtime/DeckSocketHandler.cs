using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OVSD.Core.Protocol;

namespace OVSD.Host.Realtime;

public sealed class DeckSocketHandler(IOptions<OvsdOptions> options, ILogger<DeckSocketHandler> logger)
{
    private const int MaxMessageBytes = 64 * 1024;

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var remote = context.Connection.RemoteIpAddress;
        var ct = context.RequestAborted;
        logger.LogInformation("Client connected from {Remote}", remote);

        var buffer = new byte[MaxMessageBytes];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var length = await ReceiveAsync(socket, buffer, ct);
                if (length is null) break;

                ClientMessage? message;
                try
                {
                    message = JsonSerializer.Deserialize<ClientMessage>(buffer.AsSpan(0, length.Value), ProtocolJson.Options);
                }
                catch (JsonException ex)
                {
                    await SendAsync(socket, new ErrorMessage("bad_message", ex.Message), ct);
                    continue;
                }
                if (message is not null) await DispatchAsync(socket, message, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "WebSocket closed abruptly for {Remote}", remote);
        }

        logger.LogInformation("Client disconnected from {Remote}", remote);
    }

    private async Task DispatchAsync(WebSocket socket, ClientMessage message, CancellationToken ct)
    {
        switch (message)
        {
            case HelloMessage hello when hello.ProtocolVersion != ProtocolInfo.Version:
                await SendAsync(socket, new ErrorMessage("protocol_mismatch",
                    $"Server speaks protocol {ProtocolInfo.Version}, client sent {hello.ProtocolVersion}"), ct);
                break;
            case HelloMessage hello:
                logger.LogInformation("Hello from {Device}", hello.Device.Name);
                await SendAsync(socket, new WelcomeMessage(ProtocolInfo.Version, options.Value.ServerName,
                    typeof(DeckSocketHandler).Assembly.GetName().Version?.ToString() ?? "0.0.0"), ct);
                break;
        }
    }

    /// <summary>Reads one full text message into <paramref name="buffer"/>. Returns null when the socket closes.</summary>
    private static async Task<int?> ReceiveAsync(WebSocket socket, byte[] buffer, CancellationToken ct)
    {
        var count = 0;
        while (true)
        {
            if (count == buffer.Length)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, null, ct);
                return null;
            }
            var result = await socket.ReceiveAsync(buffer.AsMemory(count), ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                return null;
            }
            count += result.Count;
            if (result.EndOfMessage) return count;
        }
    }

    private static Task SendAsync(WebSocket socket, ServerMessage message, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJson.Options);
        return socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }
}

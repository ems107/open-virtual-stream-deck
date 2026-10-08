using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OVSD.Core.Protocol;
using OVSD.Core.Runtime;
using OVSD.Core.Storage;
using OVSD.Host.Security;

namespace OVSD.Host.Realtime;

/// <summary>
/// One WebSocket per client. The first message must be "hello"; after that the runtime's outbox is
/// pumped to the socket while incoming gestures are forwarded to the runtime.
/// </summary>
public sealed class DeckSocketHandler(
    IOptions<OvsdOptions> options,
    DeckRuntime runtime,
    DeviceAuth auth,
    ConfigRepository config,
    ILogger<DeckSocketHandler> logger)
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
        var ct = context.RequestAborted;
        var isLocal = DeviceAuth.IsLocal(context);
        var buffer = new byte[MaxMessageBytes];
        DeckSession? session = null;

        try
        {
            var hello = await ReadHelloAsync(socket, buffer, ct);
            if (hello is null) return;

            var device = auth.FindByToken(hello.Token);
            if (device is null && !isLocal)
            {
                await SendAsync(socket, new ErrorMessage("unpaired", "This device is not paired with the server"), ct);
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "unpaired", ct);
                return;
            }
            if (device is not null)
                config.UpdateDevice(device.Id, d => d with { LastSeen = DateTimeOffset.UtcNow });

            await SendAsync(socket, new WelcomeMessage(
                ProtocolInfo.Version,
                options.Value.ServerName,
                AppInfo.Version,
                device?.Id,
                device?.Name,
                isLocal,
                config.Settings.Deck), ct);

            session = runtime.Attach(device, device?.Name ?? hello.Device.Name, isLocal, hello.Role);
            var writer = PumpOutboxAsync(socket, session, ct);
            await ReadLoopAsync(socket, session, buffer, ct);
            runtime.Detach(session);
            await writer;
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "WebSocket closed abruptly");
        }
        finally
        {
            if (session is not null) runtime.Detach(session);
        }
    }

    private async Task<HelloMessage?> ReadHelloAsync(WebSocket socket, byte[] buffer, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var length = await ReceiveAsync(socket, buffer, timeout.Token);
        if (length is null) return null;

        var message = TryParse(buffer, length.Value);
        switch (message)
        {
            case HelloMessage { ProtocolVersion: ProtocolInfo.Version } hello:
                return hello;
            case HelloMessage hello:
                await SendAsync(socket, new ErrorMessage("protocol_mismatch",
                    $"Server speaks protocol {ProtocolInfo.Version}, client sent {hello.ProtocolVersion}. Reload the page."), ct);
                break;
            default:
                await SendAsync(socket, new ErrorMessage("bad_message", "Expected hello"), ct);
                break;
        }
        await socket.CloseAsync(WebSocketCloseStatus.ProtocolError, null, ct);
        return null;
    }

    private async Task ReadLoopAsync(WebSocket socket, DeckSession session, byte[] buffer, CancellationToken ct)
    {
        while (socket.State == WebSocketState.Open)
        {
            var length = await ReceiveAsync(socket, buffer, ct);
            if (length is null) return;
            switch (TryParse(buffer, length.Value))
            {
                case InputMessage input:
                    runtime.HandleInput(session, input);
                    break;
                case NavigateMessage navigate:
                    runtime.Navigate(session, navigate);
                    break;
                case SelectProfileMessage select:
                    runtime.SelectProfile(session, select.ProfileId);
                    break;
                case null:
                    session.Notify("Malformed message", Core.Actions.NotifyLevel.Warning);
                    break;
            }
        }
    }

    private static async Task PumpOutboxAsync(WebSocket socket, DeckSession session, CancellationToken ct)
    {
        try
        {
            await foreach (var message in session.Outbox.ReadAllAsync(ct))
            {
                if (socket.State != WebSocketState.Open) break;
                await SendAsync(socket, message, ct);
            }
            // Outbox completed by the runtime (e.g. device revoked): close politely.
            if (socket.State == WebSocketState.Open)
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException) { }
    }

    private ClientMessage? TryParse(byte[] buffer, int length)
    {
        try
        {
            return JsonSerializer.Deserialize<ClientMessage>(buffer.AsSpan(0, length), ProtocolJson.Options);
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Malformed client message");
            return null;
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
                if (socket.State == WebSocketState.CloseReceived)
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
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

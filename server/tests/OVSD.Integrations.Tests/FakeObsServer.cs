using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using OVSD.Integrations.Obs;

namespace OVSD.Integrations.Tests;

/// <summary>Minimal obs-websocket v5 server: password auth, a couple of requests and event pushing.</summary>
public sealed class FakeObsServer : IAsyncDisposable
{
    private const string Salt = "lM1GncleQOaCu9lT1yeUZhFYnqhsLLP1G5lAGo3ixaI=";
    private const string Challenge = "+IxH4CnCiqpX1rM9scsNynZzbOe4KhDeYcTNS3PDaeY=";

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly string _password;
    private WebSocket? _socket;

    public ConcurrentQueue<JsonNode> Requests { get; } = new();
    public string CurrentScene { get; private set; } = "Main";
    public int Port { get; }
    public string Url => $"ws://127.0.0.1:{Port}/";

    public FakeObsServer(string password)
    {
        _password = password;
        Port = FreePort();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = AcceptLoop();
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { return; }
            var ws = await ctx.AcceptWebSocketAsync("obswebsocket.json");
            _socket = ws.WebSocket;
            _ = Serve(ws.WebSocket);
        }
    }

    private async Task Serve(WebSocket socket)
    {
        await Send(socket, new JsonObject
        {
            ["op"] = 0,
            ["d"] = new JsonObject
            {
                ["obsWebSocketVersion"] = "5.5.0",
                ["rpcVersion"] = 1,
                ["authentication"] = new JsonObject { ["challenge"] = Challenge, ["salt"] = Salt },
            },
        });

        var identify = await Receive(socket);
        if (identify?["d"]?["authentication"]?.GetValue<string>() != ObsClient.AuthResponse(_password, Salt, Challenge))
        {
            await socket.CloseAsync((WebSocketCloseStatus)4009, "Authentication failed", CancellationToken.None);
            return;
        }
        await Send(socket, new JsonObject { ["op"] = 2, ["d"] = new JsonObject { ["negotiatedRpcVersion"] = 1 } });

        while (await Receive(socket) is { } message)
        {
            var d = message["d"]!;
            Requests.Enqueue(d.DeepClone());
            var type = d["requestType"]!.GetValue<string>();
            JsonObject? data = null;
            var ok = true;
            switch (type)
            {
                case "GetCurrentProgramScene":
                    data = new JsonObject { ["currentProgramSceneName"] = CurrentScene };
                    break;
                case "GetStreamStatus":
                    data = new JsonObject { ["outputActive"] = true };
                    break;
                case "GetSceneList":
                    data = new JsonObject { ["scenes"] = new JsonArray(new JsonObject { ["sceneName"] = "Main" }, new JsonObject { ["sceneName"] = "BRB" }) };
                    break;
                case "SetCurrentProgramScene":
                    CurrentScene = d["requestData"]!["sceneName"]!.GetValue<string>();
                    _ = PushEvent("CurrentProgramSceneChanged", new JsonObject { ["sceneName"] = CurrentScene });
                    break;
                default:
                    ok = false;
                    break;
            }
            await Send(socket, new JsonObject
            {
                ["op"] = 7,
                ["d"] = new JsonObject
                {
                    ["requestType"] = type,
                    ["requestId"] = d["requestId"]!.GetValue<string>(),
                    ["requestStatus"] = new JsonObject { ["result"] = ok, ["code"] = ok ? 100 : 204, ["comment"] = ok ? null : "unsupported in fake" },
                    ["responseData"] = data,
                },
            });
        }
    }

    public Task PushEvent(string type, JsonObject data) =>
        _socket is null ? Task.CompletedTask : Send(_socket, new JsonObject { ["op"] = 5, ["d"] = new JsonObject { ["eventType"] = type, ["eventData"] = data } });

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private async Task Send(WebSocket socket, JsonNode node)
    {
        await _sendLock.WaitAsync();
        try { await socket.SendAsync(Encoding.UTF8.GetBytes(node.ToJsonString()), WebSocketMessageType.Text, true, CancellationToken.None); }
        finally { _sendLock.Release(); }
    }

    private static async Task<JsonNode?> Receive(WebSocket socket)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            return result.MessageType == WebSocketMessageType.Close ? null : JsonNode.Parse(buffer.AsSpan(0, result.Count));
        }
        catch (WebSocketException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Close();
    }
}

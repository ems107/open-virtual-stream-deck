using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OVSD.Core.Actions;
using OVSD.Core.Integrations;
using OVSD.Core.Model;
using OVSD.Core.Storage;
using OVSD.Core.Variables;

namespace OVSD.Integrations.Obs;

/// <summary>
/// obs-websocket v5 client (built into OBS 28+). Publishes obs.* variables and executes requests.
/// Protocol: https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md
/// </summary>
public sealed class ObsClient(ConfigRepository config, VariableStore variables, ILogger<ObsClient> logger)
    : ConnectionLoop<ObsSettings>(config, logger)
{
    private const int EventSubscriptionsAll = 2047;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<(string Scene, int ItemId), string> _sceneItems = new();
    private ClientWebSocket? _socket;

    public override string Id => "obs";
    public override string Name => "OBS Studio";
    public bool IsConnected => _socket is { State: WebSocketState.Open };

    protected override ObsSettings Select(AppSettings settings) => settings.Obs;
    protected override bool IsEnabled(ObsSettings settings) => settings.Enabled;

    protected override async Task RunConnectedAsync(ObsSettings settings, CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol("obswebsocket.json");
        await socket.ConnectAsync(new Uri(settings.Url), ct);

        var hello = await ReceiveAsync(socket, ct) ?? throw new IOException("OBS closed the connection");
        var identify = new JsonObject { ["rpcVersion"] = 1, ["eventSubscriptions"] = EventSubscriptionsAll };
        if (hello["d"]?["authentication"] is JsonObject auth)
        {
            if (string.IsNullOrEmpty(settings.Password)) throw new InvalidOperationException("OBS requires a password");
            identify["authentication"] = AuthResponse(settings.Password, auth["salt"]!.GetValue<string>(), auth["challenge"]!.GetValue<string>());
        }
        await SendRawAsync(socket, new JsonObject { ["op"] = 1, ["d"] = identify }, ct);

        var identified = await ReceiveAsync(socket, ct);
        if (identified?["op"]?.GetValue<int>() != 2)
            throw new InvalidOperationException(socket.CloseStatus == (WebSocketCloseStatus)4009
                ? "Wrong OBS password"
                : $"OBS refused the connection ({socket.CloseStatusDescription})");

        _socket = socket;
        SetStatus(IntegrationState.Connected);
        variables.Set("obs.connected", true);
        logger.LogInformation("Connected to OBS at {Url}", settings.Url);

        var reader = ReadLoopAsync(socket, ct);
        await LoadInitialStateAsync(ct);
        await reader;
    }

    protected override void OnDisconnected()
    {
        _socket = null;
        foreach (var pending in _pending.Values) pending.TrySetException(new ActionException("OBS disconnected"));
        _pending.Clear();
        _sceneItems.Clear();
        variables.Set("obs.connected", false);
        foreach (var flag in new[] { "obs.streaming", "obs.recording", "obs.recordPaused", "obs.replay", "obs.virtualcam" })
            variables.Set(flag, false);
    }

    // ------------------------------------------------------------------ requests

    public async Task<JsonElement> RequestAsync(string requestType, object? data = null, CancellationToken ct = default)
    {
        var socket = _socket;
        if (socket is null) throw new ActionException("OBS is not connected (enable it in Settings → OBS)");

        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        try
        {
            var payload = new JsonObject
            {
                ["op"] = 6,
                ["d"] = new JsonObject
                {
                    ["requestType"] = requestType,
                    ["requestId"] = id,
                    ["requestData"] = data is null ? null : JsonSerializer.SerializeToNode(data),
                },
            };
            await SendRawAsync(socket, payload, ct);
            return await tcs.Task.WaitAsync(RequestTimeout, ct);
        }
        catch (TimeoutException)
        {
            throw new ActionException($"OBS did not answer {requestType}");
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task ReadLoopAsync(ClientWebSocket socket, CancellationToken ct)
    {
        while (await ReceiveAsync(socket, ct) is { } message)
        {
            var d = message["d"];
            switch (message["op"]?.GetValue<int>())
            {
                case 5:
                    try
                    {
                        await HandleEventAsync(d!["eventType"]!.GetValue<string>(), d["eventData"] as JsonObject ?? []);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "OBS event handling failed");
                    }
                    break;
                case 7:
                    var requestId = d?["requestId"]?.GetValue<string>();
                    if (requestId is null || !_pending.TryGetValue(requestId, out var tcs)) break;
                    var status = d!["requestStatus"];
                    if (status?["result"]?.GetValue<bool>() == true)
                        tcs.TrySetResult(JsonSerializer.SerializeToElement(d["responseData"] ?? new JsonObject()));
                    else
                        tcs.TrySetException(new ActionException(
                            $"OBS: {status?["comment"]?.GetValue<string>() ?? "request failed"} ({d["requestType"]})"));
                    break;
            }
        }
    }

    // ------------------------------------------------------------------ state

    private async Task LoadInitialStateAsync(CancellationToken ct)
    {
        async Task Try(Func<Task> load)
        {
            try { await load(); }
            catch (ActionException) { }
        }

        await Try(async () => variables.Set("obs.scene", (await RequestAsync("GetCurrentProgramScene", null, ct)).GetProperty("currentProgramSceneName").GetString()));
        await Try(async () => variables.Set("obs.streaming", (await RequestAsync("GetStreamStatus", null, ct)).GetProperty("outputActive").GetBoolean()));
        await Try(async () =>
        {
            var record = await RequestAsync("GetRecordStatus", null, ct);
            variables.Set("obs.recording", record.GetProperty("outputActive").GetBoolean());
            variables.Set("obs.recordPaused", record.GetProperty("outputPaused").GetBoolean());
        });
        await Try(async () => variables.Set("obs.replay", (await RequestAsync("GetReplayBufferStatus", null, ct)).GetProperty("outputActive").GetBoolean()));
        await Try(async () => variables.Set("obs.virtualcam", (await RequestAsync("GetVirtualCamStatus", null, ct)).GetProperty("outputActive").GetBoolean()));
        await Try(async () => variables.Set("obs.studio", (await RequestAsync("GetStudioModeEnabled", null, ct)).GetProperty("studioModeEnabled").GetBoolean()));
        await Try(() => LoadInputsAsync(ct));
        await Try(() => LoadSceneItemsAsync(ct));
    }

    private async Task LoadInputsAsync(CancellationToken ct)
    {
        foreach (var input in (await RequestAsync("GetInputList", null, ct)).GetProperty("inputs").EnumerateArray())
        {
            var name = input.GetProperty("inputName").GetString()!;
            try
            {
                var mute = await RequestAsync("GetInputMute", new { inputName = name }, ct);
                variables.Set($"obs.mute.{name}", mute.GetProperty("inputMuted").GetBoolean());
                var volume = await RequestAsync("GetInputVolume", new { inputName = name }, ct);
                variables.Set($"obs.volume.{name}", Math.Round(volume.GetProperty("inputVolumeMul").GetDouble() * 100));
            }
            catch (ActionException)
            {
                // Not an audio input.
            }
        }
    }

    private async Task LoadSceneItemsAsync(CancellationToken ct)
    {
        foreach (var scene in await SceneNamesAsync(ct)) await LoadSceneAsync(scene, ct);
    }

    private async Task LoadSceneAsync(string scene, CancellationToken ct)
    {
        var items = await RequestAsync("GetSceneItemList", new { sceneName = scene }, ct);
        foreach (var item in items.GetProperty("sceneItems").EnumerateArray())
        {
            var source = item.GetProperty("sourceName").GetString()!;
            _sceneItems[(scene, item.GetProperty("sceneItemId").GetInt32())] = source;
            variables.Set($"obs.visible.{scene}.{source}", item.GetProperty("sceneItemEnabled").GetBoolean());
        }
    }

    private async Task HandleEventAsync(string type, JsonObject data)
    {
        switch (type)
        {
            case "CurrentProgramSceneChanged":
                variables.Set("obs.scene", data["sceneName"]?.GetValue<string>());
                break;
            case "CurrentPreviewSceneChanged":
                variables.Set("obs.preview", data["sceneName"]?.GetValue<string>());
                break;
            case "StreamStateChanged":
                variables.Set("obs.streaming", data["outputActive"]?.GetValue<bool>());
                break;
            case "RecordStateChanged":
                variables.Set("obs.recording", data["outputActive"]?.GetValue<bool>());
                variables.Set("obs.recordPaused", data["outputState"]?.GetValue<string>() == "OBS_WEBSOCKET_OUTPUT_PAUSED");
                break;
            case "ReplayBufferStateChanged":
                variables.Set("obs.replay", data["outputActive"]?.GetValue<bool>());
                break;
            case "VirtualcamStateChanged":
                variables.Set("obs.virtualcam", data["outputActive"]?.GetValue<bool>());
                break;
            case "StudioModeStateChanged":
                variables.Set("obs.studio", data["studioModeEnabled"]?.GetValue<bool>());
                break;
            case "InputMuteStateChanged":
                variables.Set($"obs.mute.{data["inputName"]}", data["inputMuted"]?.GetValue<bool>());
                break;
            case "InputVolumeChanged":
                variables.Set($"obs.volume.{data["inputName"]}", Math.Round((data["inputVolumeMul"]?.GetValue<double>() ?? 0) * 100));
                break;
            case "SceneItemEnableStateChanged":
                var scene = data["sceneName"]!.GetValue<string>();
                var id = data["sceneItemId"]!.GetValue<int>();
                if (!_sceneItems.ContainsKey((scene, id))) await LoadSceneAsync(scene, CancellationToken.None);
                if (_sceneItems.TryGetValue((scene, id), out var source))
                    variables.Set($"obs.visible.{scene}.{source}", data["sceneItemEnabled"]?.GetValue<bool>());
                break;
            case "SceneItemCreated" or "SceneItemRemoved" or "SceneListChanged" or "InputNameChanged":
                variables.RemovePrefix("obs.visible.");
                _sceneItems.Clear();
                await LoadSceneItemsAsync(CancellationToken.None);
                break;
            case "InputCreated" or "InputRemoved":
                variables.RemovePrefix("obs.mute.");
                variables.RemovePrefix("obs.volume.");
                await LoadInputsAsync(CancellationToken.None);
                break;
        }
    }

    public async Task<IReadOnlyList<string>> SceneNamesAsync(CancellationToken ct) =>
        (await RequestAsync("GetSceneList", null, ct)).GetProperty("scenes").EnumerateArray()
            .Select(s => s.GetProperty("sceneName").GetString()!).Reverse().ToList();

    public async Task<IReadOnlyList<string>> InputNamesAsync(CancellationToken ct) =>
        (await RequestAsync("GetInputList", null, ct)).GetProperty("inputs").EnumerateArray()
            .Select(s => s.GetProperty("inputName").GetString()!).ToList();

    public IReadOnlyList<string> SourceNames => _sceneItems.Values.Distinct().Order().ToList();

    // ------------------------------------------------------------------ wire

    public static string AuthResponse(string password, string salt, string challenge)
    {
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    private async Task SendRawAsync(ClientWebSocket socket, JsonNode payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
        await _sendLock.WaitAsync(ct);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static async Task<JsonNode?> ReceiveAsync(ClientWebSocket socket, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var result = await socket.ReceiveAsync(chunk, ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            buffer.Write(chunk, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        return JsonNode.Parse(buffer.ToArray());
    }
}

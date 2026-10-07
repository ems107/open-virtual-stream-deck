using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OVSD.Core.Actions;
using OVSD.Core.Expressions;
using OVSD.Core.Integrations;
using OVSD.Core.Model;
using OVSD.Core.Storage;
using OVSD.Core.Variables;

namespace OVSD.Integrations.Discord;

/// <summary>
/// Discord local RPC over the desktop client's named pipe. Voice control (mute/deafen) needs an application of
/// your own at https://discord.com/developers (client id + secret, redirect URI "http://localhost"); the first
/// connection asks for permission inside Discord. Without it, use keyboard actions bound to Discord's own hotkeys.
/// </summary>
public sealed class DiscordRpcClient(ConfigRepository config, VariableStore variables, ILogger<DiscordRpcClient> logger)
    : ConnectionLoop<DiscordSettings>(config, logger), IActionProvider
{
    private enum Op { Handshake = 0, Frame = 1, Close = 2, Ping = 3, Pong = 4 }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string[] Scopes = ["rpc", "rpc.voice.read", "rpc.voice.write"];

    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonNode?>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private NamedPipeClientStream? _pipe;

    public override string Id => "discord";
    public override string Name => "Discord";

    protected override DiscordSettings Select(AppSettings settings) => settings.Discord;
    protected override bool IsEnabled(DiscordSettings s) =>
        s.Enabled && !string.IsNullOrWhiteSpace(s.ClientId) && !string.IsNullOrWhiteSpace(s.ClientSecret);
    // Tokens are written back by this class; they must not trigger a reconnect.
    protected override object ConnectionKey(DiscordSettings s) => $"{s.Enabled}|{s.ClientId}|{s.ClientSecret}|{s.RedirectUri}";

    protected override async Task RunConnectedAsync(DiscordSettings settings, CancellationToken ct)
    {
        await using var pipe = await ConnectPipeAsync(ct);
        _pipe = pipe;
        try
        {
            await WriteAsync(Op.Handshake, new JsonObject { ["v"] = 1, ["client_id"] = settings.ClientId }, ct);
            var ready = await ReadAsync(ct);
            if (ready.Op != Op.Frame || ready.Payload?["evt"]?.GetValue<string>() != "READY")
                throw new InvalidOperationException("Discord rejected the handshake: " + ready.Payload?["message"]);

            var reader = ReadLoopAsync(ct);
            var token = await GetAccessTokenAsync(settings, ct);
            await CommandAsync("AUTHENTICATE", new JsonObject { ["access_token"] = token }, ct);
            PublishVoice(await CommandAsync("GET_VOICE_SETTINGS", new JsonObject(), ct));
            await CommandAsync("SUBSCRIBE", new JsonObject(), ct, evt: "VOICE_SETTINGS_UPDATE");

            SetStatus(IntegrationState.Connected);
            variables.Set("discord.connected", true);
            await reader;
        }
        finally
        {
            _pipe = null;
            foreach (var pending in _pending.Values) pending.TrySetException(new ActionException("Discord disconnected"));
            _pending.Clear();
        }
    }

    protected override void OnDisconnected() => variables.Set("discord.connected", false);

    private static async Task<NamedPipeClientStream> ConnectPipeAsync(CancellationToken ct)
    {
        for (var i = 0; i < 10; i++)
        {
            var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(500, ct);
                return pipe;
            }
            catch (TimeoutException)
            {
                await pipe.DisposeAsync();
            }
        }
        throw new InvalidOperationException("Discord desktop app is not running");
    }

    // ------------------------------------------------------------------ OAuth

    private async Task<string> GetAccessTokenAsync(DiscordSettings settings, CancellationToken ct)
    {
        if (settings.AccessToken is { } token && settings.TokenExpires > DateTimeOffset.UtcNow.AddMinutes(5)) return token;

        if (settings.RefreshToken is { } refresh)
        {
            try
            {
                return await ExchangeAsync(settings, new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh }, ct);
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex, "Discord token refresh failed; asking for authorization again");
            }
        }

        SetStatus(IntegrationState.Connecting, "Approve the authorization request in Discord");
        var authorize = await CommandAsync("AUTHORIZE", new JsonObject
        {
            ["client_id"] = settings.ClientId,
            ["scopes"] = new JsonArray(Scopes.Select(s => (JsonNode)s).ToArray()),
        }, ct, timeout: TimeSpan.FromMinutes(2));
        var code = authorize?["code"]?.GetValue<string>() ?? throw new InvalidOperationException("Authorization was not granted");
        return await ExchangeAsync(settings, new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = settings.RedirectUri ?? "http://localhost",
        }, ct);
    }

    private async Task<string> ExchangeAsync(DiscordSettings settings, Dictionary<string, string> form, CancellationToken ct)
    {
        form["client_id"] = settings.ClientId!;
        form["client_secret"] = settings.ClientSecret!;
        using var response = await Http.PostAsync("https://discord.com/api/oauth2/token", new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
        if (!response.IsSuccessStatusCode || body?["access_token"] is null)
            throw new InvalidOperationException($"Discord OAuth failed: {body?["error_description"] ?? body?["error"] ?? response.StatusCode.ToString()}");

        var access = body["access_token"]!.GetValue<string>();
        var refresh = body["refresh_token"]?.GetValue<string>();
        var expires = DateTimeOffset.UtcNow.AddSeconds(body["expires_in"]?.GetValue<double>() ?? 3600);
        Config.UpdateSettings(s => s with { Discord = s.Discord with { AccessToken = access, RefreshToken = refresh, TokenExpires = expires } });
        return access;
    }

    // ------------------------------------------------------------------ RPC

    private async Task<JsonNode?> CommandAsync(string command, JsonObject args, CancellationToken ct, string? evt = null, TimeSpan? timeout = null)
    {
        if (_pipe is null) throw new ActionException("Discord is not connected (enable it in Settings → Discord)");
        var nonce = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[nonce] = tcs;
        try
        {
            var payload = new JsonObject { ["cmd"] = command, ["args"] = args, ["nonce"] = nonce };
            if (evt is not null) payload["evt"] = evt;
            await WriteAsync(Op.Frame, payload, ct);
            return await tcs.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(10), ct);
        }
        catch (TimeoutException)
        {
            throw new ActionException($"Discord did not answer {command}");
        }
        finally
        {
            _pending.TryRemove(nonce, out _);
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        while (true)
        {
            var (op, payload) = await ReadAsync(ct);
            if (op == Op.Close) throw new IOException("Discord closed the connection: " + payload?["message"]);
            if (op == Op.Ping)
            {
                await WriteAsync(Op.Pong, payload ?? new JsonObject(), ct);
                continue;
            }
            if (op != Op.Frame || payload is null) continue;

            var evt = payload["evt"]?.GetValue<string>();
            var nonce = payload["nonce"]?.GetValue<string>();
            if (nonce is not null && _pending.TryGetValue(nonce, out var tcs))
            {
                if (evt == "ERROR") tcs.TrySetException(new ActionException("Discord: " + payload["data"]?["message"]));
                else tcs.TrySetResult(payload["data"]);
            }
            else if (evt == "VOICE_SETTINGS_UPDATE")
            {
                PublishVoice(payload["data"]);
            }
        }
    }

    private void PublishVoice(JsonNode? data)
    {
        if (data is null) return;
        variables.Set("discord.mute", data["mute"]?.GetValue<bool>());
        variables.Set("discord.deaf", data["deaf"]?.GetValue<bool>());
    }

    private async Task WriteAsync(Op op, JsonNode payload, CancellationToken ct)
    {
        var pipe = _pipe ?? throw new ActionException("Discord is not connected");
        var json = Encoding.UTF8.GetBytes(payload.ToJsonString());
        var frame = new byte[8 + json.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, (int)op);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4), json.Length);
        json.CopyTo(frame, 8);
        await _writeLock.WaitAsync(ct);
        try
        {
            await pipe.WriteAsync(frame, ct);
            await pipe.FlushAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<(Op Op, JsonNode? Payload)> ReadAsync(CancellationToken ct)
    {
        var pipe = _pipe ?? throw new IOException("Pipe closed");
        var header = new byte[8];
        await pipe.ReadExactlyAsync(header, ct);
        var op = (Op)BinaryPrimitives.ReadInt32LittleEndian(header);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        if (length is < 0 or > 1024 * 1024) throw new IOException("Invalid Discord frame");
        var body = new byte[length];
        await pipe.ReadExactlyAsync(body, ct);
        return (op, length == 0 ? null : JsonNode.Parse(body));
    }

    // ------------------------------------------------------------------ actions

    private async Task SetVoice(string field, ActionArgs args, ActionContext ctx, CancellationToken ct)
    {
        var value = args.Mode() ?? !Values.IsTruthy(ctx.Variables.Get($"discord.{field}"));
        PublishVoice(await CommandAsync("SET_VOICE_SETTINGS", new JsonObject { [field] = value }, ct));
    }

    private static readonly ParamDescriptor ModeParam = new()
    {
        Name = "mode", Label = "Mode", Type = ParamType.Select, Default = "toggle",
        Options = [new("toggle", "Toggle"), new("on", "On"), new("off", "Off")],
    };

    public IEnumerable<IActionHandler> GetActions() =>
    [
        new DelegateAction(new ActionDescriptor
        {
            Id = "discord.mute", Category = "discord", Name = "Discord mute", Icon = "mdi:microphone-off",
            Description = "Needs the Discord integration (Settings). Alternatively bind a hotkey to Discord's own shortcut.",
            Params = [ModeParam],
        }, (ctx, args, ct) => SetVoice("mute", args, ctx, ct)),
        new DelegateAction(new ActionDescriptor
        {
            Id = "discord.deafen", Category = "discord", Name = "Discord deafen", Icon = "mdi:headphones-off",
            Params = [ModeParam],
        }, (ctx, args, ct) => SetVoice("deaf", args, ctx, ct)),
    ];
}

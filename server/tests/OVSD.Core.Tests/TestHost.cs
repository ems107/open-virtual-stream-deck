using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OVSD.Core.Platform;
using OVSD.Core.Protocol;
using OVSD.Core.Runtime;

namespace OVSD.Core.Tests;

/// <summary>Records platform calls instead of touching the real machine.</summary>
public sealed class FakePlatform : IKeyboard, IMediaController, IForegroundWatcher, IAudioController
{
    public List<string> Calls { get; } = [];
    private readonly Lock _lock = new();

    private void Record(string call)
    {
        lock (_lock) Calls.Add(call);
    }

    public List<string> Snapshot()
    {
        lock (_lock) return [.. Calls];
    }

    public Task SendChordAsync(IReadOnlyList<string> keys, CancellationToken ct)
    {
        Record("chord " + string.Join("+", keys));
        return Task.CompletedTask;
    }

    public void KeyDown(IReadOnlyList<string> keys) => Record("down " + string.Join("+", keys));
    public void KeyUp(IReadOnlyList<string> keys) => Record("up " + string.Join("+", keys));

    public Task TypeTextAsync(string text, int delayMs, CancellationToken ct)
    {
        Record("type " + text);
        return Task.CompletedTask;
    }

    public Task PlayPauseAsync() { Record("playpause"); return Task.CompletedTask; }
    public Task NextAsync() { Record("next"); return Task.CompletedTask; }
    public Task PreviousAsync() { Record("previous"); return Task.CompletedTask; }
    public Task StopAsync() { Record("stop"); return Task.CompletedTask; }

    public double Volume { get; set; } = 50;
    public double? GetVolume(AudioTarget target, string? app) => Volume;
    public void SetVolume(AudioTarget target, string? app, double volume) { Volume = volume; Record($"volume {target} {volume}"); }
    public bool? IsMuted(AudioTarget target, string? app) => false;
    public void SetMute(AudioTarget target, string? app, bool? mute) => Record($"mute {target} {mute?.ToString() ?? "toggle"}");
    public IReadOnlyList<AudioDevice> GetDevices(AudioFlow flow) => [];
    public void SetDefaultDevice(string deviceId) => Record("device " + deviceId);
    public IReadOnlyList<string> GetSessionApps() => ["spotify"];

    public ForegroundApp? Current { get; private set; }
    public event Action<ForegroundApp>? Changed;

    public void Focus(string process, string title = "")
    {
        Current = new ForegroundApp(process, title);
        Changed?.Invoke(Current);
    }
}

public sealed class TestHost : IDisposable
{
    private readonly TempDataDir _dir = new();
    public ServiceProvider Services { get; }
    public FakePlatform Platform { get; } = new();

    public TestHost(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IKeyboard>(Platform);
        services.AddSingleton<IMediaController>(Platform);
        services.AddSingleton<IForegroundWatcher>(Platform);
        services.AddSingleton<IAudioController>(Platform);
        services.AddOvsdCore(_dir.Paths.Root);
        configure?.Invoke(services);
        Services = services.BuildServiceProvider();
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public void Dispose()
    {
        Services.Dispose();
        _dir.Dispose();
    }

    /// <summary>Reads every message queued for a session so far.</summary>
    public static List<ServerMessage> Drain(DeckSession session)
    {
        var messages = new List<ServerMessage>();
        while (session.Outbox.TryRead(out var m)) messages.Add(m);
        return messages;
    }

    /// <summary>Waits until the condition holds (background macro runs are asynchronous).</summary>
    public static async Task Eventually(Func<bool> condition, int timeoutMs = 2000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("Condition not met in time");
            await Task.Delay(10);
        }
    }
}

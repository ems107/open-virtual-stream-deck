namespace OVSD.Core.Platform;

/// <summary>Fallback used where no native implementation exists (non-Windows hosts, tests).</summary>
public sealed class UnsupportedPlatform : IKeyboard, IMediaController, IAudioController, IForegroundWatcher
{
    private static PlatformNotSupportedException Fail() => new("Not supported on this platform");

    public Task SendChordAsync(IReadOnlyList<string> keys, CancellationToken ct) => throw Fail();
    public void KeyDown(IReadOnlyList<string> keys) => throw Fail();
    public void KeyUp(IReadOnlyList<string> keys) => throw Fail();
    public Task TypeTextAsync(string text, int delayMs, CancellationToken ct) => throw Fail();

    public Task PlayPauseAsync() => throw Fail();
    public Task NextAsync() => throw Fail();
    public Task PreviousAsync() => throw Fail();
    public Task StopAsync() => throw Fail();

    public double? GetVolume(AudioTarget target, string? app) => null;
    public void SetVolume(AudioTarget target, string? app, double volume) => throw Fail();
    public bool? IsMuted(AudioTarget target, string? app) => null;
    public void SetMute(AudioTarget target, string? app, bool? mute) => throw Fail();
    public IReadOnlyList<AudioDevice> GetDevices(AudioFlow flow) => [];
    public void SetDefaultDevice(string deviceId) => throw Fail();
    public IReadOnlyList<string> GetSessionApps() => [];

    public ForegroundApp? Current => null;
    public event Action<ForegroundApp>? Changed { add { } remove { } }
}

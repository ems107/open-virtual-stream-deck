namespace OVSD.Core.Platform;

/// <summary>Synthesizes keyboard input on the host. Keys use the canonical names in <see cref="Keys"/>.</summary>
public interface IKeyboard
{
    /// <summary>Presses the keys in order and releases them in reverse order.</summary>
    Task SendChordAsync(IReadOnlyList<string> keys, CancellationToken ct);
    void KeyDown(IReadOnlyList<string> keys);
    void KeyUp(IReadOnlyList<string> keys);
    Task TypeTextAsync(string text, int delayMs, CancellationToken ct);
}

public sealed record CommandResult(int ExitCode, string Output, string Error);

public enum CommandShell { PowerShell, Cmd, None }

public interface IProcessLauncher
{
    void Launch(string path, string? arguments, string? workingDirectory);
    void OpenUrl(string url);
    Task<CommandResult> RunAsync(CommandShell shell, string command, TimeSpan timeout, bool hidden, CancellationToken ct);
}

public interface IMediaController
{
    Task PlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
    Task StopAsync();
}

public enum AudioTarget { Master, Mic, App }

public enum AudioFlow { Output, Input }

public sealed record AudioDevice(string Id, string Name, bool IsDefault);

public interface IAudioController
{
    /// <summary>Volume 0..100, or null if the target does not exist.</summary>
    double? GetVolume(AudioTarget target, string? app);
    void SetVolume(AudioTarget target, string? app, double volume);
    bool? IsMuted(AudioTarget target, string? app);
    /// <summary>Sets mute; null toggles.</summary>
    void SetMute(AudioTarget target, string? app, bool? mute);
    IReadOnlyList<AudioDevice> GetDevices(AudioFlow flow);
    void SetDefaultDevice(string deviceId);
    /// <summary>Process names that currently have an audio session.</summary>
    IReadOnlyList<string> GetSessionApps();
}

public sealed record ForegroundApp(string Process, string Title);

public interface IForegroundWatcher
{
    ForegroundApp? Current { get; }
    event Action<ForegroundApp>? Changed;
}

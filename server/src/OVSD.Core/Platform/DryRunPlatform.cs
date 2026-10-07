using Microsoft.Extensions.Logging;
using OVSD.Core.Variables;

namespace OVSD.Core.Platform;

/// <summary>
/// Platform that only records what it would do (log + "dryrun.last"/"dryrun.count" variables).
/// Used by end-to-end tests and demos so no keys are sent and no programs are launched.
/// </summary>
public sealed class DryRunPlatform(VariableStore variables, ILogger<DryRunPlatform> logger)
    : IKeyboard, IMediaController, IAudioController, IForegroundWatcher, IProcessLauncher
{
    private double _volume = 50;
    private bool _muted, _micMuted;
    private int _count;

    private void Record(string call)
    {
        logger.LogInformation("[dry-run] {Call}", call);
        variables.Set("dryrun.last", call);
        variables.Set("dryrun.count", Interlocked.Increment(ref _count));
    }

    public Task SendChordAsync(IReadOnlyList<string> keys, CancellationToken ct) { Record("chord " + string.Join("+", keys)); return Task.CompletedTask; }
    public void KeyDown(IReadOnlyList<string> keys) => Record("keyDown " + string.Join("+", keys));
    public void KeyUp(IReadOnlyList<string> keys) => Record("keyUp " + string.Join("+", keys));
    public Task TypeTextAsync(string text, int delayMs, CancellationToken ct) { Record("type " + text); return Task.CompletedTask; }

    public Task PlayPauseAsync() { Record("media playPause"); return Task.CompletedTask; }
    public Task NextAsync() { Record("media next"); return Task.CompletedTask; }
    public Task PreviousAsync() { Record("media previous"); return Task.CompletedTask; }
    public Task StopAsync() { Record("media stop"); return Task.CompletedTask; }

    public double? GetVolume(AudioTarget target, string? app) => target == AudioTarget.Master ? _volume : 50;

    public void SetVolume(AudioTarget target, string? app, double volume)
    {
        if (target == AudioTarget.Master)
        {
            _volume = volume;
            variables.Set("audio.volume", Math.Round(volume));
        }
        Record($"volume {target} {volume}");
    }

    public bool? IsMuted(AudioTarget target, string? app) => target == AudioTarget.Mic ? _micMuted : _muted;

    public void SetMute(AudioTarget target, string? app, bool? mute)
    {
        if (target == AudioTarget.Mic) variables.Set("audio.mic.muted", _micMuted = mute ?? !_micMuted);
        else variables.Set("audio.muted", _muted = mute ?? !_muted);
        Record($"mute {target} {mute?.ToString() ?? "toggle"}");
    }

    public IReadOnlyList<AudioDevice> GetDevices(AudioFlow flow) => [new("dry-" + flow, $"Dry-run {flow}", true)];
    public void SetDefaultDevice(string deviceId) => Record("device " + deviceId);
    public IReadOnlyList<string> GetSessionApps() => ["dryrun-app"];

    public ForegroundApp? Current => null;
    public event Action<ForegroundApp>? Changed { add { } remove { } }

    public void Launch(string path, string? arguments, string? workingDirectory) => Record($"launch {path} {arguments}".TrimEnd());
    public void OpenUrl(string url) => Record("openUrl " + url);

    public Task<CommandResult> RunAsync(CommandShell shell, string command, TimeSpan timeout, bool hidden, CancellationToken ct)
    {
        Record($"command {shell} {command}");
        return Task.FromResult(new CommandResult(0, "dry-run", ""));
    }
}

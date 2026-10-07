using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using OVSD.Core.Actions;
using OVSD.Core.Platform;
using OVSD.Core.Variables;

namespace OVSD.Platform.Windows;

/// <summary>Volume, mute and default devices through the Windows Core Audio API.</summary>
public sealed class WindowsAudio : IAudioController, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly Lock _lock = new();
    private readonly Dictionary<uint, string> _processNames = new();

    public double? GetVolume(AudioTarget target, string? app) => With(target, app,
        endpoint => endpoint.MasterVolumeLevelScalar * 100.0,
        session => session.Volume * 100.0);

    public void SetVolume(AudioTarget target, string? app, double volume)
    {
        var scalar = (float)Math.Clamp(volume / 100.0, 0, 1);
        var found = With<bool?>(target, app,
            endpoint => { endpoint.MasterVolumeLevelScalar = scalar; return true; },
            session => { session.Volume = scalar; return true; });
        if (found is null) throw NotFound(target, app);
    }

    public bool? IsMuted(AudioTarget target, string? app) => With(target, app,
        endpoint => endpoint.Mute,
        session => session.Mute);

    public void SetMute(AudioTarget target, string? app, bool? mute)
    {
        var found = With<bool?>(target, app,
            endpoint => { endpoint.Mute = mute ?? !endpoint.Mute; return true; },
            session => { session.Mute = mute ?? !session.Mute; return true; });
        if (found is null) throw NotFound(target, app);
    }

    public IReadOnlyList<AudioDevice> GetDevices(AudioFlow flow)
    {
        lock (_lock)
        {
            var dataFlow = flow == AudioFlow.Output ? DataFlow.Render : DataFlow.Capture;
            string? defaultId = null;
            try
            {
                using var def = _enumerator.GetDefaultAudioEndpoint(dataFlow, Role.Multimedia);
                defaultId = def.ID;
            }
            catch (COMException) { }

            var result = new List<AudioDevice>();
            foreach (var device in _enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active))
            {
                using (device) result.Add(new AudioDevice(device.ID, device.FriendlyName, device.ID == defaultId));
            }
            return result;
        }
    }

    public void SetDefaultDevice(string deviceId)
    {
        var known = GetDevices(AudioFlow.Output).Concat(GetDevices(AudioFlow.Input))
            .FirstOrDefault(d => d.Id == deviceId || d.Name.Equals(deviceId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ActionException($"Audio device not found: {deviceId}");
        PolicyConfig.SetDefaultEndpoint(known.Id);
    }

    public IReadOnlyList<string> GetSessionApps()
    {
        lock (_lock)
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var manager = device.AudioSessionManager;
            manager.RefreshSessions();
            var sessions = manager.Sessions;
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                if (session.IsSystemSoundsSession) continue;
                if (ProcessName(session.GetProcessID) is { } name) names.Add(name);
            }
            return [.. names];
        }
    }

    public void Dispose() => _enumerator.Dispose();

    /// <summary>Runs a function on the endpoint (master/mic) or on every session of an app; null if not found.</summary>
    private T? With<T>(AudioTarget target, string? app, Func<AudioEndpointVolume, T> onEndpoint, Func<SimpleAudioVolume, T> onSession)
    {
        lock (_lock)
        {
            try
            {
                if (target != AudioTarget.App)
                {
                    using var device = _enumerator.GetDefaultAudioEndpoint(
                        target == AudioTarget.Mic ? DataFlow.Capture : DataFlow.Render, Role.Multimedia);
                    return onEndpoint(device.AudioEndpointVolume);
                }

                if (string.IsNullOrWhiteSpace(app)) throw new ActionException("No application given");
                var wanted = StripExe(app.Trim());
                using var output = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var manager = output.AudioSessionManager;
                manager.RefreshSessions();
                var sessions = manager.Sessions;
                T? result = default;
                var found = false;
                for (var i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (!string.Equals(ProcessName(session.GetProcessID), wanted, StringComparison.OrdinalIgnoreCase)) continue;
                    result = onSession(session.SimpleAudioVolume);
                    found = true;
                }
                return found ? result : default;
            }
            catch (COMException)
            {
                // No device of that kind (e.g. no microphone plugged in).
                return default;
            }
        }
    }

    private string? ProcessName(uint pid)
    {
        if (pid == 0) return null;
        if (_processNames.TryGetValue(pid, out var cached)) return cached;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            if (_processNames.Count > 512) _processNames.Clear();
            return _processNames[pid] = process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string StripExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    private static ActionException NotFound(AudioTarget target, string? app) =>
        new(target == AudioTarget.App ? $"No audio session for '{app}' (it must be playing or have played sound)" : $"No {target} audio device");

    /// <summary>Undocumented but stable COM interface that Windows' own sound settings use to change defaults.</summary>
    private static class PolicyConfig
    {
        [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
        private class CPolicyConfigClient;

        [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            [PreserveSig] int GetMixFormat(nint a, nint b);
            [PreserveSig] int GetDeviceFormat(nint a, int b, nint c);
            [PreserveSig] int ResetDeviceFormat(nint a);
            [PreserveSig] int SetDeviceFormat(nint a, nint b, nint c);
            [PreserveSig] int GetProcessingPeriod(nint a, int b, nint c, nint d);
            [PreserveSig] int SetProcessingPeriod(nint a, nint b);
            [PreserveSig] int GetShareMode(nint a, nint b);
            [PreserveSig] int SetShareMode(nint a, nint b);
            [PreserveSig] int GetPropertyValue(nint a, int b, nint c, nint d);
            [PreserveSig] int SetPropertyValue(nint a, int b, nint c, nint d);
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int role);
            [PreserveSig] int SetEndpointVisibility(nint a, int b);
        }

        public static void SetDefaultEndpoint(string deviceId)
        {
            var config = (IPolicyConfig)new CPolicyConfigClient();
            try
            {
                for (var role = 0; role <= 2; role++) Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(deviceId, role));
            }
            finally
            {
                Marshal.ReleaseComObject(config);
            }
        }
    }
}

/// <summary>Publishes audio.* variables (volume, mute, device names) a few times per second.</summary>
public sealed class AudioVariables(IAudioController audio, VariableStore variables, ILogger<AudioVariables> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        var tick = 0;
        try
        {
            do
            {
                try
                {
                    variables.Set("audio.volume", Round(audio.GetVolume(AudioTarget.Master, null)));
                    variables.Set("audio.muted", audio.IsMuted(AudioTarget.Master, null));
                    variables.Set("audio.mic.volume", Round(audio.GetVolume(AudioTarget.Mic, null)));
                    variables.Set("audio.mic.muted", audio.IsMuted(AudioTarget.Mic, null));
                    if (tick++ % 8 == 0)
                    {
                        variables.Set("audio.device", audio.GetDevices(AudioFlow.Output).FirstOrDefault(d => d.IsDefault)?.Name);
                        variables.Set("audio.mic.device", audio.GetDevices(AudioFlow.Input).FirstOrDefault(d => d.IsDefault)?.Name);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Audio poll failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { }
    }

    private static double? Round(double? v) => v is null ? null : Math.Round(v.Value);
}

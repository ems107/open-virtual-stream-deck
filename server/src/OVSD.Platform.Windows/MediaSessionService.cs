using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OVSD.Core.Platform;
using OVSD.Core.Storage;
using OVSD.Core.Variables;
using Windows.Media.Control;

namespace OVSD.Platform.Windows;

/// <summary>
/// Follows the system media session (Spotify, browsers, players...) through Windows' System Media Transport
/// Controls and publishes media.* variables, including the album art as a media URL.
/// </summary>
public sealed class MediaSessionService(VariableStore variables, MediaStore media, IKeyboard keyboard, ILogger<MediaSessionService> logger)
    : BackgroundService, IMediaController
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private string? _lastTrackKey;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Media session API unavailable; media.* variables disabled");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(700));
        do
        {
            try
            {
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Media refresh failed");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task RefreshAsync()
    {
        var session = _manager?.GetCurrentSession();
        if (session is null)
        {
            variables.Set("media.playing", false);
            variables.Set("media.status", "none");
            return;
        }

        var playback = session.GetPlaybackInfo();
        var status = playback.PlaybackStatus;
        variables.Set("media.playing", status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
        variables.Set("media.status", status.ToString().ToLowerInvariant());
        variables.Set("media.app", AppName(session.SourceAppUserModelId));

        var props = await session.TryGetMediaPropertiesAsync();
        if (props is null) return;
        variables.Set("media.title", props.Title);
        variables.Set("media.artist", props.Artist);
        variables.Set("media.album", props.AlbumTitle);

        var trackKey = $"{session.SourceAppUserModelId}|{props.Title}|{props.Artist}|{props.AlbumTitle}";
        if (trackKey == _lastTrackKey) return;
        _lastTrackKey = trackKey;
        variables.Set("media.art", await ReadArtAsync(props));
    }

    private async Task<string?> ReadArtAsync(GlobalSystemMediaTransportControlsSessionMediaProperties props)
    {
        if (props.Thumbnail is null) return null;
        try
        {
            using var stream = await props.Thumbnail.OpenReadAsync();
            using var input = stream.AsStreamForRead();
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer);
            return buffer.Length == 0 ? null : media.Save(buffer.ToArray(), MediaStore.ExtensionFor(stream.ContentType));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read album art");
            return null;
        }
    }

    /// <summary>"Spotify.exe" / "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" → "Spotify".</summary>
    private static string AppName(string appId)
    {
        var name = appId.Split('!').Last();
        name = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        return name.Split('.').Last();
    }

    private async Task Command(Func<GlobalSystemMediaTransportControlsSession, global::Windows.Foundation.IAsyncOperation<bool>> command, string fallbackKey)
    {
        var session = _manager?.GetCurrentSession();
        if (session is not null && await command(session)) return;
        await keyboard.SendChordAsync([fallbackKey], CancellationToken.None);
    }

    public Task PlayPauseAsync() => Command(s => s.TryTogglePlayPauseAsync(), "MediaPlayPause");
    public Task NextAsync() => Command(s => s.TrySkipNextAsync(), "MediaNext");
    public Task PreviousAsync() => Command(s => s.TrySkipPreviousAsync(), "MediaPrevious");
    public Task StopAsync() => Command(s => s.TryStopAsync(), "MediaStop");
}

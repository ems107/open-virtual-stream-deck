using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OVSD.Core.Actions;
using OVSD.Core.Actions.BuiltIn;
using OVSD.Core.Engine;
using OVSD.Core.Expressions;
using OVSD.Core.Model;
using OVSD.Core.Platform;
using OVSD.Core.Protocol;
using OVSD.Core.Storage;
using OVSD.Core.Variables;

namespace OVSD.Core.Runtime;

/// <summary>
/// Heart of the server: knows which profile/page each connected deck shows, renders tiles from variables,
/// pushes only what changed, and routes gestures to macro runs.
/// </summary>
public sealed class DeckRuntime : BackgroundService
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(50);
    private const int FramesPerSample = 20;

    private readonly ProfileRepository _profiles;
    private readonly ConfigRepository _config;
    private readonly VariableStore _variables;
    private readonly BindingRunner _runner;
    private readonly IForegroundWatcher _foreground;
    private readonly ILogger<DeckRuntime> _logger;

    private readonly Lock _gate = new();
    private readonly List<DeckSession> _sessions = [];
    private readonly Dictionary<string, double> _sliderValues = new();
    private readonly Dictionary<string, Queue<double>> _history = new();
    private volatile bool _dirty = true;

    public DeckRuntime(ProfileRepository profiles, ConfigRepository config, VariableStore variables, BindingRunner runner,
        IForegroundWatcher foreground, ILogger<DeckRuntime> logger)
    {
        _profiles = profiles;
        _config = config;
        _variables = variables;
        _runner = runner;
        _foreground = foreground;
        _logger = logger;

        variables.Changed += _ => _dirty = true;
        profiles.Changed += OnProfileChanged;
        config.Changed += OnConfigChanged;
        foreground.Changed += OnForegroundChanged;
    }

    public IReadOnlyList<DeckSession> Sessions
    {
        get { lock (_gate) return _sessions.ToList(); }
    }

    // ------------------------------------------------------------------ connection lifecycle

    public DeckSession Attach(Device? device, string name, bool isLocal, ClientRole role)
    {
        var session = new DeckSession(this, device?.Id, name, isLocal, role);
        lock (_gate)
        {
            _sessions.Add(session);
            if (role == ClientRole.Deck) ResolveProfile(session, force: true);
        }
        _dirty = true;
        _logger.LogInformation("{Role} session {Session} attached ({Name})", role, session.SessionId, name);
        return session;
    }

    public void Detach(DeckSession session)
    {
        lock (_gate) _sessions.Remove(session);
        session.Close();
        _logger.LogInformation("Session {Session} detached", session.SessionId);
    }

    // ------------------------------------------------------------------ input & navigation

    public void HandleInput(DeckSession session, InputMessage input)
    {
        Control? control;
        lock (_gate)
        {
            var page = CurrentPage(session, out _);
            if (page is null) return;

            if (input.ControlId == TileRenderer.BackTileId)
            {
                if (input.Gesture == Gesture.Tap) BackLocked(session);
                return;
            }
            control = page.Controls.Find(c => c.Id == input.ControlId);
            if (control is null) return;
        }

        var context = new ActionContext(_variables, _logger) { Session = session, ControlId = control.Id };
        context.Locals["control.id"] = control.Id;

        if (input.Gesture == Gesture.Change)
        {
            var cfg = control.Slider ?? new SliderConfig();
            var value = Snap(input.Value ?? cfg.Min, cfg);
            lock (_gate) _sliderValues[control.Id] = value;
            context.Locals["value"] = value;
            _dirty = true;
        }

        if (input.Gesture == Gesture.Tap && control.State?.Mode == StateMode.Toggle)
        {
            var name = DeckActions.ToggleVariable(control.Id);
            _variables.Set(name, !Values.IsTruthy(_variables.Get(name)));
        }
        context.Locals["state"] = TileRenderer.ResolveState(control, _variables.Get);

        var steps = control.Bindings.For(input.Gesture);
        if (steps is { Count: > 0 })
            _runner.Run($"{control.Id}:{input.Gesture}", control.Concurrency, steps, context, coalesce: input.Gesture == Gesture.Change);
    }

    public void Navigate(DeckSession session, NavigateMessage message)
    {
        switch (message.Target)
        {
            case NavigateTarget.Back: Back(session); break;
            case NavigateTarget.Home: Home(session); break;
            case NavigateTarget.Page when message.PageId is not null: OpenPage(session, message.PageId); break;
        }
    }

    public void OpenPage(DeckSession session, string pageId)
    {
        lock (_gate)
        {
            var profile = session.ProfileId is null ? null : _profiles.Get(session.ProfileId);
            if (profile?.FindPage(pageId) is null)
            {
                session.Notify($"Page not found: {pageId}", NotifyLevel.Warning);
                return;
            }
            if (session.CurrentPageId != pageId) session.PageStack.Add(pageId);
        }
        _dirty = true;
    }

    public void Back(DeckSession session)
    {
        lock (_gate) BackLocked(session);
    }

    private void BackLocked(DeckSession session)
    {
        if (session.PageStack.Count > 1) session.PageStack.RemoveAt(session.PageStack.Count - 1);
        _dirty = true;
    }

    public void Home(DeckSession session)
    {
        lock (_gate)
        {
            var profile = session.ProfileId is null ? null : _profiles.Get(session.ProfileId);
            if (profile is not null) session.PageStack = [profile.HomePageId];
        }
        _dirty = true;
    }

    public void SelectProfile(DeckSession session, string profileId)
    {
        lock (_gate)
        {
            if (_profiles.Get(profileId) is null)
            {
                session.Notify($"Profile not found: {profileId}", NotifyLevel.Warning);
                return;
            }
            session.ManualProfileId = profileId;
            ResolveProfile(session, force: false);
        }
        _dirty = true;
    }

    // ------------------------------------------------------------------ change notifications

    private void OnProfileChanged(string profileId)
    {
        var revision = _profiles.Get(profileId)?.Revision;
        lock (_gate)
        {
            foreach (var session in _sessions)
            {
                if (session.Role == ClientRole.Deck && (session.ProfileId == profileId || session.ProfileId is null))
                    ResolveProfile(session, force: _profiles.Get(profileId) is null);
                session.Send(new ConfigChangedMessage(ConfigKind.Profiles, profileId, revision));
            }
        }
        _dirty = true;
    }

    private void OnConfigChanged(ServerConfig old, ServerConfig updated)
    {
        var devicesChanged = !old.Devices.SequenceEqual(updated.Devices);
        var deckSettingsChanged = old.Settings.Deck != updated.Settings.Deck;
        lock (_gate)
        {
            foreach (var session in _sessions.ToList())
            {
                if (session.DeviceId is not null && updated.Devices.All(d => d.Id != session.DeviceId))
                {
                    session.Send(new ErrorMessage("unpaired", "This device was removed"));
                    session.Close();
                    _sessions.Remove(session);
                    continue;
                }
                var before = old.Devices.Find(d => d.Id == session.DeviceId);
                var after = updated.Devices.Find(d => d.Id == session.DeviceId);
                if (before?.ProfileId != after?.ProfileId || before?.AutoProfile != after?.AutoProfile)
                    session.ManualProfileId = null;
                if (session.Role == ClientRole.Deck) ResolveProfile(session, force: false);
                if (deckSettingsChanged) session.Send(new DeckSettingsMessage(updated.Settings.Deck));
                session.Send(new ConfigChangedMessage(devicesChanged ? ConfigKind.Devices : ConfigKind.Settings, null, null));
            }
        }
        _dirty = true;
    }

    private void OnForegroundChanged(ForegroundApp app)
    {
        lock (_gate)
        {
            foreach (var session in _sessions.Where(s => s.Role == ClientRole.Deck && DeviceFor(s)?.AutoProfile == true))
            {
                session.ManualProfileId = null;
                ResolveProfile(session, force: false);
            }
        }
        _dirty = true;
    }

    // ------------------------------------------------------------------ profile resolution (call under lock)

    private Device? DeviceFor(DeckSession session) =>
        session.DeviceId is null ? null : _config.Current.Devices.Find(d => d.Id == session.DeviceId);

    private string? DesiredProfile(DeckSession session)
    {
        var all = _profiles.All;
        if (all.Count == 0) return null;
        bool Exists(string? id) => id is not null && all.Any(p => p.Id == id);

        if (Exists(session.ManualProfileId)) return session.ManualProfileId;
        var device = DeviceFor(session);
        if (device?.AutoProfile == true && _foreground.Current is { } app &&
            all.FirstOrDefault(p => ProfileMatcher.Matches(p, app)) is { } matched)
            return matched.Id;
        if (Exists(device?.ProfileId)) return device!.ProfileId;
        var fallback = _config.Settings.DefaultProfileId;
        return Exists(fallback) ? fallback : all[0].Id;
    }

    private void ResolveProfile(DeckSession session, bool force)
    {
        var desired = DesiredProfile(session);
        if (!force && desired == session.ProfileId) return;
        session.ProfileId = desired;
        var profile = desired is null ? null : _profiles.Get(desired);
        session.PageStack = profile is null ? [] : [profile.HomePageId];
    }

    private Page? CurrentPage(DeckSession session, out Profile? profile)
    {
        profile = session.ProfileId is null ? null : _profiles.Get(session.ProfileId);
        if (profile is null) return null;
        var page = session.CurrentPageId is { } id ? profile.FindPage(id) : null;
        if (page is null)
        {
            session.PageStack = [profile.HomePageId];
            page = profile.FindPage(profile.HomePageId);
        }
        return page;
    }

    // ------------------------------------------------------------------ rendering loop

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FrameInterval);
        var frame = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (++frame % FramesPerSample == 0) SampleGraphs();
                if (!_dirty) continue;
                _dirty = false;
                try
                {
                    RenderAll();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Render failed");
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _runner.CancelAll();
        }
    }

    /// <summary>Renders immediately (tests and first frame after attach).</summary>
    public void RenderAll()
    {
        lock (_gate)
        {
            var summaries = _profiles.All.Select(p => new ProfileSummary(p.Id, p.Name)).ToList();
            var profilesKey = string.Join("|", summaries.Select(s => s.Id + s.Name));
            foreach (var session in _sessions.Where(s => s.Role == ClientRole.Deck)) RenderSession(session, summaries, profilesKey);
        }
    }

    private void RenderSession(DeckSession session, List<ProfileSummary> summaries, string profilesKey)
    {
        var page = CurrentPage(session, out var profile);
        if (profile is null || page is null)
        {
            if (session.LastLayoutKey == "none") return;
            session.LastLayoutKey = "none";
            session.SentTiles.Clear();
            session.Send(new LayoutMessage("", "", 0, "", "", 0, 0, new Theme(), false, summaries));
            session.Send(new TilesMessage([], [], Reset: true));
            return;
        }

        var canGoBack = session.PageStack.Count > 1;
        var layoutKey = $"{profile.Id}:{profile.Revision}:{page.Id}:{canGoBack}:{profilesKey}";
        var reset = false;
        if (layoutKey != session.LastLayoutKey)
        {
            session.LastLayoutKey = layoutKey;
            session.SentTiles.Clear();
            reset = true;
            session.Send(new LayoutMessage(profile.Id, profile.Name, profile.Revision, page.Id, page.Name,
                profile.Grid.Rows, profile.Grid.Cols, profile.Theme, canGoBack, summaries));
        }

        var tiles = RenderTiles(page);
        var changed = new List<TileState>();
        var seen = new HashSet<string>();
        foreach (var tile in tiles)
        {
            seen.Add(tile.Id);
            var json = JsonSerializer.Serialize(tile, ProtocolJson.Options);
            if (session.SentTiles.TryGetValue(tile.Id, out var previous) && previous == json) continue;
            session.SentTiles[tile.Id] = json;
            changed.Add(tile);
        }
        var removed = session.SentTiles.Keys.Where(id => !seen.Contains(id)).ToList();
        foreach (var id in removed) session.SentTiles.Remove(id);

        if (reset || changed.Count > 0 || removed.Count > 0)
            session.Send(new TilesMessage(changed, removed, reset));
    }

    private List<TileState> RenderTiles(Page page)
    {
        var tiles = new List<TileState>(page.Controls.Count + 1);
        foreach (var control in page.Controls)
        {
            var resolve = _variables.Resolver(_sliderValues.TryGetValue(control.Id, out var v)
                ? new Dictionary<string, object?> { ["value"] = v }
                : null);
            tiles.Add(TileRenderer.Render(control, resolve,
                _sliderValues.TryGetValue(control.Id, out var slider) ? slider : null,
                _history.GetValueOrDefault(control.Id)));
        }
        if (page.ParentId is not null && !page.Controls.Any(c => c.Position.Covers(0, 0)))
            tiles.Add(TileRenderer.BackTile(0, 0));
        return tiles;
    }

    /// <summary>Takes one sample per second for every graph currently on screen.</summary>
    private void SampleGraphs()
    {
        lock (_gate)
        {
            var graphs = _sessions
                .Where(s => s.Role == ClientRole.Deck)
                .Select(s => CurrentPage(s, out _))
                .Where(p => p is not null)
                .SelectMany(p => p!.Controls)
                .Where(c => c.Kind == ControlKind.Widget && c.Widget?.Type == WidgetType.Graph)
                .DistinctBy(c => c.Id);

            foreach (var graph in graphs)
            {
                var value = TileRenderer.EvaluateNumber(graph.Widget!.Expression, _variables.Get);
                if (value is null) continue;
                if (!_history.TryGetValue(graph.Id, out var queue)) _history[graph.Id] = queue = new Queue<double>();
                queue.Enqueue(Math.Round(value.Value, 2));
                while (queue.Count > Math.Clamp(graph.Widget.History, 2, 600)) queue.Dequeue();
                _dirty = true;
            }
        }
    }

    private static double Snap(double value, SliderConfig cfg)
    {
        var (min, max) = cfg.Max >= cfg.Min ? (cfg.Min, cfg.Max) : (cfg.Max, cfg.Min);
        value = Math.Clamp(value, min, max);
        if (cfg.Step > 0) value = min + Math.Round((value - min) / cfg.Step) * cfg.Step;
        return Math.Round(Math.Clamp(value, min, max), 6);
    }
}

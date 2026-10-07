using OVSD.Core.Model;
using OVSD.Core.Protocol;
using OVSD.Core.Runtime;
using OVSD.Core.Storage;
using OVSD.Core.Variables;

namespace OVSD.Core.Tests;

public class RuntimeTests : IDisposable
{
    private readonly TestHost _host = new();
    public void Dispose() => _host.Dispose();

    private DeckRuntime Runtime => _host.Get<DeckRuntime>();
    private Profile Sample => _host.Get<ProfileRepository>().All[0];

    private Device PairDevice(Func<Device, Device>? change = null)
    {
        var device = new Device { Id = Ids.New(), Name = "Tablet", TokenHash = "x" };
        if (change is not null) device = change(device);
        _host.Get<ConfigRepository>().Update(c => c with { Devices = [.. c.Devices, device] });
        return device;
    }

    private (DeckSession Session, List<ServerMessage> Messages) Connect(Device? device = null)
    {
        var session = Runtime.Attach(device, "test", isLocal: device is null, ClientRole.Deck);
        Runtime.RenderAll();
        return (session, TestHost.Drain(session));
    }

    [Fact]
    public void AttachSendsLayoutThenAllTiles()
    {
        var (_, messages) = Connect();
        var layout = Assert.IsType<LayoutMessage>(messages[0]);
        var tiles = Assert.IsType<TilesMessage>(messages[1]);
        Assert.Equal(Sample.HomePageId, layout.PageId);
        Assert.True(tiles.Reset);
        Assert.Equal(Sample.FindPage(Sample.HomePageId)!.Controls.Count, tiles.Tiles.Count);
    }

    [Fact]
    public void OnlyChangedTilesAreResent()
    {
        var (session, _) = Connect();
        _host.Get<VariableStore>().Set("user.counter", 7);
        Runtime.RenderAll();

        var update = Assert.IsType<TilesMessage>(Assert.Single(TestHost.Drain(session)));
        var tile = Assert.Single(update.Tiles);
        Assert.Equal("7", tile.Text);
        Assert.False(update.Reset);

        Runtime.RenderAll();
        Assert.Empty(TestHost.Drain(session));
    }

    [Fact]
    public async Task TapRunsBindingAndToggleFlipsState()
    {
        var (session, _) = Connect();
        var home = Sample.FindPage(Sample.HomePageId)!;
        var hotkey = home.Controls.Single(c => c.Appearance.Text == "Task Mgr");
        var toggle = home.Controls.Single(c => c.State?.Mode == StateMode.Toggle);

        Runtime.HandleInput(session, new InputMessage(hotkey.Id, Gesture.Tap, null));
        Runtime.HandleInput(session, new InputMessage(toggle.Id, Gesture.Tap, null));
        await TestHost.Eventually(() => _host.Platform.Snapshot().Contains("chord Ctrl+Shift+Esc"));

        Runtime.RenderAll();
        var tiles = TestHost.Drain(session).OfType<TilesMessage>().SelectMany(t => t.Tiles).ToList();
        var toggled = tiles.Single(t => t.Id == toggle.Id);
        Assert.Equal("on", toggled.State);
        Assert.Equal("mdi:lightbulb-on", toggled.Icon);
    }

    [Fact]
    public void FolderNavigationAddsBackTile()
    {
        var (session, _) = Connect();
        var apps = Sample.Pages.Single(p => p.ParentId is not null);

        Runtime.OpenPage(session, apps.Id);
        Runtime.RenderAll();
        var messages = TestHost.Drain(session);
        Assert.True(Assert.IsType<LayoutMessage>(messages[0]).CanGoBack);
        Assert.Contains(((TilesMessage)messages[1]).Tiles, t => t.Id == TileRenderer.BackTileId);

        Runtime.HandleInput(session, new InputMessage(TileRenderer.BackTileId, Gesture.Tap, null));
        Runtime.RenderAll();
        Assert.Equal(Sample.HomePageId, TestHost.Drain(session).OfType<LayoutMessage>().Single().PageId);
    }

    [Fact]
    public async Task SliderChangeSnapsAndPassesValue()
    {
        var (session, _) = Connect();
        var slider = Sample.FindPage(Sample.HomePageId)!.Controls.Single(c => c.Kind == ControlKind.Slider);
        Runtime.HandleInput(session, new InputMessage(slider.Id, Gesture.Change, 33.4));
        await TestHost.Eventually(() => _host.Platform.Snapshot().Contains("volume Master 33"));
    }

    [Fact]
    public void AutoProfileFollowsForegroundApp()
    {
        var repo = _host.Get<ProfileRepository>();
        var main = Sample;
        var games = repo.Save(ProfileNormalizer.WithNewIds(main) with
        {
            Name = "Games",
            MatchRules = [new MatchRule { Process = "game*" }],
        });
        var device = PairDevice(d => d with { AutoProfile = true, ProfileId = main.Id });
        var (session, first) = Connect(device);
        Assert.Equal(main.Id, first.OfType<LayoutMessage>().Single().ProfileId);

        _host.Platform.Focus("GameClient.exe");
        Runtime.RenderAll();
        Assert.Equal(games.Id, TestHost.Drain(session).OfType<LayoutMessage>().Single().ProfileId);

        _host.Platform.Focus("notepad.exe");
        Runtime.RenderAll();
        Assert.Equal(main.Id, TestHost.Drain(session).OfType<LayoutMessage>().Single().ProfileId);
    }

    [Fact]
    public void RemovingDeviceDisconnectsIt()
    {
        var device = PairDevice();
        var (session, _) = Connect(device);
        _host.Get<ConfigRepository>().Update(c => c with { Devices = [] });
        var messages = TestHost.Drain(session);
        Assert.Equal("unpaired", messages.OfType<ErrorMessage>().Single().Code);
        Assert.True(session.Outbox.Completion.IsCompleted);
    }

    [Fact]
    public void SavingProfileResendsLayoutAndNotifiesEditors()
    {
        var (session, _) = Connect();
        var editor = Runtime.Attach(null, "editor", true, ClientRole.Editor);
        _host.Get<ProfileRepository>().Save(Sample with { Name = "Renamed" });
        Runtime.RenderAll();

        Assert.Equal("Renamed", TestHost.Drain(session).OfType<LayoutMessage>().Single().ProfileName);
        Assert.IsType<ConfigChangedMessage>(Assert.Single(TestHost.Drain(editor)));
    }

    [Theory]
    [InlineData("chrome", null, "chrome.exe", "", true)]
    [InlineData("CHROME.exe", null, "chrome.exe", "", true)]
    [InlineData("obs*", null, "obs64.exe", "", true)]
    [InlineData(null, "YouTube", "chrome.exe", "Music - YouTube", true)]
    [InlineData("chrome", "Twitch", "chrome.exe", "YouTube", false)]
    [InlineData(null, null, "chrome.exe", "", false)]
    public void MatchRules(string? process, string? title, string app, string appTitle, bool expected) =>
        Assert.Equal(expected, ProfileMatcher.Matches(new MatchRule { Process = process, TitleContains = title },
            new Platform.ForegroundApp(app, appTitle)));
}

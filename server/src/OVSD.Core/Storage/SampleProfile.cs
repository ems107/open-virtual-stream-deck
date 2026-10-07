using OVSD.Core.Model;

namespace OVSD.Core.Storage;

/// <summary>The profile created on first run, showing off the main features.</summary>
public static class SampleProfile
{
    public static Profile Create()
    {
        var homeId = Ids.New();
        var appsId = Ids.New();

        static Control Button(int row, int col, string icon, string text, params Step[] tap) => new()
        {
            Id = Ids.New(),
            Position = new Cell { Row = row, Col = col },
            Appearance = new Appearance { Icon = icon, Text = text },
            Bindings = new Bindings { Tap = [.. tap] },
        };

        static ActionStep Act(string action, params (string Key, string Value)[] args) =>
            new() { Action = action, Params = args.ToDictionary(a => a.Key, a => a.Value) };

        var home = new Page
        {
            Id = homeId,
            Name = "Inicio",
            Controls =
            [
                new Control
                {
                    Id = Ids.New(),
                    Kind = ControlKind.Widget,
                    Position = new Cell { Row = 0, Col = 0 },
                    Widget = new WidgetConfig { Type = WidgetType.Text },
                    Appearance = new Appearance { Text = "{{time.hhmm}}\n{{time.date}}", FontSize = 16 },
                },
                Button(0, 1, "mdi:skip-previous", "", Act("media.previous")),
                new Control
                {
                    Id = Ids.New(),
                    Position = new Cell { Row = 0, Col = 2 },
                    Appearance = new Appearance
                    {
                        Image = "{{media.art}}",
                        Icon = "mdi:play-pause",
                        Text = "{{truncate(media.title, 18)}}",
                        TextPosition = TextPosition.Bottom,
                        FontSize = 11,
                    },
                    Bindings = new Bindings { Tap = [Act("media.playPause")] },
                },
                Button(0, 3, "mdi:skip-next", "", Act("media.next")),
                new Control
                {
                    Id = Ids.New(),
                    Kind = ControlKind.Slider,
                    Position = new Cell { Row = 0, Col = 4, RowSpan = 3 },
                    Appearance = new Appearance { Icon = "mdi:volume-high", Text = "{{round(audio.volume)}}%" },
                    Slider = new SliderConfig { Min = 0, Max = 100, ValueExpression = "audio.volume", Color = "#5b8cff" },
                    Bindings = new Bindings
                    {
                        Change = [Act("audio.setVolume", ("target", "master"), ("value", "{{value}}"))],
                    },
                },
                new Control
                {
                    Id = Ids.New(),
                    Kind = ControlKind.Widget,
                    Position = new Cell { Row = 1, Col = 0, ColSpan = 2 },
                    Widget = new WidgetConfig { Type = WidgetType.Graph, Expression = "sys.cpu", Color = "#3ecf8e" },
                    Appearance = new Appearance { Text = "CPU {{round(sys.cpu)}}%", TextPosition = TextPosition.Top, FontSize = 13 },
                },
                new Control
                {
                    Id = Ids.New(),
                    Position = new Cell { Row = 1, Col = 2 },
                    Appearance = new Appearance { Icon = "mdi:microphone", Text = "Mic" },
                    State = new StateConfig
                    {
                        Mode = StateMode.Expression,
                        Expression = "audio.mic.muted",
                        Appearances = new()
                        {
                            ["on"] = new Appearance { Icon = "mdi:microphone-off", Background = "#7a1f1f", Text = "Muted" },
                        },
                    },
                    Bindings = new Bindings { Tap = [Act("audio.mute", ("target", "mic"), ("mode", "toggle"))] },
                },
                Button(1, 3, "mdi:folder-outline", "Apps", Act("deck.openPage", ("page", appsId))),
                new Control
                {
                    Id = Ids.New(),
                    Position = new Cell { Row = 2, Col = 0 },
                    Appearance = new Appearance { Icon = "mdi:counter", Text = "{{default(user.counter, 0)}}" },
                    Bindings = new Bindings
                    {
                        Tap = [new SetVariableStep { Name = "user.counter", Value = "default(user.counter, 0) + 1" }],
                        LongPress = [new SetVariableStep { Name = "user.counter", Value = "0" }],
                    },
                },
                Button(2, 1, "mdi:monitor-dashboard", "Task Mgr", Act("keyboard.hotkey", ("keys", "Ctrl+Shift+Esc"))),
                Button(2, 2, "mdi:web", "GitHub", Act("system.openUrl", ("url", "https://github.com"))),
                new Control
                {
                    Id = Ids.New(),
                    Position = new Cell { Row = 2, Col = 3 },
                    Appearance = new Appearance { Icon = "mdi:lightbulb-outline", Text = "Toggle" },
                    State = new StateConfig
                    {
                        Mode = StateMode.Toggle,
                        Appearances = new()
                        {
                            ["on"] = new Appearance { Icon = "mdi:lightbulb-on", IconColor = "#f5b041", Text = "ON" },
                        },
                    },
                },
            ],
        };

        var apps = new Page
        {
            Id = appsId,
            Name = "Apps",
            ParentId = homeId,
            Controls =
            [
                Button(0, 1, "mdi:note-edit-outline", "Notepad", Act("system.launch", ("path", "notepad.exe"))),
                Button(0, 2, "mdi:calculator", "Calc", Act("system.launch", ("path", "calc.exe"))),
                Button(0, 3, "mdi:folder", "Explorer", Act("system.launch", ("path", "explorer.exe"))),
            ],
        };

        return new Profile
        {
            Id = Ids.New(),
            Name = "Inicio",
            Grid = new GridSize { Rows = 3, Cols = 5 },
            HomePageId = homeId,
            Pages = [home, apps],
        };
    }
}

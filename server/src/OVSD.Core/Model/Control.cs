namespace OVSD.Core.Model;

public enum ControlKind { Button, Slider, Widget }

public enum Gesture { Tap, LongPress, DoubleTap, Press, Release, Change }

/// <summary>What happens when a binding is triggered while its previous run is still going.</summary>
public enum Concurrency { Parallel, Ignore, Restart, Queue }

public sealed record Control
{
    public required string Id { get; init; }
    public ControlKind Kind { get; init; } = ControlKind.Button;
    public required Cell Position { get; init; }
    public Appearance Appearance { get; init; } = new();
    public StateConfig? State { get; init; }
    public Bindings Bindings { get; init; } = new();
    public Concurrency Concurrency { get; init; } = Concurrency.Parallel;
    public SliderConfig? Slider { get; init; }
    public WidgetConfig? Widget { get; init; }
}

public sealed record Cell
{
    public required int Row { get; init; }
    public required int Col { get; init; }
    public int RowSpan { get; init; } = 1;
    public int ColSpan { get; init; } = 1;

    public bool Covers(int row, int col) =>
        row >= Row && row < Row + RowSpan && col >= Col && col < Col + ColSpan;
}

public enum TextPosition { Top, Center, Bottom }

public enum ImageFit { Cover, Contain }

/// <summary>Visual layers of a tile. Every field is optional so state appearances can override just a few.</summary>
public sealed record Appearance
{
    public string? Background { get; init; }
    /// <summary>Iconify name, e.g. "mdi:microphone-off".</summary>
    public string? Icon { get; init; }
    public string? IconColor { get; init; }
    /// <summary>Image URL or template (e.g. "/media/abc.png" or "{{media.art}}").</summary>
    public string? Image { get; init; }
    public ImageFit? ImageFit { get; init; }
    /// <summary>Text template, e.g. "CPU {{sys.cpu}}%".</summary>
    public string? Text { get; init; }
    public string? TextColor { get; init; }
    public int? FontSize { get; init; }
    public TextPosition? TextPosition { get; init; }

    /// <summary>Returns this appearance with every non-null field of <paramref name="over"/> applied on top.</summary>
    public Appearance Merge(Appearance? over) => over is null ? this : new Appearance
    {
        Background = over.Background ?? Background,
        Icon = over.Icon ?? Icon,
        IconColor = over.IconColor ?? IconColor,
        Image = over.Image ?? Image,
        ImageFit = over.ImageFit ?? ImageFit,
        Text = over.Text ?? Text,
        TextColor = over.TextColor ?? TextColor,
        FontSize = over.FontSize ?? FontSize,
        TextPosition = over.TextPosition ?? TextPosition,
    };
}

public enum StateMode { Toggle, Expression }

/// <summary>
/// Toggle: the state flips between "off" and "on" on every tap.
/// Expression: the state is the result of an expression (true → "on", false → "off", other values as text).
/// </summary>
public sealed record StateConfig
{
    public StateMode Mode { get; init; } = StateMode.Toggle;
    public string? Expression { get; init; }
    public Dictionary<string, Appearance> Appearances { get; init; } = [];
}

public sealed record Bindings
{
    public List<Step>? Tap { get; init; }
    public List<Step>? LongPress { get; init; }
    public List<Step>? DoubleTap { get; init; }
    public List<Step>? Press { get; init; }
    public List<Step>? Release { get; init; }
    /// <summary>Slider value changed; the value is available as the local variable "value".</summary>
    public List<Step>? Change { get; init; }

    public List<Step>? For(Gesture gesture) => gesture switch
    {
        Gesture.Tap => Tap,
        Gesture.LongPress => LongPress,
        Gesture.DoubleTap => DoubleTap,
        Gesture.Press => Press,
        Gesture.Release => Release,
        Gesture.Change => Change,
        _ => null,
    };
}

public enum Orientation { Vertical, Horizontal }

public sealed record SliderConfig
{
    public double Min { get; init; }
    public double Max { get; init; } = 100;
    public double Step { get; init; } = 1;
    public Orientation Orientation { get; init; } = Orientation.Vertical;
    /// <summary>Expression giving the current value to display (e.g. "audio.volume").</summary>
    public string? ValueExpression { get; init; }
    public string? Color { get; init; }
}

public enum WidgetType { Text, Graph, Gauge }

public sealed record WidgetConfig
{
    public WidgetType Type { get; init; } = WidgetType.Text;
    /// <summary>Numeric expression sampled for graphs/gauges (e.g. "sys.cpu").</summary>
    public string? Expression { get; init; }
    public double Min { get; init; }
    public double Max { get; init; } = 100;
    /// <summary>Samples kept for graphs (one per second).</summary>
    public int History { get; init; } = 60;
    public string? Color { get; init; }
}

using OVSD.Core.Actions.BuiltIn;
using OVSD.Core.Expressions;
using OVSD.Core.Model;
using OVSD.Core.Protocol;

namespace OVSD.Core.Runtime;

/// <summary>Turns a configured control plus the current variables into what the deck should draw.</summary>
public static class TileRenderer
{
    public const string BackTileId = "__back";

    public static TileState Render(Control control, Func<string, object?> resolve, double? sliderValue, IReadOnlyCollection<double>? series)
    {
        var state = ResolveState(control, resolve);
        var look = control.Appearance.Merge(state is null ? null : control.State!.Appearances.GetValueOrDefault(state));
        string? Field(string? template) => template is null ? null : NullIfEmpty(Template.Render(template, resolve));

        return new TileState
        {
            Id = control.Id,
            Kind = control.Kind,
            Row = control.Position.Row,
            Col = control.Position.Col,
            RowSpan = control.Position.RowSpan,
            ColSpan = control.Position.ColSpan,
            Background = Field(look.Background),
            Icon = Field(look.Icon),
            IconColor = Field(look.IconColor),
            Image = Field(look.Image),
            ImageFit = look.ImageFit,
            Text = Field(look.Text),
            TextColor = Field(look.TextColor),
            FontSize = look.FontSize,
            TextPosition = look.TextPosition,
            State = state,
            HasLongPress = control.Bindings.LongPress is { Count: > 0 },
            HasDoubleTap = control.Bindings.DoubleTap is { Count: > 0 },
            Slider = control.Kind == ControlKind.Slider ? RenderSlider(control.Slider ?? new SliderConfig(), resolve, sliderValue) : null,
            Widget = control.Kind == ControlKind.Widget ? RenderWidget(control.Widget ?? new WidgetConfig(), resolve, series) : null,
        };
    }

    public static TileState BackTile(int row, int col) => new()
    {
        Id = BackTileId,
        Row = row,
        Col = col,
        Icon = "mdi:arrow-left",
        Text = "",
    };

    public static string? ResolveState(Control control, Func<string, object?> resolve)
    {
        switch (control.State)
        {
            case null:
                return null;
            case { Mode: StateMode.Toggle }:
                return Values.IsTruthy(resolve(DeckActions.ToggleVariable(control.Id))) ? "on" : "off";
            case { Expression: { Length: > 0 } expression }:
                try
                {
                    return Expression.Evaluate(expression, resolve) switch
                    {
                        bool b => b ? "on" : "off",
                        null => "off",
                        var v => Values.ToText(v),
                    };
                }
                catch (ExpressionException)
                {
                    return "error";
                }
            default:
                return "off";
        }
    }

    public static double? EvaluateNumber(string? expression, Func<string, object?> resolve)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;
        try
        {
            return Values.ToNumber(Expression.Evaluate(expression, resolve));
        }
        catch (ExpressionException)
        {
            return null;
        }
    }

    private static SliderTile RenderSlider(SliderConfig cfg, Func<string, object?> resolve, double? lastValue)
    {
        var value = EvaluateNumber(cfg.ValueExpression, resolve) ?? lastValue ?? cfg.Min;
        var (min, max) = cfg.Max >= cfg.Min ? (cfg.Min, cfg.Max) : (cfg.Max, cfg.Min);
        return new SliderTile(Math.Clamp(value, min, max), min, max, cfg.Step, cfg.Orientation, cfg.Color);
    }

    private static WidgetTile RenderWidget(WidgetConfig cfg, Func<string, object?> resolve, IReadOnlyCollection<double>? series) =>
        new(cfg.Type,
            EvaluateNumber(cfg.Expression, resolve),
            cfg.Min,
            cfg.Max,
            cfg.Type == WidgetType.Graph ? series?.ToList() ?? [] : null,
            cfg.Color);

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;
}

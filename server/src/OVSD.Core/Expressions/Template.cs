using System.Collections.Concurrent;
using System.Text;

namespace OVSD.Core.Expressions;

/// <summary>Renders text with embedded expressions: "CPU {{round(sys.cpu)}}%".</summary>
public static class Template
{
    public const string ErrorMarker = "⚠";

    private abstract record Part;
    private sealed record TextPart(string Text) : Part;
    private sealed record ExprPart(string Source) : Part;

    private static readonly ConcurrentDictionary<string, Part[]> Cache = new();

    public static bool HasExpressions(string? template) => template is not null && template.Contains("{{");

    public static string Render(string? template, Func<string, object?> resolve)
    {
        if (string.IsNullOrEmpty(template)) return "";
        if (!HasExpressions(template)) return template;

        var parts = Cache.GetOrAdd(template, Split);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            switch (part)
            {
                case TextPart t:
                    sb.Append(t.Text);
                    break;
                case ExprPart e:
                    try
                    {
                        sb.Append(Values.ToText(Expression.Evaluate(e.Source, resolve)));
                    }
                    catch (ExpressionException)
                    {
                        sb.Append(ErrorMarker);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Like <see cref="Render"/>, but a template that is exactly one expression keeps the raw value type
    /// (so "{{value}}" passes a number through instead of text).
    /// </summary>
    public static object? Evaluate(string? template, Func<string, object?> resolve)
    {
        if (template is null) return null;
        var parts = Cache.GetOrAdd(template, Split);
        if (parts is [ExprPart only])
        {
            try { return Expression.Evaluate(only.Source, resolve); }
            catch (ExpressionException) { return ErrorMarker; }
        }
        return Render(template, resolve);
    }

    private static Part[] Split(string template)
    {
        var parts = new List<Part>();
        var pos = 0;
        while (pos < template.Length)
        {
            var open = template.IndexOf("{{", pos, StringComparison.Ordinal);
            if (open < 0) break;
            var close = template.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0) break;
            if (open > pos) parts.Add(new TextPart(template[pos..open]));
            parts.Add(new ExprPart(template[(open + 2)..close].Trim()));
            pos = close + 2;
        }
        if (pos < template.Length) parts.Add(new TextPart(template[pos..]));
        return [.. parts];
    }
}

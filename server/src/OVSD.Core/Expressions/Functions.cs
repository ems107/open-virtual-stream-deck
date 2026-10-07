using System.Globalization;
using System.Text.Json;

namespace OVSD.Core.Expressions;

/// <summary>Built-in functions callable from expressions. Names are case-sensitive.</summary>
public static class Functions
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private sealed record Fn(int Min, int Max, Func<object?[], object?> Body, string Help);

    private static readonly Dictionary<string, Fn> Table = new()
    {
        ["round"] = new(1, 2, a => Num(a[0]) is { } x ? Math.Round(x, (int)(Num(At(a, 1)) ?? 0), MidpointRounding.AwayFromZero) : null, "round(x, decimals?)"),
        ["floor"] = new(1, 1, a => Num(a[0]) is { } x ? Math.Floor(x) : null, "floor(x)"),
        ["ceil"] = new(1, 1, a => Num(a[0]) is { } x ? Math.Ceiling(x) : null, "ceil(x)"),
        ["abs"] = new(1, 1, a => Num(a[0]) is { } x ? Math.Abs(x) : null, "abs(x)"),
        ["min"] = new(1, 32, a => a.Select(Num).Where(n => n is not null).Min(), "min(a, b, ...)"),
        ["max"] = new(1, 32, a => a.Select(Num).Where(n => n is not null).Max(), "max(a, b, ...)"),
        ["clamp"] = new(3, 3, a => Num(a[0]) is { } x ? Math.Clamp(x, Num(a[1]) ?? x, Num(a[2]) ?? x) : null, "clamp(x, min, max)"),
        ["contains"] = new(2, 2, a => Text(a[0]).Contains(Text(a[1]), StringComparison.OrdinalIgnoreCase), "contains(text, part)"),
        ["startsWith"] = new(2, 2, a => Text(a[0]).StartsWith(Text(a[1]), StringComparison.OrdinalIgnoreCase), "startsWith(text, part)"),
        ["endsWith"] = new(2, 2, a => Text(a[0]).EndsWith(Text(a[1]), StringComparison.OrdinalIgnoreCase), "endsWith(text, part)"),
        ["lower"] = new(1, 1, a => Text(a[0]).ToLowerInvariant(), "lower(text)"),
        ["upper"] = new(1, 1, a => Text(a[0]).ToUpperInvariant(), "upper(text)"),
        ["trim"] = new(1, 1, a => Text(a[0]).Trim(), "trim(text)"),
        ["len"] = new(1, 1, a => (double)Text(a[0]).Length, "len(text)"),
        ["replace"] = new(3, 3, a => Text(a[0]).Replace(Text(a[1]), Text(a[2]), StringComparison.Ordinal), "replace(text, old, new)"),
        ["substr"] = new(2, 3, Substr, "substr(text, start, length?)"),
        ["truncate"] = new(2, 2, Truncate, "truncate(text, maxLength)"),
        ["str"] = new(1, 1, a => Text(a[0]), "str(x)"),
        ["num"] = new(1, 1, a => Num(a[0]), "num(x)"),
        ["bool"] = new(1, 1, a => Values.IsTruthy(a[0]), "bool(x)"),
        ["default"] = new(2, 2, a => a[0] is null or "" ? a[1] : a[0], "default(x, fallback)"),
        ["format"] = new(2, 2, Format, "format(x, '0.0')"),
        ["json"] = new(2, 2, Json, "json(text, 'path.to.0.field')"),
        ["concat"] = new(1, 32, a => string.Concat(a.Select(Text)), "concat(a, b, ...)"),
        ["now"] = new(0, 1, a => DateTime.Now.ToString(a.Length > 0 ? Text(a[0]) : "HH:mm", CultureInfo.CurrentCulture), "now('HH:mm:ss')"),
        ["if"] = new(3, 3, _ => throw new InvalidOperationException("if is evaluated lazily"), "if(condition, a, b)"),
    };

    public static bool Exists(string name) => Table.ContainsKey(name);

    public static IEnumerable<string> Help => Table.Values.Select(f => f.Help);

    public static void Arity(string name, int count, int min, int max)
    {
        if (count < min || count > max) throw new ExpressionException($"{name}() takes {min}..{max} arguments, got {count}");
    }

    public static object? Invoke(string name, object?[] args)
    {
        var fn = Table[name];
        Arity(name, args.Length, fn.Min, fn.Max);
        return Values.Normalize(fn.Body(args));
    }

    private static object? At(object?[] a, int i) => i < a.Length ? a[i] : null;
    private static double? Num(object? v) => Values.ToNumber(v);
    private static string Text(object? v) => Values.ToText(v);

    private static object? Substr(object?[] a)
    {
        var s = Text(a[0]);
        var start = Math.Clamp((int)(Num(a[1]) ?? 0), 0, s.Length);
        var length = a.Length > 2 ? Math.Clamp((int)(Num(a[2]) ?? 0), 0, s.Length - start) : s.Length - start;
        return s.Substring(start, length);
    }

    private static object? Truncate(object?[] a)
    {
        var s = Text(a[0]);
        var max = Math.Max(1, (int)(Num(a[1]) ?? s.Length));
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    private static object? Format(object?[] a)
    {
        var fmt = Text(a[1]);
        return Num(a[0]) is { } x ? x.ToString(fmt, Inv) : Text(a[0]);
    }

    /// <summary>Navigates a JSON document by a dotted path; numeric segments index arrays.</summary>
    private static object? Json(object?[] a)
    {
        try
        {
            using var doc = JsonDocument.Parse(Text(a[0]));
            var current = doc.RootElement;
            foreach (var segment in Text(a[1]).Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index))
                {
                    if (index >= current.GetArrayLength()) return null;
                    current = current[index];
                }
                else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var child))
                    current = child;
                else return null;
            }
            return Values.Normalize(current.Clone());
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

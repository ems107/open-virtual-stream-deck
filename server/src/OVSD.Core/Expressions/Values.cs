using System.Globalization;
using System.Text.Json;

namespace OVSD.Core.Expressions;

/// <summary>
/// Expression values are always one of: null, bool, double or string.
/// These helpers convert between them with forgiving, predictable rules.
/// </summary>
public static class Values
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Maps arbitrary CLR/JSON values onto the expression value domain.</summary>
    public static object? Normalize(object? value) => value switch
    {
        null => null,
        bool or double or string => value,
        int i => (double)i,
        long l => l,
        float f => (double)f,
        decimal m => (double)m,
        uint u => (double)u,
        ulong ul => ul,
        short s => (double)s,
        byte b => (double)b,
        JsonElement e => FromJson(e),
        Enum e => e.ToString(),
        DateTimeOffset d => d.ToString("O", Inv),
        _ => value.ToString(),
    };

    private static object? FromJson(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => e.GetRawText(),
    };

    public static bool IsTruthy(object? v) => v switch
    {
        null => false,
        bool b => b,
        double d => d != 0 && !double.IsNaN(d),
        string s => s.Length > 0 && !s.Equals("false", StringComparison.OrdinalIgnoreCase) && s != "0",
        _ => true,
    };

    public static double? ToNumber(object? v) => v switch
    {
        double d => d,
        bool b => b ? 1 : 0,
        string s when double.TryParse(s.Trim(), NumberStyles.Float, Inv, out var d) => d,
        _ => null,
    };

    public static string ToText(object? v) => v switch
    {
        null => "",
        bool b => b ? "true" : "false",
        double d => FormatNumber(d),
        string s => s,
        _ => v.ToString() ?? "",
    };

    public static string FormatNumber(double d) =>
        double.IsFinite(d) ? d.ToString("0.##", Inv) : d.ToString(Inv);

    public static bool AreEqual(object? a, object? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a is bool || b is bool) return IsTruthy(a) == IsTruthy(b);
        if ((a is double || b is double) && ToNumber(a) is { } x && ToNumber(b) is { } y) return x == y;
        return string.Equals(ToText(a), ToText(b), StringComparison.Ordinal);
    }

    public static int Compare(object? a, object? b)
    {
        if (ToNumber(a) is { } x && ToNumber(b) is { } y) return x.CompareTo(y);
        return string.CompareOrdinal(ToText(a), ToText(b));
    }
}

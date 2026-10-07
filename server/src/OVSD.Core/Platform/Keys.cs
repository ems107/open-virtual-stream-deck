namespace OVSD.Core.Platform;

/// <summary>
/// Canonical key names shared by the editor's hotkey recorder and the platform keyboard implementations.
/// Chords are written "Ctrl+Shift+F13".
/// </summary>
public static class Keys
{
    public static readonly IReadOnlyList<string> Modifiers = ["Ctrl", "Shift", "Alt", "Win"];

    public static readonly IReadOnlyList<string> All = BuildAll();

    private static readonly Dictionary<string, string> Lookup = BuildLookup();

    private static List<string> BuildAll()
    {
        var keys = new List<string>(Modifiers);
        keys.AddRange(Enumerable.Range('A', 26).Select(c => ((char)c).ToString()));
        keys.AddRange(Enumerable.Range(0, 10).Select(d => d.ToString()));
        keys.AddRange(Enumerable.Range(1, 24).Select(n => $"F{n}"));
        keys.AddRange([
            "Enter", "Esc", "Tab", "Space", "Backspace", "Delete", "Insert", "Home", "End", "PageUp", "PageDown",
            "Up", "Down", "Left", "Right", "PrintScreen", "ScrollLock", "Pause", "CapsLock", "NumLock", "Menu",
            "Num0", "Num1", "Num2", "Num3", "Num4", "Num5", "Num6", "Num7", "Num8", "Num9",
            "NumAdd", "NumSubtract", "NumMultiply", "NumDivide", "NumDecimal",
            "Minus", "Equals", "Comma", "Period", "Semicolon", "Quote", "Slash", "Backslash",
            "BracketLeft", "BracketRight", "Backquote", "IntlBackslash",
            "MediaPlayPause", "MediaNext", "MediaPrevious", "MediaStop", "VolumeUp", "VolumeDown", "VolumeMute",
            "BrowserBack", "BrowserForward", "BrowserRefresh", "BrowserHome",
        ]);
        return keys;
    }

    private static Dictionary<string, string> BuildLookup()
    {
        var map = All.ToDictionary(k => k, k => k, StringComparer.OrdinalIgnoreCase);
        foreach (var (alias, key) in new[]
                 {
                     ("Control", "Ctrl"), ("Ctl", "Ctrl"), ("Option", "Alt"), ("Meta", "Win"), ("Super", "Win"),
                     ("Cmd", "Win"), ("Windows", "Win"), ("Escape", "Esc"), ("Return", "Enter"), ("Del", "Delete"),
                     ("Ins", "Insert"), ("PgUp", "PageUp"), ("PgDn", "PageDown"), ("ArrowUp", "Up"),
                     ("ArrowDown", "Down"), ("ArrowLeft", "Left"), ("ArrowRight", "Right"), ("PrtSc", "PrintScreen"),
                     ("Dash", "Minus"),
                 })
            map[alias] = key;
        return map;
    }

    /// <summary>Parses "ctrl + shift + a" into ["Ctrl","Shift","A"]. Throws on unknown keys.</summary>
    public static IReadOnlyList<string> ParseChord(string chord)
    {
        var parts = chord.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new FormatException("Empty key combination");
        return parts
            .Select(p => Lookup.TryGetValue(p, out var key) ? key : throw new FormatException($"Unknown key '{p}'"))
            .ToList();
    }

    /// <summary>Parses several chords separated by spaces or commas: "Ctrl+K Ctrl+C".</summary>
    public static IReadOnlyList<IReadOnlyList<string>> ParseSequence(string sequence) =>
        sequence.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(ParseChord).ToList();
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using OVSD.Core.Platform;

namespace OVSD.Platform.Windows;

/// <summary>
/// Keyboard synthesis through SendInput. Each event carries both the virtual key (what apps read from
/// window messages) and the scan code (what games read through raw input / DirectInput).
/// </summary>
public sealed class WindowsKeyboard : IKeyboard
{
    private static readonly Dictionary<string, ushort> VirtualKeys = BuildVirtualKeys();

    /// <summary>Keys that only work as virtual keys (no meaningful scan code).</summary>
    private static readonly HashSet<string> VirtualOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        "MediaPlayPause", "MediaNext", "MediaPrevious", "MediaStop", "VolumeUp", "VolumeDown", "VolumeMute",
        "BrowserBack", "BrowserForward", "BrowserRefresh", "BrowserHome", "Pause",
    };

    private static readonly int InputSize = Marshal.SizeOf<Native.INPUT>();
    private static readonly Lock SendLock = new();

    public Task SendChordAsync(IReadOnlyList<string> keys, CancellationToken ct)
    {
        var inputs = keys.Select(k => Key(k, up: false))
            .Concat(keys.Reverse().Select(k => Key(k, up: true)))
            .ToArray();
        Send(inputs);
        return Task.CompletedTask;
    }

    public void KeyDown(IReadOnlyList<string> keys) => Send(keys.Select(k => Key(k, up: false)).ToArray());

    public void KeyUp(IReadOnlyList<string> keys) => Send(keys.Reverse().Select(k => Key(k, up: true)).ToArray());

    public async Task TypeTextAsync(string text, int delayMs, CancellationToken ct)
    {
        foreach (var ch in text.Replace("\r\n", "\n"))
        {
            ct.ThrowIfCancellationRequested();
            var inputs = ch switch
            {
                '\n' => [Key("Enter", false), Key("Enter", true)],
                '\t' => [Key("Tab", false), Key("Tab", true)],
                _ => new[] { Unicode(ch, up: false), Unicode(ch, up: true) },
            };
            Send(inputs);
            if (delayMs > 0) await Task.Delay(delayMs, ct);
        }
    }

    private static void Send(Native.INPUT[] inputs)
    {
        if (inputs.Length == 0) return;
        uint sent;
        lock (SendLock) sent = Native.SendInput((uint)inputs.Length, inputs, InputSize);
        if (sent != inputs.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "SendInput was blocked (an elevated window may be focused; run OVSD as administrator to control it)");
    }

    private static Native.INPUT Key(string name, bool up)
    {
        if (!VirtualKeys.TryGetValue(name, out var vk)) throw new FormatException($"Unknown key '{name}'");
        uint flags = up ? Native.KEYEVENTF_KEYUP : 0;
        ushort scan = 0;
        if (!VirtualOnly.Contains(name))
        {
            var mapped = Native.MapVirtualKeyW(vk, Native.MAPVK_VK_TO_VSC_EX);
            scan = (ushort)(mapped & 0xFF);
            if ((mapped & 0xFF00) is 0xE000 or 0xE100) flags |= Native.KEYEVENTF_EXTENDEDKEY;
        }
        return new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD,
            U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags } },
        };
    }

    private static Native.INPUT Unicode(char ch, bool up) => new()
    {
        type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion
        {
            ki = new Native.KEYBDINPUT
            {
                wScan = ch,
                dwFlags = Native.KEYEVENTF_UNICODE | (up ? Native.KEYEVENTF_KEYUP : 0),
            },
        },
    };

    private static Dictionary<string, ushort> BuildVirtualKeys()
    {
        var map = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ctrl"] = 0xA2, ["Shift"] = 0xA0, ["Alt"] = 0xA4, ["Win"] = 0x5B,
            ["Enter"] = 0x0D, ["Esc"] = 0x1B, ["Tab"] = 0x09, ["Space"] = 0x20, ["Backspace"] = 0x08,
            ["Delete"] = 0x2E, ["Insert"] = 0x2D, ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,
            ["Up"] = 0x26, ["Down"] = 0x28, ["Left"] = 0x25, ["Right"] = 0x27,
            ["PrintScreen"] = 0x2C, ["ScrollLock"] = 0x91, ["Pause"] = 0x13, ["CapsLock"] = 0x14, ["NumLock"] = 0x90,
            ["Menu"] = 0x5D,
            ["NumAdd"] = 0x6B, ["NumSubtract"] = 0x6D, ["NumMultiply"] = 0x6A, ["NumDivide"] = 0x6F, ["NumDecimal"] = 0x6E,
            ["Minus"] = 0xBD, ["Equals"] = 0xBB, ["Comma"] = 0xBC, ["Period"] = 0xBE, ["Semicolon"] = 0xBA,
            ["Quote"] = 0xDE, ["Slash"] = 0xBF, ["Backslash"] = 0xDC, ["BracketLeft"] = 0xDB, ["BracketRight"] = 0xDD,
            ["Backquote"] = 0xC0, ["IntlBackslash"] = 0xE2,
            ["MediaPlayPause"] = 0xB3, ["MediaNext"] = 0xB0, ["MediaPrevious"] = 0xB1, ["MediaStop"] = 0xB2,
            ["VolumeUp"] = 0xAF, ["VolumeDown"] = 0xAE, ["VolumeMute"] = 0xAD,
            ["BrowserBack"] = 0xA6, ["BrowserForward"] = 0xA7, ["BrowserRefresh"] = 0xA8, ["BrowserHome"] = 0xAC,
        };
        for (var c = 'A'; c <= 'Z'; c++) map[c.ToString()] = c;
        for (var d = 0; d <= 9; d++)
        {
            map[d.ToString()] = (ushort)('0' + d);
            map[$"Num{d}"] = (ushort)(0x60 + d);
        }
        for (var f = 1; f <= 24; f++) map[$"F{f}"] = (ushort)(0x6F + f);

        var missing = Keys.All.Where(k => !map.ContainsKey(k)).ToList();
        if (missing.Count > 0) throw new InvalidOperationException("Unmapped keys: " + string.Join(", ", missing));
        return map;
    }
}

using System.Globalization;
using Microsoft.Extensions.Logging;
using OVSD.Core.Expressions;
using OVSD.Core.Variables;

namespace OVSD.Core.Actions;

/// <summary>A failure meant to be shown to the user as-is.</summary>
public sealed class ActionException(string message) : Exception(message);

/// <summary>What an action may do to the deck that triggered it.</summary>
public interface IDeckSessionControl
{
    string SessionId { get; }
    string? DeviceId { get; }
    void OpenPage(string pageId);
    void Back();
    void Home();
    void SwitchProfile(string profileId);
    void Notify(string message, NotifyLevel level = NotifyLevel.Info);
}

public enum NotifyLevel { Info, Success, Warning, Error }

/// <summary>Everything a running macro can see: variables, local values and the originating deck.</summary>
public sealed class ActionContext(VariableStore variables, ILogger logger)
{
    public VariableStore Variables { get; } = variables;
    public ILogger Logger { get; } = logger;
    public IDeckSessionControl? Session { get; init; }
    public string? ControlId { get; init; }

    /// <summary>Macro-local values such as "value" (slider) or "index" (repeat). Shadow globals.</summary>
    public Dictionary<string, object?> Locals { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Func<string, object?> Resolver => Variables.Resolver(Locals);

    public string Render(string? template) => Template.Render(template, Resolver);

    public object? Evaluate(string expression)
    {
        try
        {
            return Expression.Evaluate(expression, Resolver);
        }
        catch (ExpressionException ex)
        {
            throw new ActionException(ex.Message);
        }
    }

    /// <summary>Sets a local if one with that name exists, otherwise a global variable.</summary>
    public void SetVariable(string name, object? value)
    {
        if (Locals.ContainsKey(name)) Locals[name] = Values.Normalize(value);
        else Variables.Set(name, value);
    }
}

/// <summary>Typed, template-rendering access to an action step's parameters.</summary>
public sealed class ActionArgs(IReadOnlyDictionary<string, string> raw, ActionContext context)
{
    public string? Raw(string name) => raw.TryGetValue(name, out var v) && !string.IsNullOrEmpty(v) ? v : null;

    public string? String(string name) => Raw(name) is { } v ? context.Render(v) : null;

    public string Required(string name) =>
        String(name) is { Length: > 0 } v ? v : throw new ActionException($"Missing parameter '{name}'");

    public double? Double(string name)
    {
        var raw1 = Raw(name);
        if (raw1 is null) return null;
        var value = Template.Evaluate(raw1, context.Resolver);
        return Values.ToNumber(value) ?? throw new ActionException($"Parameter '{name}' is not a number: {Values.ToText(value)}");
    }

    public int Int(string name, int fallback) => Double(name) is { } d ? (int)Math.Round(d) : fallback;

    public bool Bool(string name, bool fallback = false) =>
        String(name) is { } v ? Values.IsTruthy(v) : fallback;

    public T Enum<T>(string name, T fallback) where T : struct, Enum =>
        String(name) is { } v
            ? System.Enum.TryParse<T>(v, ignoreCase: true, out var parsed) ? parsed : throw new ActionException($"Invalid value '{v}' for '{name}'")
            : fallback;

    /// <summary>"on"/"off"/"toggle" → true/false/null.</summary>
    public bool? Mode(string name = "mode") => String(name)?.ToLowerInvariant() switch
    {
        null or "" or "toggle" => null,
        "on" or "true" or "start" => true,
        "off" or "false" or "stop" => false,
        var other => throw new ActionException($"Invalid mode '{other}'"),
    };

    public static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

namespace OVSD.Core.Actions;

public enum ParamType
{
    /// <summary>Single-line text; supports {{templates}}.</summary>
    Text,
    MultilineText,
    Number,
    Bool,
    /// <summary>One of <see cref="ParamDescriptor.Options"/> or of a dynamic <see cref="ParamDescriptor.OptionsSource"/>.</summary>
    Select,
    /// <summary>Key combination recorded in the editor, e.g. "Ctrl+Shift+M".</summary>
    Hotkey,
    /// <summary>A page of the profile being edited.</summary>
    Page,
    Profile,
    /// <summary>Expression (not a template), e.g. "audio.volume + 5".</summary>
    Expression,
    Variable,
    Json,
}

public sealed record OptionItem(string Value, string Label);

public sealed record ParamDescriptor
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public ParamType Type { get; init; } = ParamType.Text;
    public bool Required { get; init; }
    public string? Default { get; init; }
    public List<OptionItem>? Options { get; init; }
    /// <summary>Id of an <see cref="IOptionsProvider"/> giving dynamic choices (OBS scenes, audio devices...).</summary>
    public string? OptionsSource { get; init; }
    /// <summary>For dynamic selects: allow typing a value that is not in the list.</summary>
    public bool AllowCustom { get; init; }
    public string? Placeholder { get; init; }
    public string? Help { get; init; }
    /// <summary>Only show this parameter when another parameter has one of these values: "target=app".</summary>
    public string? ShowIf { get; init; }
}

/// <summary>Describes an action for the editor, which builds the parameter form from it.</summary>
public sealed record ActionDescriptor
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public List<ParamDescriptor> Params { get; init; } = [];
}

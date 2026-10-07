using System.Text.Json.Serialization;

namespace OVSD.Core.Model;

/// <summary>One step of a macro. A binding is a list of steps run in order.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ActionStep), "action")]
[JsonDerivedType(typeof(DelayStep), "delay")]
[JsonDerivedType(typeof(IfStep), "if")]
[JsonDerivedType(typeof(SetVariableStep), "set")]
[JsonDerivedType(typeof(RepeatStep), "repeat")]
public abstract record Step;

/// <summary>Runs a registered action. Parameter values are templates rendered at run time.</summary>
public sealed record ActionStep : Step
{
    public required string Action { get; init; }
    public Dictionary<string, string> Params { get; init; } = [];
}

public sealed record DelayStep : Step
{
    public required int Ms { get; init; }
}

public sealed record IfStep : Step
{
    public required string Condition { get; init; }
    public List<Step> Then { get; init; } = [];
    public List<Step> Else { get; init; } = [];
}

/// <summary>Sets a variable to the result of an expression.</summary>
public sealed record SetVariableStep : Step
{
    public required string Name { get; init; }
    public required string Value { get; init; }
}

public sealed record RepeatStep : Step
{
    public const int MaxCount = 1000;
    public required int Count { get; init; }
    public List<Step> Steps { get; init; } = [];
}

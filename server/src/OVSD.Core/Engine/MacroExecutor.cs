using OVSD.Core.Actions;
using OVSD.Core.Expressions;
using OVSD.Core.Model;

namespace OVSD.Core.Engine;

/// <summary>Runs a list of steps sequentially: actions, delays, conditions, variable sets and loops.</summary>
public sealed class MacroExecutor(ActionRegistry registry)
{
    public const int MaxDelayMs = 60 * 60 * 1000;
    private const int MaxDepth = 32;

    public Task RunAsync(IReadOnlyList<Step> steps, ActionContext context, CancellationToken ct) =>
        RunAsync(steps, context, 0, ct);

    private async Task RunAsync(IReadOnlyList<Step> steps, ActionContext context, int depth, CancellationToken ct)
    {
        if (depth > MaxDepth) throw new ActionException("Macro nested too deeply");

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            switch (step)
            {
                case ActionStep a:
                    var handler = registry.Get(a.Action) ?? throw new ActionException($"Unknown action '{a.Action}'");
                    await handler.ExecuteAsync(context, new ActionArgs(a.Params, context), ct);
                    break;

                case DelayStep d:
                    await Task.Delay(Math.Clamp(d.Ms, 0, MaxDelayMs), ct);
                    break;

                case IfStep i:
                    var branch = Values.IsTruthy(context.Evaluate(i.Condition)) ? i.Then : i.Else;
                    await RunAsync(branch, context, depth + 1, ct);
                    break;

                case SetVariableStep s:
                    if (string.IsNullOrWhiteSpace(s.Name)) throw new ActionException("Variable name is empty");
                    context.SetVariable(s.Name.Trim(), context.Evaluate(s.Value));
                    break;

                case RepeatStep r:
                    var count = Math.Clamp(r.Count, 0, RepeatStep.MaxCount);
                    var previousIndex = context.Locals.GetValueOrDefault("index");
                    for (var n = 0; n < count; n++)
                    {
                        context.Locals["index"] = (double)n;
                        await RunAsync(r.Steps, context, depth + 1, ct);
                    }
                    context.Locals["index"] = previousIndex;
                    break;
            }
        }
    }
}

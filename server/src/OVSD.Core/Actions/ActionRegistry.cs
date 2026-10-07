namespace OVSD.Core.Actions;

public interface IActionHandler
{
    ActionDescriptor Descriptor { get; }
    Task ExecuteAsync(ActionContext context, ActionArgs args, CancellationToken ct);
}

/// <summary>Groups related actions (one per integration or platform area) for DI registration.</summary>
public interface IActionProvider
{
    IEnumerable<IActionHandler> GetActions();
}

/// <summary>Dynamic choices for select parameters (OBS scenes, audio devices...).</summary>
public interface IOptionsProvider
{
    string Id { get; }
    Task<IReadOnlyList<OptionItem>> GetOptionsAsync(CancellationToken ct);
}

public sealed class DelegateAction(ActionDescriptor descriptor, Func<ActionContext, ActionArgs, CancellationToken, Task> run)
    : IActionHandler
{
    public ActionDescriptor Descriptor { get; } = descriptor;
    public Task ExecuteAsync(ActionContext context, ActionArgs args, CancellationToken ct) => run(context, args, ct);

    public static DelegateAction Sync(ActionDescriptor descriptor, Action<ActionContext, ActionArgs> run) =>
        new(descriptor, (ctx, args, _) =>
        {
            run(ctx, args);
            return Task.CompletedTask;
        });
}

public sealed class DelegateOptions(string id, Func<CancellationToken, Task<IReadOnlyList<OptionItem>>> get) : IOptionsProvider
{
    public string Id { get; } = id;
    public Task<IReadOnlyList<OptionItem>> GetOptionsAsync(CancellationToken ct) => get(ct);

    public static DelegateOptions Sync(string id, Func<IEnumerable<OptionItem>> get) =>
        new(id, _ => Task.FromResult<IReadOnlyList<OptionItem>>(get().ToList()));
}

public sealed class ActionRegistry
{
    private readonly Dictionary<string, IActionHandler> _actions;
    private readonly Dictionary<string, IOptionsProvider> _options;

    public ActionRegistry(IEnumerable<IActionProvider> providers, IEnumerable<IOptionsProvider> options)
    {
        _actions = providers.SelectMany(p => p.GetActions()).ToDictionary(a => a.Descriptor.Id, StringComparer.OrdinalIgnoreCase);
        _options = options.ToDictionary(o => o.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IActionHandler? Get(string id) => _actions.GetValueOrDefault(id);

    public IReadOnlyList<ActionDescriptor> Descriptors =>
        _actions.Values.Select(a => a.Descriptor).OrderBy(d => d.Category).ThenBy(d => d.Name).ToList();

    public IOptionsProvider? GetOptions(string id) => _options.GetValueOrDefault(id);
}

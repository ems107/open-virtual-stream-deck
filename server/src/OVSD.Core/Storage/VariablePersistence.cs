using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OVSD.Core.Variables;

namespace OVSD.Core.Storage;

/// <summary>Loads "user.*" variables at startup and saves them (debounced) when they change.</summary>
public sealed class VariablePersistence(DataPaths paths, VariableStore variables, ILogger<VariablePersistence> logger)
    : BackgroundService
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);
    private int _dirty;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var saved = JsonFile.Read<Dictionary<string, System.Text.Json.JsonElement>>(paths.VariablesFile);
            foreach (var (name, value) in saved ?? [])
                if (name.StartsWith(VariableStore.PersistentPrefix, StringComparison.OrdinalIgnoreCase))
                    variables.Set(name, value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load persisted variables");
        }

        variables.Changed += name =>
        {
            if (name.StartsWith(VariableStore.PersistentPrefix, StringComparison.OrdinalIgnoreCase))
                Interlocked.Exchange(ref _dirty, 1);
        };
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Debounce);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) SaveIfDirty();
        }
        catch (OperationCanceledException) { }
        SaveIfDirty();
    }

    private void SaveIfDirty()
    {
        if (Interlocked.Exchange(ref _dirty, 0) == 0) return;
        var persistent = variables.Snapshot()
            .Where(kv => kv.Key.StartsWith(VariableStore.PersistentPrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        JsonFile.WriteAtomic(paths.VariablesFile, persistent);
    }
}

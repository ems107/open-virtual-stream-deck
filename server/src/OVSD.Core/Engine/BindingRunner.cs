using Microsoft.Extensions.Logging;
using OVSD.Core.Actions;
using OVSD.Core.Model;

namespace OVSD.Core.Engine;

/// <summary>
/// Starts macro runs in the background and applies each control's concurrency policy.
/// Slider "change" bindings always coalesce: while one run is active only the latest value is kept.
/// </summary>
public sealed class BindingRunner(MacroExecutor executor, ILogger<BindingRunner> logger)
{
    private sealed class Slot
    {
        public Task Current = Task.CompletedTask;
        public CancellationTokenSource? Cts;
        public (IReadOnlyList<Step> Steps, ActionContext Context)? Pending;
    }

    private readonly Dictionary<string, Slot> _slots = new();
    private readonly Lock _lock = new();

    /// <summary>Raised when a run fails, after the error has been sent to the originating deck.</summary>
    public event Action<string, Exception>? Failed;

    /// <returns>The task of the run that was started (or the one already running when ignored).</returns>
    public Task Run(string key, Concurrency mode, IReadOnlyList<Step> steps, ActionContext context, bool coalesce = false)
    {
        lock (_lock)
        {
            if (!_slots.TryGetValue(key, out var slot)) _slots[key] = slot = new Slot();
            var busy = !slot.Current.IsCompleted;

            if (coalesce && busy)
            {
                slot.Pending = (steps, context);
                return slot.Current;
            }

            switch (mode)
            {
                case Concurrency.Ignore when busy:
                    return slot.Current;
                case Concurrency.Restart:
                    slot.Cts?.Cancel();
                    break;
            }

            var previous = mode == Concurrency.Queue ? slot.Current : Task.CompletedTask;
            var cts = new CancellationTokenSource();
            slot.Cts = cts;
            slot.Current = Task.Run(() => Execute(key, slot, previous, steps, context, cts));
            return slot.Current;
        }
    }

    /// <summary>Cancels every running macro (e.g. on shutdown).</summary>
    public void CancelAll()
    {
        lock (_lock)
            foreach (var slot in _slots.Values) slot.Cts?.Cancel();
    }

    private async Task Execute(string key, Slot slot, Task previous, IReadOnlyList<Step> steps, ActionContext context, CancellationTokenSource cts)
    {
        try
        {
            await previous.ContinueWith(_ => { }, TaskScheduler.Default);
            while (true)
            {
                await RunOnce(key, steps, context, cts.Token);
                lock (_lock)
                {
                    if (slot.Pending is not { } next) break;
                    slot.Pending = null;
                    (steps, context) = next;
                }
            }
        }
        finally
        {
            lock (_lock)
                if (ReferenceEquals(slot.Cts, cts)) slot.Cts = null;
            cts.Dispose();
        }
    }

    private async Task RunOnce(string key, IReadOnlyList<Step> steps, ActionContext context, CancellationToken ct)
    {
        try
        {
            await executor.RunAsync(steps, context, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            var message = ex is ActionException or PlatformNotSupportedException or FormatException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
            logger.LogWarning(ex, "Binding {Key} failed: {Message}", key, message);
            context.Session?.Notify(message, NotifyLevel.Error);
            Failed?.Invoke(key, ex);
        }
    }
}

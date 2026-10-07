using Microsoft.Extensions.Logging.Abstractions;
using OVSD.Core.Actions;
using OVSD.Core.Engine;
using OVSD.Core.Model;
using OVSD.Core.Platform;
using OVSD.Core.Variables;

namespace OVSD.Core.Tests;

public class EngineTests : IDisposable
{
    private readonly TestHost _host = new();
    public void Dispose() => _host.Dispose();

    private ActionContext NewContext() => new(_host.Get<VariableStore>(), NullLogger.Instance);

    private static ActionStep Hotkey(string keys) =>
        new() { Action = "keyboard.hotkey", Params = new() { ["keys"] = keys } };

    [Fact]
    public async Task RunsStepsInOrderWithConditionsAndVariables()
    {
        var executor = _host.Get<MacroExecutor>();
        var vars = _host.Get<VariableStore>();
        vars.Set("obs.scene", "Main");

        await executor.RunAsync([
            new SetVariableStep { Name = "user.n", Value = "41 + 1" },
            new IfStep
            {
                Condition = "obs.scene == 'Main' && user.n == 42",
                Then = [Hotkey("Ctrl+A")],
                Else = [Hotkey("Ctrl+B")],
            },
            new RepeatStep { Count = 3, Steps = [new ActionStep { Action = "keyboard.type", Params = new() { ["text"] = "#{{index}}" } }] },
        ], NewContext(), CancellationToken.None);

        Assert.Equal(["chord Ctrl+A", "type #0", "type #1", "type #2"], _host.Platform.Snapshot());
        Assert.Equal(42.0, vars.Get("user.n"));
    }

    [Fact]
    public async Task LocalsShadowGlobals()
    {
        var context = NewContext();
        context.Locals["value"] = 10.0;
        await _host.Get<MacroExecutor>().RunAsync([new SetVariableStep { Name = "value", Value = "value * 2" }], context, CancellationToken.None);
        Assert.Equal(20.0, context.Locals["value"]);
        Assert.Null(_host.Get<VariableStore>().Get("value"));
    }

    [Fact]
    public async Task UnknownActionFails() =>
        await Assert.ThrowsAsync<ActionException>(() =>
            _host.Get<MacroExecutor>().RunAsync([new ActionStep { Action = "nope" }], NewContext(), CancellationToken.None));

    [Fact]
    public async Task DelayIsCancellable()
    {
        using var cts = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _host.Get<MacroExecutor>().RunAsync([new DelayStep { Ms = 10_000 }], NewContext(), cts.Token));
    }

    [Fact]
    public async Task RelativeVolume()
    {
        _host.Platform.Volume = 50;
        await _host.Get<MacroExecutor>().RunAsync(
            [new ActionStep { Action = "audio.setVolume", Params = new() { ["target"] = "master", ["value"] = "+5" } }],
            NewContext(), CancellationToken.None);
        Assert.Equal(55, _host.Platform.Volume);
    }

    [Fact]
    public async Task IgnoreModeSkipsWhileRunning()
    {
        var runner = _host.Get<BindingRunner>();
        List<Step> steps = [new DelayStep { Ms = 150 }, Hotkey("A")];
        var first = runner.Run("k", Concurrency.Ignore, steps, NewContext());
        var second = runner.Run("k", Concurrency.Ignore, steps, NewContext());
        Assert.Same(first, second);
        await first;
        Assert.Equal(["chord A"], _host.Platform.Snapshot());
    }

    [Fact]
    public async Task RestartModeCancelsPreviousRun()
    {
        var runner = _host.Get<BindingRunner>();
        _ = runner.Run("k", Concurrency.Restart, [new DelayStep { Ms = 300 }, Hotkey("A")], NewContext());
        await Task.Delay(30);
        await runner.Run("k", Concurrency.Restart, [Hotkey("B")], NewContext());
        await Task.Delay(400);
        Assert.Equal(["chord B"], _host.Platform.Snapshot());
    }

    [Fact]
    public async Task QueueModeRunsSequentially()
    {
        var runner = _host.Get<BindingRunner>();
        _ = runner.Run("k", Concurrency.Queue, [new DelayStep { Ms = 100 }, Hotkey("A")], NewContext());
        await runner.Run("k", Concurrency.Queue, [Hotkey("B")], NewContext());
        Assert.Equal(["chord A", "chord B"], _host.Platform.Snapshot());
    }

    [Fact]
    public async Task CoalesceKeepsOnlyLatestPendingValue()
    {
        var runner = _host.Get<BindingRunner>();
        Task last = Task.CompletedTask;
        for (var i = 1; i <= 5; i++)
        {
            var ctx = NewContext();
            ctx.Locals["value"] = (double)i;
            last = runner.Run("slider", Concurrency.Parallel,
                [new ActionStep { Action = "keyboard.type", Params = new() { ["text"] = "{{value}}" } }, new DelayStep { Ms = 50 }],
                ctx, coalesce: true);
        }
        await last;
        var calls = _host.Platform.Snapshot();
        Assert.Equal("type 1", calls[0]);
        Assert.Equal("type 5", calls[^1]);
        Assert.True(calls.Count <= 3, string.Join(", ", calls));
    }

    [Theory]
    [InlineData("ctrl + shift + esc", "Ctrl,Shift,Esc")]
    [InlineData("Win+Left", "Win,Left")]
    [InlineData("control+f13", "Ctrl,F13")]
    public void ParsesChords(string chord, string expected) =>
        Assert.Equal(expected.Split(','), Keys.ParseChord(chord));

    [Fact]
    public void RejectsUnknownKeys() => Assert.Throws<FormatException>(() => Keys.ParseChord("Ctrl+Banana"));
}

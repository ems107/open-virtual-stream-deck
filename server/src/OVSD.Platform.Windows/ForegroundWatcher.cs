using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Hosting;
using OVSD.Core.Platform;
using OVSD.Core.Variables;

namespace OVSD.Platform.Windows;

/// <summary>Polls the foreground window and raises <see cref="Changed"/> when the app or title changes.</summary>
public sealed class ForegroundWatcher(VariableStore variables) : BackgroundService, IForegroundWatcher
{
    public ForegroundApp? Current { get; private set; }
    public event Action<ForegroundApp>? Changed;

    private uint _lastPid;
    private string _lastProcess = "";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(300));
        try
        {
            do Poll();
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { }
    }

    private void Poll()
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == 0) return;
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        var process = pid == _lastPid ? _lastProcess : ProcessName(pid);
        _lastPid = pid;
        _lastProcess = process;

        var length = Native.GetWindowTextLength(hwnd);
        var title = new StringBuilder(length + 1);
        if (length > 0) Native.GetWindowText(hwnd, title, title.Capacity);

        var app = new ForegroundApp(process, title.ToString());
        if (app == Current) return;
        Current = app;
        variables.Set("app.active", app.Process);
        variables.Set("app.title", app.Title);
        Changed?.Invoke(app);
    }

    private static string ProcessName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch (ArgumentException)
        {
            return "";
        }
    }
}

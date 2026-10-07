using System.Globalization;
using Microsoft.Extensions.Hosting;

namespace OVSD.Core.Variables;

/// <summary>Publishes time.* variables once per second.</summary>
public sealed class ClockVariables(VariableStore variables) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Publish();
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        var last = -1;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var second = DateTime.Now.Second;
                if (second == last) continue;
                last = second;
                Publish();
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Publish()
    {
        var now = DateTime.Now;
        var culture = CultureInfo.CurrentCulture;
        variables.Set("time.hh", now.ToString("HH", culture));
        variables.Set("time.mm", now.ToString("mm", culture));
        variables.Set("time.ss", now.ToString("ss", culture));
        variables.Set("time.hhmm", now.ToString("HH:mm", culture));
        variables.Set("time.hhmmss", now.ToString("HH:mm:ss", culture));
        variables.Set("time.date", now.ToString("d", culture));
        variables.Set("time.weekday", culture.TextInfo.ToTitleCase(now.ToString("dddd", culture)));
        variables.Set("time.day", now.Day);
        variables.Set("time.month", now.Month);
        variables.Set("time.year", now.Year);
        variables.Set("time.unix", now.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds);
    }
}

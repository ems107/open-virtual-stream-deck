using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OVSD.Core.Integrations;
using OVSD.Core.Model;
using OVSD.Core.Storage;

namespace OVSD.Integrations;

/// <summary>
/// Keeps a connection alive while its settings say it is enabled: connects, waits until it drops,
/// retries with backoff, and restarts immediately when the relevant settings change.
/// </summary>
public abstract class ConnectionLoop<TSettings> : BackgroundService, IIntegration where TSettings : class
{
    private static readonly TimeSpan MinRetry = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxRetry = TimeSpan.FromSeconds(30);

    private readonly ConfigRepository _config;
    private CancellationTokenSource _settingsChanged = new();

    protected ConnectionLoop(ConfigRepository config, ILogger logger)
    {
        _config = config;
        Logger = logger;
        Status = new IntegrationStatus(Id, Name, IntegrationState.Disabled, null);
        config.Changed += (old, updated) =>
        {
            if (!Equals(ConnectionKey(Select(old.Settings)), ConnectionKey(Select(updated.Settings))))
                _settingsChanged.Cancel();
        };
    }

    protected ILogger Logger { get; }
    protected ConfigRepository Config => _config;
    public abstract string Id { get; }
    public abstract string Name { get; }
    public IntegrationStatus Status { get; private set; }

    protected abstract TSettings Select(AppSettings settings);
    protected abstract bool IsEnabled(TSettings settings);

    /// <summary>Settings whose change requires reconnecting (by default all of them).</summary>
    protected virtual object ConnectionKey(TSettings settings) => settings;

    /// <summary>Connects and returns only when the connection ends (throw to report an error).</summary>
    protected abstract Task RunConnectedAsync(TSettings settings, CancellationToken ct);

    /// <summary>Called after every disconnection to clear published state.</summary>
    protected virtual void OnDisconnected() { }

    protected void SetStatus(IntegrationState state, string? message = null) =>
        Status = new IntegrationStatus(Id, Name, state, message);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retry = MinRetry;
        while (!stoppingToken.IsCancellationRequested)
        {
            var changed = _settingsChanged = new CancellationTokenSource();
            var settings = Select(_config.Settings);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, changed.Token);

            if (!IsEnabled(settings))
            {
                SetStatus(IntegrationState.Disabled);
                await Wait(Timeout.InfiniteTimeSpan, linked.Token);
                continue;
            }

            SetStatus(IntegrationState.Connecting);
            try
            {
                await RunConnectedAsync(settings, linked.Token);
                SetStatus(IntegrationState.Connecting, "Connection closed");
                retry = MinRetry;
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                retry = MinRetry;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "{Integration} connection failed", Name);
                SetStatus(IntegrationState.Error, ex.Message);
            }
            finally
            {
                OnDisconnected();
            }

            if (stoppingToken.IsCancellationRequested) break;
            if (!changed.IsCancellationRequested)
            {
                await Wait(retry, linked.Token);
                retry = TimeSpan.FromTicks(Math.Min(retry.Ticks * 2, MaxRetry.Ticks));
            }
        }
    }

    private static async Task Wait(TimeSpan delay, CancellationToken ct)
    {
        try { await Task.Delay(delay, ct); }
        catch (OperationCanceledException) { }
    }
}

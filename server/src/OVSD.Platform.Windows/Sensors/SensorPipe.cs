using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OVSD.Platform.Windows.Sensors;

/// <summary>What the sensor service sends, one JSON line per second.</summary>
public sealed record SensorMessage(string Version, bool DriverInstalled, CpuReading? Cpu, string? Error);

/// <summary>
/// Named pipe between the elevated sensor service (writer) and the OVSD app running as the user (reader).
/// The pipe is one-way: the service never reads anything, so the user side cannot make it do anything.
/// </summary>
public static class SensorPipe
{
    public const string Name = "OVSD.Sensors";
    public const string ServiceName = "OVSDSensors";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Pipe ACL: SYSTEM and administrators own it; signed-in users may only read.</summary>
    internal static PipeSecurity Security()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.Read | PipeAccessRights.Synchronize, AccessControlType.Allow));
        // The account running the service (SYSTEM) creates further pipe instances.
        if (WindowsIdentity.GetCurrent().User is { } self)
            security.AddAccessRule(new PipeAccessRule(self, PipeAccessRights.FullControl, AccessControlType.Allow));
        return security;
    }
}

/// <summary>The sensor service: samples the CPU every second and broadcasts it to every connected reader.</summary>
public sealed class SensorPipeServer(string version, ILogger<SensorPipeServer> logger) : BackgroundService
{
    private volatile string _line = "";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sampler = Task.Run(() => SampleLoop(stoppingToken), stoppingToken);
        var clients = new List<Task>();
        while (!stoppingToken.IsCancellationRequested)
        {
            NamedPipeServerStream pipe;
            try
            {
                pipe = NamedPipeServerStreamAcl.Create(SensorPipe.Name, PipeDirection.Out, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 4096, SensorPipe.Security());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Cannot create the sensor pipe");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                continue;
            }
            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync();
                break;
            }
            clients.RemoveAll(t => t.IsCompleted);
            clients.Add(ServeAsync(pipe, stoppingToken));
        }
        await Task.WhenAll([sampler, .. clients]).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private async Task SampleLoop(CancellationToken ct)
    {
        CpuSensors? sensors = null;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                SensorMessage message;
                try
                {
                    sensors ??= new CpuSensors();
                    message = new SensorMessage(version, CpuSensors.DriverInstalled, sensors.Read(), null);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "CPU sensors failed");
                    sensors?.Dispose();
                    sensors = null;
                    message = new SensorMessage(version, CpuSensors.DriverInstalled, null, ex.Message);
                }
                _line = JsonSerializer.Serialize(message, SensorPipe.Json) + "\n";
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { }
        finally
        {
            sensors?.Dispose();
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        await using (pipe)
        {
            try
            {
                while (!ct.IsCancellationRequested && pipe.IsConnected)
                {
                    if (_line.Length > 0) await pipe.WriteAsync(Encoding.UTF8.GetBytes(_line), ct);
                    await Task.Delay(1000, ct);
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // Reader went away or the service is stopping.
            }
        }
    }
}

/// <summary>App side: keeps the latest reading of the sensor service, reconnecting while it is unavailable.</summary>
public sealed class SensorPipeClient(ILogger<SensorPipeClient> logger) : BackgroundService
{
    /// <summary>Latest message, or null when the service isn't running (not installed, stopped…).</summary>
    public SensorMessage? Latest { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(".", SensorPipe.Name, PipeDirection.In, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(2000, stoppingToken);
                using var reader = new StreamReader(pipe, Encoding.UTF8);
                while (await reader.ReadLineAsync(stoppingToken) is { } line)
                {
                    // Data from another process: only accept the expected shape.
                    Latest = JsonSerializer.Deserialize<SensorMessage>(line, SensorPipe.Json);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or JsonException or UnauthorizedAccessException)
            {
                logger.LogTrace(ex, "Sensor service not available");
            }
            Latest = null;
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
